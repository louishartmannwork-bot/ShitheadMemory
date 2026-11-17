using UnityEngine;
using TMPro;
using System.Collections;

public class OpponentDialogueManager_3D : MonoBehaviour
{
    public static OpponentDialogueManager_3D Instance;
    public TextMeshProUGUI dialogueText;
    public GameObject dialogueBox;

    private Coroutine hideRoutine;

    private void Awake()
    {
        Instance = this;
        if (dialogueBox != null) dialogueBox.SetActive(false);
    }

    public void ShowComplaint(string message, bool isWinLossMessage = false)
    {
        if (dialogueText != null && dialogueBox != null)
        {
            // DEBUG: See what is coming into the dialogue manager
            Debug.Log($"[OpponentDialogueManager] ShowComplaint called with: '{message}' | IsGameOver: {TurnManager_3D.Instance?.IsGameOver()} | IsWinLoss: {isWinLossMessage}");

            // If the game is over and this is NOT the special win/loss announcement, force the post-game text!
            if (TurnManager_3D.Instance != null && TurnManager_3D.Instance.IsGameOver() && !isWinLossMessage)
            {
                message = "Es gibt nichts mehr zu tun.";
            }

            dialogueText.text = message;
            dialogueBox.SetActive(true);

            if (hideRoutine != null) StopCoroutine(hideRoutine);

            float displayDuration = isWinLossMessage ? 4f : 3f;
            hideRoutine = StartCoroutine(HideAfterSeconds(displayDuration));
        }
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (dialogueBox != null) dialogueBox.SetActive(false);
    }
}