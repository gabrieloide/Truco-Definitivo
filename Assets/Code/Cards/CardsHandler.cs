using System;
using System.Collections.Generic;
using Code.GameLogic;
using Code.Player;
using DG.Tweening;
// using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using TMPro;
using Code.Scripts.Audio;

namespace Code.Cards
{
    public class CardsHandler : MonoBehaviour
    {
        public float upDuration = 0.98f;
        public Texture2D mouseOverTexture;
        public Texture2D mouseOutTexture;

        public GameObject cardPrefab;

        [SerializeField] private Vector2 _offsetCards = new Vector2(2.5f, 5);

        [SerializeField] public List<GameObject> Cards = new List<GameObject>();


        private void Awake()
        {
            Cursor.SetCursor(mouseOutTexture, Vector2.zero, CursorMode.Auto);

            if (cardPrefab == null)
            {
            }
        }

        // [ClientRpc]
        private void DeckStatus(bool status)
        {
            GameManager.Instance.deckIsLocked = status;
        }

        [Header("3D Hand Settings")]
        public GameObject card3DPrefab;
        public Transform handTransform; // Should be a child of the camera

        private void PlayerCardSpawner(int i, Card card, int value, string type, float delay = 0f)
        {
            if (card3DPrefab == null) return;

            Transform parent = handTransform != null ? handTransform : Camera.main.transform;
            if (parent == null) return;

            // Positioning cards in an arc in front of the camera
            Vector3 localPos = new Vector3((i - 1) * 0.25f, -0.3f, 0.6f);
            Quaternion localRot = Quaternion.Euler(70, (i - 1) * 15f, 0) * Quaternion.Euler(0, 180, 0);

            GameObject c = Instantiate(card3DPrefab, parent);
            
            // Configurar capa interactiva
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                SetLayerRecursively(c, interactableLayer);
            }

            var physicalCard = c.GetComponent<PhysicalCard3D>();
            if (physicalCard != null)
            {
                physicalCard.SetupCard(card, value, type, card.dbId);
                physicalCard.owner = GetComponent<PlayerLocal>();
            }

            var cardInteraction = c.GetComponent<CardInteraction>();
            if (cardInteraction != null)
            {
                cardInteraction.Card = card;
            }

            // Animar el reparto con JuicyCardAnimator
            var juicyAnimator = c.GetComponent<JuicyCardAnimator>();
            if (juicyAnimator == null)
            {
                juicyAnimator = c.AddComponent<JuicyCardAnimator>();
            }

            Vector3 startWorldPos;
            if (TableManager.Instance != null)
            {
                startWorldPos = TableManager.Instance.CurrentDeckPosition;
                // Animación sale desde debajo del mazo
                startWorldPos.y -= 0.015f;
            }
            else
            {
                startWorldPos = parent.position - parent.up * 1f;
            }

            float dealDuration = 0.5f;

            juicyAnimator.AnimateToHand(startWorldPos, localPos, localRot, dealDuration, delay, () =>
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("card_deal_swoosh");
                }
            });

            Cards.Add(c);
        }

        public void ClearCards()
        {
            foreach (var oldCard in Cards)
            {
                if (oldCard != null) Destroy(oldCard);
            }
            Cards.Clear();
        }

        public void ToggleCardsVisibility(bool visible)
        {
            foreach (var card in Cards)
            {
                if (card != null)
                {
                    card.SetActive(visible);
                }
            }
        }

        public void ReceiveSingleCard(Card card)
        {
            int index = Cards.Count;
            PlayerCardSpawner(index, card, card.value, card.suit, 0f);
        }

        // [TargetRpc]
        public void TargetReceiveCards(/*NetworkConnection target,*/ List<Card> dealtCards)
        {
            ClearCards();

            for (int i = 0; i < dealtCards.Count; i++)
            {
                var card = dealtCards[i];
                PlayerCardSpawner(i, card, card.value, card.suit, i * 0.15f);
            }

            // Actualizar estado de Flor después de recibir todas las cartas
            var flor = FindAnyObjectByType<Code.GameLogic.Announcement.FlorAnnouncement>();
            if (flor != null) flor.CanDeclareFlower();
        }

        private void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }
}
