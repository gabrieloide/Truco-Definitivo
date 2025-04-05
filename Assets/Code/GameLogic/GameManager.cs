using System;
using System.Collections.Generic;
using Code.Cards;
using Code.Networking;
using Code.Player;
using Mirror;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                    GameObject obj = new GameObject("GameManager");
                    _instance = obj.AddComponent<GameManager>();
                    DontDestroyOnLoad(obj);
                }

                return _instance;
            }
        }

        public List<PlayerController> serverPlayers = new List<PlayerController>();

        [SyncVar] public int currentPlayerTurn = 0;
        public int playerCount;
        public bool isGameScene;
        [HideInInspector] public PlayerInput _playerInput;

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

            _playerInput = new PlayerInput();
            _playerInput.Enable();
        }

        private void OnDisable()
        {
            _playerInput.Disable();
        }

        private void Update()
        {
            if (!isServer)
                return;

            if (serverPlayers == null || serverPlayers.Count == 0)
            {
                Debug.Log("There is no enough players to start to play, please wait for others players");
                return;
            }

            if (isGameScene)
            {
                NextPlayer(serverPlayers[currentPlayerTurn]);
            }
        }

        [Command]
        public void Spawneables(GameObject GO)
        {
            NetworkServer.Spawn(GO);
        }

        [Server]
        public void AddPlayerToServer(PlayerController player)
        {
            serverPlayers.Add(player);
            Debug.Log($"Player {player.player.playerName} added to the server.");
        }

        [Server]
        private void NextPlayer(PlayerController player)
        {
            foreach (var p in serverPlayers)
            {
                if (p == player)
                {
                    p.RpcRequestChangeTurn(p.connectionToClient, true);
                }
                else
                {
                    p.RpcRequestChangeTurn(p.connectionToClient, false);
                }
            }
        }
    }
}