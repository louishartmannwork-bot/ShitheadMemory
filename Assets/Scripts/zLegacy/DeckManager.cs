using System.Collections.Generic;
using UnityEngine;
using ShitheadMemory;

public class DeckManager : MonoBehaviour
{
    public GameObject cardPrefab;
    public Transform handAreaTransform;

    private List<Card> deck = new List<Card>();

    public Sprite[] allCardSprites;

    void Start()
    {
        GenerateDeck();
        ShuffleDeck();
        DealInitialHand();
    }

    void GenerateDeck()
    {
        deck.Clear();

        foreach (CardSuit s in System.Enum.GetValues(typeof(CardSuit)))
        {
            foreach (CardValue v in System.Enum.GetValues(typeof(CardValue)))
            {
                //Sprite spriteToAssign = null;

                //Wenn Sprites da dann null austauschen
                Card newCard = new Card(s, v, null);
                deck.Add(newCard);
            }
        }

        Debug.Log($"Deck erfolgreich erstellt mit {deck.Count} Karten!");
    }
    void ShuffleDeck()
    {
        for (int i = 0; i < deck.Count; i++)
        {
            Card temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }
        Debug.Log("Deck erfolgreich gemischt!");
    }
    void DealInitialHand()
    {
        for (int i = 0; i < 4; i++)
        {
            if (deck.Count > 0)
            {
                Card drawnCard = deck[0];
                deck.RemoveAt(0);

                GameObject cardObj = Instantiate(cardPrefab, handAreaTransform);

                CardViewTest cardView = cardObj.GetComponent<CardViewTest>();
                if (cardView != null)
                {
                    cardView.SetupCard(drawnCard);
                }
            }
        }
    }
}
