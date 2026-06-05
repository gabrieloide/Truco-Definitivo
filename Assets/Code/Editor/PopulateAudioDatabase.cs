using UnityEditor;
using UnityEngine;
using System.IO;
using Code.Scripts.Audio;

namespace Code.Editor
{
    public class PopulateAudioDatabase
    {
        [MenuItem("Tools/Populate Audio Database")]
        public static void Populate()
        {
            // Find the AudioDatabase asset
            AudioDatabase database = Resources.Load<AudioDatabase>("Audio/AudioDatabase");
            if (database == null)
            {
                database = AssetDatabase.LoadAssetAtPath<AudioDatabase>("Assets/Resources/Audio/AudioDatabase.asset");
            }

            if (database == null)
            {
                Debug.LogError("[PopulateAudioDatabase] Could not find AudioDatabase asset in Resources/Audio or Assets/Resources/Audio.");
                return;
            }

            // Find all wav files in Assets/Resources/Audio
            string audioFolderPath = Path.Combine(Application.dataPath, "Resources/Audio");
            if (!Directory.Exists(audioFolderPath))
            {
                Debug.LogError($"[PopulateAudioDatabase] Audio folder not found at: {audioFolderPath}");
                return;
            }

            string[] wavFiles = Directory.GetFiles(audioFolderPath, "*.wav");
            
            // Record what we add
            database.audioDataList.Clear();

            foreach (string filePath in wavFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string relativePath = "Assets/Resources/Audio/" + Path.GetFileName(filePath);
                
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(relativePath);
                if (clip != null)
                {
                    AudioData data = new AudioData
                    {
                        id = fileName,
                        clip = clip,
                        volume = 1f,
                        pitch = 1f,
                        loop = fileName == "backyard_truco" || fileName == "main_menu_truco"
                    };
                    database.audioDataList.Add(data);
                }
            }

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }
    }
}
