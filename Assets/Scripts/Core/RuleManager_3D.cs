using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

public class RuleManager_3D : MonoBehaviour
{
    public static RuleManager_3D Instance;

    [Header("References")]
    public PileManager_3D pileManager;
    public DeckManager_3D deckManager; // Hinzugefügt, damit nachgezogen werden kann

    [Header("Game State")]
    public bool isPlayer1Turn = true;
    public bool freeTurn = false;

    [Header("Spieler Hände & Strafstapel")]
    public List<Card> player1Hand = new List<Card>();
    public List<Card> player1PenaltyPile = new List<Card>();
    public Transform player1PenaltySpawnPoint;

    [Header("Visual Prefabs for Penalty")]
    public GameObject cardPrefab;

    public List<Card> player2Hand = new List<Card>();
    public List<Card> player2PenaltyPile = new List<Card>();
    public Transform player2PenaltySpawnPoint;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public bool IsMoveLegal(Card playedCard)
    {
        if (playedCard == null) return false;
        if (freeTurn) return true;

        List<Card> currentDiscardPile = pileManager != null ? pileManager.GetDiscardPile() : null;

        if (currentDiscardPile == null || currentDiscardPile.Count == 0) return true;

        Card topCard = currentDiscardPile[currentDiscardPile.Count - 1];

        if (playedCard.value == CardValue.Ace) return true;
        if (topCard.value == CardValue.Ace) return true;

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

        if (mustPlayHigher)
        {
            return playedValue >= topValue;
        }
        else
        {
            return playedValue <= topValue;
        }
    }

    public List<Card> GetLegalMoves(List<Card> handCards)
    {
        List<Card> legalMoves = new List<Card>();
        foreach (Card card in handCards)
        {
            if (IsMoveLegal(card))
            {
                legalMoves.Add(card);
            }
        }
        return legalMoves;
    }

    public void TriggerPenaltyClickAction()
    {
        // Wenn isPlayer1Turn true ist, wird Spieler 1 bestraft. 
        // Wenn false, wird Spieler 2 (die KI) bestraft.
        List<Card> activeHand = isPlayer1Turn ? player1Hand : player2Hand;
        List<Card> opponentHand = isPlayer1Turn ? player2Hand : player1Hand;
        List<Card> activePenaltyPile = isPlayer1Turn ? player1PenaltyPile : player2PenaltyPile;
        Transform targetSpawn = isPlayer1Turn ? player1PenaltySpawnPoint : player2PenaltySpawnPoint;

        // 1. Sicherheitscheck: Hat der aktive Spieler wirklich keine legalen Züge?
        List<Card> legalMoves = GetLegalMoves(activeHand);
        if (legalMoves.Count > 0)
        {
            Debug.Log("Es sind noch legale Züge vorhanden!");
            return;
        }

        Debug.Log($"Strafe wird ausgeführt für: {(isPlayer1Turn ? "Spieler 1" : "KI")}!");

        // 2. Die Handkarten des Gegners wandern auf den Strafstapel des Bestraften
        List<Card> cardsToTransfer = new List<Card>(opponentHand);
        activePenaltyPile.AddRange(opponentHand);
        opponentHand.Clear();

        // 3. Visuelle Hände aufräumen
        if (isPlayer1Turn)
        {
            // Spieler 1 bestraft -> KI-Hand visuell leeren
            EnemyHandManager_3D enemyHandManager = Object.FindAnyObjectByType<EnemyHandManager_3D>();
            enemyHandManager?.ClearAllVisuals();
        }
        else
        {
            // KI bestraft -> Spieler-Hand visuell leeren und Logik-Hand leeren
            HandManager_3D playerHandManager = Object.FindAnyObjectByType<HandManager_3D>();
            playerHandManager?.ClearPlayerHandVisuals();
            player1Hand.Clear();
        }

        // 4. Ablagestapel verbrennen
        if (pileManager != null)
        {
            pileManager.ClearPile();
        }

        // 5. Physikalische Karten auf den Strafstapel werfen
        if (targetSpawn != null && cardPrefab != null)
        {
            StartCoroutine(AnimatePenaltyCardsToPile(cardsToTransfer, targetSpawn));
        }

        // 6. Der Gegner zieht wieder auf 3 Karten nach (falls Deck nicht leer)
        if (deckManager != null && deckManager.HasCards())
        {
            while (opponentHand.Count < 3 && deckManager.HasCards())
            {
                if (deckManager.TryDrawCard(out Card drawn))
                {
                    opponentHand.Add(drawn);

                    if (isPlayer1Turn)
                    {
                        // KI zieht nach
                        EnemyAI_3D.Instance?.enemyHandManager?.AddCardVisual(drawn);
                    }
                    else
                    {
                        // Du ziehst nach
                        HandManager_3D playerHandManager = Object.FindAnyObjectByType<HandManager_3D>();
                        playerHandManager?.AddCardVisualDirectly(drawn);
                    }
                }
            }
        }

        // 7. FREIER ZUG: Geht exakt an denjenigen, der bestraft wurde und am Zug bleibt!
        freeTurn = true;

        // WICHTIG: Wir lassen `isPlayer1Turn` genau so, wie es war. 
        // Wenn die KI bestraft wurde, bleibt isPlayer1Turn = false, sodass die KI ihren freien Zug ausführen darf!
    }

    private System.Collections.IEnumerator AnimatePenaltyCardsToPile(List<Card> cards, Transform targetSpawn)
    {
        foreach (Card card in cards)
        {
            // 1. Spawn-Position: Leicht versetzt und genau 0.5 Einheiten ÜBER dem Strafstapel
            Vector3 randomOffset = new Vector3(Random.Range(-0.05f, 0.05f), 0.5f, Random.Range(-0.05f, 0.05f));
            Vector3 spawnPos = targetSpawn.position + randomOffset;

            // 2. Leichte Zufalls-Rotation für den natürlichen Look beim Fallen
            Quaternion spawnRot = Quaternion.Euler(
                (-90),
                Random.Range(-5f, 5f),
               Random.Range(0, 360f)
            );

            GameObject cardObj = Instantiate(cardPrefab, spawnPos, spawnRot);

            cardObj.transform.parent = targetSpawn;

            // Drag-Skript entfernen, damit der Spieler sie nicht im Flug greifen kann
            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable != null) Destroy(draggable);

            // 3. Physik aktivieren (OHNE Torque, damit sie nicht unkontrolliert wegfliegen)
            Rigidbody rb = cardObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // Minimaler, winziger Impuls für leichten Versatz beim Aufprall
                rb.AddForce(new Vector3(Random.Range(-0.03f, 0.03f), 0f, Random.Range(-0.03f, 0.03f)), ForceMode.Impulse);
            }

            // Kurze Verzögerung zwischen den Karten, damit sie nacheinander herabregnen
            yield return new WaitForSeconds(0.12f);
        }
    }
    public void ResetFreeTurn()
    {
        freeTurn = false;
    }
}