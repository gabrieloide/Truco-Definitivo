using System;
using UnityEngine;

namespace Code.Persistence
{
    [Serializable]
    public class MatchSessionData
    {
        public string lobbyCode;
        public string playerId;
        public string playerName;
        public bool isHost;
        public long timestampUtc;
    }

    /// <summary>
    /// Rastrea la sesión de partida multijugador activa en PlayerPrefs.
    /// Permite a los clientes reconectarse si se desconectan accidentalmente (recarga de página, micro-corte de red).
    /// </summary>
    public static class MatchSessionTracker
    {
        private const string PrefsKeyMatchSession = "Truco_ActiveMatch_Session";
        private const double SessionMaxAgeSeconds = 15 * 60; // 15 minutos de vigencia máxima

        public static void SaveSession(string lobbyCode, string playerId, string playerName, bool isHost)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode)) return;

            var data = new MatchSessionData
            {
                lobbyCode = lobbyCode.Trim().ToUpper(),
                playerId = playerId ?? "",
                playerName = playerName ?? "Jugador",
                isHost = isHost,
                timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(PrefsKeyMatchSession, json);
            PlayerPrefs.Save();
            Debug.Log($"[MatchSessionTracker] Sesión guardada para sala {data.lobbyCode} (Host: {isHost})");
        }

        public static bool HasActiveSession(out MatchSessionData session)
        {
            session = null;
            if (!PlayerPrefs.HasKey(PrefsKeyMatchSession)) return false;

            try
            {
                string json = PlayerPrefs.GetString(PrefsKeyMatchSession, "");
                if (string.IsNullOrEmpty(json)) return false;

                var data = JsonUtility.FromJson<MatchSessionData>(json);
                if (data == null || string.IsNullOrWhiteSpace(data.lobbyCode)) return false;

                // El Host no se reconecta a sí mismo porque al cerrarse el host el servidor deja de existir
                if (data.isHost)
                {
                    ClearSession();
                    return false;
                }

                // Verificar que no sea una sesión vieja de días anteriores
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now - data.timestampUtc > SessionMaxAgeSeconds)
                {
                    Debug.Log($"[MatchSessionTracker] Sesión expirada para sala {data.lobbyCode}");
                    ClearSession();
                    return false;
                }

                session = data;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MatchSessionTracker] Error al leer sesión guardada: {ex.Message}");
                ClearSession();
                return false;
            }
        }

        public static void ClearSession()
        {
            if (PlayerPrefs.HasKey(PrefsKeyMatchSession))
            {
                PlayerPrefs.DeleteKey(PrefsKeyMatchSession);
                PlayerPrefs.Save();
                Debug.Log("[MatchSessionTracker] Sesión activa limpiada.");
            }
        }
    }
}
