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

        /// <summary>
        /// Calcula la posición y rotación local de una carta en la mano basándose en su índice.
        /// Este método centraliza la lógica para que los Gizmos y el Spawner sean idénticos.
        /// </summary>
        public void CalculateHandPosition(int cardIndex, int totalCards, Transform parent, out Vector3 localPos, out Quaternion localRot)
        {
            ChairInteractable chair = null;
            if (SeatManager.Instance != null)
            {
                var playerLocal = GetComponent<PlayerLocal>();
                GameObject searchTarget = playerLocal != null ? playerLocal.gameObject : gameObject;
                int seatIndex = SeatManager.Instance.GetPlayerSeatIndex(searchTarget);
                
                if (seatIndex == -1 && !Application.isPlaying && SeatManager.Instance.allChairs.Count > 0)
                {
                    seatIndex = 0;
                }

                if (seatIndex != -1 && SeatManager.Instance.allChairs.Count > seatIndex)
                {
                    chair = SeatManager.Instance.allChairs[seatIndex];
                }
            }

            // Usamos totalCards si es > 0, si no asumimos 3 (Truco)
            int effectiveTotal = totalCards > 0 ? totalCards : 3;
            float indexOffset = cardIndex - (effectiveTotal - 1) / 2.0f;

            if (chair != null && chair.handAnchor != null)
            {
                // Calculamos la posición en espacio de MUNDO
                Vector3 worldPos = chair.handAnchor.position + chair.handAnchor.right * (indexOffset * chair.cardSpacing);
                
                // Convertimos a LOCAL respecto al parent (la cámara)
                localPos = parent.InverseTransformPoint(worldPos);

                Quaternion rotationOffset = Quaternion.Euler(
                    indexOffset * chair.cardRotationOffset.x,
                    indexOffset * chair.cardRotationOffset.y,
                    indexOffset * chair.cardRotationOffset.z
                );
                Quaternion worldRot = chair.handAnchor.rotation * rotationOffset;
                localRot = Quaternion.Inverse(parent.rotation) * worldRot;
            }
            else
            {
                localPos = new Vector3(indexOffset * 0.25f, -0.3f, 0.6f);
                localRot = Quaternion.Euler(70, indexOffset * 15f, 0) * Quaternion.Euler(0, 180, 0);
            }
        }

        private void PlayerCardSpawner(int i, Card card, int value, string type, int totalCards, float delay = 0f)
        {
            if (card3DPrefab == null) return;

            // Buscamos la silla para obtener el handAnchor
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
            }

            // PRIORIDAD: Usar el handAnchor de la silla como padre físico de las cartas.
            // Esto las hace independientes de qué cámara esté activa.
            Transform parent = (chair != null && chair.handAnchor != null) ? chair.handAnchor : handTransform;
            if (parent == null) parent = Camera.main != null ? Camera.main.transform : null;
            
            if (parent == null)
            {
                Debug.LogError("[CardsHandler] ERROR: No se encontró handAnchor ni handTransform para emparentar las cartas.");
                return;
            }

            // Calculamos posición y rotación LOCAL respecto al PARENT elegido
            // Si el parent es el handAnchor, el offset es directo.
            int effectiveTotal = totalCards > 0 ? totalCards : 3;
            float indexOffset = i - (effectiveTotal - 1) / 2.0f;

            Vector3 localPos;
            Quaternion localRot;

            if (chair != null && chair.handAnchor != null && parent == chair.handAnchor)
            {
                // Si el padre es el anchor, la posición local es simplemente el desplazamiento en X (right)
                localPos = Vector3.right * (indexOffset * chair.cardSpacing);
                localRot = Quaternion.Euler(
                    indexOffset * chair.cardRotationOffset.x,
                    indexOffset * chair.cardRotationOffset.y,
                    indexOffset * chair.cardRotationOffset.z
                );
            }
            else
            {
                // Fallback si no hay anchor (usando cámara)
                localPos = new Vector3(indexOffset * 0.25f, -0.3f, 0.6f);
                localRot = Quaternion.Euler(70, indexOffset * 15f, 0) * Quaternion.Euler(0, 180, 0);
            }

            GameObject c = Instantiate(card3DPrefab, parent);
            c.transform.localPosition = localPos;
            c.transform.localRotation = localRot;
            
            // Configurar capa interactiva
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                SetLayerRecursively(c, interactableLayer);
            }

            var physicalCard = c.GetComponent<PhysicalCard3D>();
            if (physicalCard != null)
            {
                // Another player's hand renders as card backs: the card mesh shows the
                // same texture on both sides, so a face-up setup would leak the values
                // (e.g. the host seeing the client's hand). The real Card still goes
                // into InitialHand/CardInteraction for server-side game logic.
                var ownerLocal = GetComponent<PlayerLocal>();
                bool isOwnHand = ownerLocal == null || ownerLocal.isLocalPlayer;
                if (isOwnHand)
                    physicalCard.SetupCard(card, value, type, card.dbId);
                else
                    physicalCard.SetupCard(card, 0, "", card.dbId);
                physicalCard.owner = ownerLocal;
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

        /// <summary>
        /// Removes one rendered card from this hand so remote views stay in sync when
        /// the owner plays a card. Matches by dbId when possible; with fallbackToLast,
        /// removes the last card instead (hidden placeholders share no ids).
        /// InitialHand is untouched — game logic (envido) still needs the dealt hand.
        /// </summary>
        public void RemoveRenderedCard(int dbId, bool fallbackToLast)
        {
            int index = -1;
            if (dbId >= 0)
            {
                for (int i = 0; i < Cards.Count; i++)
                {
                    var interaction = Cards[i] != null ? Cards[i].GetComponent<CardInteraction>() : null;
                    if (interaction != null && interaction.Card != null && interaction.Card.dbId == dbId)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index == -1)
            {
                if (!fallbackToLast) return;
                index = Cards.Count - 1;
                if (index < 0) return;
            }

            var obj = Cards[index];
            Cards.RemoveAt(index);
            if (obj != null) Destroy(obj);
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
            // Siempre usamos 3 como total para que el abanico esté centrado desde el principio (Truco)
            PlayerCardSpawner(index, card, card.value, card.suit, 3, 0f);
        }

        // [TargetRpc]
        public void TargetReceiveCards(/*NetworkConnection target,*/ List<Card> dealtCards)
        {
            ClearCards();

            int total = dealtCards.Count;
            for (int i = 0; i < total; i++)
            {
                var card = dealtCards[i];
                PlayerCardSpawner(i, card, card.value, card.suit, total, i * 0.15f);
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
                CalculateHandPosition(i, 3, parent, out Vector3 localPos, out Quaternion localRot);

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
