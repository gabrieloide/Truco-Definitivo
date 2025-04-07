using System;
using System.Collections.Generic;
using Code.Cards;
using Code.Networking;
using Code.Player;
using Code.Cards;
using Mirror;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
                    GameObject obj = new GameObject("GameManager");
                    _instance = obj.AddComponent<GameManager>();
                    DontDestroyOnLoad(obj);
                }

                return _instance;
            }
        }

        public List<PlayerLocal> serverPlayers = new List<PlayerLocal>();
        [FormerlySerializedAs("localPlayerController")] public PlayerLocal localPlayerLocal;
        [SyncVar] public int currentPlayerTurn = 0;

        [HideInInspector] public int playerCount;
        public bool isGameScene;
        [HideInInspector] public PlayerInput _playerInput;
        [SerializeField] public TMP_Text currentTurnText;

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

        private bool IsLastCardPlayed() => currentPlayerTurn == serverPlayers.Count - 1 ? true : false;

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
                if (p == player)
                {
                    p.RpcRequestChangeTurn(connectionToClient, true);
                }
                else
                {
                    p.RpcRequestChangeTurn(connectionToClient, false);
                }
            }
        }

        [Server]
        public void AddPlayerToServer(PlayerLocal player)
        {
            serverPlayers.Add(player);
            Debug.Log($"Player {player.player.playerName} added to the server.");
        }
    }
}