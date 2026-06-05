using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Networking
{
    public class NetworkHUD : MonoBehaviour
    {
        private MyNetworkingManager _myNetworkingManager;
        [SerializeField] private GameObject _lobbyObject;
        [SerializeField] private GameObject _startButtonGame;

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
            if (NetworkClient.active) return;
            
            _myNetworkingManager.StartHost();
            ChangeButtonState(false);
            _lobbyObject.SetActive(true);
        }

        //public void StopHost()
        //{
        //    if (!NetworkClient.active) return;
        //    
        //    _myNetworkingManager.StopHost();
        //    ChangeButtonState(true);
        //    _lobbyObject.SetActive(false);
        //}

        public void JoinHost()
        {
            if (_myNetworkingManager.isNetworkActive) return;
    
            _myNetworkingManager.networkAddress = "localhost";
            _myNetworkingManager.StartClient();
            ChangeButtonState(false);
            _lobbyObject.SetActive(true);
            _startButtonGame.SetActive(false);
        }


        private void ChangeButtonState(bool isInteractable)
        {
            _startClientButton.interactable = isInteractable;
            _startHostButton.interactable = isInteractable;
        }
    }
}
