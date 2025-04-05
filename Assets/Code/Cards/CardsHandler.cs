using System;
using System.Collections.Generic;
using Code.GameLogic;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

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

        public void PlayerCardSpawner(int i, Card card, int value, string type)
        {
            if (Camera.main == null) return;
            var bottomCenter = Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0f, 0f));


            var c = Instantiate(cardPrefab, bottomCenter, Quaternion.identity);
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
                cardComponent.playerController = gameObject.GetComponent<PlayerController>();
                cardComponent.Card = card;
                cardComponent.number.text = value.ToString();
                cardComponent.type.text = type;
            }
            Cards.Add(c);
        }

        [Command]
        public void CmdDrawCard(List<Card> FullDeck)
        {
            foreach (var player in GameManager.Instance.serverPlayers)
            {
                for (var j = 0; j < 3; j++)
                {
                    var playerObject = player.connectionToClient.identity.gameObject.GetComponent<PlayerController>();

                    GetCard(player.connectionToClient, j, FullDeck, playerObject);
                }
            }
        }


        [TargetRpc]
        private void GetCard(NetworkConnection conn, int index, List<Card> fullDeck, PlayerController playerObject)
        {
            var random = Random.Range(0, fullDeck.Count);

            var card = fullDeck[random];
            PlayerCardSpawner(index, card, fullDeck[random].value, fullDeck[random].type);

            fullDeck.RemoveAt(random);
        }
    }
}