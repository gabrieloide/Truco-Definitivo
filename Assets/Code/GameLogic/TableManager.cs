using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.GameLogic
{
    public class TableManager : MonoBehaviour
    {
        public List<Card> cardsInTable = new List<Card>();
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

        public void DetermineHighestCard(bool lastCardPlayed)
        {
            if (!lastCardPlayed)
                return;
            Card highestCard = null;
            
            foreach (var card in cardsInTable)
            {
                if (highestCard == null || card.realValue > highestCard.realValue)
                {
                    highestCard = card;
                }
            }

            if (highestCard != null) Debug.Log("Highest card: " + highestCard.cardOwner.player.playerName);
            
        }
    }
}