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
                Debug.Log("There is no prefab on cardPrefab.");
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

        private void PlayerCardSpawner(int i, Card card, int value, string type)
        {
            if (card3DPrefab == null) return;

            Transform parent = handTransform != null ? handTransform : Camera.main.transform;
            if (parent == null) return;

            // Positioning cards in an arc in front of the camera
            Vector3 localPos = new Vector3((i - 1) * 0.25f, -0.3f, 0.6f);
            Quaternion localRot = Quaternion.Euler(70, (i - 1) * 15f, 0);

            GameObject c = Instantiate(card3DPrefab, parent);
            c.transform.localPosition = localPos;
            c.transform.localRotation = localRot;
            c.layer = LayerMask.NameToLayer("Interactable");

            var physicalCard = c.GetComponent<PhysicalCard3D>();
            if (physicalCard != null)
            {
                physicalCard.SetupCard(value, type);
                physicalCard.owner = GetComponent<PlayerLocal>();
            }

            Cards.Add(c);
        }

        // [TargetRpc]
        public void TargetReceiveCards(/*NetworkConnection target,*/ List<Card> dealtCards)
        {
            // Limpiar cartas antiguas antes de recibir nuevas
            foreach (var oldCard in Cards)
            {
                if (oldCard != null) Destroy(oldCard);
            }
            Cards.Clear();

            Debug.Log($"[CardsHandler] Recibiendo {dealtCards.Count} nuevas cartas.");
            for (int i = 0; i < dealtCards.Count; i++)
            {
                var card = dealtCards[i];
                PlayerCardSpawner(i, card, card.value, card.suit);
            }

            // Actualizar estado de Flor después de recibir todas las cartas
            var flor = FindAnyObjectByType<Code.GameLogic.Announcement.FlorAnnouncement>();
            if (flor != null) flor.CanDeclareFlower();
        }
    }
}