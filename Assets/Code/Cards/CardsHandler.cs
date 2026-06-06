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
        public float upDuration = 0.2f;
        public Texture2D mouseOverTexture;
        public Texture2D mouseOutTexture;

        public GameObject cardPrefab;

        [SerializeField] private Vector2 _offsetCards = new Vector2(2.5f, 5);

        [SerializeField] public List<GameObject> Cards = new List<GameObject>();

        public List<Card> InitialHand = new List<Card>();


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

            // Determine local position and rotation based on seat anchors if available
            Vector3 localPos;
            Quaternion localRot;

            ChairInteractable chair = null;
            if (SeatManager.Instance != null)
            {
                var playerLocal = GetComponent<PlayerLocal>();
                if (playerLocal != null)
                {
                    int seatIndex = SeatManager.Instance.GetPlayerSeatIndex(playerLocal.gameObject);
                    if (seatIndex != -1 && SeatManager.Instance.allChairs.Count > seatIndex)
                    {
                        chair = SeatManager.Instance.allChairs[seatIndex];
                    }
                }
            }

            if (chair != null && chair.cardAnchors != null && i < chair.cardAnchors.Count && chair.cardAnchors[i] != null)
            {
                Transform anchor = chair.cardAnchors[i];
                localPos = parent.InverseTransformPoint(anchor.position);
                localRot = Quaternion.Inverse(parent.rotation) * anchor.rotation;
            }
            else
            {
                // Fallback to standard arc layout
                localPos = new Vector3((i - 1) * 0.25f, -0.3f, 0.6f);
                localRot = Quaternion.Euler(70, (i - 1) * 15f, 0) * Quaternion.Euler(0, 180, 0);
            }

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
            if (cardInteraction == null)
            {
                cardInteraction = c.AddComponent<CardInteraction>();
            }

            if (cardInteraction != null)
            {
                cardInteraction.Card = card;
                cardInteraction.SetRestingPosition(localPos, localRot);
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

            InitialHand.Add(card);
            Cards.Add(c);
        }

        public void ClearCards()
        {
            foreach (var oldCard in Cards)
            {
                if (oldCard != null) Destroy(oldCard);
            }
            Cards.Clear();
            InitialHand.Clear();
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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform parent = handTransform != null ? handTransform : (Camera.main != null ? Camera.main.transform : transform);
            if (parent == null) return;

            Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.6f);
            Vector3 realCardScale = new Vector3(0.151111543f, 0.217364341f, 0.00313752703f);

            for (int i = 0; i < 3; i++)
            {
                Vector3 localPos;
                Quaternion localRot;

                ChairInteractable chair = null;
                if (SeatManager.Instance != null)
                {
                    var playerLocal = GetComponent<PlayerLocal>();
                    GameObject searchTarget = playerLocal != null ? playerLocal.gameObject : gameObject;
                    int seatIndex = SeatManager.Instance.GetPlayerSeatIndex(searchTarget);
                    if (seatIndex != -1 && SeatManager.Instance.allChairs.Count > seatIndex)
                    {
                        chair = SeatManager.Instance.allChairs[seatIndex];
                    }
                    else if (SeatManager.Instance.allChairs.Count > 0)
                    {
                        chair = SeatManager.Instance.allChairs[0];
                    }
                }

                if (chair != null && chair.cardAnchors != null && i < chair.cardAnchors.Count && chair.cardAnchors[i] != null)
                {
                    Transform anchor = chair.cardAnchors[i];
                    localPos = parent.InverseTransformPoint(anchor.position);
                    localRot = Quaternion.Inverse(parent.rotation) * anchor.rotation;
                }
                else
                {
                    localPos = new Vector3((i - 1) * 0.25f, -0.3f, 0.6f);
                    localRot = Quaternion.Euler(70, (i - 1) * 15f, 0) * Quaternion.Euler(0, 180, 0);
                }

                Vector3 worldPos = parent.TransformPoint(localPos);
                Quaternion worldRot = parent.rotation * localRot;

                Matrix4x4 oldMatrix = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(worldPos, worldRot, realCardScale);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
                
                // Dibujar cruz para identificar la orientación frontal de la carta
                Gizmos.DrawLine(new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0));
                Gizmos.DrawLine(new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0));
                Gizmos.matrix = oldMatrix;
            }
        }
#endif
    }
}
