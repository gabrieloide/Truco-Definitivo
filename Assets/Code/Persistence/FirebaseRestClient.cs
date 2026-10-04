using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Persistence
{
    /// <summary>
    /// Cliente RESTful para autenticación y base de datos en la nube.
    /// Funciona 100% de forma desacoplada mediante HTTP (UnityWebRequest),
    /// compatible con WebGL, Android, iOS y Desktop sin requerir SDKs nativos.
    /// Soporta Firebase Auth + Realtime Database REST API, o cualquier REST backend compatible.
    /// </summary>
    public class FirebaseRestClient
    {
        private readonly string _apiKey;
        private readonly string _databaseUrl; // ej: "https://truco-definitivo-default-rtdb.firebaseio.com"

        public FirebaseRestClient(string apiKey, string databaseUrl)
        {
            _apiKey = apiKey;
            _databaseUrl = databaseUrl?.TrimEnd('/');
        }

        #region DTOs
        [Serializable]
        private class AuthRequest
        {
            public string email;
            public string password;
            public bool returnSecureToken = true;
        }

        [Serializable]
        public class AuthResponse
        {
            public string idToken;
            public string email;
            public string refreshToken;
            public string expiresIn;
            public string localId;
        }

        [Serializable]
        public class RefreshTokenResponse
        {
            [JsonProperty("expires_in")]
            public string expires_in;

            [JsonProperty("token_type")]
            public string token_type;

            [JsonProperty("refresh_token")]
            public string refresh_token;

            [JsonProperty("id_token")]
            public string id_token;

            [JsonProperty("user_id")]
            public string user_id;

            [JsonProperty("project_id")]
            public string project_id;
        }

        [Serializable]
        private class ErrorContainer
        {
            public ErrorDetails error;
        }

        [Serializable]
        private class ErrorDetails
        {
            public int code;
            public string message;
        }
        #endregion

        /// <summary>
        /// Registra un nuevo usuario con email/usuario y contraseña.
        /// </summary>
        public async Task<(bool success, AuthResponse response, string errorMessage)> SignUpAsync(string identifier, string password)
        {
            string email = NormalizeToEmail(identifier);
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_apiKey}";

            var reqBody = new AuthRequest { email = email, password = password };
            return await SendAuthPostRequest(url, reqBody);
        }

        /// <summary>
        /// Inicia sesión con email/usuario y contraseña existentes.
        /// </summary>
        public async Task<(bool success, AuthResponse response, string errorMessage)> SignInAsync(string identifier, string password)
        {
            string email = NormalizeToEmail(identifier);
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";

            var reqBody = new AuthRequest { email = email, password = password };
            return await SendAuthPostRequest(url, reqBody);
        }

        /// <summary>
        /// Renueva el token de autenticación de Firebase usando el refreshToken de larga duración.
        /// Permite mantener la sesión activa indefinidamente sin pedir reingreso de credenciales.
        /// </summary>
        public async Task<(bool success, RefreshTokenResponse response, string errorMessage)> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return (false, null, "Refresh token no disponible.");
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                return (false, null, "API Key de Firebase no configurada.");
            }

            string url = $"https://securetoken.googleapis.com/v1/token?key={_apiKey}";
            string formData = $"grant_type=refresh_token&refresh_token={UnityWebRequest.EscapeURL(refreshToken)}";
            byte[] bodyRaw = Encoding.UTF8.GetBytes(formData);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");

                await SendRequestAsync(request);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string respJson = request.downloadHandler?.text;
                    var refreshResp = JsonConvert.DeserializeObject<RefreshTokenResponse>(respJson);
                    return (true, refreshResp, null);
                }
                else
                {
                    string rawErr = request.downloadHandler?.text;
                    string translated = TranslateAuthError(rawErr, request.error);
                    return (false, null, translated);
                }
            }
        }

        /// <summary>
        /// Guarda el perfil del jugador en la base de datos REST.
        /// </summary>
        public async Task<(bool success, string errorMessage, bool isAuthExpired)> SavePlayerDataAsync(string localId, string idToken, PlayerData data)
        {
            if (string.IsNullOrEmpty(_databaseUrl))
            {
                return (false, "URL de base de datos no configurada.", false);
            }

            string url = $"{_databaseUrl}/users/{localId}.json?auth={idToken}";
            string jsonBody = JsonConvert.SerializeObject(data);

            using (UnityWebRequest request = UnityWebRequest.Put(url, jsonBody))
            {
                request.SetRequestHeader("Content-Type", "application/json");
                await SendRequestAsync(request);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return (true, null, false);
                }
                else
                {
                    string err = ParseErrorMessage(request.downloadHandler?.text, request.error);
                    bool isAuthExpired = request.responseCode == 401 ||
                                         (err != null && (err.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                          err.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0));
                    return (false, err, isAuthExpired);
                }
            }
        }

        /// <summary>
        /// Carga el perfil del jugador desde la base de datos REST.
        /// </summary>
        public async Task<(bool success, PlayerData data, string errorMessage, bool isAuthExpired)> LoadPlayerDataAsync(string localId, string idToken)
        {
            if (string.IsNullOrEmpty(_databaseUrl))
            {
                return (false, null, "URL de base de datos no configurada.", false);
            }

            string url = $"{_databaseUrl}/users/{localId}.json?auth={idToken}";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                await SendRequestAsync(request);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string json = request.downloadHandler?.text;
                    if (string.IsNullOrEmpty(json) || json == "null")
                    {
                        return (true, null, null, false); // Sin datos previos guardados
                    }

                    try
                    {
                        var data = JsonConvert.DeserializeObject<PlayerData>(json);
                        return (true, data, null, false);
                    }
                    catch (Exception ex)
                    {
                        return (false, null, $"Error al deserializar perfil: {ex.Message}", false);
                    }
                }
                else
                {
                    string err = ParseErrorMessage(request.downloadHandler?.text, request.error);
                    bool isAuthExpired = request.responseCode == 401 ||
                                         (err != null && (err.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                          err.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0));
                    return (false, null, err, isAuthExpired);
                }
            }
        }

        private async Task<(bool success, AuthResponse response, string errorMessage)> SendAuthPostRequest(string url, AuthRequest reqBody)
        {
            string json = JsonConvert.SerializeObject(reqBody);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                await SendRequestAsync(request);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string respJson = request.downloadHandler?.text;
                    var authResp = JsonConvert.DeserializeObject<AuthResponse>(respJson);
                    return (true, authResp, null);
                }
                else
                {
                    string rawErr = request.downloadHandler?.text;
                    string translated = TranslateAuthError(rawErr, request.error);
                    return (false, null, translated);
                }
            }
        }

        private static async Task SendRequestAsync(UnityWebRequest request)
        {
            var op = request.SendWebRequest();
            while (!op.isDone)
            {
                await Task.Yield();
            }
        }

        /// <summary>
        /// Permite que el usuario ingrese un nombre simple (ej: 'Pedro') y lo convierte
        /// en un identificador válido para Firebase Auth ('pedro@truco.app').
        /// Si ya tiene formato de email, lo deja intacto.
        /// </summary>
        private static string NormalizeToEmail(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return "";
            string clean = identifier.Trim().ToLowerInvariant();
            if (clean.Contains("@") && clean.Contains("."))
            {
                return clean;
            }
            return $"{clean}@trucodefinitivo.app";
        }

        private static string ParseErrorMessage(string json, string fallback)
        {
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var container = JsonConvert.DeserializeObject<ErrorContainer>(json);
                    if (container?.error != null && !string.IsNullOrEmpty(container.error.message))
                    {
                        return container.error.message;
                    }
                }
                catch { }
            }
            return fallback ?? "Error de conexión con el servidor.";
        }

        private static string TranslateAuthError(string json, string fallback)
        {
            string msg = ParseErrorMessage(json, fallback);
            if (string.IsNullOrEmpty(msg)) return "Error desconocido al autenticar.";

            if (msg.Contains("EMAIL_EXISTS")) return "El nombre de usuario o email ya está en uso.";
            if (msg.Contains("EMAIL_NOT_FOUND") || msg.Contains("INVALID_LOGIN_CREDENTIALS") || msg.Contains("INVALID_PASSWORD"))
                return "Usuario o contraseña incorrectos.";
            if (msg.Contains("WEAK_PASSWORD")) return "La contraseña debe tener al menos 6 caracteres.";
            if (msg.Contains("TOO_MANY_ATTEMPTS_TRY_LATER")) return "Demasiados intentos fallidos. Intentá más tarde.";
            if (msg.Contains("INVALID_EMAIL")) return "Formato de nombre o email inválido.";

            return msg;
        }
    }
}
