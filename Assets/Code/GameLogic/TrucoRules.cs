using System;
using System.Collections.Generic;
using Code.GameLogic;
using UnityEngine;

namespace Code.GameLogic
{
    public static class TrucoRules
    {
        // Suits: "Gold", "Cup", "Sword", "Cudgel"

        public static int GetCardRealValue(Card card, Card vira)
        {
            // Determine if the card is a special piece (Perico / Perica)
            bool isViraSuit = (card.suit == vira.suit);
            
            // Adjust Perico/Perica values if the vira itself is an 11 or 10
            int pericoValueTarget = (vira.value == 11) ? 12 : 11;
            int pericaValueTarget = (vira.value == 10) ? 12 : 10;

            if (isViraSuit && card.value == pericoValueTarget) return 100; // Perico (Highest)
            if (isViraSuit && card.value == pericaValueTarget) return 99;  // Perica (Second Highest)

            // Espadilla (1 de Espadas)
            if (card.suit == "Sword" && card.value == 1) return 20;
            // Bastillo (1 de Bastos)
            if (card.suit == "Cudgel" && card.value == 1) return 19;
            // 7 de Espadas
            if (card.suit == "Sword" && card.value == 7) return 18;
            // 7 de Oros
            if (card.suit == "Gold" && card.value == 7) return 17;

            // Resto de las cartas en orden
            if (card.value == 3) return 16;
            if (card.value == 2) return 15;
            
            // Ases Falsos (Copas y Oros)
            if (card.value == 1) return 14;

            // Figuras
            if (card.value == 12) return 13;
            if (card.value == 11) return 12; // Caballos normales
            if (card.value == 10) return 11; // Sotas normales

            // 7 Falsos (Copas y Bastos)
            if (card.value == 7) return 10;

            // Cartas bajas
            if (card.value == 6) return 9;
            if (card.value == 5) return 8;
            if (card.value == 4) return 7;

            return 0; // Default
        }

        public static int CalculateEnvidoScore(List<Card> hand, Card vira)
        {
            int maxScore = 0;

            // Check if player has Perico or Perica
            bool hasPerico = false;
            bool hasPerica = false;
            int pericoTarget = (vira.value == 11) ? 12 : 11;
            int pericaTarget = (vira.value == 10) ? 12 : 10;

            foreach (var card in hand)
            {
                if (card.suit == vira.suit && card.value == pericoTarget) hasPerico = true;
                if (card.suit == vira.suit && card.value == pericaTarget) hasPerica = true;
            }

            // If Flor (Perico and Perica), envido score might not apply, but usually it's considered 59 points
            if (hasPerico && hasPerica) return 59;

            // Calculate combinations of 2 cards of the same suit
            for (int i = 0; i < hand.Count; i++)
            {
                for (int j = i + 1; j < hand.Count; j++)
                {
                    Card c1 = hand[i];
                    Card c2 = hand[j];

                    int score = 0;

                    bool c1IsPerico = (c1.suit == vira.suit && c1.value == pericoTarget);
                    bool c1IsPerica = (c1.suit == vira.suit && c1.value == pericaTarget);
                    bool c2IsPerico = (c2.suit == vira.suit && c2.value == pericoTarget);
                    bool c2IsPerica = (c2.suit == vira.suit && c2.value == pericaTarget);

                    // If they are the same suit OR one is a piece
                    if (c1.suit == c2.suit || c1IsPerico || c1IsPerica || c2IsPerico || c2IsPerica)
                    {
                        int val1 = GetEnvidoValue(c1, c1IsPerico, c1IsPerica);
                        int val2 = GetEnvidoValue(c2, c2IsPerico, c2IsPerica);

                        if (c1IsPerico || c1IsPerica || c2IsPerico || c2IsPerica)
                        {
                            // If one is a piece, and the other is ANY card, wait, 
                            // pieces only sum with their own suit in Venezuelan Truco.
                            // If they are not the same suit, the piece just plays alone? No, Perico + different suit is just the Perico value (30).
                            if (c1.suit == c2.suit)
                            {
                                score = val1 + val2; // Piece + same suit card (e.g. Perico + 7 = 30 + 7 = 37)
                            }
                            else
                            {
                                // Just the piece alone
                                score = Mathf.Max(val1, val2); 
                            }
                        }
                        else
                        {
                            // Normal cards of the same suit
                            score = 20 + val1 + val2;
                        }
                    }
                    else
                    {
                        // Different suits, no pieces. Score is just the highest card value.
                        score = Mathf.Max(GetEnvidoValue(c1, false, false), GetEnvidoValue(c2, false, false));
                    }

                    if (score > maxScore) maxScore = score;
                }
            }

            // Also check single card values in case they have no matches
            foreach (var card in hand)
            {
                bool isPerico = (card.suit == vira.suit && card.value == pericoTarget);
                bool isPerica = (card.suit == vira.suit && card.value == pericaTarget);
                int score = GetEnvidoValue(card, isPerico, isPerica);
                if (score > maxScore) maxScore = score;
            }

            return maxScore;
        }

        private static int GetEnvidoValue(Card card, bool isPerico, bool isPerica)
        {
            if (isPerico) return 30;
            if (isPerica) return 29;
            if (card.value >= 10 && card.value <= 12) return 0; // Figuras valen 0
            return card.value;
        }
    }
}
