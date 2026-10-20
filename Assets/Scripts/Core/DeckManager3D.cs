using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class DeckManager3D : MonoBehaviour
{
    public GameObject cardPrefab;
    public Transform handStartingPosition;
    public float cardSpacing = 0.9f;

    private List<Card> deck = new List<Card>();
    private List<GameObject> currentHandCards = new List<GameObject>();

    private void Start()
    {
        GenerateDeck();
        ShuffleDeck();
        
        for (int i = 0; i < 3; i++)
        {
            DrawCardToHand();
        }
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

    public void DrawCardToHand()
    {
        if (deck.Count > 0)
        {
            Card drawnCard = deck[0];
            deck.RemoveAt(0);

            // Position deines Nachziehstapels (Passe X/Z an deinen DrawPile an)
            Vector3 spawnPos = new Vector3(1.4f, 0.4f, 0f);

            // Startet verdeckt auf dem Stapel (Rückseite nach oben)
            GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(-75f, 180f, 0f));

            Card3DViewTest cardView = cardObj.GetComponent<Card3DViewTest>();
            if (cardView != null)
            {
                cardView.SetupCard(drawnCard);
            }

            // Sofortiges Starten des Drag-Modus, sodass sie direkt an der Maus klebt!
            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable != null)
            {
                draggable.StartDraggingImmediately();
            }

            Debug.Log($"Karte vom Stapel gezogen! Verbleibende Karten: {deck.Count}");
        }
        else
        {
            Debug.Log("Der Nachziehstapel ist leer!");
        }
    }
}

