using System;
using System.Collections.Generic;
using Code.GameLogic;
using Code.Player;
using DG.Tweening;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using TMPro;

namespace Code.Cards
{
    public class CardsHandler : NetworkBehaviour
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

        [ClientRpc]
        private void DeckStatus(bool status)
        {
            GameManager.Instance.deckIsLocked = status;
        }

        private void PlayerCardSpawner(int i, Card card, int value, string type)
        {
            if (Camera.main == null) return;
            var bottomCenter = Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0f, 0f));


            var c = Instantiate(cardPrefab, bottomCenter, Quaternion.identity,
                NetworkClient.localPlayer.gameObject.transform);
            Vector2 offset = i switch
            {
                0 => Vector2.up * _offsetCards.y,
                1 => new Vector3(-_offsetCards.x, _offsetCards.y),
                2 => new Vector3(_offsetCards.x, _offsetCards.y),
                _ => Vector2.zero
            };
            var worldPos = Camera.main.ScreenToWorldPoint(new Vector3(bottomCenter.x, bottomCenter.y, 0f));
            c.transform.position = new Vector3(worldPos.x + offset.x, worldPos.y + offset.y, 0f);
            var cardComponent = c.GetComponent<CardInteraction>();

            if (cardComponent != null)
            {
                cardComponent.cardPosition = i;
                cardComponent.Card = card;
                cardComponent.number.text = value.ToString();
                cardComponent.type.text = type;
            }

            Cards.Add(c);
        }

        [ClientRpc]
        public void RpcMoveCard(Vector3 newPosition, int cardposition)
        {
            if (isLocalPlayer)
                return;

            if (NotPlayerSpawner.Instance == null || NotPlayerSpawner.Instance.allNotLocalPlayer.Count == 0) 
                return;

            // Simple assumption: the first opponent is the one playing. 
            // A more complex 4-player system requires mapping NetworkConnection to Seat Index.
            var notPlayer = NotPlayerSpawner.Instance.allNotLocalPlayer[0];

            if (cardposition >= 0 && cardposition < notPlayer.transform.childCount)
            {
                var notPlayerCard = notPlayer.transform.GetChild(cardposition);
                notPlayerCard.DOMove(newPosition, 0.2f).SetEase(Ease.InOutElastic);
            }
        }

        [TargetRpc]
        public void TargetReceiveCards(NetworkConnection conn, List<Card> dealtCards)
        {
            for (int i = 0; i < dealtCards.Count; i++)
            {
                var card = dealtCards[i];
                card.cardOwner = NetworkClient.localPlayer.gameObject.GetComponent<PlayerLocal>();
                PlayerCardSpawner(i, card, card.value, card.suit);
            }
        }
    }
}