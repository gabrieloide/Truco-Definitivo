mergeInto(LibraryManager.library, {
    WebGLStorage_SetItem: function (keyPtr, valPtr) {
        try {
            var key = UTF8ToString(keyPtr);
            var val = UTF8ToString(valPtr);
            if (window.localStorage) {
                window.localStorage.setItem(key, val);
            }
        } catch (e) {
            console.warn("[WebGLStorage] SetItem failed: " + e);
        }
    },

    WebGLStorage_GetItem: function (keyPtr) {
        try {
            var key = UTF8ToString(keyPtr);
            if (window.localStorage) {
                var val = window.localStorage.getItem(key);
                if (val !== null && val !== undefined) {
                    var bufferSize = lengthBytesUTF8(val) + 1;
                    var buffer = _malloc(bufferSize);
                    stringToUTF8(val, buffer, bufferSize);
                    return buffer;
                }
            }
        } catch (e) {
            console.warn("[WebGLStorage] GetItem failed: " + e);
        }
        return 0;
    },

    WebGLStorage_RemoveItem: function (keyPtr) {
        try {
            var key = UTF8ToString(keyPtr);
            if (window.localStorage) {
                window.localStorage.removeItem(key);
            }
        } catch (e) {
            console.warn("[WebGLStorage] RemoveItem failed: " + e);
        }
    }
});
