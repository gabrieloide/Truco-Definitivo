using System;
using Code.Cards;
using Code.GameLogic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Code.Player
{
    public class PlayerLocal : NetworkBehaviour
    {
        public Player player;
        public CardsHandler cardsHandler;
        public PlayerHUD playerHUD;
        public PlayerControllers playerControllers;

        private void Awake()
        {
            if (cardsHandler == null)
                cardsHandler = GetComponent<CardsHandler>();
            if (playerControllers == null)
                playerControllers = gameObject.AddComponent<PlayerControllers>();

            cardsHandler.enabled = false;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!isLocalPlayer)
                return;
            GameManager.Instance.localPlayerLocal = this;
        }

        private void Update()
        {
            if (!isLocalPlayer)
                return;

            InitializedHand();

            if (FindAnyObjectByType<PlayerHUD>() == null)
                return;
            if (player == null)
                return;
        }

        private void InitializedHand()
        {
            var s = SceneManager.GetActiveScene();

            if (s.name != "GameScene") return;

            GameManager.Instance.isGameScene = true;
            cardsHandler.enabled = true;
        }

        public override void OnStopClient()
        {
            Destroy(gameObject);
        }

        public void AssignPlayer(Player player) => this.player = player;

        [Command]
        public void CmdIncreaseTurn(int cardPosition)
        {
            player.canPlayCard = false;
            GameManager.Instance.currentPlayerTurn++;
            cardsHandler.RpcMoveCard(new Vector3(0, 0, 0), cardPosition);

            if (GameManager.Instance.currentPlayerTurn == GameManager.Instance.serverPlayers.Count)
                GameManager.Instance.currentPlayerTurn = 0;
        }

        [TargetRpc]
        public void RpcRequestChangeTurn(NetworkConnection conn, bool turn)
        {
            player.canPlayCard = turn;
        }
    }

    [Serializable]
    public class Player
    {
        public string playerName;
        public int turnNumber = 0;
        public bool canPlayCard = false;

        public Player(string playerName, int turnNumber)
        {
            this.playerName = playerName;
            this.turnNumber = turnNumber;
        }
    }
}