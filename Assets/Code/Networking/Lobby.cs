using System;
using System.Collections.Generic;
using Code.GameLogic;
using Mirror;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;


namespace Code.Networking
{
    public class Lobby : NetworkBehaviour
    {
        [SerializeField] private TMP_Text roomName;

        [SerializeField] private TMP_Text[] _lobbyPlayersText;

        private string _restrictedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLOPQRSTUVWXYZ0123456789";


        [SyncVar] public string _allLobbyPlayers;
        [SerializeField] public List<string> _listOfLobbyPlayers = new List<string>();
        [SerializeField] private int _indexLastPlayer = 0;

        private void Awake()
        {
            if (!gameObject.activeSelf) return;

            if (_lobbyPlayersText == null)
            {
                Debug.LogError("LobbyPlayersText is null!");
                return;
            }

            foreach (var t in _lobbyPlayersText)
            {
                t.text = string.Empty;
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            GameManager.Instance.playerCount = _listOfLobbyPlayers.Count;
        }

        private void PlayerNames()
        {
            var index = 0;

            foreach (var t in _lobbyPlayersText)
            {
                if (index < _listOfLobbyPlayers.Count)
                {
                    t.text = _listOfLobbyPlayers[index];
                    index++;
                }
            }
        }

        [ClientRpc]
        public void AddPlayerToLobby(string playerName)
        {
            _allLobbyPlayers += $"{playerName};";
            var players = _allLobbyPlayers;
            var addPlayer = "";

            for (var i = _indexLastPlayer; i < players.Length; i++)
            {
                if (players[i] != ';')
                {
                    addPlayer += players[i];
                }
                else
                {
                    _listOfLobbyPlayers.Add(addPlayer);
                    addPlayer = "";
                    _indexLastPlayer = i + 1;
                }
            }

            PlayerNames();
        }

        [Server]
        public void PlayGame()
        {
            if (GameManager.Instance.serverPlayers.Count % 2 != 0)
            {
                Debug.Log("There is no enough players in Lobby.");
                return;
            }
            
            SceneChanger.Instance.ChangeScene("GameScene");
        }
    }
}