using ShitheadMemory;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI_3D : MonoBehaviour
{
    public static EnemyAI_3D Instance;

    [Header("References")]
    public PileManager_3D pileManager;
    public DeckManager_3D deckManager;
    public EnemyHandManager_3D enemyHandManager;

    [Header("Mistake Tracking")]
    public int playerMistakes = 0;
    public int maxMistakes = 10;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        Invoke(nameof(DrawInitialCards), 0.5f);
    }

    public void DrawInitialCards()
    {
        if (deckManager == null || enemyHandManager == null) return;
        for (int i = 0; i < 3; i++)
        {
            if (deckManager.TryDrawCardObject(out Card drawnCard, out GameObject cardObj))
            {
                if (cardObj != null)
                {
                    enemyHandManager.AddExistingCardVisual(cardObj, drawnCard);
                }
            }
        }
    }
    public void PlayTurn()
    {
        if (RuleManager_3D.Instance == null || enemyHandManager == null) return;

        StartCoroutine(CheckAndDrawCardsRoutine(() => {
            ExecuteEnemyMoveLogic();
        }));
    }

    private void ExecuteEnemyMoveLogic()
    {
        List<Card> currentHand = enemyHandManager.GetEnemyHandCardData();
        List<Card> legalMoves = RuleManager_3D.Instance.GetLegalMoves(currentHand);

        if (legalMoves.Count > 0)
        {
            Card cardToPlay = ChooseLowestLegalCard(legalMoves);

            PlayCard(cardToPlay);

            StartCoroutine(CheckAndDrawCardsRoutine(() => {

                int remainingHandCount = enemyHandManager.GetEnemyHandCardData().Count;
                bool deckEmpty = RuleManager_3D.Instance.deckManager != null && !RuleManager_3D.Instance.deckManager.HasCards();
                bool penaltyEmpty = RuleManager_3D.Instance.player2PenaltyPile.Count == 0;

                if (deckEmpty && remainingHandCount == 0 && penaltyEmpty)
                {
                    Debug.Log("[WinCondition] ENEMY PLACED FINAL CARD AND WON!");
                    TurnManager_3D.Instance?.CheckWinCondition();
                }
                else
                {
                    TurnManager_3D.Instance?.ScheduleEndEnemyTurn(0.8f);
                }
            }));
        }
        else
        {
            ExecuteEnemyPenalty();

            TurnManager_3D.Instance?.CheckWinCondition();
            TurnManager_3D.Instance?.ScheduleEndEnemyTurn(0.8f);
        }
    }

    private Card ChooseLowestLegalCard(List<Card> legalMoves)
    {
        legalMoves.Sort((a, b) => ((int)a.value).CompareTo((int)b.value));
        return legalMoves[0];
    }

    public void PlayCard(Card card)
    {
        if (card == null) return;

        List<Card> currentHand = enemyHandManager.GetEnemyHandCardData();
        List<Card> legalMoves = RuleManager_3D.Instance != null ? RuleManager_3D.Instance.GetLegalMoves(currentHand) : new List<Card>();

        if (!legalMoves.Contains(card))
        {
            Debug.LogError($"[AI PlayCard] BLOCKED: AI tried to play illegal card {card.value} of {card.suit}!");
            return;
        }
        int index = currentHand.IndexOf(card);

        GameObject cardToPlayObj = null;
        int targetIndex = -1;

        if (index >= 0 && index < enemyHandManager.enemyHandCards.Count)
        {
            targetIndex = index;
            cardToPlayObj = enemyHandManager.enemyHandCards[index];
        }
        else
        {
            for (int i = 0; i < enemyHandManager.enemyHandCards.Count; i++)
            {
                GameObject obj = enemyHandManager.enemyHandCards[i];
                if (obj != null)
                {
                    targetIndex = i;
                    cardToPlayObj = obj;
                    break;
                }
            }
        }

        if (cardToPlayObj == null || targetIndex == -1)
        {
            Debug.LogError("[AI PlayCard] ABORTED: No valid physical card object found in enemy hand!");
            return;
        }

        enemyHandManager.RemoveCardVisual(targetIndex);
        RuleManager_3D.Instance?.ResetFreeTurn();

        if (pileManager != null)
        {
            CardAnimation_3D anim = cardToPlayObj.GetComponent<CardAnimation_3D>();
            Collider col = cardToPlayObj.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (anim != null)
            {
                anim.PlayTween(pileManager.transform.position + new Vector3(0, 0.6f, 0), Quaternion.Euler(75f, Random.Range(-10f, 10f), 0f), 0.4f, () => {
                    CardDraggable3D draggable = cardToPlayObj.GetComponent<CardDraggable3D>();
                    if (draggable != null) Destroy(draggable);

                    if (col != null) col.enabled = true;

                    Rigidbody rb = cardToPlayObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                    }

                    pileManager.AddCardToPile(card, cardToPlayObj);
                });
            }
            else
            {
                pileManager.AddCardToPile(card, cardToPlayObj);
            }
        }
    }

    public void CheckAndDrawCards()
    {
        StartCoroutine(CheckAndDrawCardsRoutine(null));
    }

    private IEnumerator CheckAndDrawCardsRoutine(System.Action onComplete)
    {
        if (enemyHandManager == null) yield break;

        int safetyCounter = 0;

        while (enemyHandManager.GetEnemyHandCardData().Count < 3 && safetyCounter < 10)
        {
            safetyCounter++;

            DeckManager_3D deckMgr = RuleManager_3D.Instance != null ? RuleManager_3D.Instance.deckManager : null;
            if (deckMgr == null) deckMgr = FindAnyObjectByType<DeckManager_3D>();

            bool deckHasCards = deckMgr != null && deckMgr.HasCards();
            bool hasPenaltyCards = RuleManager_3D.Instance != null && RuleManager_3D.Instance.player2PenaltyPile.Count > 0;

            if (!deckHasCards && !hasPenaltyCards)
            {
                break;
            }

            bool successfullyDrew = false;

            if (deckHasCards)
            {
                if (deckMgr.deck != null && deckMgr.deck.Count > 0)
                {
                    int lastIdx = deckMgr.deck.Count - 1;
                    Card drawnCard = deckMgr.deck[lastIdx];
                    deckMgr.deck.RemoveAt(lastIdx);

                    GameObject cardObj = null;
                    if (deckMgr.physicalCardStack != null && deckMgr.physicalCardStack.Count > 0)
                    {
                        int physicalLastIdx = deckMgr.physicalCardStack.Count - 1;
                        cardObj = deckMgr.physicalCardStack[physicalLastIdx];
                        deckMgr.physicalCardStack.RemoveAt(physicalLastIdx);
                        if (cardObj != null) cardObj.transform.SetParent(null);
                    }

                    if (cardObj == null && deckMgr.cardPrefab != null)
                    {
                        cardObj = Instantiate(deckMgr.cardPrefab);
                    }

                    if (cardObj != null)
                    {
                        cardObj.GetComponent<Card3DViewTest>()?.SetupCard(drawnCard);
                        enemyHandManager.AddExistingCardVisual(cardObj, drawnCard);

                        CardDraggable3D draggable = cardObj.GetComponent<CardDraggable3D>();
                        if (draggable != null) draggable.SetFaceDown(false);

                        successfullyDrew = true;
                    }
                }
            }
            else if (hasPenaltyCards)
            {
                if (RuleManager_3D.Instance.TryDrawFromPenaltyPile(false, out Card penaltyCard, out GameObject physicalCardObj))
                {
                    if (physicalCardObj != null)
                    {
                        enemyHandManager.AddExistingCardVisual(physicalCardObj, penaltyCard);

                        CardDraggable3D draggable = physicalCardObj.GetComponent<CardDraggable3D>();
                        if (draggable != null) draggable.SetFaceDown(false);

                        successfullyDrew = true;
                    }
                }
            }

            if (!successfullyDrew)
            {
                break;
            }

            yield return new WaitForSeconds(0.2f);
        }

        onComplete?.Invoke();
    }

    private void ExecuteEnemyPenalty()
    {
        if (RuleManager_3D.Instance == null) return;
        RuleManager_3D.Instance.TriggerPenaltyClickAction();
    }
}