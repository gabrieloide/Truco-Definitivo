using Mirror;
using UnityEngine;

namespace Code.GameLogic.Announcement
{
    public abstract class Announce : NetworkBehaviour
    {
        public string announceName;
        public abstract void IncreaseTotalScore();
    }
}