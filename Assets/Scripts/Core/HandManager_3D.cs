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
    public float hoverYOffset = 0.2f;
    public float hoverZOffset = -0.3f;
    public float hoverScale = 1.15f;

    private List<GameObject> currentHandCards = new List<GameObject>();
    private GameObject hoveredCard = null;
    private Vector3 defaultCardScale = Vector3.one;
    public Card cardData;

    void Start()
    {
        if (cardPrefab != null)
        {
            defaultCardScale = cardPrefab.transform.localScale;
        }
    }

    void Update()
    {
        UpdateHandLayout();
    }
    public void TryDrawCardOrFromPenalty()
    {
        // 1. Handkarten-Begrenzung
        if (RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1Hand.Count >= 3)
        {
            Debug.Log("Du hast bereits 3 Karten auf der Hand!");
            return;
        }

        Card drawnCard = null;
        bool drawnFromPenalty = false;

        // 2. Fall A: Aus dem Haupt-Deck ziehen
        if (deckManager != null && deckManager.HasCards())
        {
            if (deckManager.TryDrawCard(out drawnCard))
            {
                drawnFromPenalty = false;
            }
        }
        // Fall B: Phase 2 – Aus dem Strafstapel ziehen
        else if (RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1PenaltyPile.Count > 0)
        {
            List<Card> penaltyPile = RuleManager_3D.Instance.player1PenaltyPile;
            int lastIndex = penaltyPile.Count - 1;
            drawnCard = penaltyPile[lastIndex];
            penaltyPile.RemoveAt(lastIndex);
            drawnFromPenalty = true;

            // Das oberste 3D-Objekt vom visuellen Strafstapel entfernen
            Transform spawnPoint = RuleManager_3D.Instance.player1PenaltySpawnPoint;
            if (spawnPoint != null && spawnPoint.childCount > 0)
            {
                Transform topCardObj = spawnPoint.GetChild(spawnPoint.childCount - 1);
                Destroy(topCardObj.gameObject);
            }
        }

        if (drawnCard == null)
        {
            Debug.Log("Keine Karten mehr zum Nachziehen vorhanden!");
            return;
        }

        // 3. Karten-Logik im RuleManager aktualisieren
        RuleManager_3D.Instance.player1Hand.Add(drawnCard);

        // 4. Visuelles Objekt erstellen
        Vector3 spawnPos = drawnFromPenalty
            ? RuleManager_3D.Instance.player1PenaltySpawnPoint.position + Vector3.up * 0.2f
            : new Vector3(1.4f, 0.4f, 0f);

        GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(75f, 0f, 0f));
        cardObj.GetComponent<Card3DViewTest>()?.SetupCard(drawnCard);

        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable != null)
        {
            draggable.Init(this, deckManager, drawnCard);
            draggable.cardData = drawnCard;
            currentHandCards.Add(cardObj);

            // WICHTIG: Sofort per Drag greifen, damit der Spieler sie an der Maus führt
            draggable.StartDraggingImmediately();
        }
    }
    #region Hand Layout & Animation
    void UpdateHandLayout()
    {
        int totalCards = currentHandCards.Count;
        if (totalCards == 0) return;

        float currentSpread = CalculateSpread(totalCards);

        for (int i = 0; i < totalCards; i++)
        {
            GameObject card = currentHandCards[i];
            if (card == null) continue;

            CardDraggable3D draggable = card.GetComponent<CardDraggable3D>();
            if (draggable != null && draggable.IsDragging) continue;

            float xPos = (i - (totalCards - 1) / 2.0f) * currentSpread;
            float yPos = baseHeightY + (i * layerOffsetY);
            float zPos = -1.5f;
            Vector3 targetScale = defaultCardScale;

            if (card == hoveredCard)
            {
                yPos += hoverYOffset + 0.2f;
                zPos += hoverZOffset;
                targetScale = defaultCardScale * hoverScale;
            }

            Vector3 targetPos = new Vector3(xPos, yPos, zPos);
            float rotationAngle = -xPos * 12f;
            Quaternion targetRot = Quaternion.Euler(60f, 0f, rotationAngle);

            CardAnimation_3D anim = card.GetComponent<CardAnimation_3D>();
            anim?.MoveTowards(targetPos, targetRot, targetScale, smoothSpeed);
        }
    }

    float CalculateSpread(int totalCards)
    {
        if (totalCards <= 1) return widthSpread;
        float totalCalculatedWidth = (totalCards - 1) * widthSpread;
        return totalCalculatedWidth > maxHandWidth ? maxHandWidth / (totalCards - 1) : widthSpread;
    }
    #endregion

    #region Card Management (Draw, Add, Remove)
    public void DrawCardToPlayerHand()
    {
        if (RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1Hand.Count >= 3)
        {
            Debug.Log("Du hast bereits 3 Karten auf der Hand!");
            return;
        }
        if (deckManager != null && deckManager.TryDrawCard(out Card drawnCard))
        {
            Debug.Log($"Gezogene Karte: {drawnCard.value} of {drawnCard.suit}");

            if (RuleManager_3D.Instance != null)
            {
                RuleManager_3D.Instance.player1Hand.Add(drawnCard);
                Debug.Log($"Karte erfolgreich zu RuleManager player1Hand hinzugefügt! Neue Anzahl: {RuleManager_3D.Instance.player1Hand.Count}");
            }
            else
            {
                Debug.LogError("RuleManager_3D.Instance ist NULL!");
            }

            Vector3 spawnPos = new Vector3(1.4f, 0.4f, 0f);
            GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(75f, 0f, 0f));

            cardObj.GetComponent<Card3DViewTest>()?.SetupCard(drawnCard);

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable != null)
            {
                // Übergib die Daten sauber über die Init-Methode:
                draggable.Init(this, deckManager, drawnCard);

                // Falls du direkt darauf zugreifen willst, mach cardData in CardDraggable3D public:
                draggable.cardData = drawnCard;

                currentHandCards.Add(cardObj);
                draggable.StartDraggingImmediately();
            }
        }
    }

    public void AddCardToHand(GameObject cardObj)
    {
        if (!currentHandCards.Contains(cardObj))
        {
            currentHandCards.Add(cardObj);
        }
        TurnManager_3D.Instance?.OnCardAddedToHand();
    }

    public void RemoveCardFromHand(GameObject cardObj)
    {
        if (currentHandCards.Contains(cardObj))
        {
            if (hoveredCard == cardObj) hoveredCard = null;
            currentHandCards.Remove(cardObj);
        }
    }
    public void ClearPlayerHandVisuals()
    {
        foreach (GameObject cardObj in currentHandCards)
        {
            if (cardObj != null)
            {
                Destroy(cardObj);
            }
        }
        currentHandCards.Clear();
    }

    /// <summary>
    /// Instanziiert eine Karte direkt in der Hand (wird für das automatische Nachziehen bei Strafen genutzt).
    /// </summary>
    public void AddCardVisualDirectly(Card cardData)
    {
        if (cardPrefab == null) return;

        Vector3 spawnPos = new Vector3(1.4f, 0.4f, 0f);
        GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(75f, 0f, 0f));

        cardObj.GetComponent<Card3DViewTest>()?.SetupCard(cardData);

        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable == null)
        {
            draggable = cardObj.AddComponent<CardDraggable3D>();
        }

        draggable.Init(this, deckManager, cardData);
        draggable.cardData = cardData;
        currentHandCards.Add(cardObj);
    }
    #endregion

    #region Hover & Input Interface
    public void SetHoveredCard(GameObject card) => hoveredCard = card;

    public void OnCardPickedUp(GameObject cardObj)
    {
        ClearHoveredCard(cardObj);
        cardObj.transform.localScale = defaultCardScale;
    }

    public void ClearHoveredCard(GameObject card)
    {
        if (hoveredCard == card) hoveredCard = null;
    }

    public void RedistributeHandCards() { }

    public void OnCardDroppedInHand(GameObject cardObj)
    {
        if (!currentHandCards.Contains(cardObj)) return;
        currentHandCards.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
    }

    public int GetHandCount()
    {
        currentHandCards.RemoveAll(card => card == null);
        return currentHandCards.Count;
    }
    #endregion
}