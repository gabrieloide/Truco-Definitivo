// using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; // Adjust namespace if using an older Cinemachine version
using Code.GameLogic;

namespace Code.Player
{
    public class CameraManager : MonoBehaviour
    {
        public bool isLocalPlayer = true;
        [Header("Cinemachine Cameras")]
        public CinemachineCamera vcamWalking; // The camera attached to the player's head
        public CinemachineCamera vcamSeated;  // The camera looking at the table
        public CinemachineCamera vcamAlternative; // La cámara secundaria de la mesa

        [Header("Seated Camera Panning")]
        public float panSpeed = 45f;
        public float edgeThreshold = 0.05f; // 5% of screen edge
        public float maxPanAngle = 40f;
        
        private Quaternion _seatedBaseRotation;
        private float _currentPan = 0f;
        private CinemachineCamera _activeSeatedCamera;

        public static CameraManager Instance { get; private set; }

        private void Awake()
        {
            if (isLocalPlayer)
            {
                if (Instance != null && Instance != this)
                {
                }
                Instance = this;
            }
        }

        private void Start()
        {
            if (!isLocalPlayer)
            {
                if (vcamWalking != null) vcamWalking.gameObject.SetActive(false);
                if (vcamSeated != null) vcamSeated.gameObject.SetActive(false);
                return;
            }

            var movement = GetComponent<PlayerMovement3D>();
            if (movement != null && movement.isSeated)
            {
                // Ya fuimos sentados por el GameManager antes de que Start() se ejecutara, 
                // así que no sobreescribimos la cámara.
                return;
            }

            // Start in walking mode
            SetWalkingCamera();
        }

        private void Update()
        {
            if (!isLocalPlayer || _activeSeatedCamera == null) return;
            
            // Check if we are using the seated camera
            if (_activeSeatedCamera.Priority > (vcamWalking != null ? vcamWalking.Priority : 0))
            {
                HandleEdgePanning();
            }
        }

        private void HandleEdgePanning()
        {
            if (Application.isMobilePlatform) return;
            if (Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            float screenWidth = Screen.width;
            
            float panDirection = 0f;
            if (mousePos.x < screenWidth * edgeThreshold)
            {
                panDirection = -1f; // Mouse on left edge
            }
            else if (mousePos.x > screenWidth * (1f - edgeThreshold))
            {
                panDirection = 1f;  // Mouse on right edge
            }

            if (panDirection != 0f)
            {
                _currentPan += panDirection * panSpeed * Time.deltaTime;
                _currentPan = Mathf.Clamp(_currentPan, -maxPanAngle, maxPanAngle);
            }

            // Apply rotation around the Y axis relative to its initial orientation
            _activeSeatedCamera.transform.rotation = _seatedBaseRotation * Quaternion.Euler(0, _currentPan, 0);
        }

        public void SetWalkingCamera()
        {
            if (!isLocalPlayer) return;
            
            if (_activeSeatedCamera != null) 
            {
                // Restore rotation when standing up
                if (_seatedBaseRotation != default(Quaternion) && _seatedBaseRotation != Quaternion.identity)
                {
                    _activeSeatedCamera.transform.rotation = _seatedBaseRotation;
                }
                _activeSeatedCamera.Priority = 0;
            }

            if (vcamSeated != null) vcamSeated.Priority = 0;
            if (vcamAlternative != null) vcamAlternative.Priority = 0;
            if (vcamWalking != null) vcamWalking.Priority = 10;
        }

        [Header("Camera Pivot")]
        [Tooltip("Asigna aquí el objeto Pivot que sigue tu vcamSeated. Si está asignado, moveremos el Pivot en lugar de la cámara.")]
        public Transform cameraPivot;

        public void SetSeatedCamera(ChairInteractable chair)
        {
            if (!isLocalPlayer) return;

            // Determine target camera: Use chair-specific camera if available, otherwise fallback to vcamSeated
            CinemachineCamera targetCamera = (chair != null && chair.virtualCamera != null) ? chair.virtualCamera : vcamSeated;

            if (targetCamera != null)
            {
                _activeSeatedCamera = targetCamera;

                // If falling back to the generic vcamSeated, use the parenting logic to move it
                if (targetCamera == vcamSeated && chair != null)
                {
                    Transform objectToMove = cameraPivot != null ? cameraPivot : vcamSeated.transform;
                    if (chair.cameraPosition != null)
                    {
                        objectToMove.SetParent(chair.cameraPosition);
                        objectToMove.localPosition = Vector3.zero;
                        objectToMove.localRotation = Quaternion.identity;
                    }
                    else if (chair.sitTransform != null)
                    {
                        objectToMove.SetParent(chair.sitTransform);
                        objectToMove.localPosition = Vector3.up * 1.5f;
                        Vector3 lookTarget = (TableManager.Instance != null && TableManager.Instance.viraPosition != null) 
                            ? TableManager.Instance.viraPosition.position 
                            : (TableManager.Instance != null ? TableManager.Instance.transform.position : Vector3.zero);
                        objectToMove.LookAt(lookTarget);
                    }
                }

                _seatedBaseRotation = targetCamera.transform.rotation;
                _currentPan = 0f;

                // Force instant cut in Cinemachine
                targetCamera.PreviousStateIsValid = false;
            }
            else
            {
                Debug.LogError("[CameraManager] ERROR: No seated camera available (chair.virtualCamera and fallback vcamSeated are both null)!");
            }

            if (vcamWalking != null) vcamWalking.Priority = 0;
            if (vcamAlternative != null) vcamAlternative.Priority = 0;
            
            // Lower priority of the fallback camera if using chair-specific camera
            if (vcamSeated != null && vcamSeated != targetCamera)
            {
                vcamSeated.Priority = 0;
            }

            if (targetCamera != null)
            {
                targetCamera.Priority = 100;
            }
        }

        public void ToggleAlternativeCamera()
        {
            if (!isLocalPlayer) return;

            // Buscar automáticamente la cámara (incluso si está inactiva o tiene mayúsculas)
            if (vcamAlternative == null)
            {
                var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var cam in allCams)
                {
                    if (cam.gameObject.name.ToLower().Contains("tablecamera"))
                    {
                        vcamAlternative = cam;
                        break;
                    }
                }
                
                if (vcamAlternative == null)
                {
                    Debug.LogError("[CameraManager] ERROR: No se encontró ninguna CinemachineCamera que contenga 'tablecamera' en su nombre.");
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent("ERROR: CÁMARA ALTERNATIVA NO ENCONTRADA", 3f);
                    return;
                }
            }

            Code.Cards.CardsHandler cardsHandler = null;
            var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p.isLocalPlayer && p.gameObject.activeInHierarchy)
                {
                    cardsHandler = p.cardsHandler;
                    if (cardsHandler == null) cardsHandler = p.GetComponentInChildren<Code.Cards.CardsHandler>(true);
                    break;
                }
            }

            // Si la cámara alternativa está activa, la desactivamos y volvemos a la normal
            if (vcamAlternative.Priority > 50)
            {
                vcamAlternative.Priority = 0;
                vcamAlternative.gameObject.SetActive(false);
                if (_activeSeatedCamera != null) 
                {
                    _activeSeatedCamera.gameObject.SetActive(true);
                    _activeSeatedCamera.Priority = 100;
                }
                else if (vcamSeated != null) 
                {
                    vcamSeated.gameObject.SetActive(true);
                    vcamSeated.Priority = 100;
                }
                else if (vcamWalking != null) 
                {
                    vcamWalking.gameObject.SetActive(true);
                    vcamWalking.Priority = 100;
                }

                if (cardsHandler != null) cardsHandler.ToggleCardsVisibility(true);
            }
            // Si no está activa, la activamos y apagamos las demás
            else
            {
                vcamAlternative.gameObject.SetActive(true);
                vcamAlternative.Priority = 100;
                if (_activeSeatedCamera != null) _activeSeatedCamera.Priority = 0;
                if (vcamSeated != null) vcamSeated.Priority = 0;
                if (vcamWalking != null) vcamWalking.Priority = 0;

                if (cardsHandler != null) cardsHandler.ToggleCardsVisibility(false);
            }
        }
    }
}
