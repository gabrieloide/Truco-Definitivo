using UnityEngine;

namespace Code.Cards
{
    public enum CardSuit
    {
        Gold,
        Cup,
        Sword,
        Cudgel
    }

    [CreateAssetMenu(fileName = "NewCardData", menuName = "Truco/Card Data")]
    public class CardData : ScriptableObject
    {
        [Header("Identifiers")]
        public int id;
        public CardSuit suit;
        
        [Range(1, 12)]
        public int value;

        [Header("Visuals")]
        public Sprite cardSprite;
        public GameObject cardPrefab;

        public string GetDisplayName()
        {
            return $"{suit} #{value}";
        }
    }
}
