using Code.GameLogic;
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
        public PlayerController playerController;
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
            if (playerController.player.canPlayCard)
            {
                _isOnHand = false;
                Cursor.SetCursor(playerController.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
                transform.DOMove(GameObject.Find("CardInTable").transform.position, 0.2f).SetEase(Ease.InOutElastic);;
                playerController.player.canPlayCard = false;
                playerController.CmdIncreaseTurn(cardPosition);
            }
            else
            {
                Debug.Log("This is not your current turn, wait until all players have been played");
            }
        }

        private void OnMouseEnter()
        {
            if (!_isOnHand) return;

            isUp = true;
            transform.DOLocalMoveY(_startPosition.y + 0.5f, playerController.cardsHandler.upDuration);
            Cursor.SetCursor(playerController.cardsHandler.mouseOverTexture, Vector2.zero, CursorMode.Auto);
        }

        private void OnMouseExit()
        {
            if (!_isOnHand) return;

            isUp = false;
            transform.DOLocalMoveY(_startPosition.y, playerController.cardsHandler.upDuration);
            Cursor.SetCursor(playerController.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
        }
    }
}