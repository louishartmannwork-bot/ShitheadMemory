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
    [SerializeField] private float cardThickness = 0.002f; // Tiny vertical offset so stacked cards don't z-fight

    private List<Card> deck = new List<Card>();
    private Stack<GameObject> physicalCardStack = new Stack<GameObject>();

    void Start()
    {
        InitializeAndSpawnDeck();
    }

    #region Deck Setup & Logic
    void InitializeAndSpawnDeck()
    {
        // Clear any existing physical cards
        while (physicalCardStack.Count > 0)
        {
            GameObject oldCard = physicalCardStack.Pop();
            if (oldCard != null) Destroy(oldCard);
        }

        deck.Clear();

        if (cardPrefab == null || deckSpawnPoint == null)
        {
            Debug.LogWarning("DeckManager_3D: Card Prefab or Deck Spawn Point is missing!");
            return;
        }

        // 1. Generate the standard 52 unique cards
        foreach (CardSuit suit in System.Enum.GetValues(typeof(CardSuit)))
        {
            foreach (CardValue val in System.Enum.GetValues(typeof(CardValue)))
            {
                deck.Add(new Card(suit, val, null));
            }
        }

        // 2. Shuffle the logical list thoroughly
        for (int i = 0; i < deck.Count; i++)
        {
            Card temp = deck[i];
            int randomIndex = Random.Range(i, deck.Count);
            deck[i] = deck[randomIndex];
            deck[randomIndex] = temp;
        }

        // 3. Instantiate physical cards matching the shuffled deck.
        for (int i = deck.Count - 1; i >= 0; i--)
        {
            Card cardData = deck[i];
            int stackIndex = deck.Count - 1 - i;
            Vector3 spawnPos = deckSpawnPoint.position + new Vector3(0f, stackIndex * cardThickness, 0f);

            // Spawn FACE DOWN on the deck
            Quaternion spawnRot = deckSpawnPoint.rotation * Quaternion.Euler(270f, 0f, Random.Range(-2f, 2f));

            GameObject cardObj = Instantiate(cardPrefab, spawnPos, spawnRot, transform);

            // --- ASSIGN UNIQUE DATA TO VIEW AND DRAGGABLE COMPONENTS ---
            cardObj.GetComponent<Card3DViewTest>()?.SetupCard(cardData);

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable == null)
            {
                draggable = cardObj.AddComponent<CardDraggable3D>();
            }
            draggable.Init(handManager, this, cardData);
            draggable.cardData = cardData;

            // Keep collider active so the draw pile stays functional
            Collider col = cardObj.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            // Freeze physics while sitting neatly in the stack
            Rigidbody rb = cardObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            physicalCardStack.Push(cardObj);
        }

        Debug.Log($"Initialized physical deck with {physicalCardStack.Count} unique face-down cards.");
    }

    public bool HasCards() => deck != null && deck.Count > 0;

    public bool TryDrawCard(out Card drawnCard)
    {
        bool success = TryDrawCardObject(out drawnCard, out GameObject cardObj);
        if (success && cardObj != null)
        {
            // Fallback clean-up if something only requested logical data
            Destroy(cardObj);
        }
        return success;
    }

    public bool TryDrawCardObject(out Card drawnCard, out GameObject cardObj)
    {
        if (HasCards())
        {
            drawnCard = deck[0];
            deck.RemoveAt(0);

            if (physicalCardStack.Count > 0)
            {
                cardObj = physicalCardStack.Pop();
            }
            else
            {
                cardObj = null;
            }

            return true;
        }

        drawnCard = null;
        cardObj = null;
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

        if (TryDrawCardObject(out Card cardData, out GameObject cardObj))
        {
            if (RuleManager_3D.Instance != null)
            {
                RuleManager_3D.Instance.player1Hand.Add(cardData);
            }

            if (cardObj != null)
            {
                Rigidbody rb = cardObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

                CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
                if (draggable != null)
                {
                    draggable.SetFaceDown(true); // <--- THIS WAS MISSING!
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