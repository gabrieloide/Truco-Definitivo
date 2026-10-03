using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Code.Persistence
{
    /// <summary>
    /// Administrador central de perfil, autenticación y guardado en la nube/local.
    /// Funciona en modo "Invitado / Sin Nube" automáticamente si el jugador no desea cuenta,
    /// o se sincroniza mediante la API RESTful de Firebase si el jugador inicia sesión.
    /// </summary>
    public class CloudAuthManager : MonoBehaviour
    {
        public static CloudAuthManager Instance { get; private set; }

        [Header("Configuración Firebase REST API")]
        [Tooltip("Tu Firebase Web API Key (de la consola de Firebase -> Project Settings).")]
        [SerializeField] private string firebaseApiKey = "AIzaSyB3GoigtTzP2R4c9eU4x6m2sPNvoRKJa4Y";

        [Tooltip("URL de tu Firebase Realtime Database (ej: https://mi-proyecto-default-rtdb.firebaseio.com)")]
        [SerializeField] private string firebaseDatabaseUrl = "https://venezuelan-truco-default-rtdb.firebaseio.com";

        public PlayerData CurrentPlayer { get; private set; }
        public bool IsLoggedIn => CurrentPlayer != null && !CurrentPlayer.isGuest && !string.IsNullOrEmpty(_currentIdToken);
        public bool IsGuest => CurrentPlayer == null || CurrentPlayer.isGuest;

        public event Action<PlayerData> OnProfileUpdated;
        public event Action<bool, string> OnAuthCompleted; // success, errorMessage

        private FirebaseRestClient _restClient;
        private string _currentIdToken = "";
        private string _currentLocalId = "";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeClient();
            LoadInitialProfile();
        }

        private void InitializeClient()
        {
            _restClient = new FirebaseRestClient(firebaseApiKey, firebaseDatabaseUrl);
        }

        public void Configure(string apiKey, string dbUrl)
        {
            firebaseApiKey = apiKey;
            firebaseDatabaseUrl = dbUrl;
            InitializeClient();
        }

        private async void LoadInitialProfile()
        {
            // 1. Cargar datos locales inmediatamente para disponibilidad instantánea
            CurrentPlayer = LocalSaveProvider.Load();

            // 2. Si tenía una sesión iniciada previamente, restaurarla en segundo plano
            if (LocalSaveProvider.TryGetAuthSession(out string email, out string token, out string localId) && !LocalSaveProvider.IsGuestPreferred())
            {
                _currentIdToken = token;
                _currentLocalId = localId;

                // Intentar refrescar datos desde la nube
                if (!string.IsNullOrEmpty(firebaseApiKey) && !string.IsNullOrEmpty(firebaseDatabaseUrl))
                {
                    var (success, cloudData, err) = await _restClient.LoadPlayerDataAsync(localId, token);
                    if (success && cloudData != null)
                    {
                        CurrentPlayer = cloudData;
                        CurrentPlayer.isGuest = false;
                        LocalSaveProvider.Save(CurrentPlayer);
                        Debug.Log($"[CloudAuthManager] Perfil cargado desde la nube: {CurrentPlayer.username} (Nivel {CurrentPlayer.level}, {CurrentPlayer.coins} fichas)");
                    }
                }
            }

            OnProfileUpdated?.Invoke(CurrentPlayer);
        }

        /// <summary>
        /// Jugar como invitado sin guardar en la nube. Guarda exclusivamente de forma local.
        /// </summary>
        public void PlayAsGuest(string guestName = null)
        {
            LocalSaveProvider.SetGuestPreferred(true);
            _currentIdToken = "";
            _currentLocalId = "";

            if (CurrentPlayer == null)
            {
                CurrentPlayer = LocalSaveProvider.Load();
            }

            CurrentPlayer.isGuest = true;
            if (!string.IsNullOrWhiteSpace(guestName))
            {
                CurrentPlayer.username = guestName.Trim();
            }

            LocalSaveProvider.Save(CurrentPlayer);
            Debug.Log($"[CloudAuthManager] Modo Invitado activo: {CurrentPlayer.username}");

            OnProfileUpdated?.Invoke(CurrentPlayer);
            OnAuthCompleted?.Invoke(true, null);
        }

        /// <summary>
        /// Registrar un nuevo usuario en la nube con usuario/email y contraseña.
        /// </summary>
        public async Task<(bool success, string error)> RegisterAsync(string usernameOrEmail, string password, string displayName = null)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Por favor completá todos los campos.");
            }

            if (password.Length < 6)
            {
                return (false, "La contraseña debe tener al menos 6 caracteres.");
            }

            if (string.IsNullOrEmpty(firebaseApiKey))
            {
                // Modo simulado / desarrollo si aún no se ingresó API Key en Inspector
                Debug.LogWarning("[CloudAuthManager] API Key de Firebase no configurada. Simulando registro local exitoso.");
                CurrentPlayer.username = string.IsNullOrWhiteSpace(displayName) ? usernameOrEmail : displayName;
                CurrentPlayer.email = usernameOrEmail;
                CurrentPlayer.isGuest = false;
                LocalSaveProvider.Save(CurrentPlayer);
                LocalSaveProvider.SetGuestPreferred(false);
                OnProfileUpdated?.Invoke(CurrentPlayer);
                OnAuthCompleted?.Invoke(true, null);
                return (true, null);
            }

            var (success, authResp, errMsg) = await _restClient.SignUpAsync(usernameOrEmail, password);
            if (!success)
            {
                OnAuthCompleted?.Invoke(false, errMsg);
                return (false, errMsg);
            }

            _currentIdToken = authResp.idToken;
            _currentLocalId = authResp.localId;

            // Conservar progreso local previo o asignar perfil nuevo
            string finalName = !string.IsNullOrWhiteSpace(displayName) ? displayName : usernameOrEmail;
            CurrentPlayer.userId = _currentLocalId;
            CurrentPlayer.username = finalName;
            CurrentPlayer.email = authResp.email;
            CurrentPlayer.isGuest = false;

            // Guardar en nube
            var (saveSuccess, saveErr) = await _restClient.SavePlayerDataAsync(_currentLocalId, _currentIdToken, CurrentPlayer);
            if (!saveSuccess)
            {
                Debug.LogWarning($"[CloudAuthManager] Cuenta creada pero fallo al sincronizar datos iniciales: {saveErr}");
            }

            // Guardar sesión local
            LocalSaveProvider.SaveAuthSession(authResp.email, _currentIdToken, _currentLocalId);
            LocalSaveProvider.Save(CurrentPlayer);

            OnProfileUpdated?.Invoke(CurrentPlayer);
            OnAuthCompleted?.Invoke(true, null);
            return (true, null);
        }

        /// <summary>
        /// Iniciar sesión con usuario/email y contraseña.
        /// </summary>
        public async Task<(bool success, string error)> LoginAsync(string usernameOrEmail, string password)
        {
            if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Por favor ingresá tu usuario y contraseña.");
            }

            if (string.IsNullOrEmpty(firebaseApiKey))
            {
                // Modo simulado / desarrollo si aún no se ingresó API Key en Inspector
                Debug.LogWarning("[CloudAuthManager] API Key de Firebase no configurada. Simulando login local exitoso.");
                CurrentPlayer.username = usernameOrEmail;
                CurrentPlayer.isGuest = false;
                LocalSaveProvider.Save(CurrentPlayer);
                LocalSaveProvider.SetGuestPreferred(false);
                OnProfileUpdated?.Invoke(CurrentPlayer);
                OnAuthCompleted?.Invoke(true, null);
                return (true, null);
            }

            var (success, authResp, errMsg) = await _restClient.SignInAsync(usernameOrEmail, password);
            if (!success)
            {
                OnAuthCompleted?.Invoke(false, errMsg);
                return (false, errMsg);
            }

            _currentIdToken = authResp.idToken;
            _currentLocalId = authResp.localId;

            // Descargar perfil de la nube
            var (loadSuccess, cloudData, loadErr) = await _restClient.LoadPlayerDataAsync(_currentLocalId, _currentIdToken);
            if (loadSuccess && cloudData != null)
            {
                CurrentPlayer = cloudData;
                CurrentPlayer.isGuest = false;
            }
            else
            {
                // Si la cuenta existía pero no tenía datos guardados en la BD todavía
                CurrentPlayer.userId = _currentLocalId;
                CurrentPlayer.username = usernameOrEmail;
                CurrentPlayer.email = authResp.email;
                CurrentPlayer.isGuest = false;
                await _restClient.SavePlayerDataAsync(_currentLocalId, _currentIdToken, CurrentPlayer);
            }

            LocalSaveProvider.SaveAuthSession(authResp.email, _currentIdToken, _currentLocalId);
            LocalSaveProvider.Save(CurrentPlayer);

            OnProfileUpdated?.Invoke(CurrentPlayer);
            OnAuthCompleted?.Invoke(true, null);
            return (true, null);
        }

        /// <summary>
        /// Cierra la sesión activa y regresa al modo invitado.
        /// </summary>
        public void Logout()
        {
            LocalSaveProvider.ClearAuthSession();
            _currentIdToken = "";
            _currentLocalId = "";

            PlayAsGuest("Invitado");
        }

        /// <summary>
        /// Guarda el estado actual en local y en la nube (si el usuario está autenticado).
        /// </summary>
        public async Task SaveProfileAsync()
        {
            if (CurrentPlayer == null) return;

            // Siempre guardar en local primero
            LocalSaveProvider.Save(CurrentPlayer);

            // Sincronizar en la nube si tiene cuenta activa
            if (!CurrentPlayer.isGuest && !string.IsNullOrEmpty(_currentIdToken) && !string.IsNullOrEmpty(_currentLocalId))
            {
                var (success, err) = await _restClient.SavePlayerDataAsync(_currentLocalId, _currentIdToken, CurrentPlayer);
                if (!success)
                {
                    Debug.LogWarning($"[CloudAuthManager] Falló la sincronización con la nube: {err}. Los datos están seguros en local.");
                }
            }
        }

        /// <summary>
        /// Registra la finalización de una partida, calcula XP, fichas y persiste los cambios.
        /// </summary>
        public void RecordMatchEnd(bool won, int envidoScore = 0)
        {
            if (CurrentPlayer == null) CurrentPlayer = LocalSaveProvider.Load();

            CurrentPlayer.RecordMatch(won, envidoScore);
            _ = SaveProfileAsync();

            Debug.Log($"[CloudAuthManager] Partida registrada. Victoria: {won}. Nivel: {CurrentPlayer.level}, XP: {CurrentPlayer.experience}/{CurrentPlayer.ExperienceForNextLevel}, Fichas: {CurrentPlayer.coins}");
            OnProfileUpdated?.Invoke(CurrentPlayer);
        }
    }
}
