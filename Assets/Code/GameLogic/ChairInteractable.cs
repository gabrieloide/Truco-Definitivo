using UnityEngine;
using System.Collections.Generic;
using Code.Player;

namespace Code.GameLogic
{
    public class ChairInteractable : MonoBehaviour, IInteractable
    {
        [Header("Chair Configuration")]
        public Transform sitTransform;
        public Transform deckAnchor; // Position to the right of the player for the deck/vira
        public Transform cardDestination; // Posición en la mesa donde aterrizan las cartas de esta silla
        public Transform cameraPosition; // Posición adonde se moverá la cámara principal al sentarse
        
        [Tooltip("Anchors for the player's 3 hand cards. Position/rotation are configured in the Scene view.")]
        public List<Transform> cardAnchors = new List<Transform>();

        public bool isOccupied = false;
        public GameObject occupant;

        public string GetInteractText()
        {
            return isOccupied ? "Seat Taken" : "Sit Down";
        }

        public void Interact(GameObject interactor)
        {
            if (isOccupied)
            {
                // If the interactor is already sitting here, maybe stand up?
                if (occupant == interactor)
                {
                    CmdStandUp(interactor);
                }
                return;
            }

            CmdSitDown(interactor);
        }

        // [Command(requiresAuthority = false)]
        private void CmdSitDown(GameObject player)
        {
            // Security check removed for local


            if (SeatManager.Instance != null)
            {
                SeatManager.Instance.RequestSeat(player, this);
            }
        }

        // [Command(requiresAuthority = false)]
        private void CmdStandUp(GameObject player)
        {

            if (SeatManager.Instance != null)
            {
                SeatManager.Instance.StandUp(player, this);
            }
        }

        // [ClientRpc]
        public void RpcSetOccupied(bool status)
        {
            isOccupied = status;
        }
    }
}
