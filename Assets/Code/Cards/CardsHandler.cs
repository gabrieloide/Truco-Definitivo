using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Cards
{
    public class CardsHandler : MonoBehaviour
    {
        public float upDuration = 0.98f;
        public Texture2D mouseOverTexture;
        public Texture2D mouseOutTexture;

        List<GameObject> InHandCard = new List<GameObject>();
        public GameObject cardPrefab;

        [SerializeField] private Vector2 _offsetCards = new Vector2(2.5f, 5);

        private void Awake()
        {
            Cursor.SetCursor(mouseOutTexture, Vector2.zero, CursorMode.Auto);

            if (cardPrefab == null)
            {
                Debug.Log("There is no prefab on cardPrefab.");
            }
        }

        private void Start()
        {
            var bottomCenter = Camera.main.ViewportToScreenPoint(new Vector3(0.5f, 0f, 0f));

            for (int i = 0; i < 3; i++)
            {
                var c = Instantiate(cardPrefab, bottomCenter, Quaternion.identity);
                Vector2 offset = i switch
                {
                    0 => Vector2.up * _offsetCards.y,
                    1 => new Vector3(-_offsetCards.x, _offsetCards.y),
                    2 => new Vector3(_offsetCards.x, _offsetCards.y),
                    _ => throw new ArgumentOutOfRangeException()
                };

                Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(bottomCenter.x, bottomCenter.y, 0f));
                c.transform.position = new Vector3(worldPos.x + offset.x, worldPos.y + offset.y, 0f);
                c.GetComponent<CardInteraction>().CardPosition = i;
                c.GetComponent<CardInteraction>()._playerController = gameObject.GetComponent<PlayerController>();

                InHandCard.Add(c);
            }
        }
    }
}