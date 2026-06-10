using UnityEngine;
using DG.Tweening;
using Unity.Cinemachine;
using Code.Player;

namespace Code.GameLogic
{
    public class JuiceVFXManager : MonoBehaviour
    {
        public static JuiceVFXManager Instance { get; private set; }

        [Header("Camera Shake Settings")]
        public float defaultShakeDuration = 0.2f;
        public float defaultShakeStrength = 0.08f;
        public int defaultShakeVibrato = 10;
        public float defaultShakeRandomness = 90f;

        [Header("Particle Settings")]
        public GameObject cardImpactParticlePrefab;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// Realiza una sacudida directamente sobre el transform de la cámara activa.
        /// </summary>
        public void ShakeCamera(float duration = -1f, float strength = -1f)
        {
            float d = duration > 0 ? duration : defaultShakeDuration;
            float s = strength > 0 ? strength : defaultShakeStrength;

            // Buscamos la cámara activa a través del CameraManager
            if (CameraManager.Instance != null && CameraManager.Instance.ActiveCamera != null)
            {
                Transform camTransform = CameraManager.Instance.ActiveCamera.transform;
                
                // CRITICAL: Capturar la posición local ACTUAL antes de sacudir
                Vector3 initialLocalPos = camTransform.localPosition;
                
                Debug.Log($"[JuiceVFXManager] Sacudiendo cámara: {CameraManager.Instance.ActiveCamera.gameObject.name}. Fuerza: {s}. Posición original: {initialLocalPos}");

                // Matar cualquier shake previo
                camTransform.DOKill(true);
                
                // Realizar sacudida
                camTransform.DOShakePosition(d, s, defaultShakeVibrato, defaultShakeRandomness)
                    .OnComplete(() => {
                        // Volver exactamente a la posición que tenía antes del shake
                        camTransform.DOLocalMove(initialLocalPos, 0.1f).SetEase(Ease.OutQuad);
                    });
            }
            else
            {
                // Fallback a Main Camera
                if (Camera.main != null)
                {
                    Transform mainCam = Camera.main.transform;
                    Vector3 initialPos = mainCam.localPosition;
                    mainCam.DOKill(true);
                    mainCam.DOShakePosition(d, s, defaultShakeVibrato, defaultShakeRandomness)
                        .OnComplete(() => mainCam.DOLocalMove(initialPos, 0.1f));
                }
            }
        }

        public void DenyActionFeedback()
        {
            ShakeCamera(0.2f, 0.05f);

            if (Code.Scripts.Audio.AudioManager.Instance != null)
            {
                Code.Scripts.Audio.AudioManager.Instance.PlaySFX("decline_noquiero_neg");
            }
        }

        public void PlayImpactParticles(Vector3 position, Color? colorHint = null)
        {
            if (cardImpactParticlePrefab == null) return;

            GameObject particles = Instantiate(cardImpactParticlePrefab, position, Quaternion.identity);

            if (colorHint.HasValue)
            {
                var ps = particles.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = colorHint.Value;
                }
            }

            Destroy(particles, 2f);
        }
    }
}
