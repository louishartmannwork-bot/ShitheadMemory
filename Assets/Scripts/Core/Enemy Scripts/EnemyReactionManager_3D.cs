using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class EnemyReactionManager_3D : MonoBehaviour
{
    public static EnemyReactionManager_3D Instance;

    public enum EnemyEventType
    {
        CardPlayed,
        IllegalMove,
        TryDrawWithFullHand,
        RefuseToDraw,
        EnemyTakesPenalty,
        PlayerTakesPenalty,
        EnemyWins,
        PlayerWins
    }

    [System.Serializable]
    public class ReactionTier
    {
        [Tooltip("Minimum mistakes required to trigger this specific tier (leave 0 for default)")]
        public int minMistakes = 0;

        [TextArea(1, 3)]
        public List<string> dialogueLines = new List<string>();
        public List<Sprite> sprites = new List<Sprite>();
        public List<AudioClip> soundEffects = new List<AudioClip>();

        [Header("Local Wiggle Settings")]
        [Tooltip("How long the wiggle lasts in seconds")]
        public float wiggleDuration = 0.25f;
        [Tooltip("Max rotation angle in degrees for this specific reaction")]
        public float wiggleMagnitude = 6f;
    }

    [System.Serializable]
    public class EventReactionConfig
    {
        public List<ReactionTier> tiers = new List<ReactionTier>();
    }

    [Header("Event Reaction Categories")]
    public EventReactionConfig cardPlayed;
    public EventReactionConfig illegalMove;
    public EventReactionConfig tryDrawWithFullHand;
    public EventReactionConfig refuseToDraw;
    public EventReactionConfig enemyTakesPenalty;
    public EventReactionConfig playerTakesPenalty;
    public EventReactionConfig enemyWins;
    public EventReactionConfig playerWins;

    [Header("Visual & Audio References")]
    public SpriteRenderer enemySpriteRenderer;
    public UnityEngine.UI.Image enemyUIImage;
    public AudioSource enemyAudioSource;

    [Header("Default Idle State")]
    public Sprite idleSprite;
    [SerializeField] private float resetDelay = 3.0f;

    private Coroutine resetSpriteRoutine;
    private Coroutine wiggleRoutine;
    private bool hasPlayedEndgameReaction = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void TriggerReaction(EnemyEventType eventType, int currentMistakes = 0)
    {
        bool isWinLossEvent = (eventType == EnemyEventType.EnemyWins || eventType == EnemyEventType.PlayerWins);

        // If the game is over and a regular action happens AFTER the win/loss event has occurred:
        if (TurnManager_3D.Instance != null && TurnManager_3D.Instance.IsGameOver() && hasPlayedEndgameReaction && !isWinLossEvent)
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Es gibt nichts mehr zu tun.", false);
            return;
        }

        if (isWinLossEvent)
        {
            hasPlayedEndgameReaction = true;
        }

        EventReactionConfig configGroup = GetConfigGroup(eventType);
        if (configGroup == null || configGroup.tiers == null || configGroup.tiers.Count == 0) return;

        ReactionTier selectedTier = null;
        int highestTierMatch = -1;

        foreach (var tier in configGroup.tiers)
        {
            if (currentMistakes >= tier.minMistakes && tier.minMistakes > highestTierMatch)
            {
                highestTierMatch = tier.minMistakes;
                selectedTier = tier;
            }
        }

        if (selectedTier == null) return;

        ExecuteDialogue(selectedTier, isWinLossEvent);
        ExecuteSpriteChange(selectedTier);
        ExecuteSoundEffect(selectedTier);
        ExecuteWiggle(selectedTier);
    }

    private void ExecuteDialogue(ReactionTier tier, bool isWinLossEvent)
    {
        if (tier.dialogueLines == null || tier.dialogueLines.Count == 0) return;
        string randomLine = tier.dialogueLines[Random.Range(0, tier.dialogueLines.Count)];

        // Pass the win/loss flag through so the dialogue manager knows not to overwrite it
        OpponentDialogueManager_3D.Instance?.ShowComplaint(randomLine, isWinLossEvent);
    }

    private void ExecuteSpriteChange(ReactionTier tier)
    {
        if (tier.sprites == null || tier.sprites.Count == 0) return;
        Sprite randomSprite = tier.sprites[Random.Range(0, tier.sprites.Count)];

        if (randomSprite != null)
        {
            if (enemySpriteRenderer != null) enemySpriteRenderer.sprite = randomSprite;
            if (enemyUIImage != null) enemyUIImage.sprite = randomSprite;

            if (resetSpriteRoutine != null) StopCoroutine(resetSpriteRoutine);
            resetSpriteRoutine = StartCoroutine(ResetToIdleAfterDelay(resetDelay));
        }
    }

    private void ExecuteSoundEffect(ReactionTier tier)
    {
        if (enemyAudioSource == null || tier.soundEffects == null || tier.soundEffects.Count == 0) return;
        AudioClip randomClip = tier.soundEffects[Random.Range(0, tier.soundEffects.Count)];
        if (randomClip != null)
        {
            enemyAudioSource.PlayOneShot(randomClip);
        }
    }

    private void ExecuteWiggle(ReactionTier tier)
    {
        Transform targetTransform = GetTargetTransform();
        if (targetTransform != null)
        {
            if (wiggleRoutine != null) StopCoroutine(wiggleRoutine);
            wiggleRoutine = StartCoroutine(WiggleRoutine(targetTransform, tier.wiggleDuration, tier.wiggleMagnitude));
        }
    }

    private Transform GetTargetTransform()
    {
        if (enemySpriteRenderer != null) return enemySpriteRenderer.transform;
        if (enemyUIImage != null) return enemyUIImage.transform;
        return null;
    }

    private IEnumerator WiggleRoutine(Transform targetTransform, float duration, float magnitude)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = elapsed / duration;
            float currentMagnitude = magnitude * (1f - progress);
            float zRot = Mathf.Sin(elapsed * 45f) * currentMagnitude;

            targetTransform.localRotation = Quaternion.Euler(0, 0, zRot);
            yield return null;
        }

        targetTransform.localRotation = Quaternion.identity;
        wiggleRoutine = null;
    }

    private EventReactionConfig GetConfigGroup(EnemyEventType eventType)
    {
        switch (eventType)
        {
            case EnemyEventType.CardPlayed: return cardPlayed;
            case EnemyEventType.IllegalMove: return illegalMove;
            case EnemyEventType.TryDrawWithFullHand: return tryDrawWithFullHand;
            case EnemyEventType.RefuseToDraw: return refuseToDraw;
            case EnemyEventType.EnemyTakesPenalty: return enemyTakesPenalty;
            case EnemyEventType.PlayerTakesPenalty: return playerTakesPenalty;
            case EnemyEventType.EnemyWins: return enemyWins;
            case EnemyEventType.PlayerWins: return playerWins;
            default: return null;
        }
    }

    private IEnumerator ResetToIdleAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (TurnManager_3D.Instance != null && TurnManager_3D.Instance.IsGameOver())
        {
            yield break;
        }

        if (idleSprite != null)
        {
            if (enemySpriteRenderer != null) enemySpriteRenderer.sprite = idleSprite;
            if (enemyUIImage != null) enemyUIImage.sprite = idleSprite;
        }

        resetSpriteRoutine = null;
    }
}