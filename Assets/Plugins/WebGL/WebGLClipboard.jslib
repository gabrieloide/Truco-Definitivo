mergeInto(LibraryManager.library, {
    CopyToClipboardJS: function (textPtr) {
        var text = UTF8ToString(textPtr);

        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).catch(function (err) {
                console.warn("[WebGLClipboard] navigator.clipboard failed: " + err);
            });
            return;
        }

        // Fallback para contextos sin Clipboard API (http sin localhost, navegadores viejos)
        var textarea = document.createElement("textarea");
        textarea.value = text;
        textarea.style.position = "fixed";
        textarea.style.opacity = "0";
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();
        try {
            document.execCommand("copy");
        } catch (err) {
            console.warn("[WebGLClipboard] execCommand copy failed: " + err);
        }
        document.body.removeChild(textarea);
    }
});
