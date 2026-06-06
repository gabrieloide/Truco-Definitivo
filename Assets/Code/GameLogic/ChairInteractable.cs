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

        [Header("Shared Settings Source")]
        [Tooltip("If assigned, this chair will copy/synchronize its settings, camera offset, and hand anchor offset from this source.")]
        public ChairInteractable settingsSource;

        private void Update()
        {
            SyncWithSettingsSource();
        }

        private void OnDrawGizmos()
        {
            SyncWithSettingsSource();
        }

        private void OnValidate()
        {
            SyncWithSettingsSource();
        }

        public void SyncWithSettingsSource()
        {
            if (settingsSource == null || settingsSource == this) return;

            // Sync simple parameters
            cardSpacing = settingsSource.cardSpacing;
            cardRotationOffset = settingsSource.cardRotationOffset;

            // Sync cameraPosition offset relative to sitTransform
            if (cameraPosition != null && settingsSource.cameraPosition != null)
            {
                Transform srcSit = settingsSource.sitTransform != null ? settingsSource.sitTransform : settingsSource.transform;
                Transform mySit = sitTransform != null ? sitTransform : transform;

                Vector3 localCamPos = srcSit.InverseTransformPoint(settingsSource.cameraPosition.position);
                Quaternion localCamRot = Quaternion.Inverse(srcSit.rotation) * settingsSource.cameraPosition.rotation;

                Vector3 targetCamPos = mySit.TransformPoint(localCamPos);
                Quaternion targetCamRot = mySit.rotation * localCamRot;

                if (Vector3.Distance(cameraPosition.position, targetCamPos) > 0.0001f)
                {
                    cameraPosition.position = targetCamPos;
                }
                if (Quaternion.Angle(cameraPosition.rotation, targetCamRot) > 0.01f)
                {
                    cameraPosition.rotation = targetCamRot;
                }
            }

            // Sync handAnchor offset relative to sitTransform
            if (handAnchor != null && settingsSource.handAnchor != null)
            {
                Transform srcSit = settingsSource.sitTransform != null ? settingsSource.sitTransform : settingsSource.transform;
                Transform mySit = sitTransform != null ? sitTransform : transform;

                Vector3 localAnchorPos = srcSit.InverseTransformPoint(settingsSource.handAnchor.position);
                Quaternion localAnchorRot = Quaternion.Inverse(srcSit.rotation) * settingsSource.handAnchor.rotation;

                Vector3 targetAnchorPos = mySit.TransformPoint(localAnchorPos);
                Quaternion targetAnchorRot = mySit.rotation * localAnchorRot;

                if (Vector3.Distance(handAnchor.position, targetAnchorPos) > 0.0001f)
                {
                    handAnchor.position = targetAnchorPos;
                }
                if (Quaternion.Angle(handAnchor.rotation, targetAnchorRot) > 0.01f)
                {
                    handAnchor.rotation = targetAnchorRot;
                }
            }

            // Sync deckAnchor offset relative to sitTransform
            if (deckAnchor != null && settingsSource.deckAnchor != null)
            {
                Transform srcSit = settingsSource.sitTransform != null ? settingsSource.sitTransform : settingsSource.transform;
                Transform mySit = sitTransform != null ? sitTransform : transform;

                Vector3 localDeckPos = srcSit.InverseTransformPoint(settingsSource.deckAnchor.position);
                Quaternion localDeckRot = Quaternion.Inverse(srcSit.rotation) * settingsSource.deckAnchor.rotation;

                Vector3 targetDeckPos = mySit.TransformPoint(localDeckPos);
                Quaternion targetDeckRot = mySit.rotation * localDeckRot;

                if (Vector3.Distance(deckAnchor.position, targetDeckPos) > 0.0001f)
                {
                    deckAnchor.position = targetDeckPos;
                }
                if (Quaternion.Angle(deckAnchor.rotation, targetDeckRot) > 0.01f)
                {
                    deckAnchor.rotation = targetDeckRot;
                }
            }

            // Sync cardDestination offset relative to sitTransform
            if (cardDestination != null && settingsSource.cardDestination != null)
            {
                Transform srcSit = settingsSource.sitTransform != null ? settingsSource.sitTransform : settingsSource.transform;
                Transform mySit = sitTransform != null ? sitTransform : transform;

                Vector3 localDestPos = srcSit.InverseTransformPoint(settingsSource.cardDestination.position);
                Quaternion localDestRot = Quaternion.Inverse(srcSit.rotation) * settingsSource.cardDestination.rotation;

                Vector3 targetDestPos = mySit.TransformPoint(localDestPos);
                Quaternion targetDestRot = mySit.rotation * localDestRot;

                if (Vector3.Distance(cardDestination.position, targetDestPos) > 0.0001f)
                {
                    cardDestination.position = targetDestPos;
                }
                if (Quaternion.Angle(cardDestination.rotation, targetDestRot) > 0.01f)
                {
                    cardDestination.rotation = targetDestRot;
                }
            }
        }

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
