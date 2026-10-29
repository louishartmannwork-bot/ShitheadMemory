using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class RuleManager_3D : MonoBehaviour
{
    public static RuleManager_3D Instance;

    [Header("References")]
    public PileManager_3D pileManager;
    public DeckManager_3D deckManager;
    public HandManager_3D playerHandManager;
    public EnemyHandManager_3D enemyHandManager;

    [Header("Game State")]
    public bool isPlayer1Turn = true;
    public bool freeTurn = false;

    [Header("Player 1 (Human) Data")]
    public List<Card> player1Hand = new List<Card>();
    public List<Card> player1PenaltyPile = new List<Card>();
    public Transform player1PenaltySpawnPoint;

    [Header("Player 2 (AI) Data")]
    public List<Card> player2Hand = new List<Card>();
    public List<Card> player2PenaltyPile = new List<Card>();
    public Transform player2PenaltySpawnPoint;

    [Header("Visual Prefabs")]
    public GameObject cardPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool IsMoveLegal(Card playedCard)
    {
        if (playedCard == null) return false;
        if (freeTurn) return true;

        List<Card> currentDiscardPile = pileManager != null ? pileManager.GetDiscardPile() : null;
        if (currentDiscardPile == null || currentDiscardPile.Count == 0) return true;

        Card topCard = currentDiscardPile[currentDiscardPile.Count - 1];

        if (playedCard.value == CardValue.Ace || topCard.value == CardValue.Ace) return true;

        return EvaluateCustomRules(playedCard, currentDiscardPile);
    }

    private bool EvaluateCustomRules(Card playedCard, List<Card> discardPile)
    {
        int playedValue = (int)playedCard.value;
        Card topCard = discardPile[discardPile.Count - 1];
        int topValue = (int)topCard.value;

        bool mustPlayHigher = false;
        if (discardPile.Count >= 2)
        {
            Card secondTopCard = discardPile[discardPile.Count - 2];
            if (topCard.value == secondTopCard.value)
            {
                mustPlayHigher = true;
            }
        }

        return mustPlayHigher ? (playedValue >= topValue) : (playedValue <= topValue);
    }

    public List<Card> GetLegalMoves(List<Card> handCards)
    {
        List<Card> legalMoves = new List<Card>();
        foreach (Card card in handCards)
        {
            if (IsMoveLegal(card)) legalMoves.Add(card);
        }
        return legalMoves;
    }

    public void TriggerPenaltyClickAction()
    {
        List<Card> activeHand = isPlayer1Turn ? player1Hand : player2Hand;
        List<Card> opponentHand = isPlayer1Turn ? player2Hand : player1Hand;
        List<Card> activePenaltyPile = isPlayer1Turn ? player1PenaltyPile : player2PenaltyPile;
        Transform targetSpawn = isPlayer1Turn ? player1PenaltySpawnPoint : player2PenaltySpawnPoint;

        List<Card> legalMoves = GetLegalMoves(activeHand);
        if (legalMoves.Count > 0)
        {
            Debug.Log("Es sind noch legale Züge vorhanden!");
            return;
        }

        Debug.Log($"Strafe ausgeführt für: {(isPlayer1Turn ? "Spieler 1" : "KI")}");

        List<Card> cardsToTransfer = new List<Card>(opponentHand);
        activePenaltyPile.AddRange(opponentHand);
        opponentHand.Clear();

        if (isPlayer1Turn)
        {
            enemyHandManager?.ClearAllVisuals();
        }
        else
        {
            player1Hand.Clear();
            playerHandManager?.ClearPlayerHandVisuals();
        }

        pileManager?.ClearPile();

        if (targetSpawn != null && cardPrefab != null)
        {
            StartCoroutine(AnimatePenaltyCardsToPile(cardsToTransfer, targetSpawn));
        }

        while (opponentHand.Count < 3 && deckManager != null && deckManager.HasCards())
        {
            if (deckManager.TryDrawCard(out Card drawn))
            {
                opponentHand.Add(drawn);
                if (isPlayer1Turn) enemyHandManager?.AddCardVisual(drawn);
                else playerHandManager?.AddCardVisualDirectly(drawn);
            }
        }

        freeTurn = true;
    }

    private System.Collections.IEnumerator AnimatePenaltyCardsToPile(List<Card> cards, Transform targetSpawn)
    {
        foreach (Card card in cards)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-0.05f, 0.05f), 0.5f, Random.Range(-0.05f, 0.05f));
            Quaternion spawnRot = Quaternion.Euler(-90, Random.Range(-5f, 5f), Random.Range(0, 360f));

            GameObject cardObj = Instantiate(cardPrefab, targetSpawn.position + randomOffset, spawnRot);
            cardObj.transform.parent = targetSpawn;

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable != null) Destroy(draggable);

            Rigidbody rb = cardObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(new Vector3(Random.Range(-0.03f, 0.03f), 0f, Random.Range(-0.03f, 0.03f)), ForceMode.Impulse);
            }

            yield return new WaitForSeconds(0.12f);
        }
    }

    public void ResetFreeTurn() => freeTurn = false;
}