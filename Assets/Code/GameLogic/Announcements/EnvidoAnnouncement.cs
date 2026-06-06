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
        public override int[] IncreasingAmount() => new[] { 1, 2, 4, 5, 7, 12 };

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
                    score = TrucoRules.CalculateEnvidoScore(cardsHandler.InitialHand, vira);
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
                int score = TrucoRules.CalculateEnvidoScore(npc.initialHand, vira);
                
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

            // GUARDAR EL RESULTADO PENDIENTE EN EL GAMEMANAGER EN LUGAR DE RESOLVERLO AHORA
            GameManager.Instance.pendingEnvidoResolution = true;
            GameManager.Instance.pendingEnvidoWinnerTeam = winnerTeam;
            GameManager.Instance.pendingEnvidoPoints = pointsToAward;
            GameManager.Instance.pendingEnvidoScoreTeam1 = bestScoreTeam1;
            GameManager.Instance.pendingEnvidoScoreTeam2 = bestScoreTeam2;
            
            // Opcional: Podríamos mostrar un mensaje muy sutil de "Envido Aceptado" sin revelar los puntos aún
            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.NotifyEvent("ENVIDO ACEPTADO (Se resuelve al final de la mano)", 2.0f);
            }
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
