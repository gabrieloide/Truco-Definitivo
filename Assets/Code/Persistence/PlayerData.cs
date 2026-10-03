using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Code.Persistence
{
    [Serializable]
    public class PlayerData
    {
        [JsonProperty("userId")]
        public string userId = "";

        [JsonProperty("username")]
        public string username = "Gaucho";

        [JsonProperty("email")]
        public string email = "";

        [JsonProperty("isGuest")]
        public bool isGuest = true;

        [JsonProperty("level")]
        public int level = 1;

        [JsonProperty("experience")]
        public int experience = 0;

        [JsonProperty("coins")]
        public int coins = 1000;

        [JsonProperty("gamesPlayed")]
        public int gamesPlayed = 0;

        [JsonProperty("gamesWon")]
        public int gamesWon = 0;

        [JsonProperty("gamesLost")]
        public int gamesLost = 0;

        [JsonProperty("highestEnvido")]
        public int highestEnvido = 0;

        [JsonProperty("lastSavedUtc")]
        public long lastSavedUtc = 0;

        [JsonIgnore]
        public float WinRate => gamesPlayed > 0 ? ((float)gamesWon / gamesPlayed) * 100f : 0f;

        [JsonIgnore]
        public int ExperienceForNextLevel => level * 100;

        public PlayerData()
        {
            userId = System.Guid.NewGuid().ToString();
            lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public PlayerData(string id, string name, bool guest = true)
        {
            userId = id;
            username = string.IsNullOrWhiteSpace(name) ? "Gaucho" : name.Trim();
            isGuest = guest;
            lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Agrega el resultado de una partida, calcula experiencia, subidas de nivel y monedas ganadas.
        /// </summary>
        public void RecordMatch(bool won, int envidoScore = 0)
        {
            gamesPlayed++;
            if (won)
            {
                gamesWon++;
                coins += 150;
                AddExperience(100);
            }
            else
            {
                gamesLost++;
                coins += 35;
                AddExperience(30);
            }

            if (envidoScore > highestEnvido)
            {
                highestEnvido = envidoScore;
            }

            lastSavedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public void AddExperience(int amount)
        {
            experience += amount;
            while (experience >= ExperienceForNextLevel)
            {
                experience -= ExperienceForNextLevel;
                level++;
                coins += 100; // Bono por subir de nivel
                Debug.Log($"[PlayerData] ¡Subida de nivel! Nivel actual: {level}");
            }
        }
    }
}
