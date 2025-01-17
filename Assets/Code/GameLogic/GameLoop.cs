using Mirror;
using UnityEngine;

namespace Code.GameLogic
{
    public class GameLoop : MonoBehaviour
    {
        [SerializeField] private GameObject notLocalPlayerPrefab;
        private Canvas _canvas;


        private void Start()
        {
            _canvas = FindAnyObjectByType<Canvas>();
            PlacingLocalPlayer();
        }

        private void PlacingLocalPlayer()
        {
            for (var i = 0; i < GameManager.Instance.playerCount - 1; i++)
            {
                var c = Instantiate(notLocalPlayerPrefab, _canvas.transform);

                c.GetComponent<RectTransform>().anchoredPosition =
                    CalculateNextNotLocalPlayerPosition(i, c.GetComponent<RectTransform>());
                
                Debug.Log("Creating new player");
            }
        }

        private Vector3 CalculateNextNotLocalPlayerPosition(int indexPlayer, RectTransform _c)
        {
            const float offset = 20f;

            var bottomLeft = Camera.main.ViewportToWorldPoint(new Vector3(0, 0, Camera.main.nearClipPlane));
            var topRight = Camera.main.ViewportToWorldPoint(new Vector3(1, 1, Camera.main.nearClipPlane));

            var right = topRight.x;
            var top = topRight.y;
            var left = bottomLeft.x;

            //Place cards in order
            switch (indexPlayer)
            {
                case 0:
                    _c.rotation = Quaternion.Euler(0, 0, 180);
                    _c.anchorMin = new Vector2(0.5f, 1);
                    _c.anchorMax = new Vector2(0.5f, 1);
                    return new Vector3(0, top - offset);

                case 1:
                    _c.rotation = Quaternion.Euler(0, 0, 90);
                    _c.anchorMin = new Vector2(1, 0.5f);
                    _c.anchorMax = new Vector2(1, 0.5f);
                    return new Vector3(right - offset, 0);

                case 2:
                    _c.rotation = Quaternion.Euler(0, 0, 270);
                    _c.anchorMin = new Vector2(0, 0.5f);
                    _c.anchorMax = new Vector2(0, 0.5f);
                    return new Vector3(left + offset, 0);

                default:
                    return new Vector3();
            }
        }
    }
}