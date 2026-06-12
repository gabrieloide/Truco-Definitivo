mergeInto(LibraryManager.library, {
    StartBackgroundTickJS__deps: ['$Browser'],
    StartBackgroundTickJS: function () {
        if (typeof document === 'undefined') return;

        // Los navegadores frenan el requestAnimationFrame de las pestañas ocultas:
        // Unity deja de correr frames, el transporte deja de mandar heartbeats
        // (Relay corta la conexión por inactividad) y las corrutinas del juego se
        // congelan, desincronizando la partida. Mientras la pestaña está oculta
        // pausamos el scheduler por rAF y tickeamos el main loop a mano con
        // setInterval, que el navegador recorta a ~1/segundo: suficiente para que
        // la red siga viva y el juego avance.
        var intervalId = null;

        function startTicking() {
            if (intervalId !== null) return;
            if (typeof Browser === 'undefined' || !Browser.mainLoop || !Browser.mainLoop.func) {
                console.warn('[BackgroundTick] Browser.mainLoop no disponible; el juego se pausará en segundo plano.');
                return;
            }
            Browser.mainLoop.pause(); // mata la cadena de rAF para no duplicar ticks al volver
            intervalId = setInterval(function () {
                try {
                    // runIter hace el bookkeeping completo de frame; func pelado es el fallback
                    if (Browser.mainLoop.runIter) Browser.mainLoop.runIter(Browser.mainLoop.func);
                    else Browser.mainLoop.func();
                } catch (e) {
                    console.error('[BackgroundTick] Error en tick de fondo: ' + e);
                }
            }, 250);
        }

        function stopTicking() {
            if (intervalId === null) return;
            clearInterval(intervalId);
            intervalId = null;
            if (typeof Browser !== 'undefined' && Browser.mainLoop && Browser.mainLoop.func) {
                try {
                    Browser.mainLoop.resume();
                } catch (e) {
                    console.error('[BackgroundTick] Error al reanudar main loop: ' + e);
                }
            }
        }

        document.addEventListener('visibilitychange', function () {
            if (document.hidden) startTicking();
            else stopTicking();
        });

        // Algunos navegadores ocultan la página sin visibilitychange al congelarla.
        window.addEventListener('pagehide', stopTicking);
    }
});
