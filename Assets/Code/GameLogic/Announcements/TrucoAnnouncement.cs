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


        protected override int[] IncreasingAmount() => new[] { 1, 3, 6, 9, 12 };
        private void Start()
        {
            var announcementManager = FindAnyObjectByType<AnnouncementManager>();

            AnnounceButton().GetComponent<Button>().onClick
                .AddListener(() => announcementManager.SendAnnounceToClient("TrucoButton"));
        }

        //1 - 3 - 6 - 9 - all the game

        public override void UpdateTotalScore()
        {
            Debug.Log("Truco");
            ScoreManager.instance.amountToIncrease = IncreasingAmount()[acceptAmount];
        }
    }
}