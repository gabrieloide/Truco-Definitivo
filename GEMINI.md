# Truco Definitivo - Reglas del Proyecto y Buenas Prácticas (AI Guidelines)

Este archivo establece las directivas obligatorias para cualquier asistente de IA o desarrollador trabajando en **Truco Definitivo**. Su propósito es proteger la estabilidad de la arquitectura, evitar regresiones destructivas y garantizar la compatibilidad entre plataformas (WebGL, Móvil, PC).

---

## 1. Reglas Críticas de Plataforma y WebGL (NO ROMPER)
- **Cero librerías nativas C++**: Toda comunicación de red externa debe usar `UnityWebRequest` o protocolos HTTP/WebSocket compatibles con WebGL. NUNCA intentar instalar SDKs nativos que incluyan `.dll` o `.so` no compatibles con navegadores (como el SDK tradicional de Firebase C#).
- **Compatibilidad Gráfica URP**:
  - **HDR debe permanecer DESACTIVADO** en todos los URP Universal Render Pipeline Assets (`Mobile`, `Low`, `Medium`, `High`, etc.). Activar HDR produce pantalla negra en navegadores móviles (WebGL iOS / Android).
  - Los shaders críticos como `MK/Toon/Standard` nunca deben ser removidos de los recursos siempre incluidos.

---

## 2. Reglas de Persistencia y Nube (Firebase & Local)
- **Persistencia en dos capas (Offline-First)**:
  - Todo cambio en el perfil (`PlayerData`) debe guardarse **inmediatamente en local** primero (`LocalSaveProvider`).
  - La sincronización con la nube (`CloudAuthManager` / `FirebaseRestClient`) se hace de forma asíncrona en segundo plano sin bloquear el hilo principal ni congelar la pantalla.
- **Respetar el Modo Invitado**:
  - El jugador **nunca debe ser forzado** a crear una cuenta ni iniciar sesión para jugar. El modo invitado debe permitir acceso inmediato y sin fricción al juego un jugador y multijugador.
- **Normalización de Identificadores**:
  - La base de datos de Firebase Realtime se ubica en `https://venezuelan-truco-default-rtdb.firebaseio.com/`. Los perfiles se indexan exclusivamente bajo `users/{userId}`.

---

## 3. Reglas de UI Toolkit y USS
- **Prohibido el uso de pseudo-clases CSS estándar no soportadas por Unity**:
  - NUNCA usar `:last-child`, `:first-child`, `:nth-child()`.
  - NUNCA usar propiedades de CSS inexistentes en USS como `text-transform: uppercase` (las transformaciones de texto deben hacerse en mayúsculas en el texto directo o en código C#).
- **Tipografías del Proyecto**:
  - Display y Títulos Criollos: `Rye-Regular.ttf`.
  - Botones y Cifras Táctiles (Pop 2.5D): `LilitaOne-Regular.ttf`.
  - Textos de lectura e inputs: `Rubik.ttf`.

---

## 4. Reglas de Red y Ciclo de Vida en Multijugador (Mirror + Relay)
- NUNCA llamar a `Destroy()` en objetos con `NetworkIdentity` en el cliente mientras estén enlazados al servidor. Mirror se encarga de sincronizar el ciclo de vida.
- Validar siempre `isLocalPlayer` antes de emitir inputs de cartas o comandos de juego.
- Separar la lógica de negocio autoritativa del servidor (`GameManager`) de la interfaz local (`PlayerHUD`).

---

## 5. Reglas de Integridad del Repositorio Git y Assets Unity
- **Cuidado con los archivos `.meta`**:
  - Cada vez que se cree un script, asset o carpeta en `Assets/`, debe asegurarse la creación y preservación de su archivo `.meta`. Modificar o mover archivos sin su `.meta` rompe los GUIDs referenciados en prefabs y escenas.
- **Compilación Limpia**:
  - Tras cualquier cambio de código, verificar que no existan errores de compilación (`error CS`) en `Logs/Editor.log`.
- **Commits Claros**:
  - Usar conventional commits (`feat:`, `fix:`, `refactor:`, `style:`).
