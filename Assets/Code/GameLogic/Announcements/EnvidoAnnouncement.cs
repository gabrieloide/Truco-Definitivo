using System;
using System.Collections.Generic;
using Code.Player;
using Code.Cards;
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

        // UI handling is now managed by PlayerHUD and AnnouncementManager
        
        public override void UpdateTotalScore()
        {
            if (global::AnnounceState.Envido != global::AnnounceState.Envido) return; // Legacy check

            
            var deckCreator = DeckCreator.Instance;
            if (deckCreator == null) return;

            var vira = deckCreator.cardVira;
            int pointsToAward = IncreasingAmount()[acceptAmount];

            // Calculate scores for everyone
            int bestScoreTeam1 = -1;
            int bestScoreTeam2 = -1;
            
            // Get all players and NPCs
            var allPlayers = FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None);
            var allNpcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                int score = 0;
                var cardsHandler = p.GetComponent<CardsHandler>();
                if (cardsHandler == null) cardsHandler = p.GetComponentInChildren<CardsHandler>();
                
                if (cardsHandler != null)
                {
                    // Convert GameObjects to Card data
                    List<Card> hand = new List<Card>();
                    foreach(var cardObj in cardsHandler.Cards) {
                        if (cardObj == null) continue;
                        
                        var physical = cardObj.GetComponent<Code.Cards.PhysicalCard3D>();
                        if (physical == null) physical = cardObj.GetComponentInParent<Code.Cards.PhysicalCard3D>();
                        
                        if (physical != null)
                        {
                            hand.Add(new Card(physical.cardValue, physical.cardSuit));
                        }
                        else
                        {
                            var interaction = cardObj.GetComponent<Code.Cards.CardInteraction>();
                            if (interaction == null) interaction = cardObj.GetComponentInParent<Code.Cards.CardInteraction>();
                            if (interaction != null) hand.Add(interaction.Card);
                        }
                    }
                    score = TrucoRules.CalculateEnvidoScore(hand, vira);
                }

                // Si alguien tiene Flor, el Envido se anula automáticamente
                if (score == -1)
                {
                    return;
                }

                if (p.team != null && p.team.teamName == "Team 1") bestScoreTeam1 = Mathf.Max(bestScoreTeam1, score);
                else if (p.team != null && p.team.teamName == "Team 2") bestScoreTeam2 = Mathf.Max(bestScoreTeam2, score);
            }

            foreach (var npc in allNpcs)
            {
                int score = TrucoRules.CalculateEnvidoScore(npc.hand, vira);
                
                // Si alguien tiene Flor, el Envido se anula automáticamente
                if (score == -1) 
                {
                    return; 
                }

                if (npc.team != null && npc.team.teamName == "Team 1") bestScoreTeam1 = Mathf.Max(bestScoreTeam1, score);
                else if (npc.team != null && npc.team.teamName == "Team 2") bestScoreTeam2 = Mathf.Max(bestScoreTeam2, score);
            }


            string winnerTeam = "";
            if (bestScoreTeam1 > bestScoreTeam2) winnerTeam = "Team 1";
            else if (bestScoreTeam2 > bestScoreTeam1) winnerTeam = "Team 2";
            else
            {
                // Tie: Mano wins.
                int manoTeamIndex = GameManager.Instance.ManoTeamIndex; // 1 or 2
                winnerTeam = "Team " + manoTeamIndex;
            }

            // Notificar los puntajes comparados en la pantalla
            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.NotifyEvent($"ENVIDO: TEAM 1 ({bestScoreTeam1}) VS TEAM 2 ({bestScoreTeam2})", 4.0f);
            }

            GameManager.Instance.AddAnnouncementPoints(winnerTeam, pointsToAward);
        }

        Card[] TestingEnvido(PlayerLocal[] local)
        {
            Card[] allCards = new Card[6];
            int playerIndex = 0; 

            for (int i = 0; i < allCards.Length; i++)
            {
                playerIndex = i == 2 ? 0 : 1;
                // local might not have enough players depending on testing setup
                PlayerLocal owner = (local.Length > playerIndex) ? local[playerIndex] : null;
                
                allCards[i] = new Card();
                allCards[i].cardOwner = owner;
                if (owner != null) allCards[i].ownerObj = owner.gameObject;
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
                    if (card.cardOwner != null && card.cardOwner.player != null && card.cardOwner.player.playerName == currentPlayerName)
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
                    if (cardPlayer[i, j] != null && cardPlayer[i, j].cardOwner != null && cardPlayer[i, j].cardOwner.player != null)
                    {
                    }
                }
            }
        }
    }
}
