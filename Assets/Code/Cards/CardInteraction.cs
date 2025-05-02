using Code.GameLogic;
using Code.Player;
using DG.Tweening;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

namespace Code.Cards
{
    public class CardInteraction : NetworkBehaviour
    {
        private Vector3 _startPosition;
        private bool _isOnHand = true;
        public bool isUp = false;
        public int cardPosition;
        public Card Card;
        public TMP_Text number;
        public TMP_Text type;

        private void Start()
        {
            _startPosition = transform.localPosition;
        }

        private void OnMouseDown()
        {
            var player = GetComponentInParent<PlayerLocal>();
            if (player.player.canPlayCard)
            {
                _isOnHand = false;
                Cursor.SetCursor(player.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
                transform.DOMove(GameObject.Find("CardInTable").transform.position, 0.2f).SetEase(Ease.InOutElastic);
                player.CmdIncreaseTurn(cardPosition);
                player.CmdAddCardToTheTable(Card);

            }
            else
            {
                Debug.Log("This is not your current turn, wait until all players have been played");
            }
        }

        private void OnMouseEnter()
        {
            if (!_isOnHand) return;
            var player = GetComponentInParent<PlayerLocal>();

            isUp = true;
            transform.DOLocalMoveY(_startPosition.y + 0.5f, player.cardsHandler.upDuration);
            Cursor.SetCursor(player.cardsHandler.mouseOverTexture, Vector2.zero, CursorMode.Auto);
        }

        private void OnMouseExit()
        {
            if (!_isOnHand) return;
            var player = GetComponentInParent<PlayerLocal>();

            isUp = false;
            transform.DOLocalMoveY(_startPosition.y, player.cardsHandler.upDuration);
            Cursor.SetCursor(player.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
        }
    }
}