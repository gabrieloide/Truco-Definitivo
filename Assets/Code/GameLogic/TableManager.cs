using System.Collections.Generic;
using Code.Player;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.GameLogic
{
    public class TableManager : NetworkBehaviour
    {
        public readonly SyncList<Card> CardsInTable = new SyncList<Card>();
        public static TableManager Instance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [Server]
        public void DetermineHighestCard()
        {
            Card highestCard = null;
            Card lowestCard = null;

            // Guard clause for empty table
            if (CardsInTable == null || CardsInTable.Count == 0)
            {
                Debug.Log("No cards in table to evaluate");
                return;
            }

            // Initialize with the first card
            highestCard = CardsInTable[0];
            lowestCard = CardsInTable[0];

            foreach (var card in CardsInTable)
            {
                // Debug current card being evaluated
                Debug.Log($"Evaluating Card: Value={card.realValue}, Suit={card.suit}, " +
                          $"Owner={card.cardOwner}, Team={card.cardOwner.player.team.teamName}");

                // Check for highest card
                if (card.realValue > highestCard.realValue)
                {
                    highestCard = card;
                }

                // Check for lowest card
                if (card.realValue < lowestCard.realValue)
                {
                    lowestCard = card;
                }
            }

            RpcHighestCard(highestCard, lowestCard);
        }


        [ClientRpc]
        private void RpcHighestCard(Card highestCard, Card lowestCard)
        {
            if (highestCard == null)
                return;

            Debug.Log("=== Final Results ===");
            Debug.Log($"Highest Card: Value={highestCard.realValue}, Suit={highestCard.suit}, " +
                      $"Owner={highestCard.cardOwner}, Team={highestCard.cardOwner.player.team.teamName}");
            Debug.Log($"Lowest Card: Value={lowestCard.realValue}, Suit={lowestCard.suit}, " +
                      $"Owner={lowestCard.cardOwner}, Team={lowestCard.cardOwner.player.team.teamName}");

            
            
            Debug.Log("=== ALL CURRENT CARDS ON THE TABLE ===");
            foreach (var card in CardsInTable)
            {
                Debug.Log($"Card: {card.value} of {card.suit}");
            }
            
            highestCard.cardOwner.player.team.teamScore++;
            PlayerHUD.Instance.ChangeScoreText();
        }
    }
}