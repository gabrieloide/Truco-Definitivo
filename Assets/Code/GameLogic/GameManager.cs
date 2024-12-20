using System.Collections.Generic;
using Code.Cards;
using Mirror;
using UnityEngine;

namespace Code.GameLogic
{
    public class GameManager : NetworkBehaviour
    {
        private static GameManager _instance;

        public static GameManager Instance
        {
            get
            {
                _instance = FindObjectOfType<GameManager>();

                if (_instance == null)
                {
                    GameObject obj = new GameObject("GameManager");
                    _instance = obj.AddComponent<GameManager>();
                    DontDestroyOnLoad(obj);
                }


                return _instance;
            }
        }

        public List<PlayerController> serverPlayers = new List<PlayerController>();
        [SyncVar] public int currentPlayerTurn = 0;
        private bool _gameStarted = false;

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

            ManagePlayerTurn();
        }

        void ManagePlayerTurn()
        {
            if (serverPlayers.Count % 2 == 0 && !_gameStarted)
            {
                _gameStarted = true;
                if (_gameStarted)
                {
                    serverPlayers[currentPlayerTurn].player.canPlayCard = true;
                }
            }
            else if (_gameStarted)
            {
                NextPlayer(serverPlayers[currentPlayerTurn]);
            }
        }

        [Server]
        public void AddPlayerToServer(PlayerController player)
        {
            serverPlayers.Add(player);
            Debug.Log($"Player {player.player.playerName} added to the server.");
        }

        [Server]
        public void RemovePlayerFromServer(PlayerController player)
        {
            serverPlayers.Remove(player);
            Debug.Log($"Player {player.player.playerName} removed from the server.");
        }

        [Server]
        void NextPlayer(PlayerController player)
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