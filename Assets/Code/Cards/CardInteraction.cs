using Code.GameLogic;
using Code.Player;
using DG.Tweening;
// using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;

namespace Code.Cards
{
    public class CardInteraction : MonoBehaviour
    {
        private Vector3 _startPosition;
        private bool _isOnHand = true;
        public bool isUp = false;
        public int cardPosition;
        public Card Card;
        public TMP_Text number;
        public TMP_Text type;

        public bool isSelected = false;

        private void Start()
        {
            _startPosition = transform.localPosition;
        }

        private void OnMouseDown()
        {
            var player = GetComponentInParent<PlayerLocal>();
            var movement = player.GetComponent<PlayerMovement3D>();

            if (movement != null && !movement.isSeated)
            {
                Debug.Log("You must sit down to play a card.");
                return;
            }

            if (player.player.canPlayCard)
            {
                if (player.selectedCardInteraction != null && player.selectedCardInteraction != this)
                {
                    player.selectedCardInteraction.Deselect();
                }

                isSelected = true;
                player.selectedCardInteraction = this;
                transform.DOLocalMoveY(_startPosition.y + 0.5f, player.cardsHandler.upDuration);
            }
            else
            {
                Debug.Log("This is not your current turn.");
            }
        }

        public void Deselect()
        {
            isSelected = false;
            transform.DOLocalMoveY(_startPosition.y, GetComponentInParent<PlayerLocal>().cardsHandler.upDuration);
        }

        public void PlayCardToTable()
        {
            var player = GetComponentInParent<PlayerLocal>();
            _isOnHand = false;
            isSelected = false;
            
            Cursor.SetCursor(player.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
            
            // Execute the play card command
            Code.GameLogic.Architecture.ICommand playCommand = new Code.GameLogic.Architecture.PlayCardCommand(Card, player.gameObject);
            playCommand.Execute();

            gameObject.SetActive(false); // Hide UI card
        }

        private void OnMouseEnter()
        {
            if (!_isOnHand || isSelected) return;
            var player = GetComponentInParent<PlayerLocal>();

            isUp = true;
            transform.DOLocalMoveY(_startPosition.y + 0.5f, player.cardsHandler.upDuration);
            Cursor.SetCursor(player.cardsHandler.mouseOverTexture, Vector2.zero, CursorMode.Auto);
        }

        private void OnMouseExit()
        {
            if (!_isOnHand || isSelected) return;
            var player = GetComponentInParent<PlayerLocal>();

            isUp = false;
            transform.DOLocalMoveY(_startPosition.y, player.cardsHandler.upDuration);
            Cursor.SetCursor(player.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
        }
    }
}