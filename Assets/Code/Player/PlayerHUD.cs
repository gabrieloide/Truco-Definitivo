using System;
using Code.GameLogic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Player
{
    public class PlayerHUD : MonoBehaviour
    {
        public static PlayerHUD Instance { get; private set; }
        public GameObject pauseMenu;
        [SerializeField] private TMP_Text currentTurn;

        [SerializeField] private TMP_Text currentScore;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
            }
            else
            {
                Instance = this;
            }
            pauseMenu.SetActive(false);
        }

        public void PauseMenuButton() =>
            FindAnyObjectByType<PlayerControllers>().PauseMenu(new InputAction.CallbackContext());

        public void ChangeCurrentTurnText(bool yourTurn)
        {
            currentScore.text =
                $"Current Turn: {GameManager.Instance.currentPlayerTurn}";
            currentTurn.text = yourTurn ? "Is your turn" : "Is not your turn";
        }

        public void LeaveNetworkButton()
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                // Host (server + client)
                NetworkManager.singleton.StopHost();
                Debug.Log("StopHost called");
            }
            else if (NetworkClient.isConnected)
            {
                // Only client
                NetworkManager.singleton.StopClient();
                Debug.Log("StopClient called");
            }
            else if (NetworkServer.active)
            {
                // Only server
                NetworkManager.singleton.StopServer();
                Debug.Log("StopServer called");
            }
            SceneChanger.Instance.ChangeScene("LobbyScene");
        }
    }
}