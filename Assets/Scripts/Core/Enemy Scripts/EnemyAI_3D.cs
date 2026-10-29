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

    private List<Card> enemyHand => RuleManager_3D.Instance != null ? RuleManager_3D.Instance.player2Hand : new List<Card>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Invoke(nameof(DrawInitialCards), 0.5f);
    }

    public void DrawInitialCards()
    {
        if (deckManager == null) return;
        for (int i = 0; i < 3; i++) DrawCard();
    }

    public void DrawCard()
    {
        if (deckManager != null && deckManager.TryDrawCardObject(out Card drawnCard, out GameObject cardObj))
        {
            enemyHand.Add(drawnCard);

            if (cardObj != null)
            {
                // Strip player interaction components since this card now belongs to the enemy
                Destroy(cardObj.GetComponent<CardDraggable3D>());

                Collider col = cardObj.GetComponent<Collider>();
                if (col != null) col.enabled = false; // Enemy cards don't need active mouse raycasts in hand

                // Pass the actual physical card object to the enemy hand manager
                enemyHandManager?.AddExistingCardVisual(cardObj, drawnCard);
            }
        }
    }

    public void RecordPlayerMistake()
    {
        playerMistakes++;
        if (playerMistakes >= maxMistakes)
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Es reicht! Du hast 10 Fehler gemacht. Ich gewinne!");
            TurnManager_3D.Instance?.SetGameOver();
            Time.timeScale = 0f;
        }
    }

    public void PlayTurn()
    {
        if (RuleManager_3D.Instance == null) return;

        CheckAndDrawCards();
        List<Card> legalMoves = RuleManager_3D.Instance.GetLegalMoves(enemyHand);

        if (legalMoves.Count > 0)
        {
            Card cardToPlay = ChooseLowestLegalCard(legalMoves);
            PlayCard(cardToPlay);
            CheckAndDrawCards();
            TurnManager_3D.Instance?.ScheduleEndEnemyTurn(0.8f);
        }
        else
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Verdammt, ich kann nicht legen! Gib mir deine Handkarten!");
            ExecuteEnemyPenalty();

            List<Card> freeLegalMoves = RuleManager_3D.Instance.GetLegalMoves(enemyHand);
            if (freeLegalMoves.Count > 0)
            {
                PlayCard(ChooseLowestLegalCard(freeLegalMoves));
            }

            CheckAndDrawCards();
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
        int index = enemyHand.IndexOf(card);
        if (index != -1)
        {
            enemyHand.RemoveAt(index);
            enemyHandManager?.RemoveCardVisual(index);
        }

        RuleManager_3D.Instance?.ResetFreeTurn();

        if (pileManager != null && enemyHandManager != null && enemyHandManager.cardPrefab != null)
        {
            Vector3 dropPos = pileManager.transform.position + new Vector3(0, 0.6f, 0);
            GameObject spawnedCard = Instantiate(enemyHandManager.cardPrefab, dropPos, Quaternion.Euler(75f, Random.Range(-10f, 10f), 0f));

            spawnedCard.GetComponent<Card3DViewTest>()?.SetupCard(card);
            Destroy(spawnedCard.GetComponent<CardDraggable3D>());

            Rigidbody rb = spawnedCard.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(new Vector3(Random.Range(-0.05f, 0.05f), -0.5f, Random.Range(-0.05f, 0.05f)), ForceMode.Impulse);
            }

            pileManager.AddCardToPile(card, spawnedCard);
        }
    }

    private void CheckAndDrawCards()
    {
        while (enemyHand.Count < 3)
        {
            // Pull actual physical cards from the deck stack
            if (deckManager != null && deckManager.HasCards() && deckManager.TryDrawCardObject(out Card drawn, out GameObject cardObj))
            {
                enemyHand.Add(drawn);

                if (cardObj != null)
                {
                    Destroy(cardObj.GetComponent<CardDraggable3D>());
                    Collider col = cardObj.GetComponent<Collider>();
                    if (col != null) col.enabled = false;

                    enemyHandManager?.AddExistingCardVisual(cardObj, drawn);
                }
            }
            else if (RuleManager_3D.Instance.player2PenaltyPile.Count > 0)
            {
                List<Card> penaltyPile = RuleManager_3D.Instance.player2PenaltyPile;
                Card penaltyCard = penaltyPile[penaltyPile.Count - 1];
                penaltyPile.RemoveAt(penaltyPile.Count - 1);

                Transform spawnPoint = RuleManager_3D.Instance.player2PenaltySpawnPoint;
                if (spawnPoint != null && spawnPoint.childCount > 0)
                {
                    Destroy(spawnPoint.GetChild(spawnPoint.childCount - 1).gameObject);
                }

                enemyHand.Add(penaltyCard);
                enemyHandManager?.AddCardVisual(penaltyCard);
            }
            else break;
        }
    }

    private void ExecuteEnemyPenalty()
    {
        if (RuleManager_3D.Instance == null) return;
        RuleManager_3D.Instance.isPlayer1Turn = false;
        RuleManager_3D.Instance.TriggerPenaltyClickAction();
    }
}