using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Code.Persistence
{
    /// <summary>
    /// Administrador central de perfil, autenticación y persistencia en la nube/local.
    /// Funciona en modo "Invitado / Sin Nube" automáticamente si el jugador no desea cuenta,
    /// o se sincroniza mediante la API RESTful de Firebase si el jugador inicia sesión.
    /// Garantiza la persistencia indefinida mediante tokens de refresco (Remember Me permanente).
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
        public bool IsLoggedIn => CurrentPlayer != null && !CurrentPlayer.isGuest && (!string.IsNullOrEmpty(_currentIdToken) || !string.IsNullOrEmpty(_currentRefreshToken));
        public bool IsGuest => CurrentPlayer == null || CurrentPlayer.isGuest;

        public event Action<PlayerData> OnProfileUpdated;
        public event Action<bool, string> OnAuthCompleted; // success, errorMessage

        private FirebaseRestClient _restClient;
        private string _currentIdToken = "";
        private string _currentRefreshToken = "";
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
            // 1. Cargar datos locales inmediatamente para disponibilidad instantánea (pantalla sin demoras)
            CurrentPlayer = LocalSaveProvider.Load();

            // 2. Si tenía una sesión iniciada previamente y no cerró sesión explícitamente, restaurarla
            bool hasSession = LocalSaveProvider.TryGetAuthSession(
                out string email,
                out string token,
                out string refreshToken,
                out string localId);

            if (hasSession && !LocalSaveProvider.IsExplicitGuest())
            {
                _currentIdToken = token;
                _currentRefreshToken = refreshToken;
                _currentLocalId = localId;

                CurrentPlayer.userId = localId;
                if (!string.IsNullOrEmpty(email) && string.IsNullOrEmpty(CurrentPlayer.email))
                {
                    CurrentPlayer.email = email;
                }
                CurrentPlayer.isGuest = false;

                // Notificar de inmediato el estado autenticado local
                OnProfileUpdated?.Invoke(CurrentPlayer);

                // 3. Sincronización y refresco de token en la nube en segundo plano
                if (!string.IsNullOrEmpty(firebaseApiKey) && !string.IsNullOrEmpty(firebaseDatabaseUrl))
                {
                    // Si tenemos refreshToken, renovar de inmediato para garantizar token vigente
                    if (!string.IsNullOrEmpty(_currentRefreshToken))
                    {
                        await RefreshSessionTokenAsync();
                    }

                    var (success, cloudData, err, isExpired) = await _restClient.LoadPlayerDataAsync(_currentLocalId, _currentIdToken);

                    // Si el token expiró y no se había refrescado, renovar y reintentar
                    if (!success && isExpired)
                    {
                        bool refreshed = await RefreshSessionTokenAsync();
                        if (refreshed)
                        {
                            (success, cloudData, err, _) = await _restClient.LoadPlayerDataAsync(_currentLocalId, _currentIdToken);
                        }
                    }

                    if (success && cloudData != null)
                    {
                        CurrentPlayer = cloudData;
                        CurrentPlayer.isGuest = false;
                        LocalSaveProvider.Save(CurrentPlayer);
                        Debug.Log($"[CloudAuthManager] Perfil cargado desde la nube: {CurrentPlayer.username} (Nivel {CurrentPlayer.level}, {CurrentPlayer.coins} fichas)");
                        OnProfileUpdated?.Invoke(CurrentPlayer);
                    }
                }
                return;
            }

            // 4. Si no había tokens activos pero hay credenciales recordadas ("Recordar mis datos")
            if (!LocalSaveProvider.IsExplicitGuest() && LocalSaveProvider.GetRememberedCredentials(out string remUser, out string remPass))
            {
                if (!string.IsNullOrEmpty(remUser) && !string.IsNullOrEmpty(remPass))
                {
                    Debug.Log("[CloudAuthManager] Restaurando sesión mediante credenciales recordadas...");
                    _ = LoginAsync(remUser, remPass, rememberMe: true, silent: true);
                    return;
                }
            }

            OnProfileUpdated?.Invoke(CurrentPlayer);
        }

        /// <summary>
        /// Renueva el ID token de Firebase usando el refresh token persistido.
        /// </summary>
        public async Task<bool> RefreshSessionTokenAsync()
        {
            if (string.IsNullOrEmpty(_currentRefreshToken))
            {
                return await TryAutoLoginWithSavedCredentialsAsync();
            }

            var (success, refreshResp, errMsg) = await _restClient.RefreshTokenAsync(_currentRefreshToken);
            if (success && refreshResp != null && !string.IsNullOrEmpty(refreshResp.id_token))
            {
                _currentIdToken = refreshResp.id_token;
                if (!string.IsNullOrEmpty(refreshResp.refresh_token))
                {
                    _currentRefreshToken = refreshResp.refresh_token;
                }
                if (!string.IsNullOrEmpty(refreshResp.user_id))
                {
                    _currentLocalId = refreshResp.user_id;
                }

                LocalSaveProvider.SaveAuthSession(CurrentPlayer?.email, _currentIdToken, _currentRefreshToken, _currentLocalId);
                Debug.Log("[CloudAuthManager] Token de Firebase renovado exitosamente.");
                return true;
            }
            else
            {
                Debug.LogWarning($"[CloudAuthManager] Falló renovación con refresh token ({errMsg}). Probando credenciales recordadas...");
                return await TryAutoLoginWithSavedCredentialsAsync();
            }
        }

        private async Task<bool> TryAutoLoginWithSavedCredentialsAsync()
        {
            if (!LocalSaveProvider.GetRememberedCredentials(out string savedUser, out string savedPass))
            {
                return false;
            }

            if (string.IsNullOrEmpty(savedUser) || string.IsNullOrEmpty(savedPass))
            {
                return false;
            }

            Debug.Log("[CloudAuthManager] Intentando inicio de sesión automático silencioso...");
            var (success, _) = await LoginAsync(savedUser, savedPass, rememberMe: true, silent: true);
            return success;
        }

        /// <summary>
        /// Jugar como invitado sin guardar en la nube. Guarda exclusivamente de forma local.
        /// </summary>
        public void PlayAsGuest(string guestName = null)
        {
            LocalSaveProvider.SetExplicitGuest(true);
            _currentIdToken = "";
            _currentRefreshToken = "";
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
        public async Task<(bool success, string error)> RegisterAsync(string usernameOrEmail, string password, string displayName = null, bool rememberMe = true)
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
                LocalSaveProvider.SetExplicitGuest(false);
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
            _currentRefreshToken = authResp.refreshToken;
            _currentLocalId = authResp.localId;

            // Conservar progreso local previo o asignar perfil nuevo
            string finalName = !string.IsNullOrWhiteSpace(displayName) ? displayName : usernameOrEmail;
            CurrentPlayer.userId = _currentLocalId;
            CurrentPlayer.username = finalName;
            CurrentPlayer.email = authResp.email;
            CurrentPlayer.isGuest = false;

            // Guardar en nube
            var (saveSuccess, saveErr, _) = await _restClient.SavePlayerDataAsync(_currentLocalId, _currentIdToken, CurrentPlayer);
            if (!saveSuccess)
            {
                Debug.LogWarning($"[CloudAuthManager] Cuenta creada pero fallo al sincronizar datos iniciales: {saveErr}");
            }

            // Guardar sesión local con respaldo y soporte 'Recordarme'
            LocalSaveProvider.SaveAuthSession(authResp.email, _currentIdToken, _currentRefreshToken, _currentLocalId, rememberMe, usernameOrEmail, password);
            LocalSaveProvider.Save(CurrentPlayer);
            LocalSaveProvider.SetExplicitGuest(false);

            OnProfileUpdated?.Invoke(CurrentPlayer);
            OnAuthCompleted?.Invoke(true, null);
            return (true, null);
        }

        /// <summary>
        /// Iniciar sesión con usuario/email y contraseña.
        /// </summary>
        public async Task<(bool success, string error)> LoginAsync(string usernameOrEmail, string password, bool rememberMe = true, bool silent = false)
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
                LocalSaveProvider.SetExplicitGuest(false);
                OnProfileUpdated?.Invoke(CurrentPlayer);
                if (!silent) OnAuthCompleted?.Invoke(true, null);
                return (true, null);
            }

            var (success, authResp, errMsg) = await _restClient.SignInAsync(usernameOrEmail, password);
            if (!success)
            {
                if (!silent) OnAuthCompleted?.Invoke(false, errMsg);
                return (false, errMsg);
            }

            _currentIdToken = authResp.idToken;
            _currentRefreshToken = authResp.refreshToken;
            _currentLocalId = authResp.localId;

            // Descargar perfil de la nube
            var (loadSuccess, cloudData, loadErr, _) = await _restClient.LoadPlayerDataAsync(_currentLocalId, _currentIdToken);
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

            LocalSaveProvider.SaveAuthSession(authResp.email, _currentIdToken, _currentRefreshToken, _currentLocalId, rememberMe, usernameOrEmail, password);
            LocalSaveProvider.Save(CurrentPlayer);
            LocalSaveProvider.SetExplicitGuest(false);

            OnProfileUpdated?.Invoke(CurrentPlayer);
            if (!silent) OnAuthCompleted?.Invoke(true, null);
            return (true, null);
        }

        /// <summary>
        /// Cierra la sesión activa y regresa al modo invitado.
        /// </summary>
        public void Logout()
        {
            LocalSaveProvider.ClearAuthSession();
            _currentIdToken = "";
            _currentRefreshToken = "";
            _currentLocalId = "";

            PlayAsGuest("Invitado");
        }

        /// <summary>
        /// Guarda el estado actual en local y en la nube (si el usuario está autenticado).
        /// Si el token caduca durante la sesión de juego, se refresca automáticamente.
        /// </summary>
        public async Task SaveProfileAsync()
        {
            if (CurrentPlayer == null) return;

            // Siempre guardar en local primero (capa dual)
            LocalSaveProvider.Save(CurrentPlayer);

            // Sincronizar en la nube si tiene cuenta activa
            if (!CurrentPlayer.isGuest && !string.IsNullOrEmpty(_currentLocalId))
            {
                var (success, err, isExpired) = await _restClient.SavePlayerDataAsync(_currentLocalId, _currentIdToken, CurrentPlayer);
                if (!success && isExpired)
                {
                    Debug.Log("[CloudAuthManager] Token caducado al guardar perfil. Renovando automáticamente...");
                    bool renewed = await RefreshSessionTokenAsync();
                    if (renewed)
                    {
                        (success, err, _) = await _restClient.SavePlayerDataAsync(_currentLocalId, _currentIdToken, CurrentPlayer);
                    }
                }

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
