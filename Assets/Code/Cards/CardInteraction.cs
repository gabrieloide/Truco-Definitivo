using Code.GameLogic;
using Code.Player;
using DG.Tweening;
// using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using Code.Scripts.Audio;

namespace Code.Cards
{
    public class CardInteraction : MonoBehaviour
    {
        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private JuicyCardAnimator _animator;
        private bool _isOnHand = true;
        public bool isUp = false;
        public int cardPosition;
        public Card Card;

        public bool isSelected = false;

        private void Start()
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;
            _animator = GetComponent<JuicyCardAnimator>();
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<JuicyCardAnimator>();
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(1)) // Click derecho
            {
                if (isSelected || isUp)
                {
                    var player = GetComponent<PhysicalCard3D>()?.owner;
                    if (player != null && player.player != null && player.player.canPlayCard)
                    {
                        if (player.selectedCardInteraction == this)
                        {
                            player.selectedCardInteraction = null;
                        }
                        PlayCardToTable(true);
                    }
                }
            }
        }

        private void OnMouseDown()
        {
            var player = GetComponent<PhysicalCard3D>()?.owner;
            var movement = player.GetComponent<PlayerMovement3D>();

            if (movement != null && !movement.isSeated)
            {
                return;
            }

            if (player.player.canPlayCard)
            {
                if (!Application.isMobilePlatform)
                {
                    // PC: Jugar directo
                    PlayCardToTable(false);
                    return;
                }

                // Móvil: Seleccionar carta para mostrar UI de acciones
                if (player.selectedCardInteraction != null && player.selectedCardInteraction != this)
                {
                    player.selectedCardInteraction.Deselect();
                }

                isSelected = true;
                player.selectedCardInteraction = this;
                
                _animator.AnimateHover(true, _startPosition, _startRotation, player.cardsHandler.upDuration);
                
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("card_select_tone");
                }
            }
            else
            {
            }
        }

        public void Deselect()
        {
            isSelected = false;
            var player = GetComponent<PhysicalCard3D>()?.owner;
            float duration = player != null ? player.cardsHandler.upDuration : 0.2f;
            _animator.AnimateHover(false, _startPosition, _startRotation, duration);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("card_deselect_tone");
            }
        }

        public void PlayCardToTable(bool isBurned = false)
        {
            var player = GetComponent<PhysicalCard3D>()?.owner;
            _isOnHand = false;
            isSelected = false;
            
            Cursor.SetCursor(player.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
            
            // Execute the play card command
            Code.GameLogic.Architecture.ICommand playCommand = new Code.GameLogic.Architecture.PlayCardCommand(Card, player.gameObject, null, isBurned);
            playCommand.Execute();

            gameObject.SetActive(false); // Hide UI card
        }

        private void OnMouseEnter()
        {
            if (!_isOnHand || isSelected) return;
            var player = GetComponent<PhysicalCard3D>()?.owner;

            isUp = true;
            _animator.AnimateHover(true, _startPosition, _startRotation, player.cardsHandler.upDuration);
            Cursor.SetCursor(player.cardsHandler.mouseOverTexture, Vector2.zero, CursorMode.Auto);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("card_hover");
            }
        }

        private void OnMouseExit()
        {
            if (!_isOnHand || isSelected) return;
            var player = GetComponent<PhysicalCard3D>()?.owner;

            isUp = false;
            _animator.AnimateHover(false, _startPosition, _startRotation, player.cardsHandler.upDuration);
            Cursor.SetCursor(player.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("card_hover");
            }
        }
    }
}
