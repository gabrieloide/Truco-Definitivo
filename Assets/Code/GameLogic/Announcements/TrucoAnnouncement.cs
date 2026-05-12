using System;
using Code.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class TrucoAnnouncement : Announce
    {
        public override GameObject AnnounceButton() => GameObject.Find("TrucoButton");

        protected override AnnounceState AnnounceState() => global::AnnounceState.Truco;


        // 1 (No cantado) - 3 (Truco) - 6 (Retruco) - 9 (Vale 9) - 30 (Vale Partida)
        protected override int[] IncreasingAmount() => new[] { 1, 3, 6, 9, 30 };
        
        // UI handling is now managed by PlayerHUD and AnnouncementManager

        //1 - 3 - 6 - 9 - Vale Partida

        public override void UpdateTotalScore()
        {
            string stateName = acceptAmount switch
            {
                1 => "Truco",
                2 => "Retruco",
                3 => "Vale 9",
                4 => "Vale Partida",
                _ => "Truco"
            };

            Debug.Log($"[TrucoAnnouncement] ¡{stateName} ACEPTADO!");
            int points = IncreasingAmount()[acceptAmount];
            GameManager.Instance.currentHandValue = points;
        }
    }
}