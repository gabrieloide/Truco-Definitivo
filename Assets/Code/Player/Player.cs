using System;
using UnityEngine;

namespace Code.Player
{
    public class Player : MonoBehaviour
    {
        public string playerName;
        public bool canPlayCard = false;
        public Team team;
        public bool haveFlower;
        // Cantó envido teniendo flor: la flor queda "quemada" y no puntúa esta mano.
        public bool florBurned;
    }
    [Serializable]
    public class Team
    {
        public string teamName;
        public int teamScore;
        public int roundsWon;
         public Team (string name)
        {
            teamName = name;
        }
    }
}