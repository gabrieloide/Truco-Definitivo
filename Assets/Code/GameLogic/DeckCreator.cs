using System.Collections.Generic;
using UnityEngine;

namespace Code.GameLogic
{
    public class DeckCreator : MonoBehaviour
    {
        private readonly string[] _cardType = { "Hearts", "Diamonds", "Clubs", "Spades" };
        public List<Card> FullDeck = new List<Card>();

        private void Start()
        {
            CreateDeck();
            foreach (var card in FullDeck)
            {
                Debug.Log(card.Type + " " + card.Value);
            }
        }

        private void CreateDeck()
        {
            foreach (var t in _cardType)
            {
                for (var j = 1; j < 13; j++)
                {
                    if (j is 8 || j == 9)
                        continue;

                    FullDeck.Add(new Card { Type = t, Value = j });
                }
            }
        }

        public void DrawCard()
        {
            var card = GetCard();
            Debug.Log(card.Type + " " + card.Value);
        }

        public Card GetCard()
        {
            var random = Random.Range(0, FullDeck.Count);
            var card = FullDeck[random];
            FullDeck.RemoveAt(random);
            return card;
        }
    }

    public class Card
    {
        public string Type;
        public int Value;
    }
}