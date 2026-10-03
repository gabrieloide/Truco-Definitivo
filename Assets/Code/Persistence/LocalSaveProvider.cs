using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Code.Persistence
{
    /// <summary>
    /// Proveedor de persistencia local en dispositivo (PlayerPrefs).
    /// Funciona de forma 100% offline para jugadores invitados o como caché local rápida.
    /// </summary>
    public static class LocalSaveProvider
    {
        private const string PrefsKeyPlayerData = "Truco_Local_PlayerData";
        private const string PrefsKeyAuthEmail = "Truco_Auth_Email";
        private const string PrefsKeyAuthToken = "Truco_Auth_Token";
        private const string PrefsKeyAuthLocalId = "Truco_Auth_LocalId";
        private const string PrefsKeyIsGuest = "Truco_Auth_IsGuest";

        public static void Save(PlayerData data)
        {
            if (data == null) return;
            try
            {
                data.lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                PlayerPrefs.SetString(PrefsKeyPlayerData, json);
                PlayerPrefs.SetInt(PrefsKeyIsGuest, data.isGuest ? 1 : 0);
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
                if (PlayerPrefs.HasKey(PrefsKeyPlayerData))
                {
                    string json = PlayerPrefs.GetString(PrefsKeyPlayerData, "");
                    if (!string.IsNullOrEmpty(json))
                    {
                        var data = JsonConvert.DeserializeObject<PlayerData>(json);
                        if (data != null) return data;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocalSaveProvider] Error al cargar datos guardados, creando perfil base: {ex.Message}");
            }

            // Perfil por defecto (Invitado)
            string savedNick = PlayerPrefs.GetString("playerNickname", "Gaucho");
            var defaultGuest = new PlayerData(Guid.NewGuid().ToString(), savedNick, guest: true);
            Save(defaultGuest);
            return defaultGuest;
        }

        public static void SaveAuthSession(string email, string idToken, string localId)
        {
            PlayerPrefs.SetString(PrefsKeyAuthEmail, email ?? "");
            PlayerPrefs.SetString(PrefsKeyAuthToken, idToken ?? "");
            PlayerPrefs.SetString(PrefsKeyAuthLocalId, localId ?? "");
            PlayerPrefs.SetInt(PrefsKeyIsGuest, 0);
            PlayerPrefs.Save();
        }

        public static bool TryGetAuthSession(out string email, out string idToken, out string localId)
        {
            email = PlayerPrefs.GetString(PrefsKeyAuthEmail, "");
            idToken = PlayerPrefs.GetString(PrefsKeyAuthToken, "");
            localId = PlayerPrefs.GetString(PrefsKeyAuthLocalId, "");

            return !string.IsNullOrEmpty(idToken) && !string.IsNullOrEmpty(localId);
        }

        public static void ClearAuthSession()
        {
            PlayerPrefs.DeleteKey(PrefsKeyAuthEmail);
            PlayerPrefs.DeleteKey(PrefsKeyAuthToken);
            PlayerPrefs.DeleteKey(PrefsKeyAuthLocalId);
            PlayerPrefs.SetInt(PrefsKeyIsGuest, 1);
            PlayerPrefs.Save();
        }

        public static bool IsGuestPreferred()
        {
            return PlayerPrefs.GetInt(PrefsKeyIsGuest, 1) == 1;
        }

        public static void SetGuestPreferred(bool isGuest)
        {
            PlayerPrefs.SetInt(PrefsKeyIsGuest, isGuest ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
