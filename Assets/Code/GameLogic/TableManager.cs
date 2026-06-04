using System;
using System.Collections.Generic;
using Code.Cards;
using DG.Tweening;
// using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.GameLogic
{
    public class TableManager : MonoBehaviour
    {
        public static event Action<Card, GameObject> OnCardPlaced;
        public static event Action OnTrickEvaluated;

        public readonly List<Card> CardsInTable = new List<Card>();
        public static TableManager Instance;

        [Header("3D Card Spawning")]
        public GameObject card3DPrefab;


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

        [Header("3D Card Transforms")]
        [Header("3D Card Transforms")]
        public Transform viraPosition;
        public GameObject deckVisualPrefab; // Prefab for the stack of cards
        private GameObject _currentViraObj;
        private GameObject _currentDeckObj;
        public List<Transform> tableCardPositions; // 4 positions, one for each seat
        
        // Track how many cards each player has played on their spot
        private Dictionary<GameObject, int> _cardsPerPlayer = new Dictionary<GameObject, int>();
        private List<GameObject> _spawnedCards = new List<GameObject>();

        // [Server]
        public void SpawnVira3D(Card card, int dealerSeatIndex = -1)
        {
            if (card3DPrefab == null) return;
            
            // Clean up old vira/deck if they exist
            if (_currentViraObj != null) Destroy(_currentViraObj);
            if (_currentDeckObj != null) Destroy(_currentDeckObj);

            // Determinar ancla base desde las posiciones de la mesa
            Transform anchor = viraPosition;
            
            // Si tenemos un índice de repartidor, intentamos usar su posición en la mesa como referencia
            if (dealerSeatIndex != -1 && tableCardPositions != null && tableCardPositions.Count > dealerSeatIndex)
            {
                anchor = tableCardPositions[dealerSeatIndex];
            }
            
            if (anchor == null)
            {
                Debug.LogWarning("[TableManager] No anchor found for Vira. Using TableManager transform.");
                anchor = this.transform;
            }

            Vector3 basePos = anchor.position;
            Quaternion baseRot = anchor.rotation;

            // Offset para el Mazo: A la derecha y un poco atrás de donde el jugador pone su carta
            // Usamos 'right' y '-forward' relativo al ancla de la mesa
            Vector3 deckOffset = (anchor.right * 0.25f) + (anchor.forward * -0.1f);
            Vector3 deckPos = basePos + deckOffset;

            // 1. Spawn the Deck Visual
            if (deckVisualPrefab != null)
            {
                Quaternion deckRot = baseRot * Quaternion.Euler(0, UnityEngine.Random.Range(-5f, 5f), 0);
                _currentDeckObj = Instantiate(deckVisualPrefab, deckPos, deckRot);
            }

            // 2. Spawn the Vira card next to the deck (un poco más al centro)
            Vector3 viraPos = deckPos + (anchor.forward * 0.15f);
            
            // Apply a 90-degree rotation on X so the card lays flat on the table (face up)
            Quaternion flatRot = baseRot * Quaternion.Euler(90f, 0f, 0f);
            _currentViraObj = Instantiate(card3DPrefab, viraPos, flatRot);
            
            var physicalCard = _currentViraObj.GetComponent<Code.Cards.PhysicalCard3D>();
            if (physicalCard != null)
            {
                physicalCard.SetupCard(card.value, card.suit);
            }
            
            Debug.Log($"[TableManager] Vira and Deck spawned near Seat {dealerSeatIndex} using offset from tableCardPositions.");
        }

        // [Server]
        public void SpawnCard3D(Card card, GameObject player)
        {
            if (card3DPrefab == null)
            {
                Debug.LogWarning("Card 3D Prefab is not assigned in TableManager.");
                return;
            }

            // Determine how many cards this player has already played in this trick
            if (!_cardsPerPlayer.ContainsKey(player))
            {
                _cardsPerPlayer.Add(player, 0);
            }

            int cardsCount = _cardsPerPlayer[player];
            _cardsPerPlayer[player]++;

            // Find which seat this player is occupying
            int seatIndex = SeatManager.Instance.GetPlayerSeatIndex(player);
            if (seatIndex == -1)
            {
                Debug.LogWarning($"[TableManager] El jugador {player.name} no está sentado. Usando posición por defecto (0).");
                seatIndex = 0;
            }

            // Get base position for this seat
            Transform basePos = (tableCardPositions != null && tableCardPositions.Count > seatIndex) 
                                ? tableCardPositions[seatIndex] 
                                : transform;

            // Stack upwards: base position + height offset per card
            float heightOffset = cardsCount * 0.025f; // Slight height increase for stacking
            Vector3 targetPos = basePos.position + Vector3.up * heightOffset;
            
            // Apply a 90-degree rotation on X so the card lays flat on the table (face up)
            Quaternion targetRot = basePos.rotation * Quaternion.Euler(90f, 0f, 0f);
            
            // Start from player position for animation
            Vector3 startPos = player.transform.position + Vector3.up * 1.5f;
            
            GameObject cardObj = Instantiate(card3DPrefab, startPos, player.transform.rotation);
            
            var physicalCard = cardObj.GetComponent<Code.Cards.PhysicalCard3D>();
            if (physicalCard != null)
            {
                physicalCard.SetupCard(card.value, card.suit);
            }
            
            // Animate card to table
            cardObj.transform.DOMove(targetPos, 0.6f).SetEase(DG.Tweening.Ease.OutQuart);
            cardObj.transform.DORotateQuaternion(targetRot, 0.6f).SetEase(DG.Tweening.Ease.OutQuart);
            
            _spawnedCards.Add(cardObj);
        }

        public void ClearTable()
        {
            // Destroy physical cards
            foreach (var cardObj in _spawnedCards)
            {
                if (cardObj != null) Destroy(cardObj);
            }
            _spawnedCards.Clear();
            _cardsPerPlayer.Clear();
            CardsInTable.Clear();
        }

        public void PlaceCard(Card card, GameObject player)
        {
            card.ownerObj = player;
            CardsInTable.Add(card);
            SpawnCard3D(card, player);
            
            // Disparar evento de dominio
            OnCardPlaced?.Invoke(card, player);
        }

        private string GetPlayerName(Card card)
        {
            if (card == null || card.ownerObj == null) return "Unknown";
            var pLocal = card.ownerObj.GetComponent<Code.Player.PlayerLocal>();
            if (pLocal != null && pLocal.player != null) return pLocal.player.playerName;
            var pNpc = card.ownerObj.GetComponent<Code.Player.NPCPlayer>();
            if (pNpc != null) return pNpc.playerName;
            return card.ownerObj.name;
        }

        private string GetTeamName(Card card)
        {
            if (card == null || card.ownerObj == null) return "Unknown";
            var pLocal = card.ownerObj.GetComponent<Code.Player.PlayerLocal>();
            if (pLocal != null && pLocal.player != null && pLocal.player.team != null) return pLocal.player.team.teamName;
            var pNpc = card.ownerObj.GetComponent<Code.Player.NPCPlayer>();
            if (pNpc != null && pNpc.team != null) return pNpc.team.teamName;
            return "Unknown Team";
        }

        // [Server]
        public void DetermineHighestCard()
        {
            if (CardsInTable == null || CardsInTable.Count == 0)
            {
                Debug.Log("There is no enough cards on the table to evaluate");
                return;
            }

            int highestValue = -1;
            List<Card> highestCards = new List<Card>();

            foreach (var card in CardsInTable)
            {
                if (card.realValue > highestValue)
                {
                    highestValue = card.realValue;
                    highestCards.Clear();
                    highestCards.Add(card);
                }
                else if (card.realValue == highestValue)
                {
                    highestCards.Add(card);
                }
            }

            if (highestCards.Count > 1)
            {
                // Verify if the highest cards belong to different teams
                string firstTeam = GetTeamName(highestCards[0]);
                bool differentTeams = false;
                foreach(var c in highestCards)
                {
                    if (GetTeamName(c) != firstTeam)
                    {
                        differentTeams = true;
                        break;
                    }
                }

                if (differentTeams)
                {
                    Debug.Log("[TableManager] ¡EMPARDADO! Varios equipos tienen la misma carta más alta.");
                    GameManager.Instance.HandleTrickResult(null); // Null means tie
                    RpcHighestCard(highestCards[0], null); // Simplified RPC
                    OnTrickEvaluated?.Invoke();
                    return;
                }
            }
            
            Card winningCard = highestCards[0];
            Debug.Log($"[TableManager] Ganador de la baza: {GetPlayerName(winningCard)} del equipo {GetTeamName(winningCard)}");
            
            GameManager.Instance.HandleTrickResult(winningCard.ownerObj);

            RpcHighestCard(winningCard, null);
            OnTrickEvaluated?.Invoke();
        }


        // [ClientRpc]
        private void RpcHighestCard(Card highestCard, Card lowestCard)
        {
            if (highestCard == null)
            {
                Debug.Log("=== Trick Tied ===");
                return;
            }

            Debug.Log("=== Final Results ===");
            Debug.Log($"Highest Card: Value={highestCard.realValue}, Suit={highestCard.suit}, " +
                      $"Owner={GetPlayerName(highestCard)}, Team={GetTeamName(highestCard)}");
        }
    }
}