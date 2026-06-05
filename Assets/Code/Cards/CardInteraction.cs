using Code.GameLogic;
using Code.Player;
using DG.Tweening;
// using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using Code.Scripts.Audio;

using UnityEngine.EventSystems;

namespace Code.Cards
{
    public class CardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
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
            _animator = GetComponent<JuicyCardAnimator>();
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<JuicyCardAnimator>();
            }
        }

        public void SetRestingPosition(Vector3 pos, Quaternion rot)
        {
            _startPosition = pos;
            _startRotation = rot;
        }

        private void Update()
        {
            // Handled by event system
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            HandlePointerEnter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HandlePointerExit();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                HandleClick();
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                HandleRightClick();
            }
        }

        // --- Compatibilidad con Physics normal (por si no se usa PhysicsRaycaster) ---
        private void OnMouseEnter()
        {
            HandlePointerEnter();
        }

        private void OnMouseExit()
        {
            HandlePointerExit();
        }

        private void OnMouseDown()
        {
            HandleClick();
        }
        
        private void OnMouseOver()
        {
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                HandleRightClick();
            }
        }

        public void HandleRightClick()
        {
            if (isSelected || isUp)
            {
                var player = GetComponent<PhysicalCard3D>()?.owner;
                if (player != null && player.player != null && player.player.canPlayCard)
                {
                    // Bloquear interacciones adicionales inmediatamente
                    player.player.canPlayCard = false;
                    if (player.selectedCardInteraction == this)
                    {
                        player.selectedCardInteraction = null;
                    }
                    PlayCardToTable(true);
                }
            }
        }

        public void HandleClick()
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

        public void HandlePointerEnter()
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

        public void HandlePointerExit()
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

        private void OnDrawGizmos()
        {
            Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.5f);
            
            // Usar la escala predeterminada de la carta
            Vector3 cardScale = new Vector3(0.151111543f, 0.217364341f, 0.00313752703f);
            
            // Dibujar el cubo respetando la rotación y posición actual
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, cardScale);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}
