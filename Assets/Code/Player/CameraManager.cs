// using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; // Adjust namespace if using an older Cinemachine version
using Code.GameLogic;
using DG.Tweening;

namespace Code.Player
{
    public class CameraManager : MonoBehaviour
    {
        public bool isLocalPlayer = true;
        [Header("Cinemachine Cameras")]
        public CinemachineCamera vcamSeated;  // The camera looking at the table
        public CinemachineCamera vcamAlternative; // La cámara secundaria de la mesa

        [Header("Seated View States (W/S Keys)")]
        [Tooltip("Offset de rotación local cuando miramos las cartas (tecla S). Solo rotamos un poco hacia abajo.")]
        public Vector3 handViewRotOffset = new Vector3(10f, 0f, 0f);
        public float viewTransitionDuration = 0.3f;

        private Quaternion _seatedBaseRotation;
        private Vector3 _seatedBaseLocalPos;
        private CinemachineCamera _activeSeatedCamera;
        private bool _isLookingAtHand = false;

        public CinemachineCamera ActiveCamera => _activeSeatedCamera != null ? _activeSeatedCamera : vcamSeated;

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
                if (vcamSeated != null) vcamSeated.gameObject.SetActive(false);
            }
            // Local player: the camera is set by SetSeatedCamera when the seat is assigned.
        }

        private void Update()
        {
            if (!isLocalPlayer || _activeSeatedCamera == null) return;
            
            // Solo permitimos el cambio de vista si la cámara sentada está activa (prioridad alta)
            if (_activeSeatedCamera.Priority >= 100)
            {
                HandleViewInput();
            }
        }

        private void HandleViewInput()
        {
            if (Keyboard.current == null) return;

            // S para bajar a ver las cartas, W para subir a ver la mesa
            if (Keyboard.current.sKey.wasPressedThisFrame && !_isLookingAtHand)
            {
                TransitionToView(true);
            }
            else if (Keyboard.current.wKey.wasPressedThisFrame && _isLookingAtHand)
            {
                TransitionToView(false);
            }
        }

        private void TransitionToView(bool lookAtHand)
        {
            _isLookingAtHand = lookAtHand;
            if (_activeSeatedCamera == null) return;
            
            Transform targetTransform = _activeSeatedCamera.transform;

            // Matar tweens previos para evitar conflictos
            targetTransform.DOKill();

            if (lookAtHand)
            {
                // Solo rotar ligeramente hacia abajo para ver las cartas
                targetTransform.DOLocalRotateQuaternion(_seatedBaseRotation * Quaternion.Euler(handViewRotOffset), viewTransitionDuration).SetEase(Ease.OutQuad);
            }
            else
            {
                // Volver a la rotación original de la mesa
                targetTransform.DOLocalRotateQuaternion(_seatedBaseRotation, viewTransitionDuration).SetEase(Ease.OutQuad);
            }
        }

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
                    Transform anchor = (chair.cameraPosition != null) ? chair.cameraPosition : chair.sitTransform;
                    if (anchor != null)
                    {
                        // IMPORTANTE: Mantener la posición de mundo antes de emparentar para que no "salte" al suelo
                        if (chair.cameraPosition != null)
                        {
                            targetCamera.transform.SetParent(chair.cameraPosition);
                            targetCamera.transform.localPosition = Vector3.zero;
                            targetCamera.transform.localRotation = Quaternion.identity;
                        }
                        else
                        {
                            targetCamera.transform.SetParent(chair.sitTransform);
                            targetCamera.transform.localPosition = Vector3.up * 1.5f; // Altura de ojos por defecto
                            
                            Vector3 lookTarget = (TableManager.Instance != null && TableManager.Instance.viraPosition != null) 
                                ? TableManager.Instance.viraPosition.position 
                                : (TableManager.Instance != null ? TableManager.Instance.transform.position : Vector3.zero);
                            targetCamera.transform.LookAt(lookTarget);
                        }
                    }
                }

                // Capturamos el estado base para volver tras un shake o cambio de vista
                _seatedBaseLocalPos = targetCamera.transform.localPosition;
                _seatedBaseRotation = targetCamera.transform.localRotation;
                _isLookingAtHand = false;

                // Force instant cut in Cinemachine
                targetCamera.PreviousStateIsValid = false;
            }
            else
            {
                Debug.LogError("[CameraManager] ERROR: No seated camera available (chair.virtualCamera and fallback vcamSeated are both null)!");
            }

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

                if (cardsHandler != null) cardsHandler.ToggleCardsVisibility(true);
            }
            // Si no está activa, la activamos y apagamos las demás
            else
            {
                vcamAlternative.gameObject.SetActive(true);
                vcamAlternative.Priority = 100;
                if (_activeSeatedCamera != null) _activeSeatedCamera.Priority = 0;
                if (vcamSeated != null) vcamSeated.Priority = 0;

                if (cardsHandler != null) cardsHandler.ToggleCardsVisibility(false);
            }
        }
    }
}
