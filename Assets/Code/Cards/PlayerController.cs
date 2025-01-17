using System;
using Code.GameLogic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Code.Cards
{
    public class PlayerController : NetworkBehaviour
    {
        public Player player;
        private PlayerInput _playerInput;
        public CardsHandler cardsHandler;
        private bool _isGameScene;

        private void Awake()
        {
            if (cardsHandler == null)
                cardsHandler = GetComponent<CardsHandler>();

            cardsHandler.enabled = false;
        }

        private void Start()
        {
            _playerInput = new PlayerInput();
            _playerInput.Player.ResetScene.performed += ReloadScene;
            _playerInput.Enable();
            DontDestroyOnLoad(gameObject);
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
        }

        private void Update()
        {
            if (!isLocalPlayer)
                return;
            InitializedHand();
        }

        private void InitializedHand()
        {
            var s = SceneManager.GetActiveScene();

            if (s.name != "GameScene" || _isGameScene) return;

            _isGameScene = true;
            cardsHandler.enabled = true;
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