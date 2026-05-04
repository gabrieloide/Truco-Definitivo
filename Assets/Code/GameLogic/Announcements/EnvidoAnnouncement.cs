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
        public override GameObject AnnounceButton() => GameObject.Find("EnvidoButton");
        protected override AnnounceState AnnounceState() => global::AnnounceState.Envido;
        protected override int[] IncreasingAmount() => new[] { 1, 2, 4, 5, 7, 12 };


        private void Start()
        {
            var announcementManager = FindAnyObjectByType<AnnouncementManager>();

            AnnounceButton().GetComponent<Button>().onClick
                .AddListener(() => announcementManager.SendAnnounceToClient("EnvidoButton"));

            GetHighestEnvido();
        }

        //1 - 2 - 4 - 5 - 7 - all the game Jesica sodieJuliana tovar 


        public override void UpdateTotalScore()
        {
            if (AnnounceState() != global::AnnounceState.Envido) return;

            Debug.Log("Envido");
            ScoreManager.instance.amountToIncrease = IncreasingAmount()[acceptAmount];
        }

        Card[] TestingEnvido(PlayerLocal[] local)
        {
            Card[] allCards = new Card[6];
            int playerIndex = 0; 

            for (int i = 0; i < allCards.Length; i++)
            {
                playerIndex = i == 2 ? 0 : 1;
                allCards[i].cardOwner = local[playerIndex];
                allCards[i].suit = "Gold";
                allCards[i].value = i + 1;
            }
            
            return  allCards;

        }
        public void GetHighestEnvido()
        {
            
            var announcementManager = FindAnyObjectByType<AnnouncementManager>();
            var cardPlayer = new Card[3, 3];
            var cardsInTable = TestingEnvido(GameManager.Instance.serverPlayers.ToArray());

            for (var i = 0; i < announcementManager.announcementPlayerNames.Length; i++)
            {
                var currentPlayerName = announcementManager.announcementPlayerNames[i];
                var cardIndex = 0;

                foreach (var card in cardsInTable)
                {
                    if (card.cardOwner.player.playerName == currentPlayerName)
                    {
                        if (cardIndex < 3)
                        {
                            cardPlayer[i, cardIndex] = card;
                            cardIndex++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            
            for (int i = 0; i < cardPlayer.GetLength(0); i++)
            {
                for (int j = 0; j < cardPlayer.GetLength(1); j++)
                {
                    Debug.Log(cardPlayer[i, j].cardOwner.player.playerName + " " + cardPlayer[i, j].suit + " " + cardPlayer[i, j].value);
                }
            }
        }
    }
}