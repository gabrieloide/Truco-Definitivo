using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Code.Editor
{
    /// <summary>
    /// Blindaje de builds WebGL contra la "pantalla negra" en ciertos dispositivos.
    ///
    /// Causas detectadas (octubre 2026):
    /// 1. "Auto Graphics API" en Unity 6.5 incluye el backend WebGPU. Cualquier navegador que exponga
    ///    navigator.gpu (Chrome Android 121+/Android 12+, Safari iOS 26+, Chrome/Edge desktop) arranca
    ///    con WebGPU, que aún es inestable en GPUs móviles -> canvas negro sin error visible.
    ///    Se fuerza WebGL 2.0 únicamente.
    /// 2. El ServiceWorker del template PWA precachea archivos ".br" que el CI elimina (404), y su
    ///    estrategia es cache-first con nombres de archivo fijos. Se reemplaza por un SW de limpieza
    ///    que borra cachés antiguas y no intercepta peticiones.
    /// 3. Sin diagnóstico en dispositivo real: se inyecta un overlay que muestra errores JS, pérdida
    ///    de contexto WebGL y datos de GPU (añadir ?debug=1 a la URL para ver información completa).
    /// 4. devicePixelRatio de 3+ en móviles genera framebuffers enormes: se limita a 2.
    /// </summary>
    public class WebGLBuildHardening : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const float MaxDevicePixelRatio = 2f;

        [MenuItem("Truco/WebGL/Aplicar ajustes anti pantalla negra")]
        public static void ApplySafeSettings()
        {
            const BuildTarget target = BuildTarget.WebGL;

            // 1. Solo WebGL 2.0 (GraphicsDeviceType.OpenGLES3 == WebGL2 en la plataforma Web).
            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.OpenGLES3 });

            // 2. Wasm más pequeño -> menos memoria al compilar en navegadores móviles.
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL,
                UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);

            AssetDatabase.SaveAssets();
            Debug.Log("[WebGLBuildHardening] Ajustes aplicados: GraphicsAPIs=WebGL2, IL2CPP=OptimizeSize.");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.WebGL);
            bool onlyWebGL2 = !PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.WebGL)
                              && apis.Length == 1 && apis[0] == GraphicsDeviceType.OpenGLES3;
            if (!onlyWebGL2)
            {
                Debug.LogWarning("[WebGLBuildHardening] La API gráfica WebGL no era solo WebGL2. Corrigiendo antes del build.");
                ApplySafeSettings();
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;

            string outputPath = report.summary.outputPath;
            if (string.IsNullOrEmpty(outputPath) || !Directory.Exists(outputPath)) return;

            WriteSafeServiceWorker(outputPath);
            PatchIndexHtml(outputPath);
        }

        private static void WriteSafeServiceWorker(string outputPath)
        {
            string swPath = Path.Combine(outputPath, "ServiceWorker.js");
            if (!File.Exists(swPath)) return;

            const string sw =
@"// Truco Definitivo: ServiceWorker de limpieza.
// No cachea nada: evita que los dispositivos se queden con builds viejos/incompatibles.
self.addEventListener('install', function () { self.skipWaiting(); });
self.addEventListener('activate', function (e) {
  e.waitUntil(
    caches.keys()
      .then(function (keys) { return Promise.all(keys.map(function (k) { return caches.delete(k); })); })
      .then(function () { return self.clients.claim(); })
  );
});
";
            File.WriteAllText(swPath, sw);
            Debug.Log("[WebGLBuildHardening] ServiceWorker.js reemplazado por versión de limpieza.");
        }

        private static void PatchIndexHtml(string outputPath)
        {
            string indexPath = Path.Combine(outputPath, "index.html");
            if (!File.Exists(indexPath)) return;

            string html = File.ReadAllText(indexPath);
            if (html.Contains("truco-diag")) return; // ya parcheado

            const string diagnostics =
@"    <script>
      // Truco Definitivo: diagnóstico de pantalla negra en dispositivos reales.
      (function () {
        var box, pending = [];
        var debug = /[?&]debug=1/.test(location.search);
        function show(msg) {
          if (!document.body) { pending.push(msg); return; }
          if (!box) {
            box = document.createElement('div');
            box.id = 'truco-diag';
            box.style.cssText = 'position:fixed;left:0;right:0;bottom:0;max-height:45%;overflow:auto;' +
              'background:rgba(110,0,0,.92);color:#fff;font:12px monospace;padding:8px;z-index:99999;white-space:pre-wrap';
            box.onclick = function () { box.style.display = 'none'; };
            document.body.appendChild(box);
          }
          box.style.display = 'block';
          box.textContent += msg + '\n';
        }
        function gpuInfo() {
          try {
            var gl = document.createElement('canvas').getContext('webgl2');
            if (!gl) return 'WebGL2: NO SOPORTADO';
            var d = gl.getExtension('WEBGL_debug_renderer_info');
            return 'WebGL2 OK | GPU: ' + (d ? gl.getParameter(d.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER)) +
              ' | maxTex: ' + gl.getParameter(gl.MAX_TEXTURE_SIZE);
          } catch (e) { return 'WebGL2 error: ' + e; }
        }
        window.addEventListener('error', function (e) {
          show('[error] ' + (e.message || e) + (e.filename ? ' @ ' + e.filename + ':' + e.lineno : ''));
        });
        window.addEventListener('unhandledrejection', function (e) {
          show('[promise] ' + ((e.reason && e.reason.message) || e.reason));
        });
        if (debug) {
          var origError = console.error;
          console.error = function () { show('[console] ' + Array.prototype.join.call(arguments, ' ')); origError.apply(console, arguments); };
        }
        document.addEventListener('DOMContentLoaded', function () {
          pending.forEach(show); pending = [];
          var cv = document.querySelector('#unity-canvas');
          if (cv) {
            cv.addEventListener('webglcontextlost', function () {
              show('[webglcontextlost] La GPU perdió el contexto WebGL (memoria insuficiente o driver).');
            });
          }
          if (debug) {
            show('[info] ' + navigator.userAgent);
            show('[info] ' + gpuInfo());
            show('[info] RAM: ' + (navigator.deviceMemory || '?') + ' GB | DPR: ' + window.devicePixelRatio +
              ' | WebGPU expuesto: ' + (!!navigator.gpu));
          }
        });
      })();
    </script>
";
            int headClose = html.IndexOf("</head>", System.StringComparison.OrdinalIgnoreCase);
            if (headClose >= 0)
            {
                html = html.Insert(headClose, diagnostics);
            }

            // Limitar devicePixelRatio (Unity 6 lo admite en el config de createUnityInstance).
            const string bannerKey = "showBanner: unityShowBanner,";
            if (html.Contains(bannerKey) && !html.Contains("devicePixelRatio: Math.min"))
            {
                html = html.Replace(bannerKey,
                    "devicePixelRatio: Math.min(window.devicePixelRatio || 1, " +
                    MaxDevicePixelRatio.ToString(System.Globalization.CultureInfo.InvariantCulture) + "),\n        " + bannerKey);
            }

            File.WriteAllText(indexPath, html);
            Debug.Log("[WebGLBuildHardening] index.html parcheado (diagnóstico + límite de devicePixelRatio).");
        }
    }
}
