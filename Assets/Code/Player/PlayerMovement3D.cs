using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMovement3D : MonoBehaviour
    {
        public bool isLocalPlayer = true;
        public float walkSpeed = 5f;

        [Header("References")]
        public Transform cameraTransform;

        private Rigidbody _rb;

        // Controlled by SeatManager
        /*[SyncVar]*/ public bool isSeated = false;

        private void Start()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.freezeRotation = true; // Prevent player from tipping over

            if (isLocalPlayer)
            {
                UpdateCursorState();
                
                // Ensure camera is active for local player
                if (cameraTransform != null)
                {
                    cameraTransform.gameObject.SetActive(true);
                }
            }
            else
            {
                // Disable camera for non-local players
                if (cameraTransform != null)
                {
                    cameraTransform.gameObject.SetActive(false);
                }
            }
        }

        public void UpdateCursorState()
        {
            if (isSeated)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
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

        private void Update()
        {
            if (!isLocalPlayer || isSeated) return;

            HandleLook();
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer || isSeated) return;

            HandleMovement();
        }

        private void HandleLook()
        {
            if (cameraTransform == null) return;

            // Sync the player's body rotation (Y axis) to match the Cinemachine camera's rotation
            // This ensures that pressing 'W' moves the player in the direction they are looking
            transform.rotation = Quaternion.Euler(0, cameraTransform.eulerAngles.y, 0);
        }

        private void HandleMovement()
        {
            Vector2 moveInput = _inputActions.Player.Move.ReadValue<Vector2>();
            Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            
            Vector3 targetVelocity = moveDirection * walkSpeed;
            targetVelocity.y = _rb.linearVelocity.y; // Keep gravity/jumping velocity

            _rb.linearVelocity = targetVelocity;
        }
    }
}
