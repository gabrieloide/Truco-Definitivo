using System;
using System.Collections.Generic;
using System.Linq;
using Code.Cards;
using Code.Networking;
using Code.Player;
// using Mirror;
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
        
        public bool isServer = true;
        private static GameManager _instance;

        public static GameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<GameManager>();
                    if (_instance == null)
                    {
                        Debug.LogError("GameManager is missing from the scene! Make sure it is added via the Editor for Mirror to work properly.");
                    }
                }
                return _instance;
            }
        }

        [Header("Player Settings")]
        public PlayerLocal localPlayer; // Referencia al jugador local (asignada manual o dinámicamente)
        public int defaultLocalSeatIndex = 0; // Índice de la silla donde el jugador local debería empezar

        [Header("Prefabs")]
        public GameObject deckPrefab;

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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
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

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameScene")
            {
                isGameScene = false;
                _gameSceneStarted = false;
                return;
            }

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
            if (Camera.main != null && Camera.main.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
            {
                Camera.main.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            }

            _gameSceneStarted = true;
            
            // Ensure only one DeckCreator exists in the scene to prevent Vira or shuffle desyncs
            DeckCreator deckCreator = DeckCreator.Instance;
            
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
            }

            var localPlayer = FindAnyObjectByType<PlayerLocal>();
            
            if (localPlayer == null)
            {
                var p = FindAnyObjectByType<Code.Player.Player>();
                if (p != null) localPlayer = p.GetComponent<PlayerLocal>();
            }

#if UNITY_EDITOR
            if (localPlayer == null)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player.prefab");
                if (prefab != null)
                {
                    var playerObj = Instantiate(prefab);
                    localPlayer = playerObj.GetComponent<PlayerLocal>();
                }
            }
#endif

            npcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None).ToList();
            allPlayers = FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None).ToList();

            if (deckCreator != null && (localPlayer != null || FindAnyObjectByType<Code.Player.Player>() != null))
            {
                var mainPlayerComponent = localPlayer != null ? localPlayer.player : FindAnyObjectByType<Code.Player.Player>();
                var mainCardsHandler = localPlayer != null ? localPlayer.cardsHandler : FindAnyObjectByType<CardsHandler>();
                var playerGameObject = localPlayer != null ? localPlayer.gameObject : mainPlayerComponent.gameObject;

                if (SeatManager.Instance != null)
                {
                    if (SeatManager.Instance.allChairs.Count > defaultLocalSeatIndex)
                    {
                        SeatManager.Instance.RequestSeat(playerGameObject, SeatManager.Instance.allChairs[defaultLocalSeatIndex]);
                    }
                    else
                    {
                        SeatManager.Instance.AutoSeatLocalPlayer(playerGameObject);
                    }
                }
                else
                {
                    Debug.LogError("[GameManager] SeatManager.Instance es NULL!");
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
            if (playerComp != null) playerComp.canPlayCard = true;
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

            Team winningTeam = null;
            foreach (var team in teams)
            {
                if (team.teamName == teamName)
                {
                    team.teamScore += points;
                    winningTeam = team;
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{teamName.ToUpper()} GANA LA MANO (+{points})!");
                    break;
                }
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("score_add_chalk");
            }

            // Check if game is over (score >= 30)
            bool gameOver = false;
            Team matchWinner = null;
            foreach (var team in teams)
            {
                if (team.teamScore >= 30)
                {
                    gameOver = true;
                    matchWinner = team;
                    break;
                }
            }

            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            Team humanTeam = (playerLocal != null && playerLocal.player != null) ? playerLocal.player.team : null;

            if (gameOver && matchWinner != null)
            {
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
            }
            else
            {
                // Play hand win fanfare if human team won this hand
                if (AudioManager.Instance != null && humanTeam != null && winningTeam == humanTeam)
                {
                    AudioManager.Instance.PlaySFX("score_buenas_fanfare");
                }
                StartCoroutine(DelayedNewHand());
            }
        }

        private System.Collections.IEnumerator DelayedQuitToMainMenu()
        {
            yield return new WaitForSeconds(6.0f);
            
            if (PlayerHUD.Instance != null)
            {
                Destroy(PlayerHUD.Instance.gameObject);
            }
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            if (playerLocal != null)
            {
                Destroy(playerLocal.gameObject);
            }
            var debugCommands = FindAnyObjectByType<DebugCommands>();
            if (debugCommands != null)
            {
                Destroy(debugCommands.gameObject);
            }
            
            Destroy(gameObject); // Destroy GameManager
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

        // [ClientRpc]
        private void RpcUpdateScores(int scoreTeam1, int scoreTeam2, int roundsTeam1 = 0, int roundsTeam2 = 0)
        {
            teams[0].teamScore = scoreTeam1;
            teams[1].teamScore = scoreTeam2;
            teams[0].roundsWon = roundsTeam1;
            teams[1].roundsWon = roundsTeam2;
            
            if (PlayerHUD.Instance != null) 
            {
                PlayerHUD.Instance.UpdateScore(scoreTeam1, scoreTeam2, roundsTeam1, roundsTeam2);
            }
        }
    }
}
