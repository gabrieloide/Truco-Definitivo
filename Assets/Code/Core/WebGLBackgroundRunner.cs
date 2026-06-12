using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// WebGL: los navegadores frenan el requestAnimationFrame de las pestañas en
    /// segundo plano (cambiar a Spotify, otra pestaña, etc.), lo que congela el
    /// juego entero — red incluida — y desincroniza las partidas online. El .jslib
    /// (WebGLBackgroundTick) mantiene el main loop corriendo a ~1 fps vía
    /// setInterval mientras la pestaña está oculta.
    /// </summary>
    public static class WebGLBackgroundRunner
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void StartBackgroundTickJS();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            StartBackgroundTickJS();
#endif
        }
    }
}
