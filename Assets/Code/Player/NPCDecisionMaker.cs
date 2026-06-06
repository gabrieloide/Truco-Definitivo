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
                case "Flor":
                    return true;
                case "ALey":
                    return TrucoRules.IsFlor(hand, vira);
                default:
                    return Random.value > 0.5f; // Random fallback
            }
        }

        public static Card SelectCardToPlay(List<Card> hand, List<Card> cardsOnTable, Card vira)
        {
            if (hand == null || hand.Count == 0) return null;
            if (hand.Count == 1) return hand[0];

            // Aseguramos que el valor real de cada carta de la mano esté actualizado
            foreach (var card in hand)
            {
                card.realValue = TrucoRules.GetCardRealValue(card, vira);
            }

            // Ordenar de menor a mayor valor real
            var sortedHand = new List<Card>(hand);
            sortedHand.Sort((a, b) => a.realValue.CompareTo(b.realValue));

            // Si somos los primeros en jugar en esta baza, jugamos una carta mediana
            if (cardsOnTable == null || cardsOnTable.Count == 0)
            {
                int middleIndex = sortedHand.Count / 2;
                return sortedHand[middleIndex];
            }

            // Encontrar el valor real más alto actualmente en la mesa
            int highestTableValue = -1;
            foreach (var tableCard in cardsOnTable)
            {
                int val = TrucoRules.GetCardRealValue(tableCard, vira);
                if (val > highestTableValue) highestTableValue = val;
            }

            // Encontrar la menor carta en nuestra mano que pueda ganarle al valor de la mesa
            foreach (var card in sortedHand)
            {
                if (card.realValue > highestTableValue)
                {
                    return card; // Retornamos la menor carta ganadora
                }
            }

            // Si no podemos ganarle a la carta más alta en la mesa, jugamos nuestra peor carta (descarte)
            return sortedHand[0];
        }
    }
}
