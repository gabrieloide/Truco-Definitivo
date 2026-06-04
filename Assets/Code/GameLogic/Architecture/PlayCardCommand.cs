using UnityEngine;
using Code.GameLogic;

namespace Code.GameLogic.Architecture
{
    public class PlayCardCommand : ICommand
    {
        private readonly Card card;
        private readonly GameObject player;
        private readonly Vector3? startPos;

        public PlayCardCommand(Card card, GameObject player, Vector3? startPos = null)
        {
            this.card = card;
            this.player = player;
            this.startPos = startPos;
        }

        public void Execute()
        {
            if (TableManager.Instance != null)
            {
                TableManager.Instance.PlaceCard(card, player, startPos);
            }
        }
    }
}
