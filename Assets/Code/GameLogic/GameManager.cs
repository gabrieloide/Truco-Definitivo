using System;
using System.Collections.Generic;
using System.Linq;
using Code.Cards;
using Code.Networking;
using Code.Player;
using Mirror;
using Unity.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;
using Code.Scripts.Audio;

namespace Code.GameLogic
{
    public class GameManager : MonoBehaviour
    {
        public static event Action<int, GameObject> OnTurnStarted;
        public static event Action<int, int> OnScoreChanged;
        
        // True when this instance is authoritative (singleplayer OR multiplayer server/host)
        public bool isServer => !NetworkClient.active || NetworkServer.active;
        private static GameManager _instance;

        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<GameManager>();
                    // Ocultamos el log de error porque es normal que no exista GameManager 
                    // cuando estamos en el Main Menu o durante las transiciones de escena.
                }
                return _instance;
            }
        }

        [Header("Player Settings")]
        public PlayerLocal localPlayer; // Referencia al jugador local (asignada manual o dinámicamente)
        public int defaultLocalSeatIndex = 0; // Índice de la silla donde el jugador local debería empezar

        [Header("Prefabs")]
        public GameObject deckPrefab;
        public GameObject playerPrefab;

        public List<PlayerLocal> serverPlayers = new List<PlayerLocal>();
        public List<Code.Player.Player> allPlayers = new List<Code.Player.Player>();

        public int currentPlayerTurn = 0;
        public int playerCount;
        public bool deckIsLocked;
        public int round;
        public int dealerIndex = 0; // The seat index of the player who is dealing
        public bool devMode;
        public bool isHandResolved = false; // Flag to stop trick resolution if hand already ended

        public bool isGameScene;
        [HideInInspector] public PlayerInput playerInput;
        [SerializeField] private bool _gameSceneStarted = false;
        public List<Team> teams = new List<Team>();
        public bool isAnnouncementPending = false; // Bloquea el flujo del juego para esperar respuesta
        
        [Header("Pending Envido State")]
        public bool pendingEnvidoResolution = false;
        public string pendingEnvidoWinnerTeam = "";
        public int pendingEnvidoPoints = 0;
        public int pendingEnvidoScoreTeam1 = 0;
        public int pendingEnvidoScoreTeam2 = 0;
        
        public Code.GameLogic.States.GameStateMachine stateMachine { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SetupAudioManager()
        {
            try
            {
                var audioManagerInstance = AudioManager.Instance;
                if (audioManagerInstance == null)
                {
                    Debug.LogError("[GameManager] AudioManager.Instance es nulo al inicializar!");
                    return;
                }

                var fieldInfo = typeof(AudioManager).GetField("_database", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldInfo == null)
                {
                    Debug.LogError("[GameManager] No se pudo encontrar el campo privado '_database' en AudioManager.");
                    return;
                }

                var database = (AudioDatabase)fieldInfo.GetValue(audioManagerInstance);
                if (database == null)
                {
                    var dbAsset = Resources.Load<AudioDatabase>("Audio/AudioDatabase");
                    if (dbAsset != null)
                    {
                        fieldInfo.SetValue(audioManagerInstance, dbAsset);
                    }
                    else
                    {
                        Debug.LogError("[GameManager] ¡No se pudo cargar Audio/AudioDatabase de Resources!");
                    }
                }
                else
                {
                }

                // Force loop=true on music tracks at runtime to guarantee proper looping
                // regardless of YAML serialization state
                var db = (AudioDatabase)fieldInfo.GetValue(audioManagerInstance);
                if (db != null)
                {
                    foreach (var audioData in db.audioDataList)
                    {
                        if (audioData.id == "backyard_truco" || audioData.id == "main_menu_truco")
                        {
                            audioData.loop = true;
                        }
                    }
                }

                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedPlayMusic;
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedPlayMusic;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Error al configurar el AudioManager: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void OnSceneLoadedPlayMusic(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                var audioManagerInstance = AudioManager.Instance;
                if (audioManagerInstance == null) return;
                
                if (scene.name == "GameScene")
                {
                    audioManagerInstance.PlayMusic("backyard_truco", crossfade: true, duration: 1.5f);
                }
                else if (scene.name == "MainMenu" || scene.name == "LobbyScene")
                {
                    audioManagerInstance.PlayMusic("main_menu_truco", crossfade: true, duration: 1.5f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameManager] Error al cambiar música de escena: {ex.Message}");
            }
        }

        [Header("Match Settings")]
        public int maxPoints = 15; // Límite de puntos para ganar la partida
        private bool _matchEnded = false;

        private void Awake()
        {
            Debug.Log($"[GameManager] Awake ejecutado. Escena activa: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}. Escena de este objeto: {gameObject.scene.name}");
            if (_instance != null && _instance != this)
            {
                Debug.Log("[GameManager] Awake: Se detecto otra instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            
            _instance = this;

            // Cargar configuración elegida en el Menú Principal (12 o 24 puntos)
            maxPoints = Code.UI.MainMenuController.SingleplayerMaxPoints;
            Debug.Log($"[GameManager] Límite de puntos configurado a: {maxPoints}");

            UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);

            stateMachine = gameObject.AddComponent<Code.GameLogic.States.GameStateMachine>();

            playerInput = new PlayerInput();
            playerInput.Enable();

            teams.Add(new Team("Team 1"));
            teams.Add(new Team("Team 2"));
        }

        private void OnDestroy()
        {
            if (playerInput != null)
            {
                playerInput.Disable();
                playerInput.Dispose();
            }
        }

        // Las escuchas de TableManager.OnCardPlaced ahora están en PlayerTurnState

        public GameObject[] GetOpponentTeam(GameObject currentPlayer)
        {
            List<GameObject> opponents = new List<GameObject>();
            Team myTeam = null;

            var playerComp = currentPlayer.GetComponent<Code.Player.Player>();
            if (playerComp != null) myTeam = playerComp.team;
            
            if (myTeam == null)
            {
                var npcComp = currentPlayer.GetComponent<NPCPlayer>();
                if (npcComp != null) myTeam = npcComp.team;
            }

            if (myTeam == null) return new GameObject[0];

            // Buscar en NPCs
            foreach (var npc in npcs)
            {
                if (npc != null && npc.team != null && npc.team.teamName != myTeam.teamName)
                {
                    opponents.Add(npc.gameObject);
                }
            }

            // Buscar en jugadores humanos
            var allPlayers = this.allPlayers;
            foreach (var p in allPlayers)
            {
                if (p != null && p.gameObject != currentPlayer && p.team != null && p.team.teamName != myTeam.teamName)
                {
                    opponents.Add(p.gameObject);
                }
            }

            return opponents.ToArray();
        }

        public List<NPCPlayer> npcs = new List<NPCPlayer>();
        private int _totalPlayersCount = 0;

        private void Update()
        {
            if (_instance != null && _instance != this) return; // Prevent ghost instances from running

            isGameScene = true;

            // Debug for PlayerHUD presence
            if (Time.frameCount % 300 == 0) // Every ~5 seconds at 60fps
            {
                var hud = FindAnyObjectByType<PlayerHUD>();
                if (hud == null)
                {
                }
            }

            if (_gameSceneStarted) return;

            RunOnlyOnce();
        }

        private void RunOnlyOnce()
        {
            // In multiplayer, pure clients only prepare the local scene (chairs, raycaster):
            // seating, dealing and turns are driven by the server via RPCs/SyncVars.
            if (NetworkClient.active && !NetworkServer.active)
            {
                _gameSceneStarted = true;

                if (Camera.main != null && Camera.main.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                    Camera.main.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();

                SeatManager.Instance?.InitializeLayout();
                // The deck visual is the DeckCreator's own GameObject, and the client
                // also needs DeckCreator.Instance to store the vira for envido checks.
                EnsureDeckCreator();
                DisableNpcsForMultiplayer();
                return;
            }

            // Multiplayer host: prepare the scene but let MyNetworkingManager seat the
            // connected players and call StartMultiplayerMatch() once everyone is ready.
            if (NetworkServer.active)
            {
                _gameSceneStarted = true;

                if (Camera.main != null && Camera.main.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                    Camera.main.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();

                if (SeatManager.Instance == null || SeatManager.Instance.InitializeLayout() == 0)
                {
                    Debug.LogError("[GameManager] CRITICAL: Sin sillas disponibles en multiplayer.");
                    return;
                }

                EnsureDeckCreator();
                DisableNpcsForMultiplayer();
                return;
            }

            Debug.Log("[GameManager] RunOnlyOnce: Iniciando configuracion de la escena de juego.");

            if (Camera.main != null && Camera.main.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
            {
                Camera.main.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            }

            _gameSceneStarted = true;

            // 1. INICIALIZACIÓN DE SILLAS (CONTROL TOTAL)
            if (SeatManager.Instance != null)
            {
                int chairCount = SeatManager.Instance.InitializeLayout();
                if (chairCount == 0)
                {
                    Debug.LogError("[GameManager] CRITICAL: El SeatManager no pudo encontrar ni crear ninguna silla. Abortando inicio.");
                    return;
                }
            }
            else
            {
                Debug.LogError("[GameManager] CRITICAL: No se encontró SeatManager en la escena.");
                return;
            }
            
            // Ensure only one DeckCreator exists in the scene to prevent Vira or shuffle desyncs
            DeckCreator deckCreator = EnsureDeckCreator();

            var localPlayer = FindAnyObjectByType<PlayerLocal>();
            Debug.Log($"[GameManager] localPlayer inicial buscado en escena = {(localPlayer != null ? localPlayer.name : "NULL")}");
            
            if (localPlayer == null)
            {
                var p = FindAnyObjectByType<Code.Player.Player>();
                if (p != null) localPlayer = p.GetComponent<PlayerLocal>();
            }

            if (localPlayer == null && playerPrefab == null)
            {
                playerPrefab = Resources.Load<GameObject>("Player");
                if (playerPrefab == null)
                {
                    Debug.LogError("[GameManager] CRITICAL ERROR: No se encontró 'Player' en la carpeta Resources ni en el Inspector. ¡Asegúrate de mover el prefab a una carpeta Resources!");
                }
                else
                {
                    Debug.Log("[GameManager] Cargado playerPrefab desde Resources.");
                }
            }

            if (localPlayer == null && playerPrefab != null)
            {
                var playerObj = Instantiate(playerPrefab);
                localPlayer = playerObj.GetComponent<PlayerLocal>();
                Debug.Log($"[GameManager] Instanciado playerPrefab: {playerObj.name}. Componente PlayerLocal = {(localPlayer != null ? "SI" : "NO")}");
            }
            else if (localPlayer == null)
            {
                Debug.LogError("[GameManager] CRITICAL ERROR: playerPrefab is not assigned in the Inspector! The game will not start in the browser build.");
            }

            npcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None).ToList();
            allPlayers = FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None).ToList();
            Debug.Log($"[GameManager] NPCs encontrados = {npcs.Count}, Players encontrados = {allPlayers.Count}");

            if (deckCreator != null && (localPlayer != null || FindAnyObjectByType<Code.Player.Player>() != null))
            {
                var mainPlayerComponent = localPlayer != null ? localPlayer.player : FindAnyObjectByType<Code.Player.Player>();
                var mainCardsHandler = localPlayer != null ? localPlayer.cardsHandler : FindAnyObjectByType<CardsHandler>();
                var playerGameObject = localPlayer != null ? localPlayer.gameObject : mainPlayerComponent.gameObject;

                Debug.Log($"[GameManager] Jugador a sentar: {playerGameObject.name}. CardsHandler = {(mainCardsHandler != null ? "SI" : "NO")}");

                if (SeatManager.Instance != null)
                {
                    Debug.Log($"[GameManager] SeatManager.Instance allChairs.Count = {SeatManager.Instance.allChairs.Count}");
                    if (SeatManager.Instance.allChairs.Count > defaultLocalSeatIndex)
                    {
                        var chair = SeatManager.Instance.allChairs[defaultLocalSeatIndex];
                        Debug.Log($"[GameManager] Requesting seat {defaultLocalSeatIndex} for {playerGameObject.name}.");
                        SeatManager.Instance.RequestSeat(playerGameObject, chair);
                    }
                    else if (SeatManager.Instance.allChairs.Count > 0)
                    {
                        Debug.Log($"[GameManager] Falling back to AutoSeatLocalPlayer for {playerGameObject.name}");
                        SeatManager.Instance.AutoSeatLocalPlayer(playerGameObject);
                    }
                }
                else
                {
                    Debug.LogError("[GameManager] SeatManager.Instance es NULL al intentar sentar al jugador!");
                }
                
                foreach (var npc in npcs)
                {
                    if (SeatManager.Instance != null && SeatManager.Instance.GetPlayerSeatIndex(npc.gameObject) == -1)
                    {
                        foreach (var chair in SeatManager.Instance.allChairs)
                        {
                            if (!chair.isOccupied)
                            {
                                SeatManager.Instance.RequestSeat(npc.gameObject, chair);
                                break;
                            }
                        }
                    }
                }

                foreach (var chair in SeatManager.Instance.allChairs)
                {
                    if (chair.occupant != null)
                    {
                        int seatIndex = SeatManager.Instance.allChairs.IndexOf(chair);
                        Team team = teams[seatIndex % 2];
                        
                        var pComp = chair.occupant.GetComponent<Code.Player.Player>();
                        if (pComp != null) pComp.team = team;
                        
                        var npcComp = chair.occupant.GetComponent<NPCPlayer>();
                        if (npcComp != null) npcComp.team = team;
                    }
                }

                if (SeatManager.Instance.allChairs.Count == 0)
                {
                    Debug.LogError("[GameManager] CRITICAL: SeatManager.allChairs está vacía. No se puede iniciar la partida.");
                    return;
                }

                int occupiedCount = 0;
                foreach (var chair in SeatManager.Instance.allChairs)
                {
                    if (chair.occupant != null) occupiedCount++;
                }
                _totalPlayersCount = occupiedCount;
                
                int startingDealer = (SeatManager.Instance.allChairs.Count - 1);
                dealerIndex = startingDealer;
                // Move to a valid dealer if startingDealer is empty
                while (SeatManager.Instance.allChairs[dealerIndex].occupant == null)
                {
                    dealerIndex = (dealerIndex + 1) % SeatManager.Instance.allChairs.Count;
                }

                currentManoSeatIndex = (dealerIndex + 1) % SeatManager.Instance.allChairs.Count;
                while (SeatManager.Instance.allChairs[currentManoSeatIndex].occupant == null)
                {
                    currentManoSeatIndex = (currentManoSeatIndex + 1) % SeatManager.Instance.allChairs.Count;
                }
                currentTrickStartSeatIndex = currentManoSeatIndex;


                // Start the State Machine directly with DealingState!
                // DealingState will now handle shuffling, dealing, updating vira, and starting the turn.
                stateMachine.ChangeState(new Code.GameLogic.States.DealingState());
            }
            else
            {
            }

            OnStartGame();
        }
        private DeckCreator EnsureDeckCreator()
        {
            DeckCreator deckCreator = DeckCreator.Instance;
            Debug.Log($"[GameManager] DeckCreator.Instance = {(deckCreator != null ? deckCreator.name : "NULL")}");

            if (deckCreator == null)
            {
                if (deckPrefab != null)
                {
                    GameObject go = Instantiate(deckPrefab);
                    go.name = "DeckCreator_Generated";
                    deckCreator = go.GetComponent<DeckCreator>();
                    if (deckCreator == null) deckCreator = go.AddComponent<DeckCreator>();
                }
                else
                {
                    GameObject go = new GameObject("DeckCreator_Generated");
                    deckCreator = go.AddComponent<DeckCreator>();
                }
                Debug.Log($"[GameManager] Creado nuevo DeckCreator: {deckCreator.name}");
            }
            return deckCreator;
        }

        private void DisableNpcsForMultiplayer()
        {
            // Online matches are humans-only: scene NPCs would steal seats and turns.
            foreach (var npc in FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None))
                npc.gameObject.SetActive(false);
            npcs = new List<NPCPlayer>();
        }

        /// <summary>
        /// Server-side match start for multiplayer. Called by MyNetworkingManager once
        /// every connected player is ready and seated. Mirrors the singleplayer tail of
        /// RunOnlyOnce: teams by seat parity, dealer/mano selection, then DealingState.
        /// </summary>
        public void StartMultiplayerMatch()
        {
            if (!NetworkServer.active) return;

            var seatMgr = SeatManager.Instance;
            if (seatMgr == null || seatMgr.allChairs.Count == 0)
            {
                Debug.LogError("[GameManager] StartMultiplayerMatch: sin sillas.");
                return;
            }

            allPlayers = FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None).ToList();

            int occupiedCount = 0;
            for (int i = 0; i < seatMgr.allChairs.Count; i++)
            {
                var occupant = seatMgr.allChairs[i].occupant;
                if (occupant == null) continue;
                occupiedCount++;

                var pComp = occupant.GetComponent<Code.Player.Player>();
                if (pComp != null)
                {
                    // The lobby team wins over chair parity: in 1v1 the players sit on
                    // facing chairs (0 and 2, both even), so parity would put them on
                    // the same team.
                    var occupantSync = occupant.GetComponent<Code.Networking.PlayerNetworkSync>();
                    int teamIdx = (occupantSync != null && occupantSync.teamIndex >= 0)
                        ? Mathf.Clamp(occupantSync.teamIndex, 0, 1)
                        : i % 2;
                    pComp.team = teams[teamIdx];
                }
            }

            if (occupiedCount < 2)
            {
                Debug.LogError($"[GameManager] StartMultiplayerMatch: solo {occupiedCount} jugador(es) sentado(s).");
                return;
            }

            _totalPlayersCount = occupiedCount;

            dealerIndex = seatMgr.allChairs.Count - 1;
            while (seatMgr.allChairs[dealerIndex].occupant == null)
                dealerIndex = (dealerIndex + 1) % seatMgr.allChairs.Count;

            currentManoSeatIndex = (dealerIndex + 1) % seatMgr.allChairs.Count;
            while (seatMgr.allChairs[currentManoSeatIndex].occupant == null)
                currentManoSeatIndex = (currentManoSeatIndex + 1) % seatMgr.allChairs.Count;
            currentTrickStartSeatIndex = currentManoSeatIndex;

            // Send each remote client its name + team index
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn.identity == null || conn is LocalConnectionToClient) continue;
                var netSync = conn.identity.GetComponent<Code.Networking.PlayerNetworkSync>();
                var pLocal = conn.identity.GetComponent<PlayerLocal>();
                if (netSync == null || pLocal == null || pLocal.player == null) continue;

                int teamIdx = pLocal.player.team != null ? teams.IndexOf(pLocal.player.team) : 0;
                netSync.TargetSyncPlayerInfo(conn, pLocal.player.playerName, Mathf.Max(teamIdx, 0));
            }

            Debug.Log($"[GameManager] StartMultiplayerMatch: {occupiedCount} jugadores. Dealer={dealerIndex}, Mano={currentManoSeatIndex}.");
            stateMachine.ChangeState(new Code.GameLogic.States.DealingState());
        }

        /// <summary>Locks the turn flag on every client (multiplayer only).</summary>
        public void BroadcastTurnLock()
        {
            if (!NetworkServer.active) return;
            foreach (var conn in NetworkServer.connections.Values)
                conn.identity?.GetComponent<Code.Networking.PlayerNetworkSync>()?.RpcSetTurn(false);
        }

        public void StartTurn(int index)
        {
            StartTurnRecursive(index, 0);
        }

        private void StartTurnRecursive(int index, int depth)
        {
            if (depth > SeatManager.Instance.allChairs.Count)
            {
                Debug.LogError("[GameManager] CRITICAL: All chairs are empty! Stopping turn assignment.");
                return;
            }

            currentPlayerTurn = (index + SeatManager.Instance.allChairs.Count) % SeatManager.Instance.allChairs.Count;
            ChairInteractable targetChair = SeatManager.Instance.allChairs[currentPlayerTurn];

            if (targetChair.occupant == null)
            {
                // Mover en sentido ANTIHORARIO (incrementando) para saltar la silla vacía
                StartTurnRecursive(currentPlayerTurn + 1, depth + 1);
                return;
            }

            GameObject occupant = targetChair.occupant;

            if (occupant.GetComponent<PlayerLocal>() != null)
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("turn_alert_ping");
                }
            }

            var playerComp = occupant.GetComponent<Code.Player.Player>();
            if (playerComp != null)
            {
                playerComp.canPlayCard = true;
                // In multiplayer, the owning client needs the flag too (it gates clicks)
                if (NetworkServer.active)
                    occupant.GetComponent<Code.Networking.PlayerNetworkSync>()?.RpcSetTurn(true);
            }
            else
            {
                var npc = occupant.GetComponent<NPCPlayer>();
                if (npc != null) npc.StartTurn();
            }

            OnTurnStarted?.Invoke(currentPlayerTurn, occupant);
        }

        // [ClientRpc]
        private void OnStartGame()
        {
        }

        public void UpdateDeckAndVira()
        {
            if (SeatManager.Instance == null || TableManager.Instance == null) return;

            var deckCreator = DeckCreator.Instance;
            
            if (deckCreator != null)
            {
                // Pasamos el índice del repartidor para que TableManager calcule el offset desde su posición de carta
                TableManager.Instance.SpawnVira3D(deckCreator.cardVira, dealerIndex);
            }
        }

        // [Server]
        public void AddPlayerToServer(PlayerLocal player)
        {
            if (!isServer)
                return;
            
            if (!serverPlayers.Contains(player))
            {
                serverPlayers.Add(player);
                playerCount++;
            }
        }

        public int lastTrickWinnerSeatIndex { get; set; } = -1;
        public int currentManoSeatIndex { get; set; } = 0; // The seat that starts every trick in the hand
        public int currentTrickStartSeatIndex { get; set; } = 0; // The seat that starts the current baza

        public void EndTurn()
        {
            if (isHandResolved || _gameSceneStarted == false) return;

            // Bloqueamos el turno actual para todos antes de evaluar
            var players = UnityEngine.Object.FindObjectsByType<Code.Player.Player>(UnityEngine.FindObjectsSortMode.None);
            foreach (var p in players) p.canPlayCard = false;
            BroadcastTurnLock();

            // Usamos ResetTurnState para asegurar que los NPCs detengan cualquier acción pendiente
            foreach (var npc in npcs) npc.ResetTurnState();

            int cardsPlayed = TableManager.Instance.CardsInTable.Count;
            int totalExpectedCards = _totalPlayersCount;


            if (cardsPlayed >= totalExpectedCards)
            {
                StartCoroutine(DelayedTrickResolution());
                return;
            }

            // Turno normal dentro de una baza: Siguiente silla en sentido ANTIHORARIO (incrementando índice)
            int nextIndex = (currentPlayerTurn + 1) % SeatManager.Instance.allChairs.Count;
            
            StartTurn(nextIndex);
        }

        private System.Collections.IEnumerator DelayedTrickResolution()
        {
            // Esperar 1.2 segundos para apreciar la última carta jugada
            yield return new WaitForSeconds(1.2f);

            int roundBeforeEvaluation = round;

            // Determinar el ganador de la baza (esto dispara HandleTrickResult)
            TableManager.Instance.DetermineHighestCard();

            // Esperar 2.2 segundos para asimilar quién ganó y ver las cartas en mesa
            yield return new WaitForSeconds(2.2f);

            // Si la mano ya fue resuelta (alguien ganó), abortamos la transición
            if (isHandResolved)
            {
                yield break;
            }

            TableManager.Instance.CardsInTable.Clear();
            round++;

            if (round >= 3)
            {
                StartNewHand();
            }
            else
            {
                if (lastTrickWinnerSeatIndex != -1)
                {
                    currentTrickStartSeatIndex = lastTrickWinnerSeatIndex;
                }
                StartTurn(currentTrickStartSeatIndex);
            }
        }

        public List<int> trickWinners = new List<int>(); // 0 = Tie, 1 = Team 1, 2 = Team 2
        private int _manoTeamIndex = 1; // Team index (1 or 2) that is "Mano" in current hand
        public int ManoTeamIndex => _manoTeamIndex;
        public int currentHandValue = 1; // Points for the winner of the hand (Truco, Retruco, etc)
        public int lastTrucoTeamIndex = 0; // The team that made the last accepted challenge (1 or 2)

        public void HandleTrickResult(GameObject winnerObj)
        {
            int winnerTeam = 0; // Tie
            lastTrickWinnerSeatIndex = -1;

            if (winnerObj != null)
            {
                lastTrickWinnerSeatIndex = SeatManager.Instance.GetPlayerSeatIndex(winnerObj);
                
                var pComp = winnerObj.GetComponent<Code.Player.Player>();
                var npcComp = winnerObj.GetComponent<NPCPlayer>();
                Team team = pComp != null ? pComp.team : (npcComp != null ? npcComp.team : null);
                
                if (team != null)
                {
                    winnerTeam = teams.IndexOf(team) + 1;
                    team.roundsWon++;
                }

                string teamNameResult = (team != null) ? team.teamName : (winnerObj != null ? winnerObj.name : "EQUIPO " + winnerTeam);
                string resultMsg = (winnerTeam == 0) ? "¡EMPATE!" : $"GANADOR: {teamNameResult.ToUpper()}";
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(resultMsg, 2f);
                

                if (AudioManager.Instance != null)
                {
                    if (winnerTeam == 0)
                    {
                        AudioManager.Instance.PlaySFX("parda_tie_dissonance");
                    }
                    else
                    {
                        var playerLocal = FindAnyObjectByType<PlayerLocal>();
                        if (playerLocal != null && playerLocal.player != null && playerLocal.player.team == team)
                        {
                            AudioManager.Instance.PlaySFX("trick_won_coin");
                        }
                    }
                }
            }
            else
            {
                string resultMsg = "¡EMPATE!";
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(resultMsg, 2f);

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("parda_tie_dissonance");
                }
            }

            trickWinners.Add(winnerTeam);

            // Parda en la primera vuelta: entra muerte súbita — cada jugador elige UNA
            // sola carta y esa define la mano entera (la tercera no se juega).
            if (trickWinners.Count == 1 && winnerTeam == 0)
            {
                const string suddenDeathMsg = "¡PARDA! MUERTE SÚBITA: ELEGÍ UNA CARTA, LA PRÓXIMA DEFINE LA MANO";
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(suddenDeathMsg, 3.5f);
                if (NetworkServer.active)
                    (NetworkManager.singleton as MyNetworkingManager)?.BroadcastHudEvent(suddenDeathMsg, 3.5f);
            }

            // Notificamos al HUD
            RpcUpdateScores(teams[0].teamScore, teams[1].teamScore, teams[0].roundsWon, teams[1].roundsWon);

            EvaluateHandResolution();
        }

        private void EvaluateHandResolution()
        {
            int currentRound = trickWinners.Count; // 1, 2 or 3
            int handWinner = 0; // 0 = not resolved, 1 = Team 1, 2 = Team 2

            // Rule 1: Someone wins 2 bazas
            if (teams[0].roundsWon >= 2) handWinner = 1;
            else if (teams[1].roundsWon >= 2) handWinner = 2;
            
            // Rule 2: Ties (Empardes)
            if (handWinner == 0)
            {
                if (currentRound == 1)
                {
                    // Tied 1st: Nothing happens yet, continue to 2nd.
                }
                else if (currentRound == 2)
                {
                    if (trickWinners[0] == 0 && trickWinners[1] != 0) handWinner = trickWinners[1];
                    else if (trickWinners[0] != 0 && trickWinners[1] == 0) handWinner = trickWinners[0];
                    // Muerte súbita tras parda en la primera: la mano termina acá sí o sí.
                    // Si la carta de desempate también emparda, gana el equipo Mano.
                    else if (trickWinners[0] == 0 && trickWinners[1] == 0) handWinner = _manoTeamIndex;
                }
                else if (currentRound == 3)
                {
                    if (trickWinners[2] != 0)
                    {
                        handWinner = trickWinners[2];
                    }
                    else
                    {
                        if (trickWinners[0] != 0)
                        {
                            handWinner = trickWinners[0];
                        }
                        else
                        {
                            handWinner = _manoTeamIndex;
                        }
                    }
                }
            }

            if (handWinner != 0)
            {
                ResolveHandWinner(teams[handWinner - 1].teamName, currentHandValue); 
            }
        }

        public void AddAnnouncementPoints(string teamName, int points)
        {
            foreach (var team in teams)
            {
                if (team.teamName == teamName)
                {
                    team.teamScore += points;
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{teamName.ToUpper()} GANA {points} PIEDRAS!");
                    break;
                }
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("score_add_chalk");
            }
            RpcUpdateScores(teams[0].teamScore, teams[1].teamScore, teams[0].roundsWon, teams[1].roundsWon);
        }

        public void ResolveHandWinner(string teamName, int points)
        {
            isHandResolved = true;

            // Stop all pending turns immediately
            foreach (var npc in npcs) npc.ResetTurnState();
            var players = UnityEngine.Object.FindObjectsByType<Code.Player.Player>(UnityEngine.FindObjectsSortMode.None);
            foreach (var p in players) p.canPlayCard = false;
            BroadcastTurnLock();

            StartCoroutine(ResolveHandWinnerCoroutine(teamName, points));
        }

        private System.Collections.IEnumerator ResolveHandWinnerCoroutine(string teamName, int points)
        {
            // 0. Resolver Flor primero (si se cantó)
            var announceManager = FindAnyObjectByType<AnnouncementManager>();
            if (announceManager != null && (announceManager.WasAnnouncementCalledThisHand(AnnounceState.Flor)
                || announceManager.WasAnnouncementCalledThisHand(AnnounceState.ALey)))
            {
                var flor = announceManager.GetComponentInChildren<Code.GameLogic.Announcement.FlorAnnouncement>();
                if (flor != null)
                {
                    flor.UpdateTotalScore();
                    yield return new WaitForSeconds(3.0f); // Tiempo para ver las cartas de la Flor
                }
            }

            // 1. Resolver Envido pendiente primero
            if (pendingEnvidoResolution)
            {
                pendingEnvidoResolution = false;
                
                if (PlayerHUD.Instance != null)
                {
                    PlayerHUD.Instance.NotifyEvent($"ENVIDO: TEAM 1 ({pendingEnvidoScoreTeam1}) VS TEAM 2 ({pendingEnvidoScoreTeam2})", 3.0f);
                }
                
                foreach (var team in teams)
                {
                    if (team.teamName == pendingEnvidoWinnerTeam)
                    {
                        team.teamScore += pendingEnvidoPoints;
                        break;
                    }
                }
                
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("score_add_chalk");
                RpcUpdateScores(teams[0].teamScore, teams[1].teamScore, teams[0].roundsWon, teams[1].roundsWon);
                
                yield return new WaitForSeconds(3.0f); // Esperar a que se lea el resultado del envido
                
                // Chequear si el Envido terminó la partida antes de dar los puntos del Truco
                if (CheckForMatchWinner()) yield break;
            }

            // 2. Resolver los puntos de la mano (Truco)
            Team winningTeam = null;
            foreach (var team in teams)
            {
                if (team.teamName == teamName)
                {
                    team.teamScore += points;
                    winningTeam = team;
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{teamName.ToUpper()} GANA LA MANO (+{points})!", 3.0f);
                    break;
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("score_add_chalk");
            }
            RpcUpdateScores(teams[0].teamScore, teams[1].teamScore, teams[0].roundsWon, teams[1].roundsWon);

            yield return new WaitForSeconds(2.0f); // Esperar para que se lea la victoria de la mano
            
            // Check if game is over (score >= 30)
            if (CheckForMatchWinner()) yield break;

            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            Team humanTeam = (playerLocal != null && playerLocal.player != null) ? playerLocal.player.team : null;

            // Play hand win fanfare if human team won this hand
            if (AudioManager.Instance != null && humanTeam != null && winningTeam == humanTeam)
            {
                AudioManager.Instance.PlaySFX("score_buenas_fanfare");
            }
            StartCoroutine(DelayedNewHand());
        }

        private bool CheckForMatchWinner()
        {
            if (_matchEnded) return true;

            bool gameOver = false;
            Team matchWinner = null;
            foreach (var team in teams)
            {
                if (team.teamScore >= maxPoints)
                {
                    gameOver = true;
                    matchWinner = team;
                    break;
                }
            }

            if (gameOver && matchWinner != null)
            {
                _matchEnded = true;
                var playerLocal = FindAnyObjectByType<PlayerLocal>();
                Team humanTeam = (playerLocal != null && playerLocal.player != null) ? playerLocal.player.team : null;

                Debug.Log($"[GameManager] ¡PARTIDA FINALIZADA! Ganador: {matchWinner.teamName} con {matchWinner.teamScore} puntos.");

                if (PlayerHUD.Instance != null)
                {
                    PlayerHUD.Instance.NotifyEvent($"¡{matchWinner.teamName.ToUpper()} GANA LA PARTIDA!", 5f);
                }
                
                if (AudioManager.Instance != null)
                {
                    if (humanTeam != null && matchWinner == humanTeam)
                    {
                        AudioManager.Instance.PlaySFX("match_victory_melody");
                    }
                    else
                    {
                        AudioManager.Instance.PlaySFX("match_defeat_sadness");
                    }
                }
                StartCoroutine(DelayedQuitToMainMenu());
                return true;
            }
            return false;
        }

        private System.Collections.IEnumerator DelayedQuitToMainMenu()
        {
            yield return new WaitForSeconds(6.0f);

            if (PlayerHUD.Instance != null)
            {
                Destroy(PlayerHUD.Instance.gameObject);
            }
            var debugCommands = FindAnyObjectByType<DebugCommands>();
            if (debugCommands != null)
            {
                Destroy(debugCommands.gameObject);
            }

            // Multiplayer: stopping the session is enough — Mirror destroys the spawned
            // player objects (a manual Destroy on a spawned identity corrupts its state)
            // and OnClientDisconnect performs the single scene change back to the menu.
            if (NetworkServer.active)
            {
                NetworkManager.singleton.StopHost();
                yield break;
            }
            if (NetworkClient.active)
            {
                NetworkManager.singleton.StopClient();
                yield break;
            }

            // Singleplayer: the player object is ours (DontDestroyOnLoad), clean it up
            // manually along with this GameManager before going back.
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            if (playerLocal != null)
            {
                Destroy(playerLocal.gameObject);
            }
            Destroy(gameObject);
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private System.Collections.IEnumerator DelayedNewHand()
        {
            yield return new WaitForSeconds(2.8f);

            bool animationFinished = false;
            TableManager.Instance.AnimateCardsToDeck(() => { animationFinished = true; });
            
            yield return new WaitUntil(() => animationFinished);
            yield return new WaitForSeconds(0.2f);

            StartNewHand();
        }

        private void StartNewHand()
        {
            isHandResolved = false;
            round = 0;
            currentHandValue = 1; 
            lastTrucoTeamIndex = 0;
            lastTrickWinnerSeatIndex = -1;
            trickWinners.Clear();
            ResetTeamsRoundsWon();

            // Limpiar estados de turno de todos los NPCs y jugadores
            foreach (var npc in npcs) npc.ResetTurnState();
            var players = UnityEngine.Object.FindObjectsByType<Code.Player.Player>(UnityEngine.FindObjectsSortMode.None);
            foreach (var p in players) p.canPlayCard = false;
            BroadcastTurnLock();

            // Reset announcements state for the new hand
            var announceManager = FindAnyObjectByType<AnnouncementManager>();
            if (announceManager != null) announceManager.ResetState();

            int startingDealer = dealerIndex;
            do
            {
                dealerIndex = (dealerIndex + 1) % SeatManager.Instance.allChairs.Count;
            } while (SeatManager.Instance.allChairs[dealerIndex].occupant == null && dealerIndex != startingDealer);

            int manoSeatIndex = dealerIndex;
            do
            {
                manoSeatIndex = (manoSeatIndex + 1) % SeatManager.Instance.allChairs.Count;
            } while (SeatManager.Instance.allChairs[manoSeatIndex].occupant == null && manoSeatIndex != dealerIndex);

            // Determinar el equipo basándose en el ocupante de la silla
            var occupant = SeatManager.Instance.allChairs[manoSeatIndex].occupant;
            var playerComp = occupant != null ? occupant.GetComponent<Code.Player.Player>() : null;
            var npcComp = occupant != null ? occupant.GetComponent<NPCPlayer>() : null;
            Team manoTeam = playerComp != null ? playerComp.team : (npcComp != null ? npcComp.team : null);
            _manoTeamIndex = manoTeam != null ? teams.IndexOf(manoTeam) + 1 : 1;

            currentTrickStartSeatIndex = manoSeatIndex;
            
            
            // Notificamos puntaje final y reseteo de bazas
            RpcUpdateScores(teams[0].teamScore, teams[1].teamScore, 0, 0);
            OnScoreChanged?.Invoke(teams[0].teamScore, teams[1].teamScore);

            var stateMachine = GetComponent<Code.GameLogic.States.GameStateMachine>();
            if (stateMachine != null)
            {
                stateMachine.ChangeState(new Code.GameLogic.States.DealingState());
            }
        }

        // [Server]
        public void AddScoreToTeam(string teamName, int pointsToIncrease)
        {
            // Redirect based on context (mostly used for Truco/Hand end)
            ResolveHandWinner(teamName, pointsToIncrease);
        }

        // [Server]
        private void ResetTeamsRoundsWon()
        {
            foreach (var team in teams) team.roundsWon = 0;
        }

        private void RpcUpdateScores(int scoreTeam1, int scoreTeam2, int roundsTeam1 = 0, int roundsTeam2 = 0)
        {
            teams[0].teamScore = scoreTeam1;
            teams[1].teamScore = scoreTeam2;
            teams[0].roundsWon = roundsTeam1;
            teams[1].roundsWon = roundsTeam2;

            if (PlayerHUD.Instance != null)
                PlayerHUD.Instance.UpdateScore(scoreTeam1, scoreTeam2, roundsTeam1, roundsTeam2);

            // Broadcast to all clients in multiplayer
            if (NetworkServer.active)
            {
                var netMgr = NetworkManager.singleton as MyNetworkingManager;
                netMgr?.BroadcastScores(scoreTeam1, scoreTeam2, roundsTeam1, roundsTeam2);
            }
        }
    }
}
