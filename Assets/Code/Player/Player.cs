using System;
using UnityEngine;

namespace Code.Player
{
    public class Player : MonoBehaviour
    {
        public string playerName;
        public Team team;
        public bool haveFlower;
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