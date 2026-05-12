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
            if (hand == null || hand.Count < 3) return 0;

            // Check if player has Flor (3 of same suit OR 2 pieces OR 1 piece + 2 same suit)
            // Note: In most rules, if you have Flor, Envido is invalid.
            if (IsFlor(hand, vira)) return -1; 

            int maxScore = 0;
            int pericoTarget = (vira.value == 11) ? 12 : 11;
            int pericaTarget = (vira.value == 10) ? 12 : 10;

            Card perico = null;
            Card perica = null;
            List<Card> normalCards = new List<Card>();

            foreach (var card in hand)
            {
                if (card.suit == vira.suit && card.value == pericoTarget) perico = card;
                else if (card.suit == vira.suit && card.value == pericaTarget) perica = card;
                else normalCards.Add(card);
            }

            // Case 1: Perico alone
            if (perico != null && perica == null)
            {
                // Piece + highest normal card
                int highestNormal = 0;
                foreach(var c in normalCards) highestNormal = Mathf.Max(highestNormal, GetEnvidoValue(c, false, false));
                maxScore = 30 + highestNormal;
            }
            // Case 2: Perica alone
            else if (perica != null && perico == null)
            {
                int highestNormal = 0;
                foreach(var c in normalCards) highestNormal = Mathf.Max(highestNormal, GetEnvidoValue(c, false, false));
                maxScore = 29 + highestNormal;
            }
            // Case 3: No pieces
            else if (perico == null && perica == null)
            {
                // Traditional combinations of 2 cards same suit
                for (int i = 0; i < hand.Count; i++)
                {
                    for (int j = i + 1; j < hand.Count; j++)
                    {
                        if (hand[i].suit == hand[j].suit)
                        {
                            int score = 20 + GetEnvidoValue(hand[i], false, false) + GetEnvidoValue(hand[j], false, false);
                            if (score > maxScore) maxScore = score;
                        }
                    }
                }
                
                // If no combinations, just the highest card
                if (maxScore == 0)
                {
                    foreach (var c in hand) maxScore = Mathf.Max(maxScore, GetEnvidoValue(c, false, false));
                }
            }

            return maxScore;
        }

        public static int CalculateFlorPoints(List<Card> hand, Card vira)
        {
            if (!IsFlor(hand, vira)) return 0;

            int pericoTarget = (vira.value == 11) ? 12 : 11;
            int pericaTarget = (vira.value == 10) ? 12 : 10;

            bool hasPerico = false;
            bool hasPerica = false;

            foreach (var card in hand)
            {
                if (card.suit == vira.suit && card.value == pericoTarget) hasPerico = true;
                if (card.suit == vira.suit && card.value == pericaTarget) hasPerica = true;
            }

            // Flor Reservada (Perico + Perica + Any card) = 5 points
            if (hasPerico && hasPerica) return 5;

            // Normal Flor = 3 points
            return 3;
        }

        public static bool IsFlor(List<Card> hand, Card vira)
        {
            if (hand == null || hand.Count < 3) return false;

            int pericoTarget = (vira.value == 11) ? 12 : 11;
            int pericaTarget = (vira.value == 10) ? 12 : 10;

            int piecesCount = 0;
            string firstNormalSuit = "";
            int sameSuitCount = 0;

            foreach (var card in hand)
            {
                bool isPiece = (card.suit == vira.suit && (card.value == pericoTarget || card.value == pericaTarget));
                if (isPiece) piecesCount++;
            }

            // 2 pieces always make a Flor (Perico + Perica + anything)
            if (piecesCount >= 2) return true;

            // 1 piece + 2 cards of same suit
            if (piecesCount == 1)
            {
                List<Card> normals = new List<Card>();
                foreach (var card in hand)
                {
                    bool isPiece = (card.suit == vira.suit && (card.value == pericoTarget || card.value == pericaTarget));
                    if (!isPiece) normals.Add(card);
                }
                return normals[0].suit == normals[1].suit;
            }

            // 0 pieces: 3 cards of same suit
            return (hand[0].suit == hand[1].suit && hand[1].suit == hand[2].suit);
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
