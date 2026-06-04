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
        private RaycastHit[] _hits = new RaycastHit[10];

        private void Start()
        {
            _mainCamera = Camera.main;
            Debug.Log($"[PlayerInteract] Script activo en {gameObject.name}. Rango: {interactRange}");
        }

        private void Update()
        {
            if (!isLocalPlayer) return;

            HandleRaycast();

            // Check for 'E' key, Input System action, or Left Mouse Click
            bool interactPressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) 
                                || _inputActions.Player.Interact.triggered 
                                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (interactPressed)
            {
                PerformInteraction();
            }
        }

        private void HandleRaycast()
        {
            if (_mainCamera == null) return;

            // Start ray from mouse position
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = _mainCamera.ScreenPointToRay(mousePos);

            // Use RaycastNonAlloc to prevent GC allocation. Usamos ~0 para detectar todas las capas y no depender de la configuración del Editor.
            int hitCount = Physics.RaycastNonAlloc(ray, _hits, interactRange, ~0, QueryTriggerInteraction.Collide);
            
            // Sort only the valid hits by distance
            System.Array.Sort(_hits, 0, hitCount, Comparer<RaycastHit>.Create((x, y) => x.distance.CompareTo(y.distance)));

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
                    // Ignore our own root body, but allow interacting with our children (like the Cards in our hand!)
                    if (hit.collider.gameObject == gameObject)
                        continue;

                    _currentInteractable = interactable;
                    return;
                }
            }

            _currentInteractable = null;
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
                Debug.Log($"[PlayerInteract] Interactuando con: {(_currentInteractable as MonoBehaviour)?.name ?? "Objeto"}");
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
