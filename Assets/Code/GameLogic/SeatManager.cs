using System.Collections.Generic;
using Code.Player;
// using Mirror;
using UnityEngine;

namespace Code.GameLogic
{
    public class SeatManager : MonoBehaviour
    {
        public static SeatManager Instance { get; private set; }

        [Header("Seat Configuration (Counter-Clockwise)")]
        public List<ChairInteractable> allChairs;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public int GetPlayerSeatIndex(GameObject player)
        {
            for (int i = 0; i < allChairs.Count; i++)
            {
                if (allChairs[i].occupant == player) return i;
            }
            Debug.Log($"[SeatManager] GetPlayerSeatIndex: Jugador {player.name} NO encontrado en ninguna silla. allChairs.Count = {allChairs.Count}");
            return -1;
        }

        // [Server]
        public void RequestSeat(GameObject player, ChairInteractable chair)
        {
            Debug.Log($"[SeatManager] RequestSeat llamado para {player.name} en silla {chair.name}");
            if (chair.isOccupied) 
            {
                Debug.LogWarning($"[SeatManager] ¡La silla {chair.name} ya está ocupada por {(chair.occupant != null ? chair.occupant.name : "NULL")}! Cancelando sentada.");
                return;
            }

            if (chair.sitTransform == null)
            {
                chair.sitTransform = chair.transform;
            }

            // Mark chair as occupied
            chair.isOccupied = true;
            chair.occupant = player;

            // Notify clients to update chair visual/state if needed
            chair.RpcSetOccupied(true);

            // RpcSitPlayer(player.GetComponent<NetworkIdentity>().connectionToClient, player, chair.gameObject);
            RpcSitPlayer(player, chair.gameObject);
        }

        public void AutoSeatLocalPlayer(GameObject player)
        {
            foreach (var chair in allChairs)
            {
                if (!chair.isOccupied)
                {
                    RequestSeat(player, chair);
                    return;
                }
            }
        }

        // [Server]
        public void StandUp(GameObject player, ChairInteractable chair)
        {
            chair.isOccupied = false;
            chair.occupant = null;
            chair.RpcSetOccupied(false);

            // RpcStandPlayer(player.GetComponent<NetworkIdentity>().connectionToClient, player);
            RpcStandPlayer(player);
        }

        // [TargetRpc]
        private void RpcSitPlayer(/*NetworkConnection conn,*/ GameObject player, GameObject chairObj)
        {
            var chair = chairObj.GetComponent<ChairInteractable>();
            var movement = player.GetComponent<PlayerMovement3D>();

            if (chair != null)
            {
                if (movement != null)
                {
                    movement.isSeated = true;
                    movement.UpdateCursorState();
                }
                
                // Teleport and lock physics
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
                
                player.transform.position = chair.sitTransform.position;
                // Make the player always face the center of the table (viraPosition) and stay upright
                Vector3 lookTarget = (TableManager.Instance != null && TableManager.Instance.viraPosition != null) 
                    ? TableManager.Instance.viraPosition.position 
                    : (TableManager.Instance != null ? TableManager.Instance.transform.position : Vector3.zero);
                lookTarget.y = player.transform.position.y;
                player.transform.LookAt(lookTarget);
                
            }

            // Only update the camera if the player who is sitting down is the local player!
            bool isLocal = player.GetComponent<PlayerLocal>() != null || player.GetComponentInChildren<PlayerLocal>(true) != null;
            if (chair != null && isLocal)
            {
                var camManager = player.GetComponent<CameraManager>();
                if (camManager == null)
                {
                    camManager = player.GetComponentInChildren<CameraManager>(true);
                }
                if (camManager == null)
                {
                    camManager = CameraManager.Instance;
                }
                if (camManager == null)
                {
                    camManager = FindAnyObjectByType<CameraManager>();
                }

                if (camManager != null)
                {
                    camManager.SetSeatedCamera(chair.cameraPosition);
                }
                else
                {
                    Debug.LogError("[SeatManager] ERROR: Jugador local sentándose pero no se encontró el CameraManager.");
                }
            }
            else if (chair != null)
            {
            }
        }

        // [TargetRpc]
        private void RpcStandPlayer(/*NetworkConnection conn,*/ GameObject player)
        {
            var movement = player.GetComponent<PlayerMovement3D>();
            var rb = player.GetComponent<Rigidbody>();

            if (movement != null) movement.isSeated = false;
            if (rb != null) rb.isKinematic = false;

            // Only update the camera if the player who is standing up is the local player!
            bool isLocal = player.GetComponent<PlayerLocal>() != null || player.GetComponentInChildren<PlayerLocal>(true) != null;
            if (isLocal)
            {
                var camManager = player.GetComponent<CameraManager>();
                if (camManager == null)
                {
                    camManager = player.GetComponentInChildren<CameraManager>(true);
                }
                if (camManager == null)
                {
                    camManager = CameraManager.Instance;
                }
                if (camManager == null)
                {
                    camManager = FindAnyObjectByType<CameraManager>();
                }

                if (camManager != null)
                {
                    camManager.SetWalkingCamera();
                }
            }
        }
    }
}
