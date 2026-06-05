using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Code.Cards;
using System.IO;

namespace Code.Editor
{
    public class CardDataGenerator
    {
        [MenuItem("Truco/Generate Card Database")]
        public static void GenerateCards()
        {
            string folderPath = "Assets/Resources/Cards";

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Cards");
            }

            CardSuit[] suits = { CardSuit.Gold, CardSuit.Cup, CardSuit.Sword, CardSuit.Cudgel };
            
            // We use standard 1-12, skipping 8 and 9.
            int[] validValues = { 1, 2, 3, 4, 5, 6, 7, 10, 11, 12 };

            List<CardData> generatedCards = new List<CardData>();
            int currentId = 1;

            foreach (var suit in suits)
            {
                foreach (var val in validValues)
                {
                    string cardName = $"{suit}_{val}";
                    string assetPath = $"{folderPath}/{cardName}.asset";

                    CardData existingCard = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);

                    if (existingCard == null)
                    {
                        CardData newCard = ScriptableObject.CreateInstance<CardData>();
                        newCard.id = currentId;
                        newCard.suit = suit;
                        newCard.value = val;

                        AssetDatabase.CreateAsset(newCard, assetPath);
                        generatedCards.Add(newCard);
                    }
                    else
                    {
                        // Update existing just in case
                        existingCard.id = currentId;
                        existingCard.suit = suit;
                        existingCard.value = val;
                        EditorUtility.SetDirty(existingCard);
                        generatedCards.Add(existingCard);
                    }
                    currentId++;
                }
            }

            // Create or update Database
            string dbPath = "Assets/Resources/CardDatabase.asset";
            CardDatabase db = AssetDatabase.LoadAssetAtPath<CardDatabase>(dbPath);

            if (db == null)
            {
                db = ScriptableObject.CreateInstance<CardDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            db.ClearCards();
            foreach (var card in generatedCards)
            {
                db.AddCard(card);
            }
            
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

        }
    }
}
