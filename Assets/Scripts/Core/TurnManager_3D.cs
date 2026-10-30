using UnityEngine;
using ShitheadMemory;

public enum GameState { PlayerTurn, WaitingForPlayerDraw, EnemyTurn, Busy, GameOver }

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
    private int illegalCardsOnTableCount = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (currentState == GameState.WaitingForPlayerDraw)
        {
            drawTimer += Time.deltaTime;
            if (drawTimer >= drawReminderTimeout && !hasRemindedPlayer)
            {
                hasRemindedPlayer = true;
                OpponentDialogueManager_3D.Instance?.ShowComplaint("Zieh mal nach!");
            }
        }
    }

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
        bool hasPenaltyCards = RuleManager_3D.Instance != null && RuleManager_3D.Instance.player1PenaltyPile.Count > 0;

        bool handIsFull = cardCount >= 3;
        bool noCardsLeftToDraw = !deckHasCards && !hasPenaltyCards;

        if (handIsFull || noCardsLeftToDraw)
        {
            currentState = GameState.Busy;
            Invoke(nameof(StartEnemyTurn), 0.8f);
        }
        else
        {
            currentState = GameState.WaitingForPlayerDraw;
        }
    }

    private void StartEnemyTurn()
    {
        if (currentState == GameState.GameOver) return;
        currentState = GameState.EnemyTurn;
        EnemyAI_3D.Instance?.PlayTurn();
    }

    public void ScheduleEndEnemyTurn(float delay = 0.8f)
    {
        CancelInvoke(nameof(StartPlayerTurn));
        Invoke(nameof(StartPlayerTurn), delay);
    }

    public void StartPlayerTurn()
    {
        if (currentState == GameState.GameOver) return;
        currentState = GameState.PlayerTurn;
    }

    public bool CanPlayerDrawCard()
    {
        if (currentState == GameState.GameOver) return false;
        if (illegalCardsOnTableCount > 0)
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Nimm erst deine falsch gelegte Karte vom Tisch auf!");
            return false;
        }
        int cardCount = playerHandManager != null ? playerHandManager.GetHandCount() : 0;
        return cardCount < 3 && (currentState == GameState.PlayerTurn || currentState == GameState.WaitingForPlayerDraw);
    }

    public void RegisterIllegalCardOnTable() => illegalCardsOnTableCount++;
    public void ResolveIllegalCardOnTable() => illegalCardsOnTableCount = Mathf.Max(0, illegalCardsOnTableCount - 1);
    public bool IsPlayerTurn() => currentState == GameState.PlayerTurn || currentState == GameState.WaitingForPlayerDraw;
    public void SetGameOver() => currentState = GameState.GameOver;

    private void ResetDrawTimer()
    {
        drawTimer = 0f;
        hasRemindedPlayer = false;
    }
}