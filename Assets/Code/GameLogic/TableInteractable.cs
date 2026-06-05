using Code.Player;
using UnityEngine;

namespace Code.GameLogic
{
    public class TableInteractable : MonoBehaviour, IInteractable
    {
        public string GetInteractText()
        {
            return "Play Card";
        }

        public void Interact(GameObject interactor)
        {
            var playerLocal = interactor.GetComponent<PlayerLocal>();
            var movement = interactor.GetComponent<PlayerMovement3D>();

            if (playerLocal == null || movement == null) return;

            if (!movement.isSeated)
            {
                return;
            }

            if (playerLocal.selectedCardInteraction != null)
            {
                playerLocal.selectedCardInteraction.PlayCardToTable();
                playerLocal.selectedCardInteraction = null; // Clear selection
            }
            else
            {
            }
        }
    }
}
