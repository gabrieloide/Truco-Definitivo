using System;
using System.Collections.Generic;
using System.Linq;
using Code.Cards;
using Code.Player;
using Mirror;
using UnityEngine;

namespace Code.GameLogic
{
    public class DeckCreator : NetworkBehaviour
    {
        private readonly string[] _cardType = { "Gold", "Cup", "Sword", "Cudgel" };
        private List<Card> _fullDeck = new List<Card>();

        private void Start()
        {
            CreateDeck();
            System.IO.File.AppendAllText($"log.txt", $"Jugador A es el primero\n {GameManager.Instance.currentPlayerTurn}");

        }

        private void CreateDeck()
        {
            foreach (var cardType in _cardType)
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

            _fullDeck = _fullDeck.OrderBy(c => c.type).ThenBy(c => c.value).ToList();
        }

        private void AddCard(string cardType, int number, int realValue)
        {
            _fullDeck.Add(new Card { type = $"{cardType} #{number}", value = number, realValue = realValue });
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
        public PlayerLocal cardOwner;
        public string type;
        public int value;
        public int realValue;
    }
}