mergeInto(LibraryManager.library, {
    CopyToClipboardJS: function (textPtr) {
        var text = UTF8ToString(textPtr);

        // Último recurso: un prompt nativo bloquea el juego un instante pero
        // garantiza que el jugador pueda copiar el código a mano en cualquier
        // navegador/contexto (http, iframes sin permiso clipboard-write, etc).
        function promptFallback() {
            window.prompt("No se pudo copiar automáticamente. Copiá el código:", text);
        }

        // Fallback para contextos sin Clipboard API (http sin localhost, navegadores viejos)
        function legacyCopy() {
            var textarea = document.createElement("textarea");
            textarea.value = text;
            textarea.style.position = "fixed";
            textarea.style.opacity = "0";
            document.body.appendChild(textarea);
            textarea.focus();
            textarea.select();
            var ok = false;
            try {
                ok = document.execCommand("copy");
            } catch (err) {
                console.warn("[WebGLClipboard] execCommand copy failed: " + err);
            }
            document.body.removeChild(textarea);
            if (!ok) promptFallback();
        }

        if (navigator.clipboard && navigator.clipboard.writeText) {
            // La promesa puede rechazarse (iframe sin clipboard-write, Safari sin
            // activación de usuario): recién ahí caemos al plan B.
            navigator.clipboard.writeText(text).catch(function (err) {
                console.warn("[WebGLClipboard] navigator.clipboard failed: " + err);
                legacyCopy();
            });
            return;
        }

        legacyCopy();
    }
});
