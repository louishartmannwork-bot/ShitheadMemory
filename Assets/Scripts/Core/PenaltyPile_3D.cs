using ShitheadMemory;
using System.Collections.Generic;
using UnityEngine;

public class PenaltyPile_3D : MonoBehaviour
{
    [Header("Player Assignment")]
    public bool isPlayer1Pile = true;

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

    private void OnMouseEnter()
    {
        if (isPlayer1Pile && RuleManager_3D.Instance != null && HandManager_3D.Instance != null)
        {
            bool isDeckEmpty = RuleManager_3D.Instance.deckManager != null && !RuleManager_3D.Instance.deckManager.HasCards();
            bool hasPenaltyCards = RuleManager_3D.Instance.player1PenaltyPile.Count > 0;
            int handCount = HandManager_3D.Instance.GetPlayerHandCardData().Count;

            // Hover when valid to draw from penalty pile in Phase 2
            if (isDeckEmpty && hasPenaltyCards && handCount < 3)
            {
                PlayerHandCursor_3D.Instance?.NotifyHoverEnter();
            }
        }
    }

    private void OnMouseExit()
    {
        PlayerHandCursor_3D.Instance?.NotifyHoverExit();
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

        HandManager_3D handManager = HandManager_3D.Instance != null ? HandManager_3D.Instance : Object.FindAnyObjectByType<HandManager_3D>();
        int handCount = handManager != null ? handManager.GetPlayerHandCardData().Count : 0;
        List<Card> playerHand = handManager != null ? handManager.GetPlayerHandCardData() : new List<Card>();

        // RULE: If hand count is less than 3, drawing is mandatory. 
        // If deck is empty, draw from penalty pile immediately!
        if (handCount < 3)
        {
            if (isDeckEmpty && hasPenaltyCards)
            {
                handManager?.TryDrawCardOrFromPenalty();
                return;
            }
            else if (!isDeckEmpty)
            {
                Debug.Log("Bitte ziehe vom Hauptstapel nach! (Hand hat weniger als 3 Karten)");
                return;
            }
        }

        // If hand is full (3 cards) and you have NO legal moves, then clicking penalty pile means you take the penalty pile into your hand
        List<Card> legalMoves = RuleManager_3D.Instance.GetLegalMoves(playerHand);
        if (handCount >= 3 && legalMoves.Count == 0)
        {
            RuleManager_3D.Instance.TriggerPenaltyClickAction();
        }
        else if (legalMoves.Count > 0)
        {
            OpponentDialogueManager_3D.Instance?.ShowComplaint("Du hast noch gültige Karten auf der Hand!");
        }
    }
}