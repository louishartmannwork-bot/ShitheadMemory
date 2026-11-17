using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class DeckManager_3D : MonoBehaviour
{
    [Header("References")]
    public GameObject cardPrefab;
    public HandManager_3D handManager;
    public Transform deckSpawnPoint;

    [Header("Deck Stack Settings")]
    [SerializeField] private float cardThickness = 0.002f;

    public List<Card> deck = new List<Card>();
    public List<GameObject> physicalCardStack = new List<GameObject>();

    void Start()
    {
        InitializeAndSpawnDeck();
    }

    #region Deck Setup & Logic
    void InitializeAndSpawnDeck()
    {
        foreach (GameObject oldCard in physicalCardStack)
        {
            if (oldCard != null) Destroy(oldCard);
        }

        physicalCardStack.Clear();
        deck.Clear();

        if (cardPrefab == null || deckSpawnPoint == null)
        {
            Debug.LogWarning("DeckManager_3D: Card Prefab or Deck Spawn Point is missing!");
            return;
        }

        foreach (CardSuit suit in System.Enum.GetValues(typeof(CardSuit)))
        {
            foreach (CardValue val in System.Enum.GetValues(typeof(CardValue)))
            {
                deck.Add(new Card(suit, val, null));
            }
        }

        for (int i = 0; i < deck.Count; i++)
        {
            Card temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        for (int i = deck.Count - 1; i >= 0; i--)
        {
            Card cardData = deck[i];
            int stackIndex = deck.Count - 1 - i;
            Vector3 spawnPos = deckSpawnPoint.position + new Vector3(0f, stackIndex * cardThickness, 0f);
            Quaternion spawnRot = deckSpawnPoint.rotation * Quaternion.Euler(270f, 0f, Random.Range(-2f, 2f));

            GameObject cardObj = Instantiate(cardPrefab, spawnPos, spawnRot, transform);

            cardObj.GetComponent<Card3DViewTest>()?.SetupCard(cardData);

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable == null)
            {
                draggable = cardObj.AddComponent<CardDraggable3D>();
            }
            draggable.Init(handManager, this, cardData);
            draggable.cardData = cardData;

            Collider col = cardObj.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            Rigidbody rb = cardObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            physicalCardStack.Add(cardObj);
        }
    }

    public bool HasCards() => deck != null && deck.Count > 0 && physicalCardStack != null && physicalCardStack.Count > 0;

    public bool TryDrawCard(out Card drawnCard)
    {
        bool success = TryDrawCardObject(out drawnCard, out GameObject cardObj);
        if (success && cardObj != null)
        {
            Destroy(cardObj);
        }
        return success;
    }

    public void OnDeckCardClicked(GameObject clickedCardObj)
    {
        int handCount = handManager != null ? handManager.GetPlayerHandCardData().Count : -1;
        bool canDraw = TurnManager_3D.Instance != null && TurnManager_3D.Instance.CanPlayerDrawCard();

        if (!canDraw)
        {
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.RefuseToDraw);
            return;
        }

        if (handCount >= 3)
        {
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.TryDrawWithFullHand);
            return;
        }

        if (TryDrawCardObject(out Card drawnCard, out GameObject cardObj))
        {
            if (handManager != null && cardObj != null)
            {
                handManager.AddExistingCardVisual(cardObj, drawnCard);

                CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
                if (draggable != null)
                {
                    draggable.SetFaceDown(true);
                    draggable.StartDraggingImmediately();
                }

                TurnManager_3D.Instance?.OnCardDrawn();
            }
        }
    }

    public bool TryDrawCardObject(out Card drawnCard, out GameObject cardObj)
    {
        drawnCard = null;
        cardObj = null;

        if (HasCards())
        {
            drawnCard = deck[0];
            deck.RemoveAt(0);

            while (physicalCardStack.Count > 0)
            {
                GameObject candidate = physicalCardStack[0];
                physicalCardStack.RemoveAt(0);

                if (candidate == null) continue;

                if (HandManager_3D.Instance != null && HandManager_3D.Instance.IsCardInHand(candidate))
                {
                    continue;
                }

                cardObj = candidate;
                break;
            }

            if (cardObj != null)
            {
                cardObj.transform.SetParent(null);
            }

            return true;
        }

        return false;
    }
    #endregion

    #region Click & Spawn Interaction
    public void OnDeckClicked()
    {
        if (TurnManager_3D.Instance != null && !TurnManager_3D.Instance.CanPlayerDrawCard())
        {
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.RefuseToDraw);
            return;
        }

        if (handManager != null && handManager.GetPlayerHandCardData().Count >= 3)
        {
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.TryDrawWithFullHand);
            return;
        }

        if (TryDrawCardObject(out Card cardData, out GameObject cardObj))
        {
            if (handManager != null && cardObj != null)
            {
                handManager.AddExistingCardVisual(cardObj, cardData);

                CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
                if (draggable != null)
                {
                    draggable.SetFaceDown(true);
                    draggable.StartDraggingImmediately();
                }
            }
        }
    }
    #endregion
}