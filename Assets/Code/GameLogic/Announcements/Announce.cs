// using Mirror;
using UnityEngine;

namespace Code.GameLogic.Announcement
{
    public abstract class Announce : MonoBehaviour
    {
        public abstract GameObject AnnounceButton();
        protected abstract AnnounceState AnnounceState();
        public abstract int[] IncreasingAmount(); 

        /*[SyncVar]*/public int acceptAmount;

        public abstract void UpdateTotalScore();

        public virtual void ResetAnnounceState()
        {
            acceptAmount = 0;
        }

        public void IncreaseAcceptAmount()
        {
            acceptAmount++;
        }
    }
}