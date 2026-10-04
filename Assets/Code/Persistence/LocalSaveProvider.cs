using System;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Code.Persistence
{
    /// <summary>
    /// Proveedor de persistencia local en dos capas (PlayerPrefs + Almacenamiento Secundario Persistente).
    /// En WebGL utiliza window.localStorage como respaldo secundario contra limpieza de caché/IndexedDB.
    /// En Desktop/Editor utiliza archivos locales en persistentDataPath.
    /// Garantiza que la sesión de usuario y las credenciales recordadas se conserven siempre.
    /// </summary>
    public static class LocalSaveProvider
    {
        private const string PrefsKeyPlayerData = "Truco_Local_PlayerData";
        private const string PrefsKeyAuthEmail = "Truco_Auth_Email";
        private const string PrefsKeyAuthToken = "Truco_Auth_Token";
        private const string PrefsKeyAuthRefreshToken = "Truco_Auth_RefreshToken";
        private const string PrefsKeyAuthLocalId = "Truco_Auth_LocalId";
        private const string PrefsKeyIsGuest = "Truco_Auth_IsGuest";
        private const string PrefsKeyExplicitGuest = "Truco_Auth_ExplicitGuest";
        private const string PrefsKeyRememberMe = "Truco_Auth_RememberMe";
        private const string PrefsKeySavedUser = "Truco_Auth_SavedUser";
        private const string PrefsKeySavedPass = "Truco_Auth_SavedPass";

        private const string ObfuscationSalt = "TrucoDefinitivoSessionKey2026";

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WebGLStorage_SetItem(string key, string val);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern string WebGLStorage_GetItem(string key);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void WebGLStorage_RemoveItem(string key);
#endif

        #region Respaldo Secundario Multicapa

        private static void WriteSecondary(string key, string value)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                WebGLStorage_SetItem(key, value ?? "");
#else
                string dir = Application.persistentDataPath;
                if (!string.IsNullOrEmpty(dir))
                {
                    string path = System.IO.Path.Combine(dir, $"{key}.json");
                    System.IO.File.WriteAllText(path, value ?? "");
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalSaveProvider] Error al escribir en respaldo secundario: {ex.Message}");
            }
        }

        private static string ReadSecondary(string key)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return WebGLStorage_GetItem(key);
#else
                string dir = Application.persistentDataPath;
                if (!string.IsNullOrEmpty(dir))
                {
                    string path = System.IO.Path.Combine(dir, $"{key}.json");
                    if (System.IO.File.Exists(path))
                    {
                        return System.IO.File.ReadAllText(path);
                    }
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalSaveProvider] Error al leer de respaldo secundario: {ex.Message}");
            }
            return null;
        }

        private static void DeleteSecondary(string key)
        {
            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                WebGLStorage_RemoveItem(key);
#else
                string dir = Application.persistentDataPath;
                if (!string.IsNullOrEmpty(dir))
                {
                    string path = System.IO.Path.Combine(dir, $"{key}.json");
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                }
#endif
            }
            catch { }
        }

        private static void SetMultiLayerString(string key, string value)
        {
            PlayerPrefs.SetString(key, value ?? "");
            WriteSecondary(key, value);
        }

        private static string GetMultiLayerString(string key, string fallback = "")
        {
            if (PlayerPrefs.HasKey(key))
            {
                string val = PlayerPrefs.GetString(key, "");
                if (!string.IsNullOrEmpty(val))
                {
                    return val;
                }
            }

            // Fallback a almacenamiento secundario persistente (localStorage o archivo en disco)
            string secondary = ReadSecondary(key);
            if (!string.IsNullOrEmpty(secondary))
            {
                PlayerPrefs.SetString(key, secondary);
                PlayerPrefs.Save();
                return secondary;
            }

            return fallback;
        }

        private static void DeleteMultiLayerKey(string key)
        {
            PlayerPrefs.DeleteKey(key);
            DeleteSecondary(key);
        }

        #endregion

        #region Guardado y Carga de PlayerData

        public static void Save(PlayerData data)
        {
            if (data == null) return;
            try
            {
                data.lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                SetMultiLayerString(PrefsKeyPlayerData, json);
                SetMultiLayerString(PrefsKeyIsGuest, data.isGuest ? "1" : "0");
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalSaveProvider] Error al guardar datos locales: {ex.Message}");
            }
        }

        public static PlayerData Load()
        {
            try
            {
                string json = GetMultiLayerString(PrefsKeyPlayerData, "");
                if (!string.IsNullOrEmpty(json))
                {
                    var data = JsonConvert.DeserializeObject<PlayerData>(json);
                    if (data != null) return data;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalSaveProvider] Error al cargar datos guardados, creando perfil base: {ex.Message}");
            }

            // Si hay sesión activa pero se borró el json del perfil, restaurar un perfil vinculado a esa cuenta
            if (TryGetAuthSession(out string email, out _, out _, out string localId))
            {
                string name = !string.IsNullOrEmpty(email) ? email.Split('@')[0] : "Jugador";
                var restoredPlayer = new PlayerData(localId, name, guest: false) { email = email };
                Save(restoredPlayer);
                return restoredPlayer;
            }

            // Perfil por defecto (Invitado)
            string savedNick = PlayerPrefs.GetString("playerNickname", "Gaucho");
            var defaultGuest = new PlayerData(Guid.NewGuid().ToString(), savedNick, guest: true);
            Save(defaultGuest);
            return defaultGuest;
        }

        #endregion

        #region Sesión y Credenciales

        public static void SaveAuthSession(string email, string idToken, string refreshToken, string localId, bool rememberMe = true, string savedUser = null, string savedPass = null)
        {
            SetMultiLayerString(PrefsKeyAuthEmail, email ?? "");
            SetMultiLayerString(PrefsKeyAuthToken, idToken ?? "");
            SetMultiLayerString(PrefsKeyAuthRefreshToken, refreshToken ?? "");
            SetMultiLayerString(PrefsKeyAuthLocalId, localId ?? "");
            SetMultiLayerString(PrefsKeyIsGuest, "0");
            SetMultiLayerString(PrefsKeyExplicitGuest, "0");
            SetMultiLayerString(PrefsKeyRememberMe, rememberMe ? "1" : "0");

            if (rememberMe && !string.IsNullOrEmpty(savedUser))
            {
                SetMultiLayerString(PrefsKeySavedUser, savedUser);
                if (!string.IsNullOrEmpty(savedPass))
                {
                    SetMultiLayerString(PrefsKeySavedPass, Obfuscate(savedPass));
                }
            }

            PlayerPrefs.Save();
        }

        public static void SaveAuthSession(string email, string idToken, string localId)
        {
            SaveAuthSession(email, idToken, "", localId, true);
        }

        public static bool TryGetAuthSession(out string email, out string idToken, out string refreshToken, out string localId)
        {
            email = GetMultiLayerString(PrefsKeyAuthEmail, "");
            idToken = GetMultiLayerString(PrefsKeyAuthToken, "");
            refreshToken = GetMultiLayerString(PrefsKeyAuthRefreshToken, "");
            localId = GetMultiLayerString(PrefsKeyAuthLocalId, "");

            // La sesión es válida si tenemos localId y (idToken o refreshToken)
            return !string.IsNullOrEmpty(localId) && (!string.IsNullOrEmpty(idToken) || !string.IsNullOrEmpty(refreshToken));
        }

        public static bool TryGetAuthSession(out string email, out string idToken, out string localId)
        {
            return TryGetAuthSession(out email, out idToken, out _, out localId);
        }

        public static void ClearAuthSession()
        {
            DeleteMultiLayerKey(PrefsKeyAuthEmail);
            DeleteMultiLayerKey(PrefsKeyAuthToken);
            DeleteMultiLayerKey(PrefsKeyAuthRefreshToken);
            DeleteMultiLayerKey(PrefsKeyAuthLocalId);
            DeleteMultiLayerKey(PrefsKeySavedPass);
            SetMultiLayerString(PrefsKeyIsGuest, "1");
            SetMultiLayerString(PrefsKeyExplicitGuest, "1");
            PlayerPrefs.Save();
        }

        public static bool GetRememberedCredentials(out string username, out string password)
        {
            username = GetMultiLayerString(PrefsKeySavedUser, "");
            string obfPass = GetMultiLayerString(PrefsKeySavedPass, "");
            password = Deobfuscate(obfPass);

            bool isRemember = GetMultiLayerString(PrefsKeyRememberMe, "1") == "1";
            return isRemember && !string.IsNullOrEmpty(username);
        }

        public static void SaveRememberedCredentials(string username, string password)
        {
            SetMultiLayerString(PrefsKeySavedUser, username ?? "");
            SetMultiLayerString(PrefsKeySavedPass, Obfuscate(password ?? ""));
            SetMultiLayerString(PrefsKeyRememberMe, "1");
            PlayerPrefs.Save();
        }

        public static bool IsRememberMeEnabled()
        {
            return GetMultiLayerString(PrefsKeyRememberMe, "1") == "1";
        }

        public static void SetRememberMeEnabled(bool enabled)
        {
            SetMultiLayerString(PrefsKeyRememberMe, enabled ? "1" : "0");
            PlayerPrefs.Save();
        }

        public static bool IsExplicitGuest()
        {
            return GetMultiLayerString(PrefsKeyExplicitGuest, "0") == "1";
        }

        public static void SetExplicitGuest(bool isGuest)
        {
            SetMultiLayerString(PrefsKeyExplicitGuest, isGuest ? "1" : "0");
            SetMultiLayerString(PrefsKeyIsGuest, isGuest ? "1" : "0");
            PlayerPrefs.Save();
        }

        public static bool IsGuestPreferred()
        {
            // Si el jugador tiene sesión guardada, NO prefiere invitado
            if (TryGetAuthSession(out _, out _, out _, out _))
            {
                return false;
            }
            return GetMultiLayerString(PrefsKeyIsGuest, "1") == "1";
        }

        public static void SetGuestPreferred(bool isGuest)
        {
            SetMultiLayerString(PrefsKeyIsGuest, isGuest ? "1" : "0");
            PlayerPrefs.Save();
        }

        #endregion

        #region Ofuscación de Credenciales

        private static string Obfuscate(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            try
            {
                byte[] key = Encoding.UTF8.GetBytes(ObfuscationSalt);
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= key[i % key.Length];
                }
                return Convert.ToBase64String(bytes);
            }
            catch
            {
                return input;
            }
        }

        private static string Deobfuscate(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            try
            {
                byte[] key = Encoding.UTF8.GetBytes(ObfuscationSalt);
                byte[] bytes = Convert.FromBase64String(input);
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= key[i % key.Length];
                }
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }

        #endregion
    }
}
