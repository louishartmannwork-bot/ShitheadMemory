using TMPro;
using UnityEngine;
using ShitheadMemory;

public class Card3DViewTest : MonoBehaviour
{
    public TextMeshProUGUI cardText;

    public void SetupCard(Card card)
    {
        if (cardText != null)
        {
            cardText.text = $"{card.value}\nof\n{card.suit}s";

            if (card.suit == CardSuit.Spade)
            {
                cardText.color = Color.black;
            }
            else if (card.suit == CardSuit.Heart)
            {
                cardText.color = Color.red;
            }
            else if (card.suit == CardSuit.Club)
            {
                cardText.color = Color.blue;
            }
            else if (card.suit == CardSuit.Diamond)
            {
                cardText.color = Color.orange;
            }
        }
    }
}
