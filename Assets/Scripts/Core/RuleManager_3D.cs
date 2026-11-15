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

    [Header("Penalty Data / Prefabs")]
    public Transform player1PenaltySpawnPoint;
    public List<Card> player1PenaltyPile = new List<Card>();
    public Transform player2PenaltySpawnPoint;
    public List<Card> player2PenaltyPile = new List<Card>();
    public GameObject cardPrefab;

    [Header("Compatibility Flags")]
    public bool isPlayer1Turn = true;
    public bool freeTurn = false;

    public void ResetFreeTurn()
    {
        freeTurn = false;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    # region Rule Evaluation
    public bool IsMoveLegal(Card playedCard)
    {
        if (playedCard == null) return false;

        List<Card> currentDiscardPile = pileManager != null ? pileManager.GetDiscardPile() : null;
        if (currentDiscardPile == null || currentDiscardPile.Count == 0) return true;

        Card topCard = currentDiscardPile[currentDiscardPile.Count - 1];

        // 1. Aces are wild and always allowed
        if (playedCard.value == CardValue.Ace || topCard.value == CardValue.Ace) return true;

        // 2. Matching rank is always legal
        if (playedCard.value == topCard.value) return true;

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
        if (handCards == null) return legalMoves;

        foreach (Card card in handCards)
        {
            if (IsMoveLegal(card)) legalMoves.Add(card);
        }
        return legalMoves;
    }
    #endregion

    #region Drop Handling (Called from CardDraggable3D)
    public void HandleCardDroppedOnPile(Card cardData, GameObject cardObj)
    {
        if (IsMoveLegal(cardData))
        {
            playerHandManager?.RemoveCardFromHand(cardObj);
            PlayerHandCursor_3D.Instance?.NotifyCardReleased();

            pileManager?.AddCardToPile(cardData, cardObj);
            cardObj.transform.rotation = Quaternion.Euler(90, Random.Range(-10f, 10f), 0);

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            draggable?.EnablePhysics();

            TurnManager_3D.Instance?.OnPlayerCompletedMove();
            if (draggable != null) Destroy(draggable);
        }
        else
        {
            HandleIllegalTableDrop(cardData, cardObj, "Digger die Karte ísses halt einfach nicht");
        }
    }

    public void HandleIllegalTableDrop(Card cardData, GameObject cardObj, string message = "Bruder leg doch auf den ablagestapel")
    {
        OpponentDialogueManager_3D.Instance?.ShowComplaint(message);
        EnemyAI_3D.Instance?.RecordPlayerMistake();

        playerHandManager?.RemoveCardFromHand(cardObj);
        PlayerHandCursor_3D.Instance?.NotifyCardReleased();

        cardObj.transform.rotation = Quaternion.Euler(90, Random.Range(-10f, 10f), 0);

        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        draggable?.EnablePhysics();

        TurnManager_3D.Instance?.RegisterIllegalCardOnTable();
    }
    #endregion

    #region Penalty Logic
    public void TriggerPenaltyClickAction()
    {
        // Determine whether Player 1 (You) or Enemy took the penalty based on TurnManager state
        bool isP1 = TurnManager_3D.Instance != null && TurnManager_3D.Instance.IsPlayerTurn();

        List<Card> targetPenaltyList = isP1 ? player1PenaltyPile : player2PenaltyPile;
        Transform targetSpawn = isP1 ? player1PenaltySpawnPoint : player2PenaltySpawnPoint;

        List<Card> opponentCardDatas = isP1
            ? (enemyHandManager != null ? enemyHandManager.GetEnemyHandCardData() : new List<Card>())
            : (playerHandManager != null ? playerHandManager.GetPlayerHandCardData() : new List<Card>());

        List<GameObject> opponentCardObjs = isP1
            ? (enemyHandManager != null ? enemyHandManager.enemyHandCards : new List<GameObject>())
            : (playerHandManager != null ? playerHandManager.currentHandCards : new List<GameObject>());

        if (opponentCardDatas.Count == 0) return;

        // Clear the discard pile
        pileManager?.ClearPile();

        // Take opponent's hand into penalty list
        targetPenaltyList.AddRange(opponentCardDatas);

        // Wipe opponent's hand visually/logically
        if (isP1)
        {
            if (enemyHandManager != null)
            {
                foreach (GameObject obj in opponentCardObjs)
                {
                    if (obj != null) Destroy(obj);
                }
                enemyHandManager.enemyHandCards.Clear();
            }
        }
        else
        {
            if (playerHandManager != null)
            {
                playerHandManager.ClearPlayerHandVisuals();
            }
        }

        // Spawn penalty cards physically
        if (targetSpawn != null && cardPrefab != null)
        {
            for (int i = 0; i < opponentCardDatas.Count; i++)
            {
                Card penaltyCardData = opponentCardDatas[i];

                Vector3 dropSpawnPos = targetSpawn.position + new Vector3(
                    Random.Range(-0.04f, 0.04f),
                    0.5f + (i * 0.15f),
                    Random.Range(-0.04f, 0.04f)
                );

                Quaternion faceDownRot = targetSpawn.rotation * Quaternion.Euler(180f, Random.Range(0f, 360f), 0f);

                GameObject droppedCardObj = Instantiate(cardPrefab, dropSpawnPos, faceDownRot);
                droppedCardObj.GetComponent<Card3DViewTest>()?.SetupCard(penaltyCardData);

                Rigidbody rb = droppedCardObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.linearVelocity = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
                }

                CardDraggable3D draggable = droppedCardObj.GetComponent<CardDraggable3D>();
                if (draggable != null)
                {
                    draggable.SetFaceDown(true);
                    Destroy(draggable);
                }

                Collider col = droppedCardObj.GetComponent<Collider>();
                if (col != null) col.enabled = true;
            }
        }

        // Clean turn handoffs after penalty
        if (isP1)
        {
            // Enemy AI refills its own hand automatically
            for (int i = 0; i < 3; i++)
            {
                if (deckManager != null && deckManager.TryDrawCardObject(out Card drawnCard, out GameObject cardObj))
                {
                    enemyHandManager?.AddExistingCardVisual(cardObj, drawnCard); // <-- Call the correct method!
                }
            }

            // Hand control back explicitly to TurnManager for the player
            TurnManager_3D.Instance?.StartPlayerTurn();
            Debug.Log("Spieler hat Strafe genommen! Zug bleibt beim Spieler.");
        }
    }
    #endregion
}