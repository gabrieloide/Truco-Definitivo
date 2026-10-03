using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI
{
    /// <summary>
    /// Sistema de animaciones "juicy" para UI Toolkit inspirado en la filosofía de MrEliptik.
    /// Proporciona Squash & Stretch en botones, micro-rotaciones táctiles, transiciones elásticas (pop-in),
    /// y rebotes ("bump") en cambios de valores.
    /// </summary>
    public static class JuicyUIHelper
    {
        private static readonly Dictionary<VisualElement, List<Tween>> ActiveTweens = new();
        private static bool _alternateTilt;

        private static void RegisterTween(VisualElement elem, Tween tween)
        {
            if (elem == null || tween == null) return;
            if (!ActiveTweens.TryGetValue(elem, out var list))
            {
                list = new List<Tween>();
                ActiveTweens[elem] = list;
            }
            list.Add(tween);
        }

        public static void KillTweens(VisualElement elem)
        {
            if (elem == null) return;
            if (ActiveTweens.TryGetValue(elem, out var list))
            {
                foreach (var t in list)
                {
                    if (t != null && t.IsActive()) t.Kill();
                }
                list.Clear();
            }
            elem.style.opacity = 1f;
        }

        /// <summary>
        /// Aplica automáticamente animaciones juicy a todos los botones del árbol UI.
        /// </summary>
        public static void EnhanceAllButtons(VisualElement root)
        {
            if (root == null) return;
            root.Query<Button>().ForEach(btn =>
            {
                if (btn.ClassListContains("profile-info-group")) return;
                EnhanceButton(btn);
            });
        }

        /// <summary>
        /// Aplica squash & stretch, micro-rotación e inercia táctil a un botón específico.
        /// </summary>
        public static void EnhanceButton(Button btn)
        {
            if (btn == null) return;

            // Evitar re-suscripciones múltiples
            if (btn.userData is string tag && tag == "JuicyEnhanced") return;
            btn.userData = "JuicyEnhanced";

            btn.RegisterCallback<PointerEnterEvent>(_ =>
            {
                KillTweens(btn);

                // Alternar ligera inclinación ±1.4 grados para dinamismo orgánico
                _alternateTilt = !_alternateTilt;
                float targetAngle = _alternateTilt ? 1.4f : -1.4f;

                // Squash inicial rápido (se estira ancho, se aplasta alto)
                btn.transform.scale = new Vector3(1.06f, 0.95f, 1f);
                btn.transform.rotation = Quaternion.Euler(0, 0, targetAngle);

                // Rebota elásticamente hacia escala de hover (1.04, 1.04) y se alinea
                float tVal = 0f;
                var tw = DOVirtual.Float(0f, 1f, 0.22f, p =>
                {
                    float sx = Mathf.LerpUnclamped(1.06f, 1.035f, p);
                    float sy = Mathf.LerpUnclamped(0.95f, 1.035f, p);
                    float ang = Mathf.LerpUnclamped(targetAngle, targetAngle * 0.4f, p);
                    btn.transform.scale = new Vector3(sx, sy, 1f);
                    btn.transform.rotation = Quaternion.Euler(0, 0, ang);
                }).SetEase(Ease.OutBack);

                RegisterTween(btn, tw);
            });

            btn.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                KillTweens(btn);
                Vector3 currentScale = btn.transform.scale;
                Vector3 currentPos = btn.transform.position;
                Quaternion currentRot = btn.transform.rotation;

                var tw = DOVirtual.Float(0f, 1f, 0.16f, p =>
                {
                    btn.transform.scale = Vector3.Lerp(currentScale, Vector3.one, p);
                    btn.transform.position = Vector3.Lerp(currentPos, Vector3.zero, p);
                    btn.transform.rotation = Quaternion.Slerp(currentRot, Quaternion.identity, p);
                }).SetEase(Ease.OutQuad);

                RegisterTween(btn, tw);
            });

            btn.RegisterCallback<PointerDownEvent>(_ =>
            {
                KillTweens(btn);
                // Squash down táctil al presionar (sensación de peso físico)
                var tw = DOVirtual.Float(0f, 1f, 0.08f, p =>
                {
                    btn.transform.scale = Vector3.Lerp(btn.transform.scale, new Vector3(0.96f, 0.91f, 1f), p);
                    btn.transform.position = Vector3.Lerp(btn.transform.position, new Vector3(0f, 3.5f, 0f), p);
                    btn.transform.rotation = Quaternion.Slerp(btn.transform.rotation, Quaternion.identity, p);
                }).SetEase(Ease.OutQuad);

                RegisterTween(btn, tw);
            });

            btn.RegisterCallback<PointerUpEvent>(_ =>
            {
                KillTweens(btn);
                // Liberación con rebote elástico hacia estado hover
                var tw = DOVirtual.Float(0f, 1f, 0.18f, p =>
                {
                    btn.transform.scale = Vector3.LerpUnclamped(new Vector3(0.96f, 0.91f, 1f), new Vector3(1.035f, 1.035f, 1f), p);
                    btn.transform.position = Vector3.Lerp(new Vector3(0f, 3.5f, 0f), Vector3.zero, p);
                }).SetEase(Ease.OutBack);

                RegisterTween(btn, tw);
            });
        }

        /// <summary>
        /// Realiza una transición elástica "Pop-In" al mostrar un panel o pantalla.
        /// </summary>
        public static void PopIn(VisualElement element, float duration = 0.24f, float startScale = 0.86f, Action onComplete = null)
        {
            if (element == null) return;
            KillTweens(element);

            element.style.display = DisplayStyle.Flex;
            element.transform.scale = Vector3.one * startScale;
            element.style.opacity = 0f;

            var tw = DOVirtual.Float(0f, 1f, duration, p =>
            {
                // Escala elástica con rebote (OutBack)
                float s = Mathf.LerpUnclamped(startScale, 1f, DOVirtual.EasedValue(0f, 1f, p, Ease.OutBack));
                element.transform.scale = Vector3.one * s;
                // Opacidad fluida
                element.style.opacity = Mathf.Clamp01(p * 1.5f);
            }).OnComplete(() =>
            {
                element.transform.scale = Vector3.one;
                element.style.opacity = 1f;
                onComplete?.Invoke();
            });

            if (tw == null)
            {
                element.transform.scale = Vector3.one;
                element.style.opacity = 1f;
            }
            else
            {
                RegisterTween(element, tw);
            }
        }

        /// <summary>
        /// Realiza una transición rápida "Pop-Out" al ocultar un panel o pantalla.
        /// </summary>
        public static void PopOut(VisualElement element, float duration = 0.14f, Action onComplete = null)
        {
            if (element == null) return;
            KillTweens(element);

            Vector3 currentScale = element.transform.scale;
            float currentOpacity = element.resolvedStyle.opacity;

            var tw = DOVirtual.Float(0f, 1f, duration, p =>
            {
                float s = Mathf.Lerp(currentScale.x, 0.92f, p);
                element.transform.scale = Vector3.one * s;
                element.style.opacity = Mathf.Lerp(currentOpacity, 0f, p);
            }).SetEase(Ease.InQuad).OnComplete(() =>
            {
                element.style.display = DisplayStyle.None;
                element.transform.scale = Vector3.one;
                element.style.opacity = 1f;
                onComplete?.Invoke();
            });

            RegisterTween(element, tw);
        }

        /// <summary>
        /// Efecto de "Bump" elástico cuando un valor, texto o notificación se actualiza.
        /// </summary>
        public static void Bump(VisualElement element, float punchMultiplier = 1.18f, float duration = 0.22f)
        {
            if (element == null) return;
            KillTweens(element);

            var tw = DOVirtual.Float(0f, 1f, duration, p =>
            {
                float curve;
                if (p < 0.35f)
                {
                    // Sube rápido a punchMultiplier
                    float subT = p / 0.35f;
                    curve = Mathf.LerpUnclamped(1f, punchMultiplier, DOVirtual.EasedValue(0f, 1f, subT, Ease.OutQuad));
                }
                else
                {
                    // Rebota de regreso a 1f con inercia
                    float subT = (p - 0.35f) / 0.65f;
                    curve = Mathf.LerpUnclamped(punchMultiplier, 1f, DOVirtual.EasedValue(0f, 1f, subT, Ease.OutBack));
                }
                element.transform.scale = Vector3.one * curve;
            }).OnComplete(() =>
            {
                element.transform.scale = Vector3.one;
            });

            RegisterTween(element, tw);
        }

        /// <summary>
        /// Inicia una sutil oscilación senoidal (respiración / flotación) sobre el elemento.
        /// </summary>
        public static Tween StartIdleFloat(VisualElement element, float amplitude = 3.5f, float cycleDuration = 3f)
        {
            if (element == null) return null;
            KillTweens(element);

            var tw = DOVirtual.Float(0f, Mathf.PI * 2f, cycleDuration, rad =>
            {
                float yOffset = Mathf.Sin(rad) * amplitude;
                element.transform.position = new Vector3(0f, yOffset, 0f);
            }).SetLoops(-1, LoopType.Restart).SetEase(Ease.Linear);

            RegisterTween(element, tw);
            return tw;
        }
    }
}
