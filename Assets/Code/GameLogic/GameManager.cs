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
                _instance = FindAnyObjectByType<GameManager>();

                if (_instance == null)
                {
                    Debug.Log("New Game Manager has been created!");
                    var obj = new GameObject("GameManager");
                    _instance = obj.AddComponent<GameManager>();
                    DontDestroyOnLoad(obj);
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
            }
            else
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }

            if (GetComponent<NetworkIdentity>() == null)
            {
                gameObject.AddComponent<NetworkIdentity>();
            }

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
            OnStartGame();
        }

        [ClientRpc]
        private void OnStartGame()
        {
            Debug.Log("Game has started");
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
    }
}