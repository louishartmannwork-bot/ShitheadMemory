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

        // Shuffle deck data list
        for (int i = 0; i < deck.Count; i++)
        {
            Card temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        // Spawn physical cards matching the exact order of the deck list
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
        if (TurnManager_3D.Instance != null && !TurnManager_3D.Instance.CanPlayerDrawCard())
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Du musst jetzt nicht nachziehen!");
            return;
        }

        if (handManager != null && handManager.GetPlayerHandCardData().Count >= 3)
        {
            Debug.Log("Du hast bereits 3 Karten auf der Hand!");
            return;
        }

        // Find where this card is in our physical stack
        int index = physicalCardStack.IndexOf(clickedCardObj);
        if (index != -1)
        {
            // Remove data and physical reference atomically
            Card drawnCard = deck[index];
            deck.RemoveAt(index);
            physicalCardStack.RemoveAt(index);

            // Unparent and send to hand
            clickedCardObj.transform.SetParent(null);
            handManager.AddExistingCardVisual(clickedCardObj, drawnCard);

            CardDraggable3D draggable = clickedCardObj.GetComponent<CardDraggable3D>();
            if (draggable != null)
            {
                draggable.SetFaceDown(true);
                draggable.StartDraggingImmediately();
            }
        }
    }
    public bool TryDrawCardObject(out Card drawnCard, out GameObject cardObj)
    {
        drawnCard = null;
        cardObj = null;

        if (HasCards())
        {
            // 1. Remove the top card data from the synced list
            drawnCard = deck[0];
            deck.RemoveAt(0);

            // 2. Pull the exact top physical card object from index 0
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

            // Unparent from deck hierarchy so it is officially out of the deck
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
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Du musst jetzt nicht nachziehen!");
            return;
        }

        if (handManager != null && handManager.GetPlayerHandCardData().Count >= 3)
        {
            Debug.Log("Du hast bereits 3 Karten auf der Hand!");
            return;
        }

        // Pull the exact existing physical card from the top of the stack
        if (TryDrawCardObject(out Card cardData, out GameObject cardObj))
        {
            if (handManager != null && cardObj != null)
            {
                // Reassign the existing physical card object directly to the player's hand (Zero instantiation!)
                handManager.AddExistingCardVisual(cardObj, cardData);

                CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
                if (draggable != null)
                {
                    draggable.SetFaceDown(true);
                    draggable.StartDraggingImmediately();
                }
            }
        }
        else
        {
            Debug.Log("Der Nachziehstapel ist leer!");
        }
    }
    #endregion
}