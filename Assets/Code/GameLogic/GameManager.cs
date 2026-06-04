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

        public List<PlayerLocal> serverPlayers = new List<PlayerLocal>();
        public List<Code.Player.Player> allPlayers = new List<Code.Player.Player>();

        public int currentPlayerTurn = 0;
        public int playerCount;
        public bool deckIsLocked;
        public int round;
        public int dealerIndex = 0; // The seat index of the player who is dealing
        public bool devMode;

        public bool isGameScene;
        [HideInInspector] public PlayerInput playerInput;
        [SerializeField] private bool _gameSceneStarted = false;
        public List<Team> teams = new List<Team>();
        public bool isAnnouncementPending = false; // Bloquea el flujo del juego para esperar respuesta

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);

            playerInput = new PlayerInput();
            playerInput.Enable();

            teams.Add(new Team("Team 1"));
            teams.Add(new Team("Team 2"));
        }

        private void OnEnable()
        {
            TableManager.OnCardPlaced += HandleCardPlaced;
        }

        private void OnDisable()
        {
            TableManager.OnCardPlaced -= HandleCardPlaced;
        }

        private void HandleCardPlaced(Card card, GameObject player)
        {
            // End the current turn after a brief delay to let card animation finish
            StartCoroutine(DelayedEndTurn());
        }

        private System.Collections.IEnumerator DelayedEndTurn()
        {
            yield return new WaitForSeconds(2.5f);
            EndTurn();
        }

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
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene")
            {
                isGameScene = true;
            }

            if (!isGameScene) return;

            // Debug for PlayerHUD presence
            if (Time.frameCount % 300 == 0) // Every ~5 seconds at 60fps
            {
                var hud = FindAnyObjectByType<PlayerHUD>();
                if (hud == null)
                {
                    Debug.LogWarning("[GameManager] CRITICAL: No se encontró ningún PlayerHUD en la escena.");
                }
                Debug.Log($"[GameManager] Status: isGameScene={isGameScene}, _gameSceneStarted={_gameSceneStarted}");
            }

            if (_gameSceneStarted) return;

            RunOnlyOnce();
        }

        private void RunOnlyOnce()
        {
            _gameSceneStarted = true;
            Debug.Log("[GameManager] RunOnlyOnce: Iniciando partida...");
            
            // Intentar encontrar DeckCreator de forma más exhaustiva (incluyendo inactivos)
            var deckCreator = FindFirstObjectByType<DeckCreator>(FindObjectsInactive.Include);
            
            // Si sigue sin aparecer, intentamos crearlo dinámicamente o buscarlo en los hijos del GameManager
            if (deckCreator == null)
            {
                deckCreator = GetComponentInChildren<DeckCreator>();
            }

            if (deckCreator == null)
            {
                Debug.LogWarning("[GameManager] DeckCreator no encontrado en la escena. Intentando crearlo dinámicamente...");
                GameObject go = new GameObject("DeckCreator_Generated");
                deckCreator = go.AddComponent<DeckCreator>();
            }

            var localPlayer = FindAnyObjectByType<PlayerLocal>();
            
            if (localPlayer == null)
            {
                Debug.Log("[GameManager] Buscando Player component...");
                var p = FindAnyObjectByType<Code.Player.Player>();
                if (p != null) localPlayer = p.GetComponent<PlayerLocal>();
            }

#if UNITY_EDITOR
            if (localPlayer == null)
            {
                Debug.Log("[GameManager] Editor: Spawning local player from prefab...");
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
            Debug.Log($"[GameManager] NPCs encontrados: {npcs.Count}");

            if (deckCreator != null && (localPlayer != null || FindAnyObjectByType<Code.Player.Player>() != null))
            {
                Debug.Log("[GameManager] DeckCreator y Player encontrados. Configurando mesa...");
                var mainPlayerComponent = localPlayer != null ? localPlayer.player : FindAnyObjectByType<Code.Player.Player>();
                var mainCardsHandler = localPlayer != null ? localPlayer.cardsHandler : FindAnyObjectByType<CardsHandler>();
                var playerGameObject = localPlayer != null ? localPlayer.gameObject : mainPlayerComponent.gameObject;

                deckCreator.ShuffleAndSetVira();
                
                if (SeatManager.Instance != null)
                {
                    SeatManager.Instance.AutoSeatLocalPlayer(playerGameObject);
                }
                else
                {
                    Debug.LogError("[GameManager] SeatManager.Instance es NULL!");
                }

                var myCards = deckCreator.DealCards(3);
                if (mainCardsHandler != null) 
                {
                    Debug.Log("[GameManager] Repartiendo cartas al jugador local...");
                    mainCardsHandler.TargetReceiveCards(myCards);
                }
                
                foreach (var npc in npcs)
                {
                    Debug.Log($"[GameManager] Repartiendo cartas a NPC: {npc.playerName}");
                    npc.ReceiveCards(deckCreator.DealCards(3));
                    if (SeatManager.Instance != null && SeatManager.Instance.GetPlayerSeatIndex(npc.gameObject) == -1)
                    {
                        foreach (var chair in SeatManager.Instance.allChairs)
                        {
                            if (!chair.isOccupied)
                            {
                                chair.occupant = npc.gameObject;
                                chair.isOccupied = true;
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

                _totalPlayersCount = 1 + npcs.Count;
                dealerIndex = (SeatManager.Instance.allChairs.Count - 1) % SeatManager.Instance.allChairs.Count;
                _currentManoSeatIndex = (dealerIndex + 1) % SeatManager.Instance.allChairs.Count;
                _currentTrickStartSeatIndex = _currentManoSeatIndex;

                Debug.Log($"[GameManager] Dealer: {dealerIndex}, Mano: {_currentManoSeatIndex}");

                UpdateDeckAndVira();
                StartTurn(_currentTrickStartSeatIndex);
            }
            else
            {
                Debug.LogWarning($"[GameManager] No se pudo iniciar: deckCreator={deckCreator!=null}, localPlayer={localPlayer!=null}");
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
            Debug.Log($"[GameManager] Turno de: {occupant.name} (Silla {currentPlayerTurn})");

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
            Debug.Log("Game has started! Check the Vira.");
        }

        public void UpdateDeckAndVira()
        {
            if (SeatManager.Instance == null || TableManager.Instance == null) return;

            var deckCreator = FindAnyObjectByType<DeckCreator>();
            
            if (deckCreator != null)
            {
                Debug.Log($"[GameManager] El repartidor es la silla {dealerIndex}. Colocando mazo y vira...");
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
                Debug.Log($"[GameManager] Jugador {player.name} añadido al servidor. Total: {playerCount}");
            }
        }

        private int _lastTrickWinnerSeatIndex = -1;
        private int _currentManoSeatIndex = 0; // The seat that starts every trick in the hand
        private int _currentTrickStartSeatIndex = 0; // The seat that starts the current baza

        public void EndTurn()
        {
            // Bloqueamos el turno actual para todos antes de evaluar
            var players = this.allPlayers;
            foreach (var p in players) p.canPlayCard = false;
            
            // Usamos ResetTurnState para asegurar que los NPCs detengan cualquier acción pendiente
            foreach (var npc in npcs) npc.ResetTurnState();

            int cardsPlayed = TableManager.Instance.CardsInTable.Count;
            int totalExpectedCards = _totalPlayersCount;

            Debug.Log($"[GameManager] EndTurn: Cartas en mesa {cardsPlayed}/{totalExpectedCards}. Ronda {round}");

            if (cardsPlayed >= totalExpectedCards)
            {
                StartCoroutine(DelayedTrickResolution());
                return;
            }

            // Turno normal dentro de una baza: Siguiente silla en sentido ANTIHORARIO (incrementando índice)
            int nextIndex = (currentPlayerTurn + 1) % SeatManager.Instance.allChairs.Count;
            
            Debug.Log($"[GameManager] Turno finalizado. Siguiente en sentido antihorario: Silla {nextIndex}");
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

            // Si la mano finalizó en el camino, abortamos la transición
            if (round == 0 && roundBeforeEvaluation != 0)
            {
                Debug.Log("[GameManager] La mano finalizó en DelayedTrickResolution.");
                yield break;
            }

            TableManager.Instance.CardsInTable.Clear();
            round++;

            if (round >= 3)
            {
                Debug.Log("[GameManager] Fin de la 3ra ronda. Iniciando nueva mano.");
                StartNewHand();
            }
            else
            {
                if (_lastTrickWinnerSeatIndex != -1)
                {
                    _currentTrickStartSeatIndex = _lastTrickWinnerSeatIndex;
                }
                Debug.Log($"[GameManager] Baza finalizada. Sale el anterior ganador: Silla {_currentTrickStartSeatIndex}");
                StartTurn(_currentTrickStartSeatIndex);
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
            _lastTrickWinnerSeatIndex = -1;

            if (winnerObj != null)
            {
                _lastTrickWinnerSeatIndex = SeatManager.Instance.GetPlayerSeatIndex(winnerObj);
                
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
                
                Debug.Log($"[GameManager] Baza {trickWinners.Count}: {resultMsg}");
            }
            else
            {
                string resultMsg = "¡EMPATE!";
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(resultMsg, 2f);
                Debug.Log($"[GameManager] Baza {trickWinners.Count}: {resultMsg}");
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
                Debug.Log($"[GameManager] ¡MANO FINALIZADA! Ganador: Equipo {handWinner} por {currentHandValue} puntos.");
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
                    Debug.Log($"[GameManager] Añadidos {points} puntos de ANUNCIO al equipo {teamName}. Total: {team.teamScore}");
                    break;
                }
            }
            RpcUpdateScores(teams[0].teamScore, teams[1].teamScore, teams[0].roundsWon, teams[1].roundsWon);
        }

        public void ResolveHandWinner(string teamName, int points)
        {
            foreach (var team in teams)
            {
                if (team.teamName == teamName)
                {
                    team.teamScore += points;
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{teamName.ToUpper()} GANA LA MANO (+{points})!");
                    Debug.Log($"[GameManager] Equipo {teamName} gana la MANO por {points} puntos.");
                    break;
                }
            }
            StartCoroutine(DelayedNewHand());
        }

        private System.Collections.IEnumerator DelayedNewHand()
        {
            yield return new WaitForSeconds(2.8f);
            StartNewHand();
        }

        private void StartNewHand()
        {
            Debug.Log("[GameManager] --- INICIANDO NUEVA MANO ---");
            round = 0;
            currentHandValue = 1; 
            lastTrucoTeamIndex = 0;
            _lastTrickWinnerSeatIndex = -1;
            trickWinners.Clear();
            TableManager.Instance.ClearTable();
            ResetTeamsRoundsWon();

            // Limpiar estados de turno de todos los NPCs y jugadores
            foreach (var npc in npcs) npc.ResetTurnState();
            var players = this.allPlayers;
            foreach (var p in players) p.canPlayCard = false;

            // Reset announcements state for the new hand
            var announceManager = FindAnyObjectByType<AnnouncementManager>();
            if (announceManager != null) announceManager.ResetState();

            var deckCreator = FindAnyObjectByType<DeckCreator>();
            if (deckCreator != null)
            {
                deckCreator.ShuffleAndSetVira();
                
                var myCards = deckCreator.DealCards(3);
                var mainCardsHandler = FindAnyObjectByType<CardsHandler>();
                if (mainCardsHandler != null) mainCardsHandler.TargetReceiveCards(myCards);
                
                foreach (var npc in npcs) npc.ReceiveCards(deckCreator.DealCards(3));
                
                UpdateDeckAndVira();
            }
            
            dealerIndex = (dealerIndex + 1) % SeatManager.Instance.allChairs.Count;
            int manoSeatIndex = (dealerIndex + 1) % SeatManager.Instance.allChairs.Count;
            _manoTeamIndex = (manoSeatIndex % 2) + 1; // Team 1 or 2
            _currentTrickStartSeatIndex = manoSeatIndex;
            
            Debug.Log($"[GameManager] Nueva mano. Repartidor: Silla {dealerIndex}. Mano: Silla {manoSeatIndex} (Equipo {_manoTeamIndex})");
            
            // Notificamos puntaje final y reseteo de bazas
            RpcUpdateScores(teams[0].teamScore, teams[1].teamScore, 0, 0);
            OnScoreChanged?.Invoke(teams[0].teamScore, teams[1].teamScore);

            StartTurn(_currentTrickStartSeatIndex);
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