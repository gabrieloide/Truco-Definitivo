using System.Collections.Generic;
using UnityEngine;

namespace Code.Cards
{
    [CreateAssetMenu(fileName = "CardDatabase", menuName = "Truco/Card Database")]
    public class CardDatabase : ScriptableObject
    {
        [SerializeField] private List<CardData> _cards = new List<CardData>();
        
        // Un diccionario interno para búsquedas rápidas por ID O(1)
        private Dictionary<int, CardData> _cardDict;

        public void Initialize()
        {
            _cardDict = new Dictionary<int, CardData>();
            foreach (var card in _cards)
            {
                if (card != null && !_cardDict.ContainsKey(card.id))
                {
                    _cardDict.Add(card.id, card);
                }
            }
        }

        public CardData GetCardById(int id)
        {
            if (_cardDict == null || _cardDict.Count != _cards.Count)
            {
                Initialize();
            }

            if (_cardDict != null && _cardDict.TryGetValue(id, out CardData cardData))
            {
                return cardData;
            }

            Debug.LogError($"[CardDatabase] No se encontró ninguna carta con ID {id}.");
            return null;
        }

        public List<CardData> GetAllCards()
        {
            return new List<CardData>(_cards);
        }

        // Método de utilidad para añadir cartas desde scripts del editor
        public void AddCard(CardData card)
        {
            if (!_cards.Contains(card))
            {
                _cards.Add(card);
            }
        }
        
        public void ClearCards()
        {
            _cards.Clear();
        }
    }
}
