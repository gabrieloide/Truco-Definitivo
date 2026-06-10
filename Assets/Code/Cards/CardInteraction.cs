using Code.GameLogic;
using Code.Networking;
using Code.Player;
using DG.Tweening;
using Mirror;
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
                if (player == null || !player.isLocalPlayer) return;

                if (player.player != null && player.player.canPlayCard)
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
            if (player == null || !player.isLocalPlayer) return;

            if (player.player != null && player.player.canPlayCard)
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
                // Feedback de "No es tu turno" o "No puedes jugar"
                if (JuiceVFXManager.Instance != null)
                {
                    JuiceVFXManager.Instance.DenyActionFeedback();
                }
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
            if (player == null) return;
            _isOnHand = false;
            isSelected = false;

            if (player.cardsHandler != null && player.cardsHandler.mouseOutTexture != null)
                Cursor.SetCursor(player.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);

            // IMPORTANTE: Quitar la carta de la lista de la mano para que no reaparezca al cambiar de cámara
            if (player.cardsHandler != null && player.cardsHandler.Cards.Contains(gameObject))
            {
                player.cardsHandler.Cards.Remove(gameObject);
            }
            
            // In multiplayer pure client: route through server Command
            if (NetworkClient.active && !NetworkServer.active)
            {
                var netSync = player.GetComponent<PlayerNetworkSync>();
                if (netSync != null)
                {
                    netSync.CmdPlayCard(Card.dbId, Card.value, Card.suit, isBurned);
                    gameObject.SetActive(false);
                    return;
                }
            }

            // Singleplayer or host: execute directly
            Code.GameLogic.Architecture.ICommand playCommand = new Code.GameLogic.Architecture.PlayCardCommand(Card, player.gameObject, null, isBurned);
            playCommand.Execute();

            gameObject.SetActive(false); // Hide UI card
        }

        public void HandlePointerEnter()
        {
            if (!_isOnHand || isSelected) return;
            var player = GetComponent<PhysicalCard3D>()?.owner;
            if (player == null || !player.isLocalPlayer) return;

            isUp = true;
            float duration = (player.cardsHandler != null) ? player.cardsHandler.upDuration : 0.2f;
            if (_animator != null) _animator.AnimateHover(true, _startPosition, _startRotation, duration);
            
            if (player.cardsHandler != null && player.cardsHandler.mouseOverTexture != null)
            {
                Cursor.SetCursor(player.cardsHandler.mouseOverTexture, Vector2.zero, CursorMode.Auto);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("card_hover");
            }
        }

        public void HandlePointerExit()
        {
            if (!_isOnHand || isSelected) return;
            var player = GetComponent<PhysicalCard3D>()?.owner;
            if (player == null || !player.isLocalPlayer) return;

            isUp = false;
            float duration = (player.cardsHandler != null) ? player.cardsHandler.upDuration : 0.2f;
            if (_animator != null) _animator.AnimateHover(false, _startPosition, _startRotation, duration);
            
            if (player.cardsHandler != null && player.cardsHandler.mouseOutTexture != null)
            {
                Cursor.SetCursor(player.cardsHandler.mouseOutTexture, Vector2.zero, CursorMode.Auto);
            }

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
