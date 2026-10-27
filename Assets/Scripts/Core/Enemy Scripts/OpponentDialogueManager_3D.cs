using UnityEngine;
using TMPro;
using System.Collections;

public class OpponentDialogueManager_3D : MonoBehaviour
{
    public static OpponentDialogueManager_3D Instance;
    public TextMeshProUGUI dialogueText; // Textfeld für die Sprechblase
    public GameObject dialogueBox;     // Das Panel/die Sprechblase

    private Coroutine hideRoutine;

    private void Awake()
    {
        Instance = this;
        if (dialogueBox != null) dialogueBox.SetActive(false);
    }

    public void ShowComplaint(string message)
    {
        if (dialogueText != null && dialogueBox != null)
        {
            dialogueText.text = message;
            dialogueBox.SetActive(true);

            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterSeconds(3f));
        }
    }

    private IEnumerator HideAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (dialogueBox != null) dialogueBox.SetActive(false);
    }
}