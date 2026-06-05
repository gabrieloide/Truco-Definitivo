using UnityEngine;
using DG.Tweening;

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

        private Vector3 _originalCameraPos;
        private Transform _mainCameraTransform;

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

            SetupCameraReference();
        }

        private void SetupCameraReference()
        {
            if (Camera.main != null)
            {
                _mainCameraTransform = Camera.main.transform;
                _originalCameraPos = _mainCameraTransform.localPosition;
            }
        }

        /// <summary>
        /// Realiza una sacudida de la cámara principal.
        /// </summary>
        public void ShakeCamera(float duration = -1f, float strength = -1f)
        {
            if (_mainCameraTransform == null) SetupCameraReference();
            if (_mainCameraTransform == null) return;

            float d = duration > 0 ? duration : defaultShakeDuration;
            float s = strength > 0 ? strength : defaultShakeStrength;

            // Reiniciar posición antes de sacudir
            _mainCameraTransform.DOKill();
            
            _mainCameraTransform.DOShakePosition(d, s, defaultShakeVibrato, defaultShakeRandomness)
                .OnComplete(() =>
                {
                    // Volver suavemente a la posición original local para evitar desalineación de la cámara
                    _mainCameraTransform.DOLocalMove(_originalCameraPos, 0.1f);
                });
        }

        /// <summary>
        /// Genera partículas en el punto donde impacta la carta contra la mesa.
        /// </summary>
        public void PlayImpactParticles(Vector3 position, Color? colorHint = null)
        {
            if (cardImpactParticlePrefab == null)
            {
                // Fallback silencioso si no hay prefab asignado
                return;
            }

            GameObject particles = Instantiate(cardImpactParticlePrefab, position, Quaternion.identity);
            
            // Si el prefab tiene un componente ParticleSystem y queremos pasarle un tinte de color
            if (colorHint.HasValue)
            {
                var ps = particles.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var main = ps.main;
                    main.startColor = colorHint.Value;
                }
            }

            // Destruir automáticamente las partículas tras finalizar su reproducción
            Destroy(particles, 2f);
        }
    }
}
