using System;
using Mirror;
using UnityEngine;
using System.Collections.Generic;
using Code.Cards;

namespace Code.GameLogic
{
    public class NotPlayerSpawner : MonoBehaviour
    {
        public static NotPlayerSpawner Instance { get; private set; }
        
        [SerializeField] private GameObject notLocalPlayerPrefab;
        [HideInInspector] public List<GameObject> allNotLocalPlayer = new List<GameObject>();
        
        private Canvas _canvas;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            _canvas = FindAnyObjectByType<Canvas>();
            PlacingLocalPlayer();
            gameObject.SetActive(true);
        }

        private void PlacingLocalPlayer()
        {
            if (_canvas == null)
            {
                Debug.LogWarning("[NotPlayerSpawner] Canvas is null! Cannot place non-local players UI.");
                return;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogWarning("[NotPlayerSpawner] GameManager.Instance is null! Cannot place non-local players UI.");
                return;
            }

            if (notLocalPlayerPrefab == null)
            {
                Debug.LogWarning("[NotPlayerSpawner] notLocalPlayerPrefab is null! Cannot place non-local players UI.");
                return;
            }

            for (var i = 0; i < GameManager.Instance.playerCount - 1; i++)
            {
                var obj = Instantiate(notLocalPlayerPrefab, _canvas.transform);
                if (obj == null) continue;
                
                var rectTransform = obj.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchoredPosition = CalculateNextNotLocalPlayerPosition(i, rectTransform);
                }

                obj.name = $"NotLocalPlayer_{i}";
                allNotLocalPlayer.Add(obj);
            }
        }

        private Vector3 CalculateNextNotLocalPlayerPosition(int indexPlayer, RectTransform c)
        {
            if (c == null) return Vector3.zero;

            const float offset = 20f;
            if (Camera.main == null)
            {
                return Vector3.zero;
            }


            var bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
            var topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, Camera.main.nearClipPlane));

            var right = topRight.x;
            var top = topRight.y;
            var left = bottomLeft.x;

            //Place cards in order (Anti-clockwise mapping on screen)
            switch (indexPlayer)
            {
                case 0: // 1st Next Player -> RIGHT
                     c.rotation = Quaternion.Euler(0, 0, 90);
                     c.anchorMin = new Vector2(1, 0.5f);
                     c.anchorMax = new Vector2(1, 0.5f);
                     return new Vector3(right - offset, 0);

                case 1: // 2nd Next Player -> TOP
                     c.rotation = Quaternion.Euler(0, 0, 180);
                     c.anchorMin = new Vector2(0.5f, 1);
                     c.anchorMax = new Vector2(0.5f, 1);
                     return new Vector3(0, top - offset);

                case 2: // 3rd Next Player -> LEFT
                     c.rotation = Quaternion.Euler(0, 0, 270);
                     c.anchorMin = new Vector2(0, 0.5f);
                     c.anchorMax = new Vector2(0, 0.5f);
                     return new Vector3(left + offset, 0);

                default:
                     return Vector3.zero;
            }
        }
    }
}