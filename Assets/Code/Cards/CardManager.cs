using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Cards
{
    public class CardManager : MonoBehaviour
    {
        public static CardManager Instance;
        public float upDuration = 0.98f;
        public Texture2D mouseOverTexture;
        public Texture2D mouseOutTexture;
        public Transform deckPosition;


        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            Cursor.SetCursor(mouseOutTexture, Vector2.zero, CursorMode.Auto);
        }
    }
}