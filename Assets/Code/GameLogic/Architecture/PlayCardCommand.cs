using UnityEngine;
using Code.GameLogic;

namespace Code.GameLogic.Architecture
{
    public class PlayCardCommand : ICommand
    {
        private Card _card;
        private GameObject _player;

        public PlayCardCommand(Card card, GameObject player)
        {
            _card = card;
            _player = player;
        }

        public void Execute()
        {
            if (TableManager.Instance != null)
            {
                TableManager.Instance.PlaceCard(_card, _player);
            }
        }
    }
}
