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

            // 3. Sort chairs counter-clockwise around the center
            // We use Atan2 to get the angle of each chair relative to the center
            chairs = chairs.OrderBy(c => 
            {
                Vector3 dir = c.transform.position - center;
                return Mathf.Atan2(dir.z, dir.x);
            }).ToList();

            // Rotate the list so that the chair named "Chair1" is at index 0 (so the player is first)
            int playerChairIndex = chairs.FindIndex(c => c.name.ToLower().Contains("chair1"));
            if (playerChairIndex != -1)
            {
                var rotatedChairs = new List<ChairInteractable>();
                for (int i = 0; i < chairs.Count; i++)
                {
                    rotatedChairs.Add(chairs[(playerChairIndex + i) % chairs.Count]);
                }
                chairs = rotatedChairs;
            }
            else
            {
            }

            seatManager.allChairs = chairs;
            EditorUtility.SetDirty(seatManager);

            // 4. Find all card positions (Transforms with "CardPosition" or similar, excluding hands)
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var cardPositions = allTransforms
                .Where(t => t.name.ToLower().Contains("cardpos") || t.name.ToLower().Contains("carddest"))
                .Where(t => !t.name.ToLower().Contains("hand"))
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
            
        }
    }
}
