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

        [Header("Cinemachine Camera")]
        [Tooltip("The virtual camera specific to this chair.")]
        public Unity.Cinemachine.CinemachineCamera virtualCamera;

        public bool isOccupied = false;
        public GameObject occupant;

        public string GetInteractText()
        {
            return isOccupied ? "Seat Taken" : "Sit Down";
        }

        public void Interact(GameObject interactor)
        {
            // Players are always seated — once in a chair there is no standing up
            // or moving to another one.
            if (isOccupied) return;
            if (SeatManager.Instance == null) return;
            if (SeatManager.Instance.GetPlayerSeatIndex(interactor) >= 0) return;

            SeatManager.Instance.RequestSeat(interactor, this);
        }

        // [ClientRpc]
        public void RpcSetOccupied(bool status)
        {
            isOccupied = status;
        }
    }
}
