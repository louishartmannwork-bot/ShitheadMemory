using UnityEngine;
using ShitheadMemory;

public enum GameState { PlayerTurn, WaitingForPlayerDraw, EnemyTurn, WaitingForEnemyDraw, Busy, GameOver }

public class TurnManager_3D : MonoBehaviour
{
    public static TurnManager_3D Instance;

    [Header("References")]
    [SerializeField] private HandManager_3D playerHandManager;
    [SerializeField] private DeckManager_3D deckManager;

    [Header("State")]
    [SerializeField] private GameState currentState = GameState.WaitingForPlayerDraw;

    private int illegalCardsOnTableCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool IsPlayerTurn() => currentState == GameState.PlayerTurn || currentState == GameState.WaitingForPlayerDraw;
    public bool CanPlayerDrawCard() => CanPlayerDraw();
    public bool IsGameOver() => currentState == GameState.GameOver;

    private void Start()
    {
        currentState = GameState.WaitingForPlayerDraw;
    }

    public bool CanPlayerDraw()
    {
        if (currentState == GameState.GameOver) return false;
        if (illegalCardsOnTableCount > 0)
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Nimm erst deine falsch gelegte Karte vom Tisch auf!");
            return false;
        }
        return currentState == GameState.WaitingForPlayerDraw;
    }

    public bool CanPlayerPlayCard()
    {
        if (currentState == GameState.GameOver) return false;
        if (illegalCardsOnTableCount > 0) return false;

        return currentState == GameState.PlayerTurn;
    }

    // --- STATE TRANSITIONS & HOOKS ---

    public void OnPlayerCompletedMove()
    {
        if (currentState == GameState.GameOver) return;

        CheckWinCondition();

        int cardCount = playerHandManager != null ? playerHandManager.GetHandCount() : 0;
        bool hasCardsToDraw = (deckManager != null && deckManager.HasCards()) ||
                             (RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1PenaltyPile.Count > 0);

        if (cardCount < 3 && hasCardsToDraw)
        {
            currentState = GameState.WaitingForPlayerDraw;
        }
        else
        {
            TriggerEnemyTurnFlow();
        }
    }

    public void OnCardDrawn()
    {
        if (currentState != GameState.WaitingForPlayerDraw) return;

        int cardCount = playerHandManager != null ? playerHandManager.GetHandCount() : 0;
        bool deckHasCards = deckManager != null && deckManager.HasCards();
        bool hasPenaltyCards = RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1PenaltyPile.Count > 0;
        bool noCardsLeftToDraw = !deckHasCards && !hasPenaltyCards;

        Debug.Log($"[TurnManager] Card drawn. Current hand count: {cardCount}");

        if (cardCount >= 3 || noCardsLeftToDraw)
        {
            Debug.Log("[TurnManager] Player finished drawing. Transitioning flow to EnemyTurn.");
            TriggerEnemyTurnFlow();
        }
    }

    private void TriggerEnemyTurnFlow()
    {
        currentState = GameState.Busy;
        Invoke(nameof(StartEnemyTurn), 0.6f);
    }

    public void StartEnemyTurn()
    {
        if (currentState == GameState.GameOver) return;
        CancelInvoke(nameof(StartEnemyTurn));
        currentState = GameState.EnemyTurn;

        EnemyAI_3D.Instance?.PlayTurn();
    }

    public void TriggerEnemyRedrawAfterPenalty()
    {
        currentState = GameState.WaitingForEnemyDraw;
        Debug.Log("[TurnManager] Player took penalty. Waiting for enemy to redraw hand.");

        Invoke(nameof(ExecuteEnemyRedraw), 0.8f);
    }

    private void ExecuteEnemyRedraw()
    {
        CancelInvoke(nameof(ExecuteEnemyRedraw));

        if (EnemyAI_3D.Instance != null)
        {
            EnemyAI_3D.Instance.CheckAndDrawCards();
        }

        Debug.Log("[Enemy Redraw] Enemy finished redrawing. Returning to PlayerTurn.");
        currentState = GameState.PlayerTurn;
    }

    public void ScheduleEndEnemyTurn(float delay = 0.8f)
    {
        CancelInvoke(nameof(StartPlayerTurn));
        Invoke(nameof(StartPlayerTurn), delay);
    }

    public void StartPlayerTurn()
    {
        if (currentState == GameState.GameOver) return;

        CheckWinCondition();

        int cardCount = playerHandManager != null ? playerHandManager.GetHandCount() : 0;
        bool hasCardsToDraw = (deckManager != null && deckManager.HasCards()) ||
                             (RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1PenaltyPile.Count > 0);

        if (cardCount < 3 && hasCardsToDraw)
        {
            currentState = GameState.WaitingForPlayerDraw;
        }
        else
        {
            currentState = GameState.PlayerTurn;
        }
    }

    public void CheckWinCondition()
    {
        if (currentState == GameState.GameOver) return;

        bool deckEmpty = deckManager != null && !deckManager.HasCards();

        int playerHandCount = playerHandManager != null ? playerHandManager.GetHandCount() : 99;
        int playerPenaltyCount = RuleManager_3D.Instance != null ? RuleManager_3D.Instance.player1PenaltyPile.Count : 99;

        EnemyHandManager_3D enemyHandMgr = EnemyAI_3D.Instance != null ? EnemyAI_3D.Instance.enemyHandManager : null;
        int enemyHandCount = enemyHandMgr != null ? enemyHandMgr.GetEnemyHandCardData().Count : 99;
        int enemyPenaltyCount = RuleManager_3D.Instance != null ? RuleManager_3D.Instance.player2PenaltyPile.Count : 99;

        // --- PLAYER WINS (Enemy Loses) ---
        if (deckEmpty && playerHandCount == 0 && playerPenaltyCount == 0)
        {
            Debug.Log("[WinCondition] PLAYER 1 WINS! All cards, hand, and penalty pile cleared.");
            currentState = GameState.GameOver;

            // Trigger Player Wins reaction (Enemy reacts to losing!)
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.PlayerWins);
            return;
        }

        // --- ENEMY WINS (Player Loses) ---
        if (deckEmpty && enemyHandCount == 0 && enemyPenaltyCount == 0)
        {
            Debug.Log("[WinCondition] ENEMY WINS! Opponent cleared all cards, hand, and penalty pile.");
            currentState = GameState.GameOver;

            // Trigger Enemy Wins reaction (Smug victory dialogue & sprite)
            EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.EnemyWins);
            return;
        }
    }

    public void RegisterIllegalCardOnTable() => illegalCardsOnTableCount++;
    public void ResolveIllegalCardOnTable() => illegalCardsOnTableCount = Mathf.Max(0, illegalCardsOnTableCount - 1);
    public void SetGameOver()
    {
        // If we are already in Game Over, block any duplicate calls completely!
        if (currentState == GameState.GameOver) return;

        currentState = GameState.GameOver;
        EnemyReactionManager_3D.Instance?.TriggerReaction(EnemyReactionManager_3D.EnemyEventType.EnemyWins);
    }
}