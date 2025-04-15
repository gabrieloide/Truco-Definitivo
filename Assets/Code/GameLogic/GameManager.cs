using System;
using System.Collections.Generic;
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
        public Team[] teams;

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
            teams = new Team[2]
            {
                new Team("Team 1"),
                new Team("Team 2")
            };

            playerInput.Enable();
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
            round++;
            RunOnlyOnce();
        }

        [Server]
        private void RunOnlyOnce()
        {
            serverPlayers[0].canPlayCard = true;
            _gameSceneStarted = true;
        }

        [Command]
        public void Spawneables(GameObject GO)
        {
            NetworkServer.Spawn(GO);
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