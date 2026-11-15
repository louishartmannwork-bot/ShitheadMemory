using UnityEngine;
using ShitheadMemory;
using System.Collections.Generic;

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
            // Use TryDrawCardObject to pull a pre-spawned physical card from the deck stack
            if (deckManager.TryDrawCardObject(out Card drawnCard, out GameObject cardObj))
            {
                if (cardObj != null)
                {
                    enemyHandManager.AddExistingCardVisual(cardObj, drawnCard);
                }
            }
        }
    }

    public void RecordPlayerMistake()
    {
        playerMistakes++;
        if (playerMistakes >= maxMistakes)
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Junge kannst du nich nach den regeln spielen? ich hab kein bock mehr");
            TurnManager_3D.Instance?.SetGameOver();
            Time.timeScale = 0f;
        }
    }

    public void PlayTurn()
    {
        if (RuleManager_3D.Instance == null || enemyHandManager == null) return;

        CheckAndDrawCards();
        List<Card> currentHand = enemyHandManager.GetEnemyHandCardData();
        List<Card> legalMoves = RuleManager_3D.Instance.GetLegalMoves(currentHand);

        if (legalMoves.Count > 0)
        {
            // Normal turn: Play lowest legal card
            Card cardToPlay = ChooseLowestLegalCard(legalMoves);
            PlayCard(cardToPlay);
            CheckAndDrawCards();
            TurnManager_3D.Instance?.ScheduleEndEnemyTurn(0.8f);
        }
        else
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Ok dann kann ich halt nich man");

            // Enemy has no legal moves, so they take the penalty!
            // This clears the pile, wipes your hand into their penalty pile, and refills their hand.
            ExecuteEnemyPenalty();
            CheckAndDrawCards();

            // TAKE PENALTY ENDS THE TURN. Do NOT try to play another card here!
            // Just hand control back to the player.
            TurnManager_3D.Instance?.ScheduleEndEnemyTurn(0.8f);
        }
    }

    private Card ChooseLowestLegalCard(List<Card> legalMoves)
    {
        legalMoves.Sort((a, b) => ((int)a.value).CompareTo((int)b.value));
        return legalMoves[0];
    }

    private void PlayCard(Card card)
    {
        // 1. ABSOLUTE RULE CHECK: Refuse to play illegal cards even if chosen
        if (RuleManager_3D.Instance != null && !RuleManager_3D.Instance.IsMoveLegal(card))
        {
            Debug.LogError($"[AI PlayCard] BLOCKED: AI tried to play illegal card {card.value}!");
            return;
        }

        List<Card> currentHand = enemyHandManager.GetEnemyHandCardData();
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
            // Fallback search
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

        // Safely remove visual and data from enemy hand
        enemyHandManager.RemoveCardVisual(targetIndex);
        RuleManager_3D.Instance?.ResetFreeTurn();

        if (pileManager != null)
        {
            CardAnimation_3D anim = cardToPlayObj.GetComponent<CardAnimation_3D>();
            Collider col = cardToPlayObj.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            // Use PileManager to handle the drop cleanly, maintaining proper discard state!
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

                    // This updates PileManager data and objects correctly so the discard state never corrupts!
                    pileManager.AddCardToPile(card, cardToPlayObj);
                });
            }
            else
            {
                pileManager.AddCardToPile(card, cardToPlayObj);
            }
        }
    }
    private void CheckAndDrawCards()
    {
        if (enemyHandManager == null) return;

        Debug.Log($"[AI CheckAndDrawCards] Current enemy hand count before draw: {enemyHandManager.GetEnemyHandCardData().Count}");

        while (enemyHandManager.GetEnemyHandCardData().Count < 3)
        {
            bool successfullyDrew = false;

            // 1. Try drawing from the main deck stack first
            if (deckManager != null && deckManager.HasCards())
            {
                if (deckManager.TryDrawCardObject(out Card drawnCard, out GameObject cardObj))
                {
                    if (cardObj != null)
                    {
                        enemyHandManager.AddExistingCardVisual(cardObj, drawnCard);
                        successfullyDrew = true;
                        Debug.Log("[AI CheckAndDrawCards] Successfully drew card from deck.");
                    }
                }
                else
                {
                    Debug.LogWarning("[AI CheckAndDrawCards] DeckManager.HasCards() was true, but TryDrawCardObject failed!");
                }
            }
            else
            {
                Debug.Log("[AI CheckAndDrawCards] Deck has no cards left.");
            }

            // 2. If deck is empty, draw from Enemy Penalty Pile (Player 2 Penalty Pile)
            if (!successfullyDrew && RuleManager_3D.Instance != null && RuleManager_3D.Instance.player2PenaltyPile.Count > 0)
            {
                List<Card> penaltyPile = RuleManager_3D.Instance.player2PenaltyPile;
                Card penaltyCard = penaltyPile[penaltyPile.Count - 1];
                penaltyPile.RemoveAt(penaltyPile.Count - 1);

                Transform spawnPoint = RuleManager_3D.Instance.player2PenaltySpawnPoint;
                if (spawnPoint != null && spawnPoint.childCount > 0)
                {
                    Destroy(spawnPoint.GetChild(spawnPoint.childCount - 1).gameObject);
                }

                enemyHandManager.AddCardVisual(penaltyCard);
                successfullyDrew = true;
                Debug.Log("[AI CheckAndDrawCards] Drew card from enemy penalty pile.");
            }

            // If neither source has cards left, stop trying to prevent infinite loops
            if (!successfullyDrew)
            {
                Debug.LogWarning("[AI CheckAndDrawCards] COULD NOT DRAW! Both deck and penalty pile failed or were empty.");
                break;
            }
        }
    }

    private void ExecuteEnemyPenalty()
    {
        if (RuleManager_3D.Instance == null) return;
        RuleManager_3D.Instance.isPlayer1Turn = false;
        RuleManager_3D.Instance.TriggerPenaltyClickAction();
    }
}