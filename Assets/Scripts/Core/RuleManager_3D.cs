using ShitheadMemory;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Required for TextMeshPro UI

public class RuleManager_3D : MonoBehaviour
{
    public static RuleManager_3D Instance;

    public enum GamePhase
    {
        StandardPlay, // Deck has cards -> Must play LOWER or EQUAL (<=)
        EmptyDeck     // Deck is empty -> Must play HIGHER or EQUAL (>=)
    }

    [Header("Game Phase State")]
    public GamePhase currentPhase = GamePhase.StandardPlay;

    [Header("References")]
    public PileManager_3D pileManager;
    public DeckManager_3D deckManager;
    public HandManager_3D playerHandManager;
    public EnemyHandManager_3D enemyHandManager;

    [Header("Mistake & Patience Tracking")]
    public int playerMistakes = 0;
    public int maxMistakes = 10;
    public TextMeshProUGUI mistakeCounterText; // Drag your UI Text element here in the Inspector

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

    private void Start()
    {
        UpdateMistakeUI();
    }

    private void Update()
    {
        UpdateGamePhaseState();
    }

    public void UpdateGamePhaseState()
    {
        bool hasDeckCards = deckManager != null && deckManager.HasCards();

        if (hasDeckCards && currentPhase != GamePhase.StandardPlay)
        {
            currentPhase = GamePhase.StandardPlay;
            Debug.Log("[RuleManager] Phase switched to: STANDARD PLAY (Deck has cards)");
        }
        else if (!hasDeckCards && currentPhase != GamePhase.EmptyDeck)
        {
            currentPhase = GamePhase.EmptyDeck;
            Debug.Log("[RuleManager] Phase switched to: EMPTY DECK (Higher or equal rules active)");
        }
    }

    #region Rule Evaluation
    public bool IsMoveLegal(Card playedCard)
    {
        if (playedCard == null) return false;

        List<Card> currentDiscardPile = pileManager != null ? pileManager.GetDiscardPile() : null;
        if (currentDiscardPile == null || currentDiscardPile.Count == 0) return true;

        Card topCard = currentDiscardPile[currentDiscardPile.Count - 1];

        if (playedCard.value == CardValue.Ace || topCard.value == CardValue.Ace) return true;

        if (playedCard.value == topCard.value) return true;

        return EvaluateCustomRules(playedCard, currentDiscardPile);
    }

    private bool EvaluateCustomRules(Card playedCard, List<Card> discardPile)
    {
        UpdateGamePhaseState();

        int playedValue = (int)playedCard.value;
        Card topCard = discardPile[discardPile.Count - 1];
        int topValue = (int)topCard.value;

        bool mustBeHigher = (currentPhase == GamePhase.EmptyDeck);

        if (discardPile.Count >= 2)
        {
            Card secondTopCard = discardPile[discardPile.Count - 2];
            if (secondTopCard != null && topCard.value == secondTopCard.value)
            {
                mustBeHigher = !mustBeHigher;
                Debug.Log($"[RuleManager] Same rank back-to-back detected! Rule inverted. MustBeHigher: {mustBeHigher}");
            }
        }

        return mustBeHigher ? (playedValue >= topValue) : (playedValue <= topValue);
    }

    public List<Card> GetLegalMoves(List<Card> hand)
    {
        List<Card> legalMoves = new List<Card>();
        if (hand == null || hand.Count == 0) return legalMoves;

        UpdateGamePhaseState();

        Card topCard = pileManager != null ? pileManager.GetTopCard() : null;
        if (topCard == null)
        {
            return new List<Card>(hand);
        }

        foreach (Card card in hand)
        {
            if (card == null) continue;

            if (IsMoveLegal(card))
            {
                legalMoves.Add(card);
            }
        }

        return legalMoves;
    }
    #endregion

    #region Drop Handling
    public void HandleCardDroppedOnPile(Card cardData, GameObject cardObj)
    {
        if (TurnManager_3D.Instance != null && !TurnManager_3D.Instance.CanPlayerPlayCard())
        {
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.TryDrawWithFullHand);
            cardObj.GetComponent<CardDraggable3D>()?.ReturnToHand();
            return;
        }

        if (IsMoveLegal(cardData))
        {
            playerHandManager?.RemoveCardFromHand(cardObj);
            PlayerHandCursor_3D.Instance?.NotifyCardReleased();

            pileManager?.AddCardToPile(cardData, cardObj);
            cardObj.transform.rotation = Quaternion.Euler(90, Random.Range(-10f, 10f), 0);

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            draggable?.EnablePhysics();

            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.CardPlayed);

            TurnManager_3D.Instance?.OnPlayerCompletedMove();
            if (draggable != null) Destroy(draggable);
        }
        else
        {
            HandleIllegalTableDrop(cardData, cardObj);
        }
    }

    public void HandleIllegalTableDrop(Card cardData, GameObject cardObj, string message = "")
    {
        playerMistakes++;
        UpdateMistakeUI();

        // PASS THE MISTAKE COUNT HERE:
        EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.IllegalMove, playerMistakes);

        if (playerMistakes >= maxMistakes)
        {
            TurnManager_3D.Instance?.SetGameOver();
            return;
        }

        playerHandManager?.RemoveCardFromHand(cardObj);
        PlayerHandCursor_3D.Instance?.NotifyCardReleased();

        cardObj.transform.rotation = Quaternion.Euler(90, Random.Range(-10f, 10f), 0);

        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
        if (draggable != null)
        {
            draggable.EnablePhysics();
            draggable.MarkAsIllegalOnTable();
        }

        TurnManager_3D.Instance?.RegisterIllegalCardOnTable();
    }

    private void UpdateMistakeUI()
    {
        if (mistakeCounterText != null)
        {
            mistakeCounterText.text = $"{playerMistakes}";
        }
    }
    #endregion

    #region Penalty Logic
    public void TriggerPenaltyClickAction()
    {
        bool playerTakesPenalty = TurnManager_3D.Instance != null && TurnManager_3D.Instance.IsPlayerTurn();

        if (playerTakesPenalty)
        {
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.PlayerTakesPenalty);
        }
        else
        {
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.EnemyTakesPenalty);
        }

        List<Card> targetPenaltyList = playerTakesPenalty ? player1PenaltyPile : player2PenaltyPile;
        Transform targetSpawn = playerTakesPenalty ? player1PenaltySpawnPoint : player2PenaltySpawnPoint;

        List<Card> opponentCardDatas = playerTakesPenalty
            ? (enemyHandManager != null ? enemyHandManager.GetEnemyHandCardData() : new List<Card>())
            : (playerHandManager != null ? playerHandManager.GetPlayerHandCardData() : new List<Card>());

        List<GameObject> opponentCardObjs = playerTakesPenalty
            ? (enemyHandManager != null ? enemyHandManager.enemyHandCards : new List<GameObject>())
            : (playerHandManager != null ? playerHandManager.currentHandCards : new List<GameObject>());

        if (opponentCardDatas.Count == 0) return;

        pileManager?.ClearPile();
        targetPenaltyList.AddRange(opponentCardDatas);

        List<GameObject> cardsToMove = new List<GameObject>(opponentCardObjs);

        if (playerTakesPenalty)
        {
            enemyHandManager?.enemyHandCards.Clear();
            if (TurnManager_3D.Instance != null)
            {
                TurnManager_3D.Instance.TriggerEnemyRedrawAfterPenalty();
            }
        }
        else
        {
            playerHandManager?.currentHandCards.Clear();
        }

        for (int i = 0; i < cardsToMove.Count; i++)
        {
            GameObject cardObj = cardsToMove[i];
            if (cardObj == null) continue;

            CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
            if (draggable != null) draggable.DisablePhysics();

            Collider col = cardObj.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Vector3 stackOffset = new Vector3(Random.Range(-0.015f, 0.015f), i * 0.02f, Random.Range(-0.015f, 0.015f));
            Vector3 targetPos = targetSpawn.position + stackOffset;
            Quaternion targetRot = targetSpawn.rotation * Quaternion.Euler(180f, 0f, 0f);

            CardAnimation_3D anim = cardObj.GetComponent<CardAnimation_3D>();
            if (anim != null)
            {
                Vector3 hoverPos = targetPos + new Vector3(0f, 0.35f + (i * 0.04f), 0f);
                float staggerDelay = i * 0.08f;

                bool isLastCard = (i == cardsToMove.Count - 1);

                anim.PlayArcTween(hoverPos, targetRot, 0.35f, 0.2f, staggerDelay, () => {
                    cardObj.transform.SetParent(targetSpawn);

                    if (draggable != null)
                    {
                        draggable.SetFaceDown(true);
                        draggable.EnablePhysics();
                    }

                    if (col != null) col.enabled = true;

                    Rigidbody rb = cardObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;

                        rb.linearVelocity = new Vector3(Random.Range(-0.05f, 0.05f), -0.2f, Random.Range(-0.05f, 0.05f));
                    }

                    if (isLastCard && TurnManager_3D.Instance != null)
                    {
                        TurnManager_3D.Instance.CheckWinCondition();
                    }
                });
            }
            else
            {
                cardObj.transform.position = targetPos;
                cardObj.transform.rotation = targetRot;
                cardObj.transform.SetParent(targetSpawn);
                if (col != null) col.enabled = true;

                if (i == cardsToMove.Count - 1 && TurnManager_3D.Instance != null)
                {
                    TurnManager_3D.Instance.CheckWinCondition();
                }
            }
        }
    }

    public bool TryDrawFromPenaltyPile(bool isPlayer1, out Card drawnCard, out GameObject cardObj)
    {
        drawnCard = null;
        cardObj = null;

        List<Card> penaltyList = isPlayer1 ? player1PenaltyPile : player2PenaltyPile;
        Transform spawnPoint = isPlayer1 ? player1PenaltySpawnPoint : player2PenaltySpawnPoint;

        if (penaltyList == null || penaltyList.Count == 0) return false;

        int lastIndex = penaltyList.Count - 1;
        drawnCard = penaltyList[lastIndex];
        penaltyList.RemoveAt(lastIndex);

        if (spawnPoint != null && spawnPoint.childCount > 0)
        {
            Transform topCardTransform = spawnPoint.GetChild(spawnPoint.childCount - 1);
            cardObj = topCardTransform.gameObject;

            cardObj.transform.SetParent(null);
        }
        else
        {
            if (cardPrefab != null)
            {
                Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
                cardObj = Instantiate(cardPrefab, spawnPos, Quaternion.identity);
                cardObj.GetComponent<Card3DViewTest>()?.SetupCard(drawnCard);
            }
        }

        return drawnCard != null && cardObj != null;
    }
    #endregion
}