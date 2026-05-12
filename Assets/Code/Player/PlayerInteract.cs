using Code.GameLogic;
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

        private void Start()
        {
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
            if (Camera.main == null) return;

            // Start ray from mouse position
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            // Using ~0 to hit EVERYTHING
            RaycastHit[] hits = Physics.RaycastAll(ray, interactRange, ~0, QueryTriggerInteraction.Collide);
            
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

            foreach (var hit in hits)
            {
                // Intentamos buscar el componente en el objeto, en sus padres o en sus hijos
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable == null) interactable = hit.collider.GetComponentInChildren<IInteractable>();
                
                if (interactable != null)
                {
                    _currentInteractable = interactable;
                    return;
                }

                // If it's NOT an interactable and it's us or a child, ignore it
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                    continue;
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
