using UnityEngine;
using Code.GameLogic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Code.DebugTools
{
    /// <summary>
    /// Helper tool to quickly create and align Sit and Deck transforms for chairs.
    /// Attach this to a Chair or a parent object to help with setup.
    /// </summary>
    [ExecuteInEditMode]
    public class ChairSetupTool : MonoBehaviour
    {
        public float deckOffset = 0.6f; // Distance to the right
        public float deckForwardOffset = 0.2f; // Distance forward from seat

        [ContextMenu("Setup Anchors")]
        public void SetupAnchors()
        {
            ChairInteractable chair = GetComponent<ChairInteractable>();
            if (chair == null)
            {
                Debug.LogError("ChairSetupTool must be attached to a GameObject with a ChairInteractable component.");
                return;
            }

            // 1. Ensure Sit Transform exists
            if (chair.sitTransform == null)
            {
                GameObject sitObj = new GameObject("SitTransform");
                sitObj.transform.SetParent(transform);
                sitObj.transform.localPosition = Vector3.zero;
                sitObj.transform.localRotation = Quaternion.identity;
                chair.sitTransform = sitObj.transform;
            }

            // 2. Ensure Deck Anchor exists
            if (chair.deckAnchor == null)
            {
                GameObject deckObj = new GameObject("DeckAnchor");
                deckObj.transform.SetParent(transform);
                
                // Position it to the right and slightly forward relative to the chair's orientation
                // In Unity, transform.right is usually the right side.
                deckObj.transform.localPosition = new Vector3(deckOffset, 0, deckForwardOffset);
                deckObj.transform.localRotation = Quaternion.Euler(90, 0, 0); // Flat on the table
                
                chair.deckAnchor = deckObj.transform;
            }
            
            Debug.Log($"[ChairSetupTool] Anchors configured for {gameObject.name}");
        }

        private void OnDrawGizmosSelected()
        {
            ChairInteractable chair = GetComponent<ChairInteractable>();
            if (chair == null) return;

            if (chair.sitTransform != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(chair.sitTransform.position, 0.2f);
                Gizmos.DrawRay(chair.sitTransform.position, chair.sitTransform.forward * 0.5f);
            }

            if (chair.deckAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(chair.deckAnchor.position, new Vector3(0.15f, 0.01f, 0.25f));
                Gizmos.DrawRay(chair.deckAnchor.position, chair.deckAnchor.up * 0.2f);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ChairSetupTool))]
    public class ChairSetupToolEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ChairSetupTool tool = (ChairSetupTool)target;
            if (GUILayout.Button("Auto Setup Anchors"))
            {
                tool.SetupAnchors();
                EditorUtility.SetDirty(tool.gameObject.GetComponent<ChairInteractable>());
            }
        }
    }
#endif
}
