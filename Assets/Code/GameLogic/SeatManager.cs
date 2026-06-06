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
        public List<ChairInteractable> allChairs = new List<ChairInteractable>();

        [Header("Dynamic Spawning Options")]
        [Tooltip("If true, chairs will be spawned at runtime around the table center.")]
        public bool spawnChairsAtRuntime = false;
        [Tooltip("Prefab to instantiate for chairs. Must have ChairInteractable on it.")]
        public GameObject chairPrefab;
        [Tooltip("Number of chairs to spawn.")]
        public int chairCount = 4;
        [Tooltip("Distance from the table center to spawn chairs.")]
        public float spawnRadius = 2.5f;
        [Tooltip("Height offset relative to the table center.")]
        public float spawnHeight = 0f;
        [Tooltip("Starting angle in degrees. -90 degrees is the bottom of the screen (South).")]
        public float startAngleDegrees = -90f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (spawnChairsAtRuntime && chairPrefab != null)
            {
                SpawnChairs();
            }
        }

        public void SpawnChairs()
        {
            // Clear existing chairs
            foreach (var existingChair in allChairs)
            {
                if (existingChair != null)
                {
                    Destroy(existingChair.gameObject);
                }
            }
            allChairs.Clear();

            Vector3 center = Vector3.zero;
            var table = FindAnyObjectByType<TableManager>();
            if (table != null && table.viraPosition != null)
            {
                center = table.viraPosition.position;
            }
            else if (table != null)
            {
                center = table.transform.position;
            }

            for (int i = 0; i < chairCount; i++)
            {
                float angleDegrees = startAngleDegrees + i * (360f / chairCount);
                float angleRad = angleDegrees * Mathf.Deg2Rad;
                Vector3 spawnPos = center + new Vector3(Mathf.Cos(angleRad) * spawnRadius, spawnHeight, Mathf.Sin(angleRad) * spawnRadius);

                GameObject chairObj = Instantiate(chairPrefab, spawnPos, Quaternion.identity, transform);
                chairObj.name = $"Chair{i + 1}";

                // Orient to face the center of the table on the XZ plane
                Vector3 lookDir = (center - spawnPos).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    chairObj.transform.rotation = Quaternion.LookRotation(lookDir);
                }

                var chair = chairObj.GetComponent<ChairInteractable>();
                if (chair != null)
                {
                    allChairs.Add(chair);
                }
            }
        }

        [ContextMenu("Spawn Chairs in Editor")]
        public void SpawnChairsInEditor()
        {
#if UNITY_EDITOR
            if (chairPrefab == null)
            {
                Debug.LogError("[SeatManager] No se ha asignado el Prefab de la silla (chairPrefab) en el inspector.");
                return;
            }

            // Clear existing chairs in hierarchy that are children of SeatManager
            var chairsToDestroy = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.GetComponent<ChairInteractable>() != null)
                {
                    chairsToDestroy.Add(child.gameObject);
                }
            }
            foreach (var go in chairsToDestroy)
            {
                UnityEditor.Undo.DestroyObjectImmediate(go);
            }
            allChairs.Clear();

            Vector3 center = Vector3.zero;
            var table = FindAnyObjectByType<TableManager>();
            if (table != null && table.viraPosition != null)
            {
                center = table.viraPosition.position;
            }
            else if (table != null)
            {
                center = table.transform.position;
            }

            for (int i = 0; i < chairCount; i++)
            {
                float angleDegrees = startAngleDegrees + i * (360f / chairCount);
                float angleRad = angleDegrees * Mathf.Deg2Rad;
                Vector3 spawnPos = center + new Vector3(Mathf.Cos(angleRad) * spawnRadius, spawnHeight, Mathf.Sin(angleRad) * spawnRadius);

                GameObject chairObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(chairPrefab, transform);
                chairObj.transform.position = spawnPos;
                chairObj.transform.rotation = Quaternion.identity;
                chairObj.name = $"Chair{i + 1}";

                // Orient to face the center of the table on the XZ plane
                Vector3 lookDir = (center - spawnPos).normalized;
                lookDir.y = 0;
                if (lookDir != Vector3.zero)
                {
                    chairObj.transform.rotation = Quaternion.LookRotation(lookDir);
                }

                var chair = chairObj.GetComponent<ChairInteractable>();
                if (chair != null)
                {
                    allChairs.Add(chair);
                }

                UnityEditor.Undo.RegisterCreatedObjectUndo(chairObj, "Spawn Chair");
            }

            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[SeatManager] Se instanciaron {chairCount} sillas correctamente alrededor de la mesa.");
#endif
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
                    camManager.SetSeatedCamera(chair);
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
