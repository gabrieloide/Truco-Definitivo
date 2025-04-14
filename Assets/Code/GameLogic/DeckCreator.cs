using System;
using System.Collections.Generic;
using System.Linq;
using Code.Cards;
using Code.Player;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace Code.GameLogic
{
    public class DeckCreator : NetworkBehaviour
    {
        private readonly string[] _cardSuit = { "Gold", "Cup", "Sword", "Cudgel" };
        public List<Card> _fullDeck = new List<Card>();
        public Card cardVira;

        private void Start()
        {
            CreateDeck();
        }

        private void CreateDeck()
        {
            foreach (var cardType in _cardSuit)
            {
                var earlyRealValue = 8;
                for (var number = 1; number <= 3; number++)
                {
                    var assignedRealValue = (number == 1 && cardType == "Cudgel") ? 13 :
                        (number == 1 && cardType == "Sword") ? 14 :
                        earlyRealValue;

                    AddCard(cardType, number, assignedRealValue);
                    earlyRealValue++;
                }

                var runningRealValue = 1;
                for (var number = 4; number <= 12; number++)
                {
                    if (number == 8 || number == 9)
                        continue;

                    var assignedRealValue = (number == 7 && cardType == "Gold") ? 11 :
                        (number == 7 && cardType == "Sword") ? 12 :
                        runningRealValue;

                    AddCard(cardType, number, assignedRealValue);
                    runningRealValue++;
                }
            }

            _fullDeck = _fullDeck.OrderBy(c => c.suit).ThenBy(c => c.value).ToList();
        }

        private void AddCard(string cardType, int number, int realValue)
        {
            if (number > 9)
                _fullDeck.Add(new CardPerico
                {
                    suit = $"{cardType} #{number}", value = number, realValue = realValue,
                    IsPerico = cardType == cardVira.suit && number == 11,
                    IsPerica = cardType == cardVira.suit && number == 10
                });

            else
                _fullDeck.Add(new Card { suit = $"{cardType} #{number}", value = number, realValue = realValue });
        }

        private void OnMouseDown()
        {
            if (GameManager.Instance.deckIsLocked)
            {
                Debug.Log("You can't draw any card");
                return;
            }

            if (!NetworkClient.isConnected) return;

            var localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null) return;

            var cardsHandler = localPlayer.GetComponent<CardsHandler>();

            if (cardsHandler == null) return;

            cardsHandler.CmdDrawCard(_fullDeck);
            localPlayer.GetComponent<AnnouncementSystem>().CanDeclareFlower();
        }
    }

    [Serializable]
    public class Card
    {
        public PlayerLocal cardOwner;
        public string suit;
        public int value;
        public int realValue;
    }

    public class CardPerico : Card
    {
        public bool IsPerico;
        public bool IsPerica;
    }
}