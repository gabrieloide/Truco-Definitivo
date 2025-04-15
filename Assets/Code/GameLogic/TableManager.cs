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

            if (CardsInTable == null || CardsInTable.Count == 0)
            {
                Debug.Log("There is no enough cards on the table to evaluate");
                return;
            }

            highestCard = CardsInTable[0];
            lowestCard = CardsInTable[0];

            foreach (var card in CardsInTable)
            {
                if (card.realValue > highestCard.realValue)
                {
                    highestCard = card;
                }

                if (card.realValue < lowestCard.realValue)
                {
                    lowestCard = card;
                }
            }

            RpcHighestCard(highestCard, lowestCard);
            CardsInTable.Clear();
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
            
            FindAnyObjectByType<ScoreManager>().IncreaseScore(highestCard.cardOwner.player.team.teamName);
            Debug.Log(
                $"Highest Card {highestCard.cardOwner.player.team.teamName} Score: {highestCard.cardOwner.player.team.teamScore}");
            PlayerHUD.Instance.ChangeScoreText();
        }
    }
}