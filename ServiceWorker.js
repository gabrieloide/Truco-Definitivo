// Truco Definitivo: ServiceWorker de limpieza.
// No cachea nada: evita que los dispositivos se queden con builds viejos/incompatibles.
self.addEventListener('install', function () { self.skipWaiting(); });
self.addEventListener('activate', function (e) {
  e.waitUntil(
    caches.keys()
      .then(function (keys) { return Promise.all(keys.map(function (k) { return caches.delete(k); })); })
      .then(function () { return self.clients.claim(); })
  );
});
