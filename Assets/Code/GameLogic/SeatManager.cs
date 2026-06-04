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
            return -1;
        }

        // [Server]
        public void RequestSeat(GameObject player, ChairInteractable chair)
        {
            Debug.Log($"[SeatManager] RequestSeat recibida de {player.name} para la silla {chair.name}");

            if (chair.isOccupied) 
            {
                Debug.LogWarning("[SeatManager] La silla ya está ocupada.");
                return;
            }

            if (chair.sitTransform == null)
            {
                Debug.LogWarning("[SeatManager] La silla no tiene asignado un 'Sit Transform'. Usando el transform de la silla como fallback.");
                chair.sitTransform = chair.transform;
            }

            // Mark chair as occupied
            chair.isOccupied = true;
            chair.occupant = player;

            // Notify clients to update chair visual/state if needed
            chair.RpcSetOccupied(true);

            // RpcSitPlayer(player.GetComponent<NetworkIdentity>().connectionToClient, player, chair.gameObject);
            RpcSitPlayer(player, chair.gameObject);
            Debug.Log("[SeatManager] Proceso de sentarse completado en el servidor local.");
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
            Debug.LogWarning("[SeatManager] No hay sillas disponibles para el jugador local.");
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
            var camManager = player.GetComponent<CameraManager>();

            if (chair != null && movement != null)
            {
                movement.isSeated = true;
                movement.UpdateCursorState();
                
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
                Vector3 lookTarget = TableManager.Instance.viraPosition.position;
                lookTarget.y = player.transform.position.y;
                player.transform.LookAt(lookTarget);
                
                Debug.Log($"[SeatManager] Teleportando jugador a {chair.sitTransform.position} y mirando a la mesa.");
            }

            if (chair != null && camManager != null)
            {
                Debug.Log("[SeatManager] Solicitando cambio de cámara a CameraManager.");
                camManager.SetSeatedCamera(chair.tableCamera);
            }
        }

        // [TargetRpc]
        private void RpcStandPlayer(/*NetworkConnection conn,*/ GameObject player)
        {
            var movement = player.GetComponent<PlayerMovement3D>();
            var camManager = player.GetComponent<CameraManager>();
            var rb = player.GetComponent<Rigidbody>();

            if (movement != null) movement.isSeated = false;
            if (rb != null) rb.isKinematic = false;
            if (camManager != null) camManager.SetWalkingCamera();
        }
    }
}
