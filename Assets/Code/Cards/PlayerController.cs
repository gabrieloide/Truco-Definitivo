using System;
using System.Collections.Generic;
using Code.GameLogic;
using Code.Player;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using Random = UnityEngine.Random;


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

        public void AssignCard(Card cardAssigned, int index)
        {
            // Verificar que el índice esté dentro del rango de la lista
            if (index < 0 || index >= cardsHandler.Cards.Count)
            {
                Debug.LogError($"Índice {index} fuera de rango. Tamaño de la lista: {cardsHandler.Cards.Count}");
                return;
            }

            // Verificar que el elemento no sea null
            if (cardsHandler.Cards[index] != null)
            {
                cardsHandler.Cards[index].GetComponent<CardInteraction>().Card = cardAssigned;
            }
            else
            {
                Debug.LogError($"La carta en el índice {index} es null");
            }
        }

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

            if (notPlayer == null) return;

            var notPlayerCard = notPlayer.transform.GetChild(cardposition);
            notPlayerCard.DOMove(newPosition, 0.2f).SetEase(Ease.InOutElastic);
        }
    }

    [Serializable]
    public class Player
    {
        public string playerName;
        public int turnNumber = 0;
        public bool canPlayCard = false;
        private NetworkConnectionToClient conn;

        public Player(string playerName, int turnNumber, NetworkConnectionToClient conn)
        {
            this.playerName = playerName;
            this.turnNumber = turnNumber;
            this.conn = conn;
        }
    }
}