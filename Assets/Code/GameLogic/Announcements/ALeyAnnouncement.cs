using System;
using Code.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class ALeyAnnouncement : Announce
    {
        public override GameObject AnnounceButton() => GameObject.Find("ALeyButton");
        protected override AnnounceState AnnounceState() => global::AnnounceState.ALey;

        protected override int[] IncreasingAmount() => throw new NotImplementedException();

        private void Start()
        {
            var announcementManager = FindAnyObjectByType<AnnouncementManager>();
            AnnounceButton().GetComponent<Button>().onClick
                .AddListener(() => announcementManager.SendAnnounceToClient("ALeyButton"));
        }


        public override void UpdateTotalScore()
        {
            throw new NotImplementedException();
        }
    }
}