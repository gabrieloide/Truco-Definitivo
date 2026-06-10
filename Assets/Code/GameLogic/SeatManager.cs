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

        [Header("Manual Spawning Options")]
        [Tooltip("If true, chairs will be spawned at the positions defined in 'manualPositions'.")]
        public bool useManualPositions = false;
        [Tooltip("List of transforms defining the positions and rotations for manual chair spawning.")]
        public List<Transform> manualPositions = new List<Transform>();

        [Header("Dynamic Spawning Options")]
        [Tooltip("If true, chairs will be spawned at runtime around the table center.")]
        public bool spawnChairsAtRuntime = false;
        [Tooltip("Prefab to instantiate for chairs. Must have ChairInteractable on it.")]
        public GameObject chairPrefab;
        [Tooltip("Number of chairs to spawn (used if useManualPositions is false).")]
        public int chairCount = 4;
        [Tooltip("Distance from the table center to spawn chairs.")]
        public float spawnRadius = 2.5f;
        [Tooltip("Height offset relative to the table center.")]
        public float spawnHeight = 0f;
        [Tooltip("Starting angle in degrees. -90 degrees is the bottom of the screen (South).")]
        public float startAngleDegrees = -90f;

        /// <summary>
        /// Método principal de inicialización controlado por el GameManager.
        /// Detecta sillas existentes o las crea según la configuración.
        /// </summary>
        public int InitializeLayout()
        {
            Debug.Log($"[SeatManager] InitializeLayout: Iniciando. useManualPositions={useManualPositions}, spawnChairsAtRuntime={spawnChairsAtRuntime}");

            // 1. Limpiar lista actual
            allChairs.Clear();

            // 2. Buscar si ya hay sillas puestas como hijos en la jerarquía
            foreach (Transform child in transform)
            {
                var chair = child.GetComponent<ChairInteractable>();
                if (chair != null)
                {
                    allChairs.Add(chair);
                }
            }

            // 3. Si no hay sillas hijas, procedemos a crearlas si el usuario lo solicita (Manual o Radial)
            if (allChairs.Count == 0)
            {
                if (useManualPositions || spawnChairsAtRuntime)
                {
                    if (chairPrefab == null)
                    {
                        Debug.LogError("[SeatManager] CRITICAL: No se puede inicializar el layout porque chairPrefab es NULL.");
                        return 0;
                    }
                    SpawnChairs();
                }
                else
                {
                    Debug.LogWarning("[SeatManager] No hay sillas como hijos y 'useManualPositions'/'spawnChairsAtRuntime' están desactivados.");
                }
            }

            Debug.Log($"[SeatManager] InitializeLayout finalizado. Total sillas en allChairs: {allChairs.Count}");
            return allChairs.Count;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Ya NO llamamos a nada aquí. El GameManager tiene el control.
        }

        public void SpawnChairs()
        {
            Debug.Log($"[SeatManager] SpawnChairs ejecutado. useManualPositions={useManualPositions}");
            
            if (useManualPositions)
            {
                if (manualPositions == null || manualPositions.Count == 0)
                {
                    Debug.LogWarning("[SeatManager] useManualPositions es true pero no hay posiciones asignadas!");
                    return;
                }

                for (int i = 0; i < manualPositions.Count; i++)
                {
                    if (manualPositions[i] == null) continue;

                    GameObject chairObj = Instantiate(chairPrefab, manualPositions[i].position, manualPositions[i].rotation, transform);
                    chairObj.name = $"Chair{i + 1}";

                    var chair = chairObj.GetComponent<ChairInteractable>();
                    if (chair != null) allChairs.Add(chair);
                }
            }
            else
            {
                // Lógica de spawn radial original
                Vector3 center = Vector3.zero;
                var table = FindAnyObjectByType<TableManager>();
                if (table != null && table.viraPosition != null) center = table.viraPosition.position;
                else if (table != null) center = table.transform.position;

                for (int i = 0; i < chairCount; i++)
                {
                    float angleDegrees = startAngleDegrees + i * (360f / chairCount);
                    float angleRad = angleDegrees * Mathf.Deg2Rad;
                    Vector3 spawnPos = center + new Vector3(Mathf.Cos(angleRad) * spawnRadius, spawnHeight, Mathf.Sin(angleRad) * spawnRadius);

                    GameObject chairObj = Instantiate(chairPrefab, spawnPos, Quaternion.identity, transform);
                    chairObj.name = $"Chair{i + 1}";

                    Vector3 lookDir = (center - spawnPos).normalized;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero) chairObj.transform.rotation = Quaternion.LookRotation(lookDir);

                    var chair = chairObj.GetComponent<ChairInteractable>();
                    if (chair != null) allChairs.Add(chair);
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

            if (useManualPositions)
            {
                for (int i = 0; i < manualPositions.Count; i++)
                {
                    if (manualPositions[i] == null) continue;

                    GameObject chairObj = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(chairPrefab, transform);
                    chairObj.transform.position = manualPositions[i].position;
                    chairObj.transform.rotation = manualPositions[i].rotation;
                    chairObj.name = $"Chair{i + 1}";

                    var chair = chairObj.GetComponent<ChairInteractable>();
                    if (chair != null)
                    {
                        allChairs.Add(chair);
                    }

                    UnityEditor.Undo.RegisterCreatedObjectUndo(chairObj, "Spawn Chair");
                }
            }
            else
            {
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
            }

            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[SeatManager] Se instanciaron {allChairs.Count} sillas correctamente.");
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

        // [TargetRpc]
        private void RpcSitPlayer(/*NetworkConnection conn,*/ GameObject player, GameObject chairObj)
        {
            var chair = chairObj.GetComponent<ChairInteractable>();

            if (chair != null)
            {
                player.transform.position = chair.sitTransform.position;
                // Make the player always face the center of the table (viraPosition) and stay upright
                Vector3 lookTarget = (TableManager.Instance != null && TableManager.Instance.viraPosition != null) 
                    ? TableManager.Instance.viraPosition.position 
                    : (TableManager.Instance != null ? TableManager.Instance.transform.position : Vector3.zero);
                lookTarget.y = player.transform.position.y;
                player.transform.LookAt(lookTarget);
                
            }

            // Only update the camera if the player who is sitting down is the local player!
            // Every networked player carries a PlayerLocal component, so checking for
            // its presence is not enough — isLocalPlayer must be true.
            var seatedPlayerLocal = player.GetComponent<PlayerLocal>();
            if (seatedPlayerLocal == null) seatedPlayerLocal = player.GetComponentInChildren<PlayerLocal>(true);
            bool isLocal = seatedPlayerLocal != null && seatedPlayerLocal.isLocalPlayer;
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
        }
    }
}
