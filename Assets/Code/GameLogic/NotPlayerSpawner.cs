using System;
using Mirror;
using UnityEngine;
using System.Collections.Generic;
using Code.Cards;

namespace Code.GameLogic
{
    public class NotPlayerSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject notLocalPlayerPrefab;
        [HideInInspector] public List<GameObject> allNotLocalPlayer;
        
        private Canvas _canvas;

        private void Start()
        {
            _canvas = FindAnyObjectByType<Canvas>();
            PlacingLocalPlayer();
            gameObject.SetActive(true);
        }

        private void PlacingLocalPlayer()
        {
            for (var i = 0; i < GameManager.Instance.playerCount - 1; i++)
            {
                var obj = Instantiate(notLocalPlayerPrefab, _canvas.transform);
                //GameManager.Instance.Spawneables(obj);
                
                obj.GetComponent<RectTransform>().anchoredPosition =
                    CalculateNextNotLocalPlayerPosition(i, obj.GetComponent<RectTransform>());

                obj.name = "NotLocalPlayer";
                allNotLocalPlayer.Add(obj);
            }
        }

        private Vector3 CalculateNextNotLocalPlayerPosition(int indexPlayer, RectTransform c)
        {
            const float offset = 20f;
            if (Camera.main == null)
                throw new ArgumentOutOfRangeException();


            var bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
            var topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, Camera.main.nearClipPlane));

            var right = topRight.x;
            var top = topRight.y;
            var left = bottomLeft.x;

            //Place cards in order
            switch (indexPlayer)
            {
                case 0:
                    c.rotation = Quaternion.Euler(0, 0, 180);
                    c.anchorMin = new Vector2(0.5f, 1);
                    c.anchorMax = new Vector2(0.5f, 1);
                    return new Vector3(0, top - offset);

                case 1:
                    c.rotation = Quaternion.Euler(0, 0, 90);
                    c.anchorMin = new Vector2(1, 0.5f);
                    c.anchorMax = new Vector2(1, 0.5f);
                    return new Vector3(right - offset, 0);

                case 2:
                    c.rotation = Quaternion.Euler(0, 0, 270);
                    c.anchorMin = new Vector2(0, 0.5f);
                    c.anchorMax = new Vector2(0, 0.5f);
                    return new Vector3(left + offset, 0);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}