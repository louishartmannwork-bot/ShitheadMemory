using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class EnemyHandManager_3D : MonoBehaviour
{
    [Header("References")]
    public DeckManager_3D deckManager;
    public GameObject cardPrefab;

    [Header("Enemy Hand Layout Settings")]
    public float widthSpread = 0.5f;
    public float maxHandWidth = 4.0f;
    public float baseHeightY = 0.8f; // Etwas höher auf dem Tisch
    public float baseZPos = 2.5f;    // Auf der Gegnerseite (weiter weg von der Kamera)
    public float layerOffsetY = 0.015f;
    public float smoothSpeed = 12f;

    private List<GameObject> enemyHandCards = new List<GameObject>();
    private Vector3 defaultCardScale = Vector3.one;

    private void Start()
    {
        if (cardPrefab != null)
        {
            defaultCardScale = cardPrefab.transform.localScale;
        }
    }

    private void Update()
    {
        int totalCards = enemyHandCards.Count;
        if (totalCards == 0) return;

        float currentSpread = widthSpread;
        if (totalCards > 1)
        {
            float totalCalculatedWidth = (totalCards - 1) * widthSpread;
            if (totalCalculatedWidth > maxHandWidth)
            {
                currentSpread = maxHandWidth / (totalCards - 1);
            }
        }

        for (int i = 0; i < totalCards; i++)
        {
            GameObject card = enemyHandCards[i];
            if (card == null) continue;

            float xPos = (i - (totalCards - 1) / 2.0f) * currentSpread;
            float yPos = baseHeightY + (i * layerOffsetY);
            float zPos = baseZPos;

            Vector3 targetPos = new Vector3(xPos, yPos, zPos);
            // Gegner-Karten leicht zum Tisch/Spieler geneigt
            Quaternion targetRot = Quaternion.Euler(75f, 180f, xPos * 10f);

            CardAnimation_3D anim = card.GetComponent<CardAnimation_3D>();
            if (anim != null)
            {
                anim.MoveTowards(targetPos, targetRot, defaultCardScale, smoothSpeed);
            }
            else
            {
                card.transform.position = targetPos;
                card.transform.rotation = targetRot;
            }
        }
    }

    /// <summary>
    /// Instanziiert eine visuelle Karte für die Gegnerhand und fügt sie dem Fächer hinzu.
    /// </summary>
    public void AddCardVisual(Card cardData)
    {
        if (cardPrefab == null) return;

        Vector3 spawnPos = new Vector3(0f, 1.0f, 2.5f);
        GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(75f, 180f, 0f));

        Card3DViewTest cardView = cardObj.GetComponent<Card3DViewTest>();
        if (cardView != null)
        {
            cardView.SetupCard(cardData);
        }

        // Gegner-Karten sind KI-gesteuert, daher brauchen sie kein Drag-Skript
        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable != null)
        {
            Destroy(draggable);
        }

        enemyHandCards.Add(cardObj);
    }

    /// <summary>
    /// Entfernt eine visuelle Karte aus der Gegnerhand (z.B. beim Ausspielen).
    /// </summary>
    public void RemoveCardVisual(int index)
    {
        if (index >= 0 && index < enemyHandCards.Count)
        {
            GameObject cardObj = enemyHandCards[index];
            enemyHandCards.RemoveAt(index);
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        }
    }

    /// <summary>
    /// Zerstört alle visuellen Handkarten des Gegners (wird bei Strafen aufgerufen).
    /// </summary>
    public void ClearAllVisuals()
    {
        foreach (GameObject cardObj in enemyHandCards)
        {
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        }
        enemyHandCards.Clear();
    }
}