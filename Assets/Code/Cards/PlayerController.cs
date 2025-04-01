using System;
using Code.GameLogic;
using Code.Player;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;


namespace Code.Cards
{
    public class PlayerController : NetworkBehaviour
    {
        public Player player;
        public CardsHandler cardsHandler;
        [SyncVar] private string debugPlayer;

        private void Awake()
        {
            if (cardsHandler == null)
                cardsHandler = GetComponent<CardsHandler>();

            cardsHandler.enabled = false;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (GameObject.Find("LogText"))
            {
                GameObject.Find("LogText").GetComponent<TMP_Text>().text = debugPlayer;
            }

            if (!isLocalPlayer)
                return;

            InitializedHand();
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


        [TargetRpc]
        public void RpcRequestChangeTurn(NetworkConnection conn, bool turn)
        {
            player.canPlayCard = turn;
            if (FindAnyObjectByType<PlayerHUD>() == null)
                return;

            PlayerHUD.Instance.ChangeCurrentTurnText(turn);
        }

        [Command]
        public void CmdIncreaseTurn(int cardPosition)
        {
            GameManager.Instance.currentPlayerTurn++;
            RpcMoveCard(new Vector3(0, 0, 0), cardPosition);

            if (GameManager.Instance.currentPlayerTurn == GameManager.Instance.serverPlayers.Count)
                GameManager.Instance.currentPlayerTurn = 0;
        }

        [ClientRpc]
        private void RpcMoveCard(Vector3 newPosition, int cardposition)
        {
            if (isLocalPlayer)
                return;

            var notPlayer = GameObject.Find("NotLocalPlayer");
            if (notPlayer != null)
            {
                notPlayer.transform.GetChild(cardposition).DOMove(newPosition, 0.75f);
            }
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