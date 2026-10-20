using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class HandManager_3D : MonoBehaviour
{
    [Header("References")]
    public DeckManager_3D deckManager;
    public GameObject cardPrefab;

    [Header("Hand Layout Settings")]
    public float widthSpread = 0.6f;
    public float maxHandWidth = 4.5f;
    public float baseHeightY = 0.35f;
    public float layerOffsetY = 0.015f;
    public float smoothSpeed = 12f;

    [Header("Hover Settings")]
    public float hoverYOffset = 0.2f;      // Wie weit die Karte nach oben wandert
    public float hoverZOffset = -0.3f;     // Wie weit die Karte Richtung Kamera kommt
    public float hoverScale = 1.15f;       // Wie viel größer die Karte wird (1.15 = 15% größer)

    private List<GameObject> currentHandCards = new List<GameObject>();
    private GameObject hoveredCard = null;
    private Vector3 defaultCardScale = Vector3.one;

    private void Start()
    {
        // Speichert die Original-Skalierung des Prefabs ab, damit nichts verzerrt wird
        if (cardPrefab != null)
        {
            defaultCardScale = cardPrefab.transform.localScale;
        }
    }

    public void SetHoveredCard(GameObject card)
    {
        hoveredCard = card;
    }

    public void ClearHoveredCard(GameObject card)
    {
        if (hoveredCard == card)
        {
            hoveredCard = null;
        }
    }

    private void Update()
    {
        int totalCards = currentHandCards.Count;
        if (totalCards == 0) return;

        // Dynamische Breitenanpassung, falls viele Karten in der Hand sind
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
            GameObject card = currentHandCards[i];
            if (card == null) continue;

            CardDraggable3D draggable = card.GetComponent<CardDraggable3D>();
            if (draggable != null && draggable.IsDragging) continue; // Während des Ziehens nicht stören

            float xPos = (i - (totalCards - 1) / 2.0f) * currentSpread;
            float yPos = baseHeightY + (i * layerOffsetY);
            float zPos = -1.5f;
            Vector3 targetScale = defaultCardScale; // Nutzt die echte Basis-Skalierung

            // HOVER-EFFEKT: Wenn diese Karte aktuell gehievt wird
            if (card == hoveredCard)
            {
                yPos += hoverYOffset;                     // Anheben
                zPos += hoverZOffset;                     // Näher an die Kamera
                yPos += 0.2f;                             // In den Vordergrund schieben
                targetScale = defaultCardScale * hoverScale; // Relativ zur Originalgröße vergrößern
            }

            Vector3 targetPos = new Vector3(xPos, yPos, zPos);
            float rotationAngle = -xPos * 12f;
            Quaternion targetRot = Quaternion.Euler(60f, 0f, rotationAngle);

            CardAnimation_3D anim = card.GetComponent<CardAnimation_3D>();
            if (anim != null)
            {
                anim.MoveTowards(targetPos, targetRot, targetScale, smoothSpeed);
            }
        }
    }

    // Wird vom Nachziehstapel aufgerufen
    public void DrawCardToPlayerHand()
    {
        if (deckManager != null && deckManager.TryDrawCard(out Card drawnCard))
        {
            Vector3 spawnPos = new Vector3(1.4f, 0.4f, 0f);
            GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(75f, 0f, 0f));

            Card3DViewTest cardView = cardObj.GetComponent<Card3DViewTest>();
            if (cardView != null)
            {
                cardView.SetupCard(drawnCard);
            }

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable != null)
            {
                draggable.Init(this, deckManager, drawnCard);
                currentHandCards.Add(cardObj);
                draggable.StartDraggingImmediately();
            }
        }
    }

    // Entfernt eine Karte aus der Hand-Liste
    public void RemoveCardFromHand(GameObject cardObj)
    {
        if (currentHandCards.Contains(cardObj))
        {
            if (hoveredCard == cardObj) hoveredCard = null;
            currentHandCards.Remove(cardObj);
        }
    }

    // Schnittstelle für Draggable-Skripte
    public void RedistributeHandCards() { }

    // Sortiert die Handkarten nach ihrer X-Position, wenn sie in der Hand abgelegt werden
    public void OnCardDroppedInHand(GameObject cardObj)
    {
        if (!currentHandCards.Contains(cardObj)) return;
        currentHandCards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
    }
}