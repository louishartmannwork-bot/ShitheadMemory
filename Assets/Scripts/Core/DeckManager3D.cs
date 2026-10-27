using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class DeckManager_3D : MonoBehaviour
{
    [Header("References")]
    public GameObject cardPrefab;
    public HandManager_3D handManager;
    public Transform deckSpawnPoint;

    private List<Card> deck = new List<Card>();

    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }

    #region Deck Setup & Logic
    void InitializeDeck()
    {
        deck.Clear();
        foreach (CardSuit suit in System.Enum.GetValues(typeof(CardSuit)))
        {
            foreach (CardValue val in System.Enum.GetValues(typeof(CardValue)))
            {
                deck.Add(new Card(suit, val, null));
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

    public bool HasCards() => deck != null && deck.Count > 0;

    public bool TryDrawCard(out Card drawnCard)
    {
        if (HasCards())
        {
            drawnCard = deck[0];
            deck.RemoveAt(0);
            return true;
        }
        drawnCard = null;
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

        if (TryDrawCard(out Card cardData))
        {
            // --- HIER WIRD DIE KARTE NUN SAUBER BEIM RULE MANAGER REGISTRIERT ---
            if (RuleManager_3D.Instance != null)
            {
                RuleManager_3D.Instance.player1Hand.Add(cardData);
            }
            // -------------------------------------------------------------------

            SpawnDraggableCardFromDeck(cardData);
        }
        else
        {
            Debug.Log("Der Nachziehstapel ist leer!");
        }
    }

    private void SpawnDraggableCardFromDeck(Card cardData)
    {
        if (cardPrefab == null) return;

        Vector3 spawnPos = deckSpawnPoint != null ? deckSpawnPoint.position : transform.position;
        GameObject cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.Euler(90f, 0f, 0f));

        cardObj.GetComponent<Card3DViewTest>()?.SetupCard(cardData);

        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable == null)
        {
            draggable = cardObj.AddComponent<CardDraggable3D>();
        }

        draggable.Init(handManager, this, cardData);
        draggable.cardData = cardData; // Sicherstellen, dass die Daten zugewiesen sind
        draggable.StartDraggingImmediately();
    }
    #endregion
}