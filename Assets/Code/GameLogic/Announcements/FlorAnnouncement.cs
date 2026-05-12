using System;
using System.Collections.Generic;
using System.Linq;
using Code.Player;
using Code.Cards;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class FlorAnnouncement : Announce
    {
        public override GameObject AnnounceButton() => null; // Handled by PlayerHUD and UI Toolkit
        protected override AnnounceState AnnounceState() => global::AnnounceState.Flor;

        protected override int[] IncreasingAmount() => new[] { 3, 3, 3 }; 

        // UI handling is now managed by PlayerHUD and AnnouncementManager

        public void CanDeclareFlower()
        {
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            if (playerLocal == null) return;

            var cardsHandler = playerLocal.cardsHandler;

            if (cardsHandler.Cards == null || cardsHandler.Cards.Count < 3)
            {
                playerLocal.player.haveFlower = false;
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.RefreshActionButtons(playerLocal.player.canPlayCard);
                return;
            }

            var deckCreator = FindAnyObjectByType<DeckCreator>();
            if (deckCreator == null) return;

            // Get cards from CardsHandler
            List<Card> hand = new List<Card>();
            foreach(var cardObj in cardsHandler.Cards)
            {
                if (cardObj == null) continue;
                var physical = cardObj.GetComponent<Code.Cards.PhysicalCard3D>();
                if (physical != null) hand.Add(new Card(physical.cardValue, physical.cardSuit));
            }

            bool isFlor = TrucoRules.IsFlor(hand, deckCreator.cardVira);
            playerLocal.player.haveFlower = isFlor;

            if (isFlor) Debug.Log($"[Flor] El jugador tiene FLOR.");

            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.RefreshActionButtons(playerLocal.player.canPlayCard);
            }
        }


        public override void UpdateTotalScore()
        {
            // Flor is typically accepted automatically or results in immediate points
            // in some Venezuelan Truco variants when called.
            
            // For now, let's find the team that called it.
            var announcementManager = FindAnyObjectByType<AnnouncementManager>();
            if (announcementManager == null) return;

            // El equipo que cantó la flor gana 3 puntos (o 5 si es reservada)
            // Para simplificar, buscaremos quién cantó recientemente o quién tiene la propiedad
            
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            if (playerLocal != null && playerLocal.player != null && playerLocal.player.haveFlower)
            {
                int points = 3; // Default
                var deckCreator = FindAnyObjectByType<DeckCreator>();
                if (deckCreator != null)
                {
                    List<Card> hand = new List<Card>();
                    foreach(var cardObj in playerLocal.cardsHandler.Cards)
                    {
                        if (cardObj == null) continue;
                        var physical = cardObj.GetComponent<Code.Cards.PhysicalCard3D>();
                        if (physical != null) hand.Add(new Card(physical.cardValue, physical.cardSuit));
                    }
                    points = TrucoRules.CalculateFlorPoints(hand, deckCreator.cardVira);
                }
                
                GameManager.Instance.AddAnnouncementPoints(playerLocal.player.team.teamName, points);
                return;
            }

            // Check NPCs
            var npcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);
            foreach(var npc in npcs)
            {
                var deckCreator = FindAnyObjectByType<DeckCreator>();
                if (deckCreator != null && TrucoRules.IsFlor(npc.hand, deckCreator.cardVira))
                {
                    int points = TrucoRules.CalculateFlorPoints(npc.hand, deckCreator.cardVira);
                    GameManager.Instance.AddAnnouncementPoints(npc.team.teamName, points);
                    return;
                }
            }
        }
    }
}