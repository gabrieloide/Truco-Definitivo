using System.Linq;
using Code.Player;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class FlorAnnouncement : Announce
    {
        public void CanDeclareFlower()
        {
            var playerLocal = NetworkClient.localPlayer.GetComponent<PlayerLocal>();
            var cardsHandler = playerLocal.cardsHandler;

            if (cardsHandler.Cards == null || cardsHandler.Cards.Count == 0)
            {
                playerLocal.player.haveFlower = false;
                UpdateFlowerButton(false);
                return;
            }

            var allCardsMatch = cardsHandler.Cards.All(card => card == cardsHandler.Cards[0]);
            playerLocal.player.haveFlower = allCardsMatch;

            Debug.Log($"All cards match: {allCardsMatch}, now player can declare flower");


            void UpdateFlowerButton(bool canDeclareFlower)
            {
                var button = PlayerHUD.Instance.playerFlowerButton.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => playerLocal.player.haveFlower = true);
                button.interactable = false;
                PlayerHUD.Instance.playerFlowerButton.SetActive(canDeclareFlower);
            }
        }

        public override void IncreaseTotalScore()
        {
            throw new System.NotImplementedException();
        }
    }
}