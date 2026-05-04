using Mirror;
using UnityEngine;

namespace Code.GameLogic.Announcement
{
    public abstract class Announce : NetworkBehaviour
    {
        public abstract GameObject AnnounceButton();
        protected abstract AnnounceState AnnounceState();
        protected abstract int[] IncreasingAmount(); 

        [SyncVar]public int acceptAmount;

        public abstract void UpdateTotalScore();
        public void IncreaseAcceptAmount()
        {
            acceptAmount++;
        }
    }
}