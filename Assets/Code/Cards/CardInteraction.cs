using Code.GameLogic;
using DG.Tweening;
using Mirror;
using UnityEngine;

namespace Code.Cards
{
    public class CardInteraction : NetworkBehaviour
    {
        private Vector3 _startPosition;
        private bool _isOnHand = true;
        public PlayerController _playerController;
        public bool IsUp = false;
        public int CardPosition;

        private void Start()
        {
            _startPosition = transform.localPosition;
        }

        private void OnMouseDown()
        {
            if (_playerController.player.canPlayCard)
            {
                _isOnHand = false;
                Cursor.SetCursor(_playerController.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
                transform.DOMove(GameObject.Find("CardInTable").transform.position, 0.2f);
                _playerController.player.canPlayCard = false;
                _playerController.CmdIncreaseTurn(CardPosition);
            }
            else
            {
                Debug.Log("This is not your current turn, wait until all players have been played");
            }
        }

        private void OnMouseEnter()
        {
            if (!_isOnHand) return;

            IsUp = true;
            transform.DOLocalMoveY(_startPosition.y + 0.5f, _playerController.cardsHandler.upDuration);
            Cursor.SetCursor(_playerController.cardsHandler.mouseOverTexture, Vector2.zero, CursorMode.Auto);
        }

        private void OnMouseExit()
        {
            if (!_isOnHand) return;

            IsUp = false;
            transform.DOLocalMoveY(_startPosition.y, _playerController.cardsHandler.upDuration);
            Cursor.SetCursor(_playerController.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
        }
    }
}