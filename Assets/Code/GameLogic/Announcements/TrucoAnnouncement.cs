using System;
using Code.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class TrucoAnnouncement : Announce
    {
        private GameObject _trucoButton;

        private void Start()
        {
            _trucoButton = GameObject.Find("TrucoButton");
            
            var announcementManager = FindAnyObjectByType<AnnouncementManager>();
            
            _trucoButton.GetComponent<Button>().onClick
                .AddListener(() => announcementManager.SendAnnounceToClient("TrucoButton"));
        }

        public override void IncreaseTotalScore()
        {
            throw new System.NotImplementedException();
        }
    }
}