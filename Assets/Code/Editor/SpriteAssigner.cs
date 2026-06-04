using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Code.Cards;
using System.Linq;

namespace Code.Editor
{
    public class SpriteAssigner : UnityEditor.Editor
    {
        [MenuItem("Truco/Auto-Slice and Assign Sprites")]
        public static void SliceAndAssign()
        {
            if (Selection.activeObject == null || !(Selection.activeObject is Texture2D))
            {
                Debug.LogError("[Truco] Por favor, selecciona la imagen 'CardsSheet' en la ventana de Project primero.");
                return;
            }

            Texture2D texture = (Texture2D)Selection.activeObject;
            string path = AssetDatabase.GetAssetPath(texture);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            // 1. Configurar importador
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;

            int columns = 12;
            int rows = 5;
            int spriteWidth = texture.width / columns;
            int spriteHeight = texture.height / rows;

            List<SpriteMetaData> metaDataList = new List<SpriteMetaData>();

            string[] suits = { "Gold", "Cup", "Sword", "Cudgel" };

            // Las coordenadas de Y en Unity empiezan desde abajo. 
            // La fila superior de la imagen (Oros) será row 4 en Y.
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    SpriteMetaData smd = new SpriteMetaData();
                    
                    // Unity Y = 0 es abajo, así que invertimos la fila
                    int unityY = (rows - 1 - r);
                    
                    smd.rect = new Rect(c * spriteWidth, unityY * spriteHeight, spriteWidth, spriteHeight);
                    smd.alignment = 0; // Center
                    
                    if (r < 4) 
                    {
                        int cardValue = c + 1;
                        smd.name = $"{suits[r]}_{cardValue}";
                    }
                    else 
                    {
                        smd.name = $"Back_{c}";
                    }

                    metaDataList.Add(smd);
                }
            }

            importer.spritesheet = metaDataList.ToArray();
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log("[Truco] Imagen recortada exitosamente. Asignando sprites a CardDatabase...");

            // 2. Asignar a CardDatabase
            string dbPath = "Assets/Resources/CardDatabase.asset";
            CardDatabase db = AssetDatabase.LoadAssetAtPath<CardDatabase>(dbPath);

            if (db == null)
            {
                Debug.LogError("[Truco] No se encontró CardDatabase. Corre 'Generate Card Database' primero.");
                return;
            }

            // Cargar todos los sprites generados
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            List<Sprite> sprites = new List<Sprite>();
            foreach (var asset in assets)
            {
                if (asset is Sprite s) sprites.Add(s);
            }

            int assignedCount = 0;
            foreach (CardData card in db.GetAllCards())
            {
                string expectedSpriteName = $"{card.suit}_{card.value}";
                Sprite match = sprites.FirstOrDefault(s => s.name == expectedSpriteName);

                if (match != null)
                {
                    card.cardSprite = match;
                    EditorUtility.SetDirty(card);
                    assignedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Truco] Se han asignado {assignedCount} sprites correctamente a los ScriptableObjects!");
        }
    }
}
