using UnityEngine;

public enum GameState
{
    PlayerTurn,
    WaitingForPlayerDraw,
    EnemyTurn,
    Busy,
    GameOver
}

public class TurnManager_3D : MonoBehaviour
{
    public static TurnManager_3D Instance;

    [Header("References")]
    [SerializeField] private HandManager_3D playerHandManager;
    [SerializeField] private DeckManager_3D deckManager;

    [Header("State")]
    [SerializeField] private GameState currentState = GameState.PlayerTurn;

    [Header("Settings & Timers")]
    [SerializeField] private float drawReminderTimeout = 4.0f;
    private float drawTimer = 0f;
    private bool hasRemindedPlayer = false;

    // Penalty Tracking für ungelöste Strafkarten auf dem Tisch
    private int illegalCardsOnTableCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        HandleDrawReminderTimer();
    }

    private void HandleDrawReminderTimer()
    {
        if (currentState != GameState.WaitingForPlayerDraw) return;

        drawTimer += Time.deltaTime;
        if (drawTimer >= drawReminderTimeout && !hasRemindedPlayer)
        {
            hasRemindedPlayer = true;
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Ey! Zieh erst eine Karte nach, bevor ich legen kann!");
        }
    }

    #region Turn State Control

    public void OnPlayerCompletedMove()
    {
        if (currentState == GameState.GameOver) return;

        ResetDrawTimer();
        currentState = GameState.WaitingForPlayerDraw;
        EvaluateTurnProgression();
    }

    public void OnCardAddedToHand()
    {
        if (currentState == GameState.WaitingForPlayerDraw)
        {
            ResetDrawTimer();
            EvaluateTurnProgression();
        }
    }

    private void EvaluateTurnProgression()
    {
        int cardCount = playerHandManager != null ? playerHandManager.GetHandCount() : 0;
        bool deckHasCards = deckManager != null && deckManager.HasCards();

        // Prüfen, ob noch Karten im eigenen Strafstapel liegen (für Phase 2)
        bool hasPenaltyCards = RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1PenaltyPile.Count > 0;

        // Der Zug geht erst dann an den Gegner über, wenn:
        // 1. Die Hand wieder voll ist (cardCount >= 3)
        // ODER
        // 2. Das Deck leer ist UND KEINE Karten mehr im Strafstapel liegen (Spieler kann schlichtweg nicht mehr nachziehen)
        bool handIsFull = cardCount >= 3;
        bool noCardsLeftToDraw = !deckHasCards && !hasPenaltyCards;

        if (handIsFull || noCardsLeftToDraw)
        {
            SetState(GameState.Busy);
            Invoke(nameof(StartEnemyTurn), 0.8f);
        }
        else
        {
            // Solange das Deck leer ist, aber noch Strafstapel-Karten da sind
            // UND die Hand noch unter 3 Karten ist, bleiben wir im Wartezustand!
            SetState(GameState.WaitingForPlayerDraw);
        }
    }

    private void StartEnemyTurn()
    {
        if (currentState == GameState.GameOver) return;

        SetState(GameState.EnemyTurn);
        Debug.Log("<color=orange>TurnManager: Gegner ist am Zug.</color>");

        // Die KI spielt ihren Zug (wird ab jetzt komplett in EnemyAI_3D gesteuert)
        EnemyAI_3D.Instance?.PlayTurn();

        // HINWEIS: Das automatische Invoke(StartPlayerTurn) flog hier raus, 
        // damit die KI nach einer Strafe ihren Free-Turn erst ausspielen kann!
    }

    /// <summary>
    /// Wird von der KI aufgerufen, wenn ihr kompletter Zug (inkl. eventuellem Free-Turn) beendet ist.
    /// </summary>
    public void ScheduleEndEnemyTurn(float delay = 0.8f)
    {
        CancelInvoke(nameof(StartPlayerTurn));
        Invoke(nameof(StartPlayerTurn), delay);
    }

    public void StartPlayerTurn()
    {
        if (currentState == GameState.GameOver) return;
        SetState(GameState.PlayerTurn);
        Debug.Log("<color=green>TurnManager: Spieler ist am Zug.</color>");
    }

    private void SetState(GameState newState)
    {
        currentState = newState;
    }

    #endregion

    #region Validation & Rules

    public bool CanPlayerDrawCard()
    {
        if (currentState == GameState.GameOver) return false;

        // Blockieren, solange Strafkarten auf dem Tisch liegen
        if (illegalCardsOnTableCount > 0)
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Nimm erst deine falsch gelegte Karte vom Tisch auf, bevor du nachziehst!");
            return false;
        }

        int cardCount = playerHandManager != null ? playerHandManager.GetHandCount() : 0;
        return cardCount < 3 && (currentState == GameState.PlayerTurn || currentState == GameState.WaitingForPlayerDraw);
    }

    public void RegisterIllegalCardOnTable() => illegalCardsOnTableCount++;
    public void ResolveIllegalCardOnTable() => illegalCardsOnTableCount = Mathf.Max(0, illegalCardsOnTableCount - 1);

    public bool IsPlayerTurn() => currentState == GameState.PlayerTurn || currentState == GameState.WaitingForPlayerDraw;
    public void SetGameOver() => SetState(GameState.GameOver);

    private void ResetDrawTimer()
    {
        drawTimer = 0f;
        hasRemindedPlayer = false;
    }

    #endregion
}