using UnityEngine;
using Code.GameLogic;

namespace Code.GameLogic
{
    /// Controls a fan with spinning blades and left-to-right oscillation (swing).
    /// Always active.
    /// </summary>
    public class FanController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The transform of the blades that will spin fast.")]
        public Transform bladesTransform;
        [Tooltip("The transform of the fan head that will move left to right.")]
        public Transform headTransform;

        [Header("Blade Spinning Settings")]
        public bool bladesActive = true;
        public float bladeRotationSpeed = 1500f;
        public Vector3 bladeRotationAxis = Vector3.forward;

        [Header("Oscillation (Swing) Settings")]
        public bool oscillationActive = true;
        public float oscillationSpeed = 2f;
        public float oscillationAngle = 45f;
        public Vector3 oscillationAxis = Vector3.up;

        private float _oscillationTimer;
        private Quaternion _initialHeadRotation;

        private void Start()
        {
            if (headTransform != null)
            {
                _initialHeadRotation = headTransform.localRotation;
            }
        }

        private void Update()
        {
            // 1. Blade Rotation
            if (bladesActive && bladesTransform != null)
            {
                bladesTransform.Rotate(bladeRotationAxis * (bladeRotationSpeed * Time.deltaTime));
            }

            // 2. Head Oscillation (Left to Right)
            if (oscillationActive && headTransform != null)
            {
                _oscillationTimer += Time.deltaTime * oscillationSpeed;
                // Sin wave provides smooth back-and-forth motion
                float currentAngle = Mathf.Sin(_oscillationTimer) * oscillationAngle;
                headTransform.localRotation = _initialHeadRotation * Quaternion.Euler(oscillationAxis * currentAngle);
            }
        }
        
        // Optional: Method to toggle blades if needed from another script or UI
        public void ToggleBlades(bool state)
        {
            bladesActive = state;
        }
    }
}
