using ShitheadMemory;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAI_3D : MonoBehaviour
{
    public static EnemyAI_3D Instance;

    [Header("References")]
    public PileManager_3D pileManager;
    public DeckManager_3D deckManager;
    public EnemyHandManager_3D enemyHandManager;

    [Header("Enemy State")]
    public List<Card> enemyHand => RuleManager_3D.Instance != null ? RuleManager_3D.Instance.player2Hand : new List<Card>();

    [Header("Mistake Tracking")]
    public int playerMistakes = 0;
    public int maxMistakes = 10;

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

        for (int i = 0; i < 3; i++)
        {
            DrawCard();
        }
    }

    public void DrawCard()
    {
        if (deckManager != null && deckManager.TryDrawCard(out Card drawnCard))
        {
            enemyHand.Add(drawnCard);
            if (enemyHandManager != null)
            {
                enemyHandManager.AddCardVisual(drawnCard);
            }
        }
    }

    public void RecordPlayerMistake()
    {
        playerMistakes++;
        Debug.Log($"Spielerfehler registriert! Total: {playerMistakes}/{maxMistakes}");

        if (playerMistakes >= maxMistakes)
        {
            TriggerGameOver();
        }
    }

    private void TriggerGameOver()
    {
        if (OpponentDialogueManager_3D.Instance != null)
        {
            OpponentDialogueManager_3D.Instance.ShowComplaint("Es reicht jetzt endgültig! Du hast 10 Fehler gemacht und nur geschummelt. Ich gewinne, das Spiel ist vorbei!");
        }

        if (TurnManager_3D.Instance != null)
        {
            TurnManager_3D.Instance.SetGameOver();
        }

        Time.timeScale = 0f;
    }

    public void PlayTurn()
    {
        if (RuleManager_3D.Instance == null) return;

        // 1. ZUERST nachziehen, damit die Hand voll ist (3 Karten), BEVOR wir Züge prüfen!
        CheckAndDrawCards();

        // 2. Jetzt erst prüfen, ob die KI mit ihrer vollen Hand legale Züge hat
        List<Card> legalMoves = RuleManager_3D.Instance.GetLegalMoves(enemyHand);

        if (legalMoves.Count > 0)
        {
            // Normaler Zug: Niedrigste legale Karte spielen
            Card cardToPlay = ChooseLowestLegalCard(legalMoves);
            PlayCard(cardToPlay);

            // Nach dem Ausspielen direkt wieder auf 3 Karten auffüllen (falls möglich)
            CheckAndDrawCards();

            // Zug beenden und an den Spieler übergeben
            TurnManager_3D.Instance?.ScheduleEndEnemyTurn(0.8f);
        }
        else
        {
            if (OpponentDialogueManager_3D.Instance != null)
            {
                OpponentDialogueManager_3D.Instance.ShowComplaint("Verdammt, ich kann nicht legen! Gib mir deine Handkarten!");
            }

            // 3. KI nimmt die Strafe (deine Handkarten wandern auf ihren Strafstapel, du ziehst nach, KI kriegt freeTurn)
            ExecuteEnemyPenalty();

            // 4. Da freeTurn nun aktiv ist, hat die KI dank des Free-Turns sofort legale Züge
            List<Card> freeLegalMoves = RuleManager_3D.Instance.GetLegalMoves(enemyHand);
            if (freeLegalMoves.Count > 0)
            {
                Card freeCardToPlay = ChooseLowestLegalCard(freeLegalMoves);
                PlayCard(freeCardToPlay); // Das beendet den freeTurn automatisch
            }

            // 5. Nochmals nachziehen falls nötig
            CheckAndDrawCards();

            // 6. Jetzt ist der KI-Zug komplett vorbei -> Übergabe an den Spieler
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
            if (enemyHandManager != null)
            {
                enemyHandManager.RemoveCardVisual(index);
            }
        }

        // --- HIER WIRD DER FREIE ZUG DER KI BEENDET ---
        if (RuleManager_3D.Instance != null)
        {
            RuleManager_3D.Instance.ResetFreeTurn();
        }

        if (pileManager != null && enemyHandManager != null && enemyHandManager.cardPrefab != null)
        {
            Vector3 dropPos = pileManager.transform.position + new Vector3(0, 0.6f, 0);
            GameObject spawnedCard = Instantiate(enemyHandManager.cardPrefab, dropPos, Quaternion.Euler(90f, Random.Range(-10f, 10f), 0f));

            Card3DViewTest cardView = spawnedCard.GetComponent<Card3DViewTest>();
            if (cardView != null)
            {
                cardView.SetupCard(card);
            }

            CardDraggable3D dragScript = spawnedCard.GetComponent<CardDraggable3D>();
            if (dragScript != null) Destroy(dragScript);

            Rigidbody rb = spawnedCard.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.AddForce(new Vector3(Random.Range(-0.05f, 0.05f), -0.5f, Random.Range(-0.05f, 0.05f)), ForceMode.Impulse);
            }

            pileManager.AddCardToPile(card, spawnedCard);
        }

        Debug.Log($"Gegner hat {card.value} of {card.suit} ausgespielt.");
    }

    private void CheckAndDrawCards()
    {
        if (RuleManager_3D.Instance == null) return;

        // Die Schleife stoppt exakt, sobald 3 Karten in der Hand sind
        while (enemyHand.Count < 3)
        {
            if (deckManager != null && deckManager.HasCards())
            {
                if (deckManager.TryDrawCard(out Card drawn))
                {
                    enemyHand.Add(drawn);
                    enemyHandManager?.AddCardVisual(drawn);
                }
            }
            else if (RuleManager_3D.Instance.player2PenaltyPile.Count > 0)
            {
                List<Card> aiPenaltyPile = RuleManager_3D.Instance.player2PenaltyPile;
                int lastIndex = aiPenaltyPile.Count - 1;
                Card penaltyCard = aiPenaltyPile[lastIndex];
                aiPenaltyPile.RemoveAt(lastIndex);

                Transform spawnPoint = RuleManager_3D.Instance.player2PenaltySpawnPoint;
                if (spawnPoint != null && spawnPoint.childCount > 0)
                {
                    Destroy(spawnPoint.GetChild(spawnPoint.childCount - 1).gameObject);
                }

                enemyHand.Add(penaltyCard);
                enemyHandManager?.AddCardVisual(penaltyCard);
            }
            else
            {
                break; // Keine Karten mehr im Deck und im Strafstapel vorhanden
            }
        }
    }

    private void ExecuteEnemyPenalty()
    {
        if (RuleManager_3D.Instance == null) return;

        // Der Gegner (Spieler 2) bestraft sich/den Spieler:
        // Handkarten von Spieler 1 (Mensch) wandern auf den Strafstapel von Spieler 2 (KI)
        RuleManager_3D.Instance.isPlayer1Turn = false; // Sicherstellen, dass die KI als aktiver Spieler zählt
        RuleManager_3D.Instance.TriggerPenaltyClickAction();
    }
}