using System;
using Code.GameLogic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Code.Player
{
    public class PlayerHUD : MonoBehaviour
    {
        public static PlayerHUD Instance { get; private set; }
        public GameObject playerFlowerButton;
        public GameObject playerTrucoButton;
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

        public void ChangeScoreText()
        {
            currentScore.text =
                $"{GameManager.Instance.teams[0].teamScore} | {GameManager.Instance.teams[1].teamScore}";
        }

        public void PauseMenuButton() =>
            FindAnyObjectByType<PlayerControllers>().PauseMenu(new InputAction.CallbackContext());

        public void ChangeCurrentTurnText(bool yourTurn)
        {
            currentTurn.text = yourTurn ? "Its your turn" : "Its not your turn";
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
            SceneManager.LoadScene("LobbyScene");
        }
    }
}