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

namespace Code.GameLogic
{
    public class GameManager : NetworkBehaviour
    {
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

        [SyncVar] public int currentPlayerTurn = 0;
        [SyncVar] public int playerCount;
        [SyncVar] public bool deckIsLocked;
        [SyncVar] public int round;

        public bool isGameScene;
        [HideInInspector] public PlayerInput playerInput;
        [SerializeField] private bool _gameSceneStarted = false;
        public List<Team> teams = new List<Team>();

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

        private void Update()
        {
            if (!isServer) return;


            if (serverPlayers == null || serverPlayers.Count == 0)
            {
                Debug.Log("There is no enough players to start to play, please wait for others players");
                return;
            }

            if (!isGameScene) return;
            NextPlayer(serverPlayers[currentPlayerTurn]);
            if (_gameSceneStarted) return;
            RunOnlyOnce();
        }


        public GameObject[] GetOpponentTeam(GameObject currentPlayer)
        {
            if (currentPlayer == null)
            {
                Debug.LogError("Current player is null!");
                return null;
            }

            PlayerLocal currentPlayerLocal = currentPlayer.GetComponent<PlayerLocal>();
            if (currentPlayerLocal == null)
            {
                Debug.LogError("Player doesn't have a PlayerLocal component!");
                return null;
            }

            var opponentTeamName = "";

            foreach (var team in teams)
            {
                if (team.teamName != currentPlayerLocal.player.team.teamName)
                {
                    opponentTeamName = team.teamName;
                    break;
                }
            }

            GameObject[] opponentPlayers = serverPlayers
                .Where(player => player.player.team.teamName == opponentTeamName)
                .Select(player => player.gameObject)
                .ToArray();

            return opponentPlayers;
        }

        [Server]
        private void RunOnlyOnce()
        {
            serverPlayers[0].player.canPlayCard = true;
            _gameSceneStarted = true;
            
            // Server deals cards
            var deckCreator = FindAnyObjectByType<DeckCreator>();
            if (deckCreator != null)
            {
                deckCreator.ShuffleAndSetVira();

                foreach (var player in serverPlayers)
                {
                    var dealtCards = deckCreator.DealCards(3);
                    player.cardsHandler.TargetReceiveCards(player.connectionToClient, dealtCards);
                }
            }
            
            OnStartGame();
        }

        [ClientRpc]
        private void OnStartGame()
        {
            Debug.Log("Game has started! Check the Vira.");
        }

        [Server]
        private void NextPlayer(PlayerLocal player)
        {
            foreach (var p in serverPlayers)
            {
                var isCurrentPlayer = (p == player);
                p.RpcRequestChangeTurn(p.connectionToClient, isCurrentPlayer);
            }
        }

        [Server]
        public void AddPlayerToServer(PlayerLocal player)
        {
            if (!isServer)
                return;
            serverPlayers.Add(player);
            playerCount++;
        }

        [Server]
        public void AddScoreToTeam(string teamName, int pointsToIncrease)
        {
            foreach (var team in teams)
            {
                if (team.teamName == teamName)
                {
                    team.roundsWon++;
                    if (team.roundsWon >= 2)
                    {
                        team.teamScore += pointsToIncrease;
                        ResetTeamsRoundsWon();
                        // Sync scores to all clients
                        RpcUpdateScores(teams[0].teamScore, teams[1].teamScore);
                    }
                    break;
                }
            }
        }

        [Server]
        private void ResetTeamsRoundsWon()
        {
            foreach (var team in teams) team.roundsWon = 0;
        }

        [ClientRpc]
        private void RpcUpdateScores(int scoreTeam1, int scoreTeam2)
        {
            teams[0].teamScore = scoreTeam1;
            teams[1].teamScore = scoreTeam2;
            
            if (PlayerHUD.Instance != null) 
            {
                PlayerHUD.Instance.ChangeScoreText();
            }
        }
    }
}