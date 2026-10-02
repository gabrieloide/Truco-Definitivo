using UnityEngine;
using DG.Tweening;

namespace Code.Cards
{
    /// <summary>
    /// Componente opcional para añadir a cualquier objeto decorativo sobre la mesa
    /// (vasos, mate, cenicero, monedas, etc.) para que reaccione físicamente y salte
    /// cuando un jugador estampe una carta fuerte contra la madera.
    /// </summary>
    public class TablePropBounce : MonoBehaviour
    {
        [Header("Bounce Settings")]
        [SerializeField] private float bounceHeight = 0.04f;
        [SerializeField] private float bounceDuration = 0.25f;
        [SerializeField] private float maxDistance = 2.5f;

        private Vector3 _originalPos;
        private Quaternion _originalRot;
        private bool _isInitialized = false;

        private void Start()
        {
            InitializeRestingTransform();
        }

        private void InitializeRestingTransform()
        {
            if (_isInitialized) return;
            _originalPos = transform.position;
            _originalRot = transform.rotation;
            _isInitialized = true;
        }

        public void Bounce(Vector3 slamPoint)
        {
            InitializeRestingTransform();

            float dist = Vector3.Distance(transform.position, slamPoint);
            if (dist > maxDistance) return;

            // Atenuar según distancia al golpe
            float factor = Mathf.Clamp01(1f - (dist / maxDistance));
            float height = bounceHeight * factor;
            if (height < 0.01f) return;

            transform.DOKill(false);
            transform.DOJump(transform.position, height, 1, bounceDuration)
                .SetEase(Ease.OutQuad);

            Vector3 wobble = new Vector3(
                Random.Range(-4f, 4f) * factor,
                Random.Range(-6f, 6f) * factor,
                Random.Range(-4f, 4f) * factor
            );

            transform.DORotate(transform.eulerAngles + wobble, bounceDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    // Regresar a la rotación de reposo suavemente
                    transform.DORotateQuaternion(_originalRot, 0.15f).SetEase(Ease.OutQuad);
                });
        }
    }
}
