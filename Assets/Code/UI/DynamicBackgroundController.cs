using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI
{
    /// <summary>
    /// Controlador del fondo dinámico para Truco Definitivo.
    /// Inspirado en la técnica de MrEliptik (Lexispell):
    /// - Fondo base azul petróleo profundo con viñeta de obsidiana y luz ambiental central.
    /// - Desplazamiento continuo en diagonal de patrón temático (seamless UV scroll).
    /// - Efecto Parallax interactivo sensible a la posición del mouse.
    /// - Partículas ambientales de polvo dorado y chispas flotantes.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DynamicBackgroundController : MonoBehaviour
    {
        [Header("Texturas del Fondo")]
        [SerializeField] private Texture2D patternTexture;
        [SerializeField] private Texture2D vignetteTexture;
        [SerializeField] private Texture2D radialGlowTexture;

        [Header("Parámetros de Scroll")]
        [SerializeField] private float scrollSpeedX = 0.018f;
        [SerializeField] private float scrollSpeedY = 0.014f;
        [SerializeField] private float patternTileSize = 256f;

        [Header("Parámetros de Parallax (Mouse)")]
        [SerializeField] private float parallaxStrength = 24f;
        [SerializeField] private float parallaxSmoothness = 6f;

        [Header("Partículas Ambientales")]
        [SerializeField] private int particleCount = 22;
        [SerializeField] private float minParticleSpeed = 16f;
        [SerializeField] private float maxParticleSpeed = 42f;

        private UIDocument _uiDocument;
        private VisualElement _root;
        private VisualElement _bgRoot;
        private VisualElement _bgSolidBase;
        private VisualElement _bgGlow;
        private VisualElement _bgPattern;
        private VisualElement _bgVignette;
        private VisualElement _bgParticles;

        private float _scrollU;
        private float _scrollV;
        private Vector2 _currentMouseNormalized;
        private readonly List<AmbientParticle> _particles = new();

        private class AmbientParticle
        {
            public VisualElement Element;
            public float X;
            public float Y;
            public float Speed;
            public float BaseSize;
            public float SinOffset;
            public float SinSpeed;
            public float Alpha;
        }

        private void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            LoadDefaultTexturesIfMissing();
        }

        private void OnEnable()
        {
            if (_uiDocument == null) _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            SetupBackgroundElements();
            SetupParticles();
        }

        private void OnDisable()
        {
            if (_bgPattern != null)
            {
                _bgPattern.generateVisualContent -= OnGeneratePatternVisualContent;
            }
            _particles.Clear();
        }

        private void LoadDefaultTexturesIfMissing()
        {
#if UNITY_EDITOR
            if (patternTexture == null)
                patternTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/bg_truco_pattern.png");
            if (vignetteTexture == null)
                vignetteTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/bg_vignette.png");
            if (radialGlowTexture == null)
                radialGlowTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/bg_radial_glow.png");
#endif
            // Si aún son nulas, generar una textura procedural de fallback
            if (patternTexture == null) patternTexture = CreateFallbackPatternTexture();
            if (vignetteTexture == null) vignetteTexture = CreateFallbackVignetteTexture();
            if (radialGlowTexture == null) radialGlowTexture = CreateFallbackGlowTexture();
        }

        private void SetupBackgroundElements()
        {
            // Intentar buscar los elementos en UXML
            _bgRoot = _root.Q<VisualElement>("bg-root");

            // Si no existen en UXML, crearlos dinámicamente como primer hijo de la raíz
            if (_bgRoot == null)
            {
                _bgRoot = new VisualElement { name = "bg-root", pickingMode = PickingMode.Ignore };
                _bgRoot.AddToClassList("bg-root");
                _root.Insert(0, _bgRoot);
            }

            _bgSolidBase = _bgRoot.Q<VisualElement>("bg-solid-base");
            if (_bgSolidBase == null)
            {
                _bgSolidBase = new VisualElement { name = "bg-solid-base", pickingMode = PickingMode.Ignore };
                _bgSolidBase.AddToClassList("bg-solid-base");
                _bgRoot.Add(_bgSolidBase);
            }

            _bgGlow = _bgRoot.Q<VisualElement>("bg-glow");
            if (_bgGlow == null)
            {
                _bgGlow = new VisualElement { name = "bg-glow", pickingMode = PickingMode.Ignore };
                _bgGlow.AddToClassList("bg-glow");
                _bgRoot.Add(_bgGlow);
            }
            if (radialGlowTexture != null)
            {
                _bgGlow.style.backgroundImage = new StyleBackground(radialGlowTexture);
            }

            _bgPattern = _bgRoot.Q<VisualElement>("bg-pattern");
            if (_bgPattern == null)
            {
                _bgPattern = new VisualElement { name = "bg-pattern", pickingMode = PickingMode.Ignore };
                _bgPattern.AddToClassList("bg-pattern");
                _bgRoot.Add(_bgPattern);
            }
            _bgPattern.generateVisualContent -= OnGeneratePatternVisualContent;
            _bgPattern.generateVisualContent += OnGeneratePatternVisualContent;

            _bgVignette = _bgRoot.Q<VisualElement>("bg-vignette");
            if (_bgVignette == null)
            {
                _bgVignette = new VisualElement { name = "bg-vignette", pickingMode = PickingMode.Ignore };
                _bgVignette.AddToClassList("bg-vignette");
                _bgRoot.Add(_bgVignette);
            }
            if (vignetteTexture != null)
            {
                _bgVignette.style.backgroundImage = new StyleBackground(vignetteTexture);
            }

            _bgParticles = _bgRoot.Q<VisualElement>("bg-particles");
            if (_bgParticles == null)
            {
                _bgParticles = new VisualElement { name = "bg-particles", pickingMode = PickingMode.Ignore };
                _bgParticles.AddToClassList("bg-particles");
                _bgRoot.Add(_bgParticles);
            }
        }

        private void OnGeneratePatternVisualContent(MeshGenerationContext mgc)
        {
            try
            {
                if (patternTexture == null || _bgPattern == null) return;

                Rect rect = _bgPattern.contentRect;
                if (rect.width <= 1f || rect.height <= 1f) return;

                float uSpan = rect.width / patternTileSize;
                float vSpan = rect.height / patternTileSize;

                float u0 = _scrollU;
                float v0 = _scrollV;
                float u1 = u0 + uSpan;
                float v1 = v0 + vSpan;

                var mesh = mgc.Allocate(4, 6, patternTexture);
                Color32 tint = new Color32(255, 255, 255, 225);

                mesh.SetNextVertex(new Vertex { position = new Vector3(0, 0, Vertex.nearZ), tint = tint, uv = new Vector2(u0, v0) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.width, 0, Vertex.nearZ), tint = tint, uv = new Vector2(u1, v0) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.width, rect.height, Vertex.nearZ), tint = tint, uv = new Vector2(u1, v1) });
                mesh.SetNextVertex(new Vertex { position = new Vector3(0, rect.height, Vertex.nearZ), tint = tint, uv = new Vector2(u0, v1) });

                mesh.SetNextIndex(0);
                mesh.SetNextIndex(1);
                mesh.SetNextIndex(2);
                mesh.SetNextIndex(2);
                mesh.SetNextIndex(3);
                mesh.SetNextIndex(0);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DynamicBackgroundController] Mesh generation skipped: {ex.Message}");
            }
        }

        private void SetupParticles()
        {
            if (_bgParticles == null) return;
            _bgParticles.Clear();
            _particles.Clear();

            float screenW = Screen.width > 0 ? Screen.width : 1920f;
            float screenH = Screen.height > 0 ? Screen.height : 1080f;

            for (int i = 0; i < particleCount; i++)
            {
                var elem = new VisualElement { pickingMode = PickingMode.Ignore };
                elem.AddToClassList("bg-particle");

                float size = Random.Range(3.5f, 7.5f);
                float alpha = Random.Range(0.20f, 0.55f);
                elem.style.width = size;
                elem.style.height = size;
                elem.style.borderTopLeftRadius = size * 0.5f;
                elem.style.borderTopRightRadius = size * 0.5f;
                elem.style.borderBottomLeftRadius = size * 0.5f;
                elem.style.borderBottomRightRadius = size * 0.5f;
                elem.style.backgroundColor = new Color(0.93f, 0.83f, 0.62f, alpha); // Oro criollo cálido

                _bgParticles.Add(elem);

                var p = new AmbientParticle
                {
                    Element = elem,
                    X = Random.Range(0f, screenW),
                    Y = Random.Range(0f, screenH),
                    Speed = Random.Range(minParticleSpeed, maxParticleSpeed),
                    BaseSize = size,
                    SinOffset = Random.Range(0f, Mathf.PI * 2f),
                    SinSpeed = Random.Range(1.2f, 2.5f),
                    Alpha = alpha
                };

                _particles.Add(p);
            }
        }

        private void Update()
        {
            // 1. Scroll continuo del patrón
            _scrollU += scrollSpeedX * Time.deltaTime;
            _scrollV += scrollSpeedY * Time.deltaTime;
            if (_bgPattern != null)
            {
                _bgPattern.MarkDirtyRepaint();
            }

            // 2. Parallax interactivo del mouse (estilo MrEliptik)
            UpdateParallax();

            // 3. Simulación de partículas ambientales
            UpdateParticles();
        }

        private void UpdateParallax()
        {
            if (_bgRoot == null) return;

            float screenW = Mathf.Max(1f, Screen.width);
            float screenH = Mathf.Max(1f, Screen.height);

            // Coordenadas normalizadas [-0.5, 0.5] desde el centro
            Vector3 mousePos = Input.mousePosition;
            float targetNormX = (mousePos.x / screenW) - 0.5f;
            float targetNormY = (mousePos.y / screenH) - 0.5f;

            _currentMouseNormalized = Vector2.Lerp(
                _currentMouseNormalized,
                new Vector2(targetNormX, targetNormY),
                Time.deltaTime * parallaxSmoothness
            );

            // El fondo se desplaza en dirección opuesta al cursor
            float offsetX = -_currentMouseNormalized.x * parallaxStrength;
            float offsetY = _currentMouseNormalized.y * parallaxStrength; // Invertir eje Y de UI Toolkit

            _bgRoot.transform.position = new Vector3(offsetX, offsetY, 0f);
        }

        private void UpdateParticles()
        {
            if (_bgParticles == null) return;

            float screenW = _bgParticles.resolvedStyle.width > 0 ? _bgParticles.resolvedStyle.width : Screen.width;
            float screenH = _bgParticles.resolvedStyle.height > 0 ? _bgParticles.resolvedStyle.height : Screen.height;

            float dt = Time.deltaTime;
            float time = Time.time;

            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                if (p.Element == null) continue;

                // Movimiento hacia arriba
                p.Y -= p.Speed * dt;

                // Ondulación horizontal senoidal sutil
                float waveX = Mathf.Sin(time * p.SinSpeed + p.SinOffset) * 1.8f;
                float currentX = p.X + waveX;

                // Si sale por arriba, reaparece por abajo con posición X aleatoria
                if (p.Y < -20f)
                {
                    p.Y = screenH + Random.Range(10f, 40f);
                    p.X = Random.Range(0f, screenW);
                    p.Speed = Random.Range(minParticleSpeed, maxParticleSpeed);
                }

                // Foreground parallax adicional sobre las partículas (capa de mayor profundidad)
                float particleParallaxX = -_currentMouseNormalized.x * (parallaxStrength * 1.35f);
                float particleParallaxY = _currentMouseNormalized.y * (parallaxStrength * 1.35f);

                p.Element.transform.position = new Vector3(currentX + particleParallaxX, p.Y + particleParallaxY, 0f);
            }
        }

        // ==========================================
        // TEXTURAS DE FALLBACK PROCEDURALES
        // ==========================================
        private Texture2D CreateFallbackPatternTexture()
        {
            int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            Color transparent = new Color(0, 0, 0, 0);
            Color blue = new Color(0.35f, 0.50f, 0.70f, 0.15f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isGrid = (x % 32 == 0) || (y % 32 == 0) || ((x + y) % 32 == 0);
                    tex.SetPixel(x, y, isGrid ? blue : transparent);
                }
            }
            tex.Apply();
            return tex;
        }

        private Texture2D CreateFallbackVignetteTexture()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color edgeColor = new Color(0.03f, 0.04f, 0.06f, 0.95f);

            for (int y = 0; y < size; y++)
            {
                float ny = (y - size * 0.5f) / (size * 0.5f);
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - size * 0.5f) / (size * 0.5f);
                    float d = Mathf.Sqrt(nx * nx + ny * ny);
                    float alpha = Mathf.SmoothStep(0.35f, 1.15f, d) * 0.95f;
                    tex.SetPixel(x, y, new Color(edgeColor.r, edgeColor.g, edgeColor.b, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        private Texture2D CreateFallbackGlowTexture()
        {
            int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < size; y++)
            {
                float ny = (y - size * 0.5f) / (size * 0.5f);
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - size * 0.5f) / (size * 0.5f);
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                    float alpha = d * d * 0.35f;
                    tex.SetPixel(x, y, new Color(0.20f, 0.40f, 0.55f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
