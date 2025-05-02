using System;
using Code.Player;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class EnvidoAnnouncement : Announce
    {
        public GameObject announceEnvidoButton;
        public GameObject[] envidoButtons = new GameObject[2];

        [SyncVar] public int envidoTurn = 0;

        private void Start()
        {
            announceEnvidoButton = GameObject.Find("EnvidoButton");
            string[] buttonsName = { "AcceptEnvidoButton", "DeclineEnvidoButton" };
            var announcementManager = FindAnyObjectByType<AnnouncementManager>();

            for (var i = 0; i < buttonsName.Length; i++)
            {
                Debug.Log(i);
                var button = buttonsName[i];

                envidoButtons[i] = GameObject.Find(buttonsName[i]);
                envidoButtons[i].GetComponent<Button>().onClick
                    .AddListener(() => announcementManager.LocalButtonInteract(button));
                envidoButtons[i].SetActive(false);
                Debug.Log(envidoButtons[i].GetComponent<Button>().name);
            }

            announceEnvidoButton.GetComponent<Button>().onClick
                .AddListener(() => announcementManager.SendAnnounceToClient("EnvidoButton"));
        }

        public override void IncreaseTotalScore()
        {
        }
    }
}