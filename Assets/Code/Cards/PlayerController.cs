using System;
using Code.GameLogic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Cards
{
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] private GameObject handPrefab;
        private GameObject _handInGame;
        public Player player;
        public PlayerInput playerInput;

        private void Start()
        {
            playerInput = new PlayerInput();
            playerInput.Player.ResetScene.performed += ReloadScene;
            playerInput.Enable();
        }

        void ReloadScene(InputAction.CallbackContext context)
        {
            Debug.Log("Reloading scene...");
            SceneChanger.Instance.ChangeScene("LobbyScene");
        }

        public override void OnStartClient()
        {
            if (!isLocalPlayer)
                return;
            //Debug.Log($"Player {player.playerName} has been started");

            //_handInGame = Instantiate(handPrefab, transform, true);
        }

        public override void OnStopClient()
        {
            Destroy(gameObject);
        }

        public void AssignPlayer(Player player)
        {
            this.player = player;
        }

        [TargetRpc]
        public void RpcRequestChangeTurn(NetworkConnection conn, bool turn)
        {
            player.canPlayCard = turn;
            Debug.Log("next player");
        }

        [Command]
        public void IncreaseTurn()
        {
            GameManager.Instance.currentPlayerTurn++;
            if (GameManager.Instance.currentPlayerTurn == GameManager.Instance.serverPlayers.Count)
                GameManager.Instance.currentPlayerTurn = 0;
        }
    }

    [Serializable]
    public class Player
    {
        public string playerName;
        public int playerId;
        public int turnNumber = 0;
        public bool canPlayCard = false;

        public Player(string playerName, int turnNumber, int playerId)
        {
            this.playerName = playerName;
            this.playerId = playerId;
            this.turnNumber = turnNumber;
        }
    }
}