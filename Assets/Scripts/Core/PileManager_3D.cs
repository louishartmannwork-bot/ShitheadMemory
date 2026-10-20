using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class PileManager_3D : MonoBehaviour
{
    [Header("Pile Settings")]
    public Transform pileCenterPoint; // Optional: Der exakte Ablagepunkt auf dem Tisch

    // Liste aller Karten, die aktuell auf dem Ablagestapel liegen (Index 0 = ganz unten, Ende der Liste = ganz oben)
    private List<Card> pileCards = new List<Card>();
    private List<GameObject> pileCardObjects = new List<GameObject>();

    /// <string>
    /// Fügt eine neu ausgespielte Karte dem Ablagestapel hinzu.
    /// </string>
    public void AddCardToPile(Card cardData, GameObject cardObj)
    {
        if (cardData != null)
        {
            pileCards.Add(cardData);
            pileCardObjects.Add(cardObj);

            Debug.Log($"Karte {cardData.value} of {cardData.suit} auf Ablagestapel gelegt. Gesamtkarten auf Stapel: {pileCards.Count}");
        }
    }

    /// <summary>
    /// Gibt die oberste Karte zurück, die aktuell geschlagen werden muss.
    /// Gibt 'null' zurück, wenn der Stapel leer ist.
    /// </summary>
    public Card GetTopCard()
    {
        if (pileCards.Count > 0)
        {
            return pileCards[pileCards.Count - 1];
        }
        return null;
    }

    /// <summary>
    /// Optional: Leert den Ablagestapel (z.B. wenn er "weggebrannt" wird).
    /// </summary>
    public void ClearPile()
    {
        foreach (GameObject obj in pileCardObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }
        pileCards.Clear();
        pileCardObjects.Clear();
        Debug.Log("Ablagestapel wurde geleert/weggebrannt.");
    }
}