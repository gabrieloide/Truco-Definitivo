using System.Collections.Generic;
using Code.Cards;
using Code.GameLogic;
using UnityEngine;

namespace Code.Player
{
    public static class NPCDecisionMaker
    {
        public static bool ShouldAnnounceFlor(List<Card> hand, Card vira)
        {
            return TrucoRules.IsFlor(hand, vira);
        }

        public static bool ShouldAnnounceEnvido(List<Card> hand, Card vira, int threshold = 28)
        {
            int score = TrucoRules.CalculateEnvidoScore(hand, vira);
            if (score == -1) return false; // Is Flor
            return score >= threshold;
        }

        public static bool ShouldAnnounceTruco(List<Card> hand, Card vira, int threshold = 18)
        {
            // Simple heuristic: if any card is high value (e.g., > 18)
            foreach (var card in hand)
            {
                if (TrucoRules.GetCardRealValue(card, vira) >= threshold) return true;
            }
            return false;
        }

        public static bool ShouldAcceptAnnounce(string announceType, List<Card> hand, Card vira)
        {
            switch (announceType)
            {
                case "Envido":
                    int score = TrucoRules.CalculateEnvidoScore(hand, vira);
                    if (score == -1) return true; // Accept Envido by calling Flor (handled by NPC logic)
                    return score >= 25;
                case "Truco":
                    // If we have at least one card better than a normal 3 (value 16)
                    foreach (var card in hand)
                    {
                        if (TrucoRules.GetCardRealValue(card, vira) >= 16) return true;
                    }
                    return false;
                default:
                    return Random.value > 0.5f; // Random fallback
            }
        }
    }
}
