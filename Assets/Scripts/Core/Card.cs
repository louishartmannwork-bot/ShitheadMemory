using UnityEngine;

namespace ShitheadMemory
{
    [System.Serializable]
    public class Card
    {
        public CardSuit suit;
        public CardValue value;
        public Sprite cardSprite;

        [Header("Card Memories")]
        public string memoryTitle;
        public string memoryDescription;
        public AudioClip memoryAudioClip;

        public Card(CardSuit suit, CardValue value, Sprite cardSprite, string title = "", string desc = "")
        {
            this.suit = suit;
            this.value = value;
            this.cardSprite = cardSprite;
            this.memoryTitle = title;
            this.memoryDescription = desc;
        }
    }
}