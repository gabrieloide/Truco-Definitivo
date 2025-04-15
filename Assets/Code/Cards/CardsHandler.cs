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

        [Command]
        public void CmdDrawCard(List<Card> FullDeck)
        {
            CreateVira();

            DeckStatus(true);
            foreach (var player in GameManager.Instance.serverPlayers)
            {
                for (var j = 0; j < 3; j++)
                {
                    GetCard(player.connectionToClient, j, FullDeck);
                }
            }
        }

        [ClientRpc]
        private void CreateVira()
        {
            ref var card = ref FindAnyObjectByType<DeckCreator>().cardVira;
            var fullDeck = FindAnyObjectByType<DeckCreator>()._fullDeck;

            var random = Random.Range(0, fullDeck.Count);
            card = fullDeck[random];

            fullDeck.Remove(card);
            Debug.Log($"La vira es {card.value}, {card.suit}");
        }

        [ClientRpc]
        public void RpcMoveCard(Vector3 newPosition, int cardposition)
        {
            if (isLocalPlayer)
                return;

            var notPlayer = GameObject.Find("NotLocalPlayer");

            if (notPlayer == null) return;

            var notPlayerCard = notPlayer.transform.GetChild(cardposition);
            notPlayerCard.DOMove(newPosition, 0.2f).SetEase(Ease.InOutElastic);
        }

        [TargetRpc]
        private void GetCard(NetworkConnection conn, int index, List<Card> fullDeck)
        {
            var random = Random.Range(0, fullDeck.Count);

            var card = fullDeck[random];
            card.cardOwner = NetworkClient.localPlayer.gameObject.GetComponent<PlayerLocal>();


            PlayerCardSpawner(index, card, fullDeck[random].value, fullDeck[random].suit);
            DeleteCardFromDeck(card);
        }

        [Server]
        private void DeleteCardFromDeck(Card card)
        {
            ref var fullDeck = ref FindAnyObjectByType<DeckCreator>()._fullDeck;

            fullDeck.Remove(card);
        }
    }
}