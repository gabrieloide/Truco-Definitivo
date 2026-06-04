// using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; // Adjust namespace if using an older Cinemachine version

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

        private void Start()
        {
            if (!isLocalPlayer)
            {
                if (vcamWalking != null) vcamWalking.gameObject.SetActive(false);
                if (vcamSeated != null) vcamSeated.gameObject.SetActive(false);
                return;
            }

            // Start in walking mode
            SetWalkingCamera();
        }

        private void Update()
        {
            if (!isLocalPlayer || vcamSeated == null) return;
            
            // Check if we are using the seated camera
            if (vcamSeated.Priority > (vcamWalking != null ? vcamWalking.Priority : 0))
            {
                HandleEdgePanning();
            }
        }

        private void HandleEdgePanning()
        {
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
            vcamSeated.transform.rotation = _seatedBaseRotation * Quaternion.Euler(0, _currentPan, 0);
        }

        public void SetWalkingCamera()
        {
            if (!isLocalPlayer) return;
            
            if (vcamSeated != null) 
            {
                // Restore rotation when standing up
                if (_seatedBaseRotation != default(Quaternion) && _seatedBaseRotation != Quaternion.identity)
                {
                    vcamSeated.transform.rotation = _seatedBaseRotation;
                }
                vcamSeated.Priority = 0;
            }
            if (vcamAlternative != null) vcamAlternative.Priority = 0;
            if (vcamWalking != null) vcamWalking.Priority = 10;
        }

        public void SetSeatedCamera(CinemachineCamera tableCamera)
        {
            if (!isLocalPlayer) return;

            // Optional: If the table has a specific camera we want to blend to, 
            // we can reassign vcamSeated or just activate the table's own camera.
            // For now, we assume vcamSeated is a camera child of the player 
            // that we move to the table's position, or we just switch priorities.

            if (tableCamera != null)
            {
                vcamSeated = tableCamera;
                _seatedBaseRotation = vcamSeated.transform.rotation;
                _currentPan = 0f;
            }

            if (vcamWalking != null) vcamWalking.Priority = 0;
            if (vcamAlternative != null) vcamAlternative.Priority = 0;
            if (vcamSeated != null) vcamSeated.Priority = 100;

            Debug.Log($"[CameraManager] Cámara cambiada. Seated: {vcamSeated.name} (Prio 100), Walking: {(vcamWalking != null ? vcamWalking.name : "None")} (Prio 0)");
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

            // Si la cámara alternativa está activa, la desactivamos y volvemos a la normal
            if (vcamAlternative.Priority > 50)
            {
                vcamAlternative.Priority = 0;
                vcamAlternative.gameObject.SetActive(false);
                if (vcamSeated != null) 
                {
                    vcamSeated.gameObject.SetActive(true);
                    vcamSeated.Priority = 100;
                }
                else if (vcamWalking != null) 
                {
                    vcamWalking.gameObject.SetActive(true);
                    vcamWalking.Priority = 100;
                }
            }
            // Si no está activa, la activamos y apagamos las demás
            else
            {
                vcamAlternative.gameObject.SetActive(true);
                vcamAlternative.Priority = 100;
                if (vcamSeated != null) vcamSeated.Priority = 0;
                if (vcamWalking != null) vcamWalking.Priority = 0;
            }
        }
    }
}
