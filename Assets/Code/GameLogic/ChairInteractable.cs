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
        
        [Tooltip("Anchor for the player's hand cards (center card).")]
        public Transform handAnchor;

        [Tooltip("Distance spacing between cards in the hand.")]
        public float cardSpacing = 0.25f;

        [Tooltip("Rotation offset (in degrees) on each axis to fan/tilt the cards out.")]
        public Vector3 cardRotationOffset = new Vector3(0f, 15f, 0f);

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
