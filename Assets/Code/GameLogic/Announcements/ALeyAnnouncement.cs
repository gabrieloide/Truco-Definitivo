using System;
using Code.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class ALeyAnnouncement : Announce
    {
        public override GameObject AnnounceButton() => null; // Handled by PlayerHUD and UI Toolkit
        protected override AnnounceState AnnounceState() => global::AnnounceState.ALey;

        public override int[] IncreasingAmount() => new[] { 0, 0 }; // A Ley doesn't add or subtract points

        public override void UpdateTotalScore()
        {
            // No score increase for A Ley per user rules
        }
    }
}
