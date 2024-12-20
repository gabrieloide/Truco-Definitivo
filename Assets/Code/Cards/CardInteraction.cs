using System;
using Code.GameLogic;
using DG.Tweening;
using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Code.Cards
{
    public class CardInteraction : NetworkBehaviour
    {
        private Vector3 _startPosition;
        private bool _isOnHand = true;
        private PlayerController _playerController;

        private void Start()
        {
            _startPosition = transform.localPosition;
            _playerController = GetComponentInParent<PlayerController>();
        }
        private void OnMouseDown()
        {
            if (_playerController.player.canPlayCard)
            {
                _isOnHand = false;
                Cursor.SetCursor(CardManager.Instance.mouseOutTexture, Vector2.zero, CursorMode.Auto);
                transform.DOMove(CardManager.Instance.deckPosition.position, 0.2f);
                _playerController.player.canPlayCard = false;
                _playerController.IncreaseTurn();
                
            }
            else
            {
                Debug.Log("This is not your current turn, wait until all players have been played");
            }
        }

        private void OnMouseEnter()
        {
            if (_isOnHand)
            {
                transform.DOLocalMoveY(_startPosition.y + 0.5f, CardManager.Instance.upDuration);
                Cursor.SetCursor(CardManager.Instance.mouseOverTexture, Vector2.zero, CursorMode.Auto);
            }
        }

        private void OnMouseExit()
        {
            if (_isOnHand)
            {
                transform.DOLocalMoveY(_startPosition.y, CardManager.Instance.upDuration);
                Cursor.SetCursor(CardManager.Instance.mouseOutTexture, Vector2.zero, CursorMode.Auto);
            }
        }
    }
}