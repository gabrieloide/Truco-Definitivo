using UnityEngine;

namespace Code.GameLogic
{
    public interface IInteractable
    {
        string GetInteractText();
        void Interact(GameObject interactor);
    }
}
