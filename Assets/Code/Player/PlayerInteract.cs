using Code.GameLogic;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Player
{
    public class PlayerInteract : MonoBehaviour
    {
        public bool isLocalPlayer = true;
        [Header("Interaction Settings")]
        public float interactRange = 10f; // Increased range
        public LayerMask interactableLayer;
        public Transform playerCamera;

        private IInteractable _currentInteractable;
        private Camera _mainCamera;
        private RaycastHit[] _hits = new RaycastHit[50];

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            HandleRaycast();

            // Check for 'E' key, Input System action, or Left Mouse Click
            bool interactPressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) 
                                || _inputActions.Player.Interact.triggered 
                                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                                || UnityEngine.Input.GetMouseButtonDown(0);

            bool rightClickPressed = (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                                  || UnityEngine.Input.GetMouseButtonDown(1);

            if (interactPressed)
            {
                PerformInteraction();
            }
            else if (rightClickPressed)
            {
                if (_currentInteractable != null)
                {
                    // Intentar obtener CardInteraction si el interactable actual es una carta
                    var interactableMono = _currentInteractable as MonoBehaviour;
                    if (interactableMono != null)
                    {
                        var cardInteraction = interactableMono.GetComponent<Code.Cards.CardInteraction>();
                        if (cardInteraction != null)
                        {
                            cardInteraction.HandleRightClick();
                        }
                    }
                }
            }
        }

        private void HandleRaycast()
        {
            if (_mainCamera == null)
            {
                // First try to find the Cinemachine camera, since we know this game uses Cinemachine
                var brain = Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineBrain>();
                if (brain != null)
                {
                    _mainCamera = brain.GetComponent<Camera>();
                }
                
                if (_mainCamera == null) _mainCamera = Camera.main;
                
                if (_mainCamera == null)
                {
                    _mainCamera = Object.FindFirstObjectByType<Camera>();
                    if (_mainCamera == null) return;
                }
            }

            // Start ray from mouse position using New Input System, with Legacy Input fallback
            Vector2 mousePos;
            if (Mouse.current != null)
                mousePos = Mouse.current.position.ReadValue();
            else
                mousePos = UnityEngine.Input.mousePosition;

            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // Use RaycastNonAlloc to prevent GC allocation. Usamos ~0 para detectar todas las capas y no depender de la configuración del Editor.
            int hitCount = Physics.RaycastNonAlloc(ray, _hits, interactRange, ~0, QueryTriggerInteraction.Collide);
            
            // Sort only the valid hits by distance
            System.Array.Sort(_hits, 0, hitCount, Comparer<RaycastHit>.Create((x, y) => x.distance.CompareTo(y.distance)));

            IInteractable newInteractable = null;

            IInteractable bestInteractable = null;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _hits[i];
                
                // Try component avoids allocating memory if missing
                if (!hit.collider.TryGetComponent<IInteractable>(out var interactable))
                {
                    interactable = hit.collider.GetComponentInParent<IInteractable>();
                }
                
                if (interactable != null)
                {
                    // Ignore our own root body
                    if (hit.collider.gameObject == gameObject)
                        continue;

                    if (bestInteractable == null)
                    {
                        bestInteractable = interactable;
                    }

                    // Prioritize cards because they are in the foreground and might be inside a large trigger zone
                    if (interactable is Code.Cards.PhysicalCard3D)
                    {
                        bestInteractable = interactable;
                        break;
                    }
                }
            }
            newInteractable = bestInteractable;

            if (newInteractable != _currentInteractable)
            {
                if (_currentInteractable != null)
                {
                    var oldMono = _currentInteractable as MonoBehaviour;
                    if (oldMono != null)
                    {
                        var oldCard = oldMono.GetComponent<Code.Cards.CardInteraction>();
                        if (oldCard != null) oldCard.HandlePointerExit();
                    }
                }
                
                if (newInteractable != null)
                {
                    var newMono = newInteractable as MonoBehaviour;
                    if (newMono != null)
                    {
                        var newCard = newMono.GetComponent<Code.Cards.CardInteraction>();
                        if (newCard != null) newCard.HandlePointerEnter();
                    }
                }
                
                _currentInteractable = newInteractable;
            }
        }

        private InputSystem_Actions _inputActions;

        private void Awake()
        {
            _inputActions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _inputActions.Enable();
        }

        private void OnDisable()
        {
            _inputActions.Disable();
        }

        private void PerformInteraction()
        {
            if (!isLocalPlayer) return;

            if (_currentInteractable != null)
            {
                // Usamos la raíz para que PhysicalCard3D encuentre al PlayerLocal siempre
                _currentInteractable.Interact(transform.root.gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            if (playerCamera == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(playerCamera.position, playerCamera.forward * interactRange);
        }
    }
}
