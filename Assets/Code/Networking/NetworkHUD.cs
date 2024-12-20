using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Code.Networking
{
    public class NetworkHUD : MonoBehaviour
    {
        private MyNetworkingManager _myNetworkingManager;
        [SerializeField] private GameObject _lobbyObject;

        private Button _startClientButton;
        private Button _startHostButton;

        private void Start()
        {
            _myNetworkingManager = GetComponent<MyNetworkingManager>();
            _startClientButton = GameObject.Find("StartClientButton").GetComponent<Button>();
            _startHostButton = GameObject.Find("StartHostButton").GetComponent<Button>();

            if (_lobbyObject == null)
                _lobbyObject = GameObject.Find("LobbyPanel");
        }

        public void StartHost()
        {
            if (!NetworkClient.active)
            {
                _myNetworkingManager.StartHost();
                ChangeButtonState(false);
                _lobbyObject.SetActive(true);
            }
        }

        public void StopHost()
        {
            if (NetworkClient.active)
            {
                _myNetworkingManager.StopHost();
                ChangeButtonState(true);
                _lobbyObject.SetActive(false);
            }
        }

        public void JoinHost()
        {
            _myNetworkingManager.networkAddress = "localhost";
            ChangeButtonState(false);
            _myNetworkingManager.StartClient();
            _lobbyObject.SetActive(true);
            Debug.Log("Client connected");
        }

        void ChangeButtonState(bool isInteractable)
        {
            _startClientButton.interactable = isInteractable;
            _startHostButton.interactable = isInteractable;
        }
    }
}