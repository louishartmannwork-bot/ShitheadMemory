using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class HandManager_3D : MonoBehaviour
{
    public static HandManager_3D Instance; // Singleton reference for cross-script access

    [Header("References")]
    public DeckManager_3D deckManager;
    public GameObject cardPrefab;

    [Header("Hand Area Settings")]
    public Transform handAreaAnchor; // Drag an empty GameObject here to position your hand area in the scene!

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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

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

    public bool IsCardInHand(GameObject cardObj)
    {
        return currentHandCards.Contains(cardObj);
    }

    public void TryDrawCardOrFromPenalty()
    {
        if (RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1Hand.Count >= 3)
        {
            Debug.Log("Du hast bereits 3 Karten auf der Hand!");
            return;
        }

        Card drawnCard = null;
        bool drawnFromPenalty = false;

        if (deckManager != null && deckManager.HasCards())
        {
            if (deckManager.TryDrawCard(out drawnCard))
            {
                drawnFromPenalty = false;
            }
        }
        else if (RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1PenaltyPile.Count > 0)
        {
            List<Card> penaltyPile = RuleManager_3D.Instance.player1PenaltyPile;
            int lastIndex = penaltyPile.Count - 1;
            drawnCard = penaltyPile[lastIndex];
            penaltyPile.RemoveAt(lastIndex);
            drawnFromPenalty = true;

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

        RuleManager_3D.Instance.player1Hand.Add(drawnCard);

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

            // --- PREVENT HAND LAYOUT FROM OVERRIDING DRAGGING OR FLIPPING ---
            if (draggable != null && (draggable.IsDragging || draggable.IsFlipping)) continue;
            // ----------------------------------------------------------------

            float xOffset = (i - (totalCards - 1) / 2.0f) * currentSpread;
            float yOffset = baseHeightY + (i * layerOffsetY);
            float zOffset = 0f;
            Vector3 targetScale = defaultCardScale;

            if (card == hoveredCard)
            {
                yOffset += hoverYOffset + 0.2f;
                zOffset += hoverZOffset;
                targetScale = defaultCardScale * hoverScale;
            }

            Vector3 targetPos;
            Quaternion targetRot;

            if (handAreaAnchor != null)
            {
                Vector3 localPos = new Vector3(xOffset, yOffset, zOffset);
                targetPos = handAreaAnchor.position + handAreaAnchor.TransformDirection(localPos);

                float rotationAngle = -xOffset * 12f;
                Quaternion localRot = Quaternion.Euler(60f, 0f, rotationAngle);
                targetRot = handAreaAnchor.rotation * localRot;
            }
            else
            {
                float xPos = xOffset;
                float yPos = yOffset;
                float zPos = -1.5f + zOffset;
                targetPos = new Vector3(xPos, yPos, zPos);

                float rotationAngle = -xPos * 12f;
                targetRot = Quaternion.Euler(60f, 0f, rotationAngle);
            }

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
            if (RuleManager_3D.Instance != null)
            {
                RuleManager_3D.Instance.player1Hand.Add(drawnCard);
            }

            Vector3 spawnPos = new Vector3(1.4f, 0.4f, 0f);
            GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(75f, 0f, 0f));

            cardObj.GetComponent<Card3DViewTest>()?.SetupCard(drawnCard);

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable != null)
            {
                draggable.Init(this, deckManager, drawnCard);
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