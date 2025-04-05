using System;
using System.Collections.Generic;
using System.Linq;
using Code.Cards;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace Code.GameLogic
{
    public class DeckCreator : NetworkBehaviour
    {
        private readonly string[] _cardType = { "Gold", "Cup", "Sword", "Cudgel" };
        private List<Card> _fullDeck = new List<Card>();

        private void Start()
        {
            CreateDeck();

            _fullDeck = _fullDeck.OrderBy(c => c.type).ThenBy(c => c.value).ToList();
        }

        private void CreateDeck()
        {
            foreach (var cardType in _cardType)
            {
                // Agregar cartas con números del 1 al 3
                int earlyRealValue = 8;
                for (int number = 1; number <= 3; number++)
                {
                    int assignedRealValue = (number == 1 && cardType == "Cudgel") ? 13 :
                        (number == 1 && cardType == "Sword") ? 14 :
                        earlyRealValue;

                    AddCard(cardType, number, assignedRealValue);
                    earlyRealValue++;
                }

                // Agregar cartas con números del 4 al 12 (omitiendo 8 y 9)
                int runningRealValue = 1;
                for (int number = 4; number <= 12; number++)
                {
                    if (number == 8 || number == 9)
                        continue;

                    int assignedRealValue = (number == 7 && cardType == "Gold") ? 11 :
                        (number == 7 && cardType == "Sword") ? 12 :
                        runningRealValue;

                    AddCard(cardType, number, assignedRealValue);
                    runningRealValue++;
                }
            }
        }

        private void AddCard(string cardType, int number, int realValue)
        {
            _fullDeck.Add(new Card { type = $"{cardType} #{number}", value = number, realValue = realValue });
        }


        private void ChangeRealCardValues()
        {
            CreateDeck();

            foreach (var card in _fullDeck)
            {
            }
        }

        private void OnMouseDown()
        {
            if (!NetworkClient.isConnected) return;

            var localPlayer = NetworkClient.localPlayer;
            if (localPlayer == null) return;

            var playerController = localPlayer.GetComponent<CardsHandler>();
            if (playerController == null) return;


            playerController.CmdDrawCard(_fullDeck);
        }
    }

    [Serializable]
    public class Card
    {
        public string type;
        public int value;
        public int realValue;
    }
}