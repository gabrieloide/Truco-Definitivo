using UnityEngine;
using UnityEditor;
using Code.GameLogic;
using System.Linq;
using System.Collections.Generic;

namespace Code.Editor
{
    public class AutoConfigureTable : MonoBehaviour
    {
        [MenuItem("TrucoTools/Auto Configure Table")]
        public static void AutoConfigure()
        {
            var seatManager = FindAnyObjectByType<SeatManager>();
            var tableManager = FindAnyObjectByType<TableManager>();

            if (seatManager == null || tableManager == null)
            {
                Debug.LogError("[AutoConfigure] No se encontró SeatManager o TableManager.");
                return;
            }

            // 1. Find all chairs
            var chairs = FindObjectsByType<ChairInteractable>(FindObjectsSortMode.None).ToList();
            if (chairs.Count == 0)
            {
                Debug.LogError("[AutoConfigure] No se encontraron sillas.");
                return;
            }

            // 2. Find center of table (Vira position)
            Vector3 center = tableManager.viraPosition != null ? tableManager.viraPosition.position : Vector3.zero;

            // 3. Sort chairs clockwise around the center
            // We use Atan2 to get the angle of each chair relative to the center
            chairs = chairs.OrderBy(c => 
            {
                Vector3 dir = c.transform.position - center;
                return Mathf.Atan2(dir.z, dir.x);
            }).ToList();

            seatManager.allChairs = chairs;
            EditorUtility.SetDirty(seatManager);

            // 4. Find all card positions (Transforms with "CardPosition" or similar)
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var cardPositions = allTransforms
                .Where(t => t.name.ToLower().Contains("cardpos") || t.name.ToLower().Contains("carddest"))
                .ToList();

            // 5. For each chair, assign the CLOSEST card position
            foreach (var chair in chairs)
            {
                if (cardPositions.Count > 0)
                {
                    Transform closestCardPos = cardPositions.OrderBy(cp => Vector3.Distance(chair.transform.position, cp.position)).First();
                    chair.cardDestination = closestCardPos;
                    EditorUtility.SetDirty(chair);
                    
                    // Remove from list so it's not assigned twice
                    cardPositions.Remove(closestCardPos);
                }
            }

            // Force Unity to save the changes in the scene
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            
            Debug.Log($"[AutoConfigure] ¡Éxito! Se configuraron {chairs.Count} sillas en orden circular y se asignaron sus destinos de cartas más cercanos.");
        }
    }
}
