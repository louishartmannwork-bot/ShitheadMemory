using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class HandManager_3D : MonoBehaviour
{
    public static HandManager_3D Instance;

    [Header("References")]
    public DeckManager_3D deckManager;
    public GameObject cardPrefab;

    [Header("Hand Area Settings")]
    public Transform handAreaAnchor;

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

    public List<GameObject> currentHandCards = new List<GameObject>();
    private GameObject hoveredCard = null;
    private Vector3 defaultCardScale = Vector3.one;

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

    public bool IsCardInHand(GameObject cardObj) => currentHandCards.Contains(cardObj);

    public List<Card> GetPlayerHandCardData()
    {
        List<Card> cardDataList = new List<Card>();
        foreach (GameObject cardObj in currentHandCards)
        {
            if (cardObj != null)
            {
                CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
                if (draggable != null && draggable.cardData != null)
                {
                    cardDataList.Add(draggable.cardData);
                }
            }
        }
        return cardDataList;
    }

    public int GetHandCount()
    {
        currentHandCards.RemoveAll(card => card == null);
        return currentHandCards.Count;
    }

    #region Card Drawing & Adding
    public void DrawCardToPlayerHand()
    {
        TryDrawCardOrFromPenalty();
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
            if (cardObj != null) Destroy(cardObj);
        }
        currentHandCards.Clear();
    }
    #endregion

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
            if (draggable != null && (draggable.IsDragging || draggable.IsFlipping)) continue;

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

    public void AddExistingCardVisual(GameObject cardObj, Card cardData)
    {
        if (cardObj == null) return;

        Card3DViewTest cardView = cardObj.GetComponent<Card3DViewTest>();
        if (cardView != null)
        {
            cardView.SetupCard(cardData);
        }

        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable == null)
        {
            draggable = cardObj.AddComponent<CardDraggable3D>();
        }

        draggable.Init(this, deckManager, cardData);
        draggable.cardData = cardData;

        Collider col = cardObj.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        Rigidbody rb = cardObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        currentHandCards.Add(cardObj);
    }

    public void TryDrawCardOrFromPenalty()
    {
        // Consult TurnManager authoritative check
        if (TurnManager_3D.Instance != null && !TurnManager_3D.Instance.CanPlayerDrawCard())
        {
            Debug.Log("Kann aktuell keine Karte nachziehen.");
            return;
        }

        // 1. Try drawing from unified deck stack first
        if (deckManager != null && deckManager.HasCards())
        {
            if (deckManager.TryDrawCardObject(out Card drawnCard, out GameObject cardObj))
            {
                if (cardObj != null)
                {
                    AddExistingCardVisual(cardObj, drawnCard);
                    CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
                    if (draggable != null)
                    {
                        draggable.SetFaceDown(true);
                    }

                    TurnManager_3D.Instance?.OnCardAddedToHand();
                    return;
                }
            }
        }

        // 2. If deck is empty, draw from Player 1 Penalty Pile
        if (RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1PenaltyPile.Count > 0)
        {
            List<Card> penaltyPile = RuleManager_3D.Instance.player1PenaltyPile;
            int lastIndex = penaltyPile.Count - 1;
            Card penaltyCard = penaltyPile[lastIndex];
            penaltyPile.RemoveAt(lastIndex);

            Transform spawnPoint = RuleManager_3D.Instance.player1PenaltySpawnPoint;
            if (spawnPoint != null && spawnPoint.childCount > 0)
            {
                Transform topCardObj = spawnPoint.GetChild(spawnPoint.childCount - 1);
                Destroy(topCardObj.gameObject);
            }

            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position + Vector3.up * 0.2f : new Vector3(1.4f, 0.4f, 0f);
            GameObject spawnedCardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(75f, 0f, 0f));
            spawnedCardObj.GetComponent<Card3DViewTest>()?.SetupCard(penaltyCard);

            CardDraggable3D draggableComp = spawnedCardObj.GetComponent<CardDraggable3D>();
            if (draggableComp != null)
            {
                draggableComp.Init(this, deckManager, penaltyCard);
                draggableComp.cardData = penaltyCard;
                currentHandCards.Add(spawnedCardObj);
            }

            TurnManager_3D.Instance?.OnCardAddedToHand();
            return;
        }

        Debug.Log("Keine Karten mehr zum Nachziehen vorhanden!");
    }
    #endregion
}