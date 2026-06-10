using System;
using UnityEngine;
using DG.Tweening;
using Code.Scripts.Audio;

namespace Code.Cards
{
    public class JuicyCardAnimator : MonoBehaviour
    {
        [Header("Hover Settings")]
        public float hoverScaleMultiplier = 1.15f;
        public float hoverYOffset = 0.15f;
        public Vector3 hoverRotationOffset = new Vector3(-10f, 0f, 5f); // Se inclina ligeramente hacia la cámara
        
        [Header("Play Settings")]
        public float playJumpPower = 1.2f;
        public float playDuration = 0.6f;

        [Header("Outline Settings")]
        public Color hoverOutlineColor = new Color(1f, 0.85f, 0f, 1f); // Amarillo dorado
        public float hoverOutlineSize = 4.0f;

        private Vector3 _originalScale;
        private Tween _moveTween;
        private Tween _rotateTween;
        private Tween _scaleTween;
        private Sequence _squashSequence;
        private Tween _outlineTween;

        private Transform _visualTransform;
        private Material _cardMaterial;

        // Para evitar el bug de oscilación cuando el BoxCollider está en la misma raíz
        private Vector3 _originalBoxCenter;
        private Vector3 _originalBoxSize;
        private bool _boxExpanded = false;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        private void Start()
        {
            // Always try to animate the first child (which holds the visual mesh or canvas)
            // so the root (which holds the BoxCollider) never moves, preventing oscillation.
            if (transform.childCount > 0)
                _visualTransform = transform.GetChild(0);
            else
                _visualTransform = transform; // Fallback, but will cause oscillation

            var renderer = GetComponentInChildren<Renderer>(true);
            if (renderer != null)
            {
                _cardMaterial = renderer.material;
            }
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }

        private void KillAllTweens()
        {
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _scaleTween?.Kill();
            _squashSequence?.Kill();
            _outlineTween?.Kill();
        }

        /// <summary>
        /// Anima la carta al repartirse desde el mazo hacia la mano.
        /// </summary>
        public void AnimateToHand(Vector3 startWorldPos, Vector3 targetLocalPos, Quaternion targetLocalRot, float duration, float delay, Action onComplete = null)
        {
            KillAllTweens();
            
            // Empezamos invisible y en escala cero hasta que el delay pase
            transform.localScale = Vector3.zero;

            Sequence dealSeq = DOTween.Sequence();
            dealSeq.AppendInterval(delay);
            
            dealSeq.AppendCallback(() => {
                // Justo cuando empieza el movimiento, colocamos la carta en el mazo (espacio de mundo).
                transform.position = startWorldPos;
                transform.rotation = Quaternion.identity;
                
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("card_deal_swoosh");
                }
            });
            
            // Escalado progresivo de aparición (Append para que sea el primero después del callback)
            dealSeq.Append(transform.DOScale(_originalScale, duration).SetEase(Ease.OutBack));
            
            // Movimiento local al contenedor de la mano (Join para que ocurra al mismo tiempo que el escalado)
            dealSeq.Join(transform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutQuad));
            
            // Rotación local a su lugar en el abanico
            dealSeq.Join(transform.DOLocalRotateQuaternion(targetLocalRot, duration).SetEase(Ease.OutQuad));
            
            dealSeq.OnComplete(() => onComplete?.Invoke());
        }

        /// <summary>
        /// Anima el comportamiento de hover/selección al pasar el mouse por encima.
        /// </summary>
        public void AnimateHover(bool isHovered, Vector3 startLocalPos, Quaternion startLocalRot, float duration)
        {
            // Detener tweens previos de escala, rotación y outline para evitar tirones
            _scaleTween?.Kill();
            _moveTween?.Kill();
            _rotateTween?.Kill();
            _outlineTween?.Kill();

            if (isHovered)
            {
                // Escalar hacia arriba (la raíz se escala para afectar a los colisionadores si se desea, o solo el renderer)
                _scaleTween = transform.DOScale(_originalScale * hoverScaleMultiplier, duration).SetEase(Ease.OutQuad);
                
                // Mover hacia adelante/arriba
                Vector3 targetLocalPos = startLocalPos + Vector3.up * hoverYOffset;
                
                if (_visualTransform != transform)
                {
                    // Movemos visualmente el hijo, dejando la raíz y su colisionador en su sitio
                    _moveTween = _visualTransform.DOLocalMove(Vector3.up * hoverYOffset, duration).SetEase(Ease.OutQuad);
                    Quaternion targetLocalRot = Quaternion.Euler(hoverRotationOffset);
                    _rotateTween = _visualTransform.DOLocalRotateQuaternion(targetLocalRot, duration).SetEase(Ease.OutQuad);
                }
                else
                {
                    // [OSCILLATION BUG FIX] El visual es el mismo objeto que la raíz (donde está el BoxCollider).
                    // Al moverlo hacia arriba, el colisionador se escapará del ratón. 
                    // Solución: Estiramos el BoxCollider hacia abajo temporalmente.
                    var box = GetComponent<BoxCollider>();
                    if (box != null && !_boxExpanded)
                    {
                        _originalBoxCenter = box.center;
                        _originalBoxSize = box.size;
                        // Ajustar asumiendo que up es el eje vertical de la carta
                        box.size = new Vector3(_originalBoxSize.x, _originalBoxSize.y + hoverYOffset, _originalBoxSize.z);
                        box.center = new Vector3(_originalBoxCenter.x, _originalBoxCenter.y - (hoverYOffset / 2f), _originalBoxCenter.z);
                        _boxExpanded = true;
                    }

                    _moveTween = transform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutQuad);
                    Quaternion targetLocalRot = startLocalRot * Quaternion.Euler(hoverRotationOffset);
                    _rotateTween = transform.DOLocalRotateQuaternion(targetLocalRot, duration).SetEase(Ease.OutQuad);
                }

                // Animar outline si el material lo soporta (por ejemplo, MK Toon)
                if (_cardMaterial != null && _cardMaterial.HasProperty("_OutlineSize"))
                {
                    _cardMaterial.SetColor("_OutlineColor", hoverOutlineColor);
                    _outlineTween = DOTween.To(() => _cardMaterial.GetFloat("_OutlineSize"), x => _cardMaterial.SetFloat("_OutlineSize", x), hoverOutlineSize, duration)
                        .SetEase(Ease.OutQuad);
                }
            }
            else
            {
                // Regresar a la escala original
                _scaleTween = transform.DOScale(_originalScale, duration).SetEase(Ease.OutQuad);
                
                // Regresar a posición y rotación originales de la mano
                if (_visualTransform != transform)
                {
                    _moveTween = _visualTransform.DOLocalMove(Vector3.zero, duration).SetEase(Ease.OutQuad);
                    _rotateTween = _visualTransform.DOLocalRotateQuaternion(Quaternion.identity, duration).SetEase(Ease.OutQuad);
                }
                else
                {
                    // Restaurar el BoxCollider a su tamaño original
                    var box = GetComponent<BoxCollider>();
                    if (box != null && _boxExpanded)
                    {
                        box.center = _originalBoxCenter;
                        box.size = _originalBoxSize;
                        _boxExpanded = false;
                    }

                    _moveTween = transform.DOLocalMove(startLocalPos, duration).SetEase(Ease.OutQuad);
                    _rotateTween = transform.DOLocalRotateQuaternion(startLocalRot, duration).SetEase(Ease.OutQuad);
                }

                // Animar outline de regreso a 0
                if (_cardMaterial != null && _cardMaterial.HasProperty("_OutlineSize"))
                {
                    _outlineTween = DOTween.To(() => _cardMaterial.GetFloat("_OutlineSize"), x => _cardMaterial.SetFloat("_OutlineSize", x), 0f, duration)
                        .SetEase(Ease.OutQuad);
                }
            }
        }

        /// <summary>
        /// Anima el lanzamiento de la carta hacia la mesa usando un arco parabólico y rebote (Squash & Stretch) al caer.
        /// </summary>
        public void AnimatePlayToTable(Vector3 targetPos, Quaternion targetRot, float duration, Action onImpact, Action onComplete = null)
        {
            KillAllTweens();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("card_fly_whistle");
            }

            // Lanzar usando DOMove para ir directo a la mesa
            _moveTween = transform.DOMove(targetPos, duration)
                .SetEase(Ease.OutQuad);

            // Rotar en el aire (voltear a posición de mesa + giro adicional de 360 grados para espectacularidad)
            Vector3 rotTarget = targetRot.eulerAngles;
            // Añadir un giro de 360 en el aire para dinamismo
            transform.localRotation = transform.rotation;
            _rotateTween = transform.DORotate(new Vector3(rotTarget.x + 360f, rotTarget.y, rotTarget.z), duration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuad);

            _moveTween.OnComplete(() =>
            {
                // Disparar evento de impacto (sonido, partículas, sacudida de cámara)
                onImpact?.Invoke();

                // Animación de Squash & Stretch (rebote elástico)
                _squashSequence = DOTween.Sequence();
                
                // Aplastamiento inicial (Y se achica, XZ se expanden)
                _squashSequence.Append(transform.DOScale(new Vector3(_originalScale.x * 1.25f, _originalScale.y * 0.4f, _originalScale.z * 1.25f), 0.08f).SetEase(Ease.OutQuad));
                
                // Rebote hacia arriba (Y se estira, XZ se achican)
                _squashSequence.Append(transform.DOScale(new Vector3(_originalScale.x * 0.85f, _originalScale.y * 1.15f, _originalScale.z * 0.85f), 0.08f).SetEase(Ease.InOutQuad));
                
                // Retorno al tamaño normal
                _squashSequence.Append(transform.DOScale(_originalScale, 0.1f).SetEase(Ease.OutQuad));

                _squashSequence.OnComplete(() => onComplete?.Invoke());
            });
        }

        /// <summary>
        /// Anima el reparto y posterior volteo (flip) de la carta de la vira.
        /// </summary>
        public void AnimateViraReveal(Vector3 startPos, Vector3 targetPos, Quaternion targetRot, float duration, Action onImpact, Action onComplete = null)
        {
            KillAllTweens();

            // 1. Iniciar boca abajo en la pila del mazo
            transform.position = startPos;
            // Rotación boca abajo: 90 en X pero con la cara oculta (180 adicionales en Z/Y)
            Quaternion faceDownRot = targetRot * Quaternion.Euler(0, 180f, 0);
            transform.rotation = faceDownRot;
            transform.localScale = _originalScale;

            // 2. Desplazar desde el mazo hacia el lado
            transform.DOMove(targetPos, duration * 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                // Disparar sonido del deslizamiento inicial si es necesario
                onImpact?.Invoke();

                // 3. Voltear (Giro de 180 grados en Y local) para revelar la cara
                transform.DORotateQuaternion(targetRot, duration * 0.5f).SetEase(Ease.OutBack)
                    .OnComplete(() => onComplete?.Invoke());
            });
        }
    }
}
