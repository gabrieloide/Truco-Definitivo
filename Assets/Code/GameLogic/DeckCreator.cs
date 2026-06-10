using System;
using System.Collections.Generic;
using System.Linq;
using Code.Player;
using Mirror;
using UnityEngine;
using Code.Scripts.Audio;
using Random = UnityEngine.Random;

namespace Code.GameLogic
{
    public class DeckCreator : MonoBehaviour
    {
        public static DeckCreator Instance { get; private set; }

        private readonly string[] _cardSuit = { "Gold", "Cup", "Sword", "Cudgel" };
        public List<Card> _fullDeck = new List<Card>();
        /*[SyncVar]*/ public Card cardVira;
        
        [SerializeField] private Code.Cards.CardDatabase _cardDatabase;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (_cardDatabase == null)
            {
                _cardDatabase = Resources.Load<Code.Cards.CardDatabase>("CardDatabase");
                if (_cardDatabase == null) Debug.LogError("[DeckCreator] No se encontró CardDatabase en Resources!");
                else _cardDatabase.Initialize();
            }

            // DO NOT call CreateDeck() here because DealingState handles ShuffleAndSetVira()
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

            // Check for duplicates in _fullDeck
            var dupes = _fullDeck.GroupBy(c => new {c.suit, c.value}).Where(g => g.Count() > 1).ToList();
            if (dupes.Count > 0) {
                Debug.LogError($"[DeckCreator] CRITICAL BUG! _fullDeck has duplicates AFTER CreateDeck: {string.Join(", ", dupes.Select(d => $"{d.Key.value} of {d.Key.suit}"))}");
            } else {
                Debug.Log($"[DeckCreator] CreateDeck finished perfectly. Deck size: {_fullDeck.Count}");
            }
        }

        // [Server]
        public void ShuffleAndSetVira()
        {
            // Clients don't shuffle — server is authoritative
            if (NetworkClient.active && !NetworkServer.active) return;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("card_shuffle_rattle");
            }
            CreateDeck(); // Refresh deck
            
            // Shuffle
            var oldState = Random.state;
            _fullDeck = _fullDeck.OrderBy(x => System.Guid.NewGuid()).ToList();
            
            Debug.Log($"[DeckCreator] Deck shuffled. First 5 cards: {_fullDeck[0].GetDisplayName()}, {_fullDeck[1].GetDisplayName()}, {_fullDeck[2].GetDisplayName()}, {_fullDeck[3].GetDisplayName()}, {_fullDeck[4].GetDisplayName()}");
            
            // Check for duplicates AGAIN after shuffle
            var dupes = _fullDeck.GroupBy(c => new {c.suit, c.value}).Where(g => g.Count() > 1).ToList();
            if (dupes.Count > 0) Debug.LogError("[DeckCreator] CRITICAL BUG! Duplicates found AFTER shuffle!");

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
        public bool isBurned = false;

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
