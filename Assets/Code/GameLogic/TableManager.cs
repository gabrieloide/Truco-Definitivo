using System.Collections.Generic;
using Code.Player;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.GameLogic
{
    public class TableManager : NetworkBehaviour
    {
        public readonly SyncList<Card> CardsInTable = new SyncList<Card>();
        public static TableManager Instance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        [Server]
        public void DetermineHighestCard()
        {
            Card highestCard = null;

            foreach (var card in CardsInTable)
            {
                if (highestCard == null || card.realValue > highestCard.realValue)
                {
                    highestCard = card;
                }
            }

            RpcHighestCard(highestCard);
        }

        [ClientRpc]
        private void RpcHighestCard(Card highestCard)
        {
            if (highestCard == null)
                return;

            Debug.Log(
                $"Highest card: {highestCard.value} of {highestCard.suit} Name of the player is: {highestCard.cardOwner.player.playerName} from the team: {highestCard.cardOwner.player.team.teamName}");


            //highestCard.cardOwner.structPlayer.team.teamScore++;
            PlayerHUD.Instance.ChangeScoreText();
        }
    }
}