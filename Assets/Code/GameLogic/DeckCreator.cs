using System;
using System.Collections.Generic;
using System.Linq;
using Code.Player;
// using Mirror;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.GameLogic
{
    public class DeckCreator : MonoBehaviour
    {
        private readonly string[] _cardSuit = { "Gold", "Cup", "Sword", "Cudgel" };
        public List<Card> _fullDeck = new List<Card>();
        /*[SyncVar]*/ public Card cardVira;
        
        [SerializeField] private Code.Cards.CardDatabase _cardDatabase;

        private void Start()
        {
            if (_cardDatabase == null)
            {
                _cardDatabase = Resources.Load<Code.Cards.CardDatabase>("CardDatabase");
                if (_cardDatabase == null) Debug.LogError("[DeckCreator] No se encontró CardDatabase en Resources!");
                else _cardDatabase.Initialize();
            }

            CreateDeck();
        }

        // [Server]
        private void CreateDeck()
        {
            _fullDeck.Clear();
            foreach (var cardType in _cardSuit)
            {
                for (var number = 1; number <= 12; number++)
                {
                    if (number == 8 || number == 9)
                        continue;

                    // Encontrar el ID correspondiente en la base de datos
                    int cardId = -1;
                    if (_cardDatabase != null)
                    {
                        var data = _cardDatabase.GetAllCards().FirstOrDefault(c => c.suit.ToString() == cardType && c.value == number);
                        if (data != null) cardId = data.id;
                    }

                    _fullDeck.Add(new Card { suit = cardType, value = number, dbId = cardId });
                }
            }
        }

        // [Server]
        public void ShuffleAndSetVira()
        {
            CreateDeck(); // Refresh deck
            
            // Shuffle
            _fullDeck = _fullDeck.OrderBy(x => Random.value).ToList();

            // Pick Vira
            int viraIndex = Random.Range(0, _fullDeck.Count);
            cardVira = _fullDeck[viraIndex];
            _fullDeck.RemoveAt(viraIndex);

            // Update real values based on the new Vira
            foreach (var card in _fullDeck)
            {
                card.realValue = TrucoRules.GetCardRealValue(card, cardVira);
            }
            
            // Also update the vira's real value just in case
            cardVira.realValue = TrucoRules.GetCardRealValue(cardVira, cardVira);

            Debug.Log($"[SERVER] Vira selected: {cardVira.value} of {cardVira.suit}");
        }

        // [Server]
        public List<Card> DealCards(int count)
        {
            List<Card> dealtCards = new List<Card>();
            for (int i = 0; i < count; i++)
            {
                if (_fullDeck.Count > 0)
                {
                    Card c = _fullDeck[0];
                    _fullDeck.RemoveAt(0);
                    dealtCards.Add(c);
                }
            }
            return dealtCards;
        }
    }

    [Serializable]
    public class Card
    {
        public PlayerLocal cardOwner;
        public GameObject ownerObj;
        public string suit;
        public int value;
        public int realValue;
        public int dbId; // Referencia al ID del ScriptableObject en la base de datos

        public Card() { }

        public Card(int v, string s, PlayerLocal owner = null, GameObject ownerObj = null)
        {
            value = v;
            suit = s;
            cardOwner = owner;
            this.ownerObj = ownerObj ?? (owner != null ? owner.gameObject : null);
        }
        
        // Formatted display name, e.g., "Gold #7"
        public string GetDisplayName()
        {
            return $"{suit} #{value}";
        }
    }
}