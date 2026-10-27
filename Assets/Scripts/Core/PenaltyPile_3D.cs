using ShitheadMemory;
using System.Collections.Generic;
using UnityEngine;

public class PenaltyPile_3D : MonoBehaviour
{
    [Header("Player Assignment")]
    public bool isPlayer1Pile = true; // Haken setzen für Spieler 1

    [Header("Click Radius")]
    public float clickRadius = 1.0f;

    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            CheckForClick();
        }
    }

    private void CheckForClick()
    {
        Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
            {
                TryInteractWithPenalty();
                return;
            }

            float distanceToCenter = Vector3.Distance(
                new Vector3(hit.point.x, 0, hit.point.z),
                new Vector3(transform.position.x, 0, transform.position.z)
            );

            if (distanceToCenter <= clickRadius)
            {
                TryInteractWithPenalty();
                return;
            }
        }
    }

    private void TryInteractWithPenalty()
    {
        if (RuleManager_3D.Instance == null) return;

        if (!isPlayer1Pile)
        {
            Debug.Log("Du kannst den Strafstapel des Gegners nicht anklicken!");
            return;
        }

        if (TurnManager_3D.Instance != null && !TurnManager_3D.Instance.IsPlayerTurn())
        {
            Debug.Log("Du bist gerade nicht am Zug!");
            return;
        }

        RuleManager_3D.Instance.isPlayer1Turn = true;

        bool isDeckEmpty = RuleManager_3D.Instance.deckManager != null && !RuleManager_3D.Instance.deckManager.HasCards();
        bool hasPenaltyCards = RuleManager_3D.Instance.player1PenaltyPile.Count > 0;

        if (isDeckEmpty)
        {
            // --- PHASE 2 / ENDGAME LOGIK ---

            // 1. Wenn der Strafstapel noch Karten hat UND deine Hand < 3 ist -> Nachziehen
            if (hasPenaltyCards && RuleManager_3D.Instance.player1Hand.Count < 3)
            {
                HandManager_3D handManager = Object.FindAnyObjectByType<HandManager_3D>();
                handManager?.TryDrawCardOrFromPenalty();
                return;
            }

            // 2. Endgame / Strafstapel leer ODER Hand voll: Prüfen ob man legen kann.
            // Wenn KEINE legalen Züge mehr da sind -> STRAFE AUSLÖSEN!
            List<Card> legalMoves = RuleManager_3D.Instance.GetLegalMoves(RuleManager_3D.Instance.player1Hand);
            if (legalMoves.Count == 0)
            {
                Debug.Log("[Endgame] Strafstapel leer & keine legalen Züge! Strafe wird ausgelöst.");
                RuleManager_3D.Instance.TriggerPenaltyClickAction();
                return;
            }
            else
            {
                Debug.Log("Du hast noch gültige Karten auf der Hand!");
                OpponentDialogueManager_3D.Instance?.ShowComplaint("Du hast noch gültige Karten auf der Hand!");
                return;
            }
        }

        // --- PHASE 1 LOGIK (Hauptdeck hat noch Karten) ---
        List<Card> phase1LegalMoves = RuleManager_3D.Instance.GetLegalMoves(RuleManager_3D.Instance.player1Hand);

        if (phase1LegalMoves.Count == 0)
        {
            Debug.Log("Keine legalen Züge! Strafe wird getriggert.");
            RuleManager_3D.Instance.TriggerPenaltyClickAction();
        }
        else
        {
            Debug.Log("Du hast noch gültige Karten auf der Hand!");
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Du hast noch gültige Karten auf der Hand!");
        }
    }
}