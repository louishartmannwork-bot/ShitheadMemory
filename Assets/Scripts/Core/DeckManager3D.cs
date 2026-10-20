using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class DeckManager_3D : MonoBehaviour
{
    private List<Card> deck = new List<Card>();

    private void Start()
    {
        GenerateDeck();
        ShuffleDeck();
    }

    void GenerateDeck()
    {
        deck.Clear();
        foreach (CardSuit s in System.Enum.GetValues(typeof(CardSuit)))
        {
            foreach (CardValue v in System.Enum.GetValues(typeof(CardValue)))
            {
                Card newCard = new Card(s, v, null);
                deck.Add(newCard);
            }
        }
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
    }

    // Gibt die nächste Karte aus dem Deck zurück (falls noch da)
    public bool TryDrawCard(out Card drawnCard)
    {
        if (deck.Count > 0)
        {
            drawnCard = deck[0];
            deck.RemoveAt(0);
            Debug.Log($"Karte gezogen. Verbleibend im Deck: {deck.Count}");
            return true;
        }

        drawnCard = null;
        Debug.Log("Deck ist leer!");
        return false;
    }

    // Nimmt verworfene Karten wieder ins Deck auf
    public void ReturnCardToDeck(Card card)
    {
        deck.Add(card);
        Debug.Log($"Karte ins Deck zurückgelegt. Deck-Größe: {deck.Count}");
    }
}