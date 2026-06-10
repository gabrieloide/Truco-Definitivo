using Mirror;
using Code.Networking;
using UnityEditor;
using UnityEngine;

namespace Code.Editor
{
    /// <summary>
    /// Ensures the Player prefab has NetworkIdentity + PlayerNetworkSync.
    /// Runs automatically on project load and can be triggered via Tools menu.
    /// </summary>
    [InitializeOnLoad]
    public static class NetworkPrefabSetup
    {
        static NetworkPrefabSetup()
        {
            // Defer to avoid "can't import during import" errors
            EditorApplication.delayCall += SetupPlayerPrefab;
        }

        [MenuItem("Tools/Setup Network Prefabs")]
        public static void SetupPlayerPrefab()
        {
            const string path = "Assets/Resources/Player.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[NetworkPrefabSetup] Player prefab not found at " + path);
                return;
            }

            bool changed = false;

            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root = scope.prefabContentsRoot;

                if (root.GetComponent<NetworkIdentity>() == null)
                {
                    root.AddComponent<NetworkIdentity>();
                    Debug.Log("[NetworkPrefabSetup] Added NetworkIdentity to Player prefab.");
                    changed = true;
                }

                if (root.GetComponent<PlayerNetworkSync>() == null)
                {
                    root.AddComponent<PlayerNetworkSync>();
                    Debug.Log("[NetworkPrefabSetup] Added PlayerNetworkSync to Player prefab.");
                    changed = true;
                }
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[NetworkPrefabSetup] Player prefab updated and saved.");
            }
        }
    }
}
