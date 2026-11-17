using UnityEngine;
using ShitheadMemory;
using System.Collections;

public class CardDraggable3D : MonoBehaviour
{
    private Camera cam;
    private Collider cardCollider;
    private Rigidbody rb;

    private HandManager_3D handManager;
    private DeckManager_3D deckManager;
    public PileManager_3D pileManager;
    public Card cardData;

    private bool isDragging = false;
    private bool isCurrentlyHovering = false;

    public bool isFaceDown = false;
    private bool isFlipping = false;
    public bool IsFlipping => isFlipping;
    public bool IsDragging => isDragging;

    public void SetFaceDown(bool faceDown) => isFaceDown = faceDown;

    public void Init(HandManager_3D hand, DeckManager_3D deck, Card data)
    {
        handManager = hand;
        deckManager = deck;
        cardData = data;
    }

    void Start()
    {
        cam = Camera.main;
        cardCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        DisablePhysics();
    }

    void Update()
    {
        if (!isDragging) CheckForClickToDrag();
        else
        {
            HandleDraggingMovement();
            CheckForMouseRelease();
        }
    }

    #region Hover & Click Interaction
    private void OnMouseEnter()
    {
        if (isDragging) return;
        if (handManager != null && handManager.IsCardInHand(gameObject))
        {
            isCurrentlyHovering = true;
            handManager.SetHoveredCard(gameObject);
            PlayerHandCursor_3D.Instance?.NotifyHandHoverEnter();
        }
        else
        {
            PlayerHandCursor_3D.Instance?.NotifyPileHoverEnter();
        }
    }

    private void OnMouseExit()
    {
        if (handManager != null && handManager.IsCardInHand(gameObject))
        {
            isCurrentlyHovering = false;
            handManager?.ClearHoveredCard(gameObject);
            PlayerHandCursor_3D.Instance?.NotifyHandHoverExit();
        }
        else
        {
            PlayerHandCursor_3D.Instance?.NotifyPileHoverExit();
        }
    }

    void CheckForClickToDrag()
    {
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider != null && hit.collider.gameObject == gameObject)
            {
                if (deckManager != null && deckManager.physicalCardStack.Contains(gameObject))
                {
                    if (TurnManager_3D.Instance != null && TurnManager_3D.Instance.CanPlayerDraw())
                    {
                        deckManager.OnDeckCardClicked(gameObject);
                        TurnManager_3D.Instance.OnCardDrawn();
                    }
                    else
                    {
                        OpponentDialogueManager_3D.Instance?.ShowComplaint("Du musst jetzt nicht nachziehen!");
                    }
                    return;
                }

                if (TurnManager_3D.Instance != null && !TurnManager_3D.Instance.CanPlayerPlayCard())
                {
                    OpponentDialogueManager_3D.Instance?.ShowComplaint("Zieh erst deine Karten nach!");
                    return;
                }

                PrepareForDrag();
            }
        }
    }

    void PrepareForDrag()
    {
        if (TurnManager_3D.Instance != null && !TurnManager_3D.Instance.IsPlayerTurn()) return;

        if (isCurrentlyHovering)
        {
            isCurrentlyHovering = false;
            handManager?.ClearHoveredCard(gameObject);
            PlayerHandCursor_3D.Instance?.NotifyHandHoverExit();
        }

        DisablePhysics();
        handManager?.OnCardPickedUp(gameObject);
        isDragging = true;

        Vector3 pos = transform.position;
        pos.y = 0.6f;
        transform.position = pos;

        PlayerHandCursor_3D.Instance?.NotifyCardPickedUp();
    }
    #endregion

    #region Dragging Movement & Release
    void HandleDraggingMovement()
    {
        if (PlayerHandCursor_3D.Instance != null)
        {
            Vector3 targetCardPos = PlayerHandCursor_3D.Instance.CardSnapPosition;
            targetCardPos.y = 0.6f;
            targetCardPos -= PlayerHandCursor_3D.Instance.transform.right * 0.63f;

            transform.position = targetCardPos;

            Quaternion cursorRot = PlayerHandCursor_3D.Instance.transform.rotation;
            Quaternion normalDragRot = cursorRot * Quaternion.Euler(PlayerHandCursor_3D.Instance.dragPitchX, 0f, 0f);

            transform.rotation = isFaceDown ? normalDragRot * Quaternion.Euler(0f, 180f, 0f) : normalDragRot;
        }
    }

    void CheckForMouseRelease()
    {
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EvaluateDropZone();
        }
    }

    void EvaluateDropZone()
    {
        isDragging = false;
        PlayerHandCursor_3D.Instance?.NotifyCardReleased();

        if (transform.position.z < -0.8f)
        {
            DropInHand();
            return;
        }

        if (IsDroppedOnPile())
        {
            RuleManager_3D.Instance?.HandleCardDroppedOnPile(cardData, gameObject);
            return;
        }

        RuleManager_3D.Instance?.HandleIllegalTableDrop(cardData, gameObject);
    }

    bool IsDroppedOnPile()
    {
        if (pileManager == null && RuleManager_3D.Instance != null)
            pileManager = RuleManager_3D.Instance.pileManager;

        if (pileManager != null)
        {
            float distance = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(pileManager.transform.position.x, 0, pileManager.transform.position.z)
            );
            return distance < 1.5f;
        }
        return false;
    }
    #endregion

    #region Hand & Flip Actions
    void DropInHand()
    {
        DisablePhysics();
        handManager?.AddCardToHand(gameObject);

        if (isFaceDown)
        {
            StartCoroutine(AnimateDrawToHandRoutine());
        }
        else
        {
            CardAnimation_3D anim = GetComponent<CardAnimation_3D>();
            Quaternion targetRot = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

            if (anim != null)
            {
                anim.PlayTween(transform.position, targetRot, 0.2f, () => {
                    if (cardCollider != null) cardCollider.enabled = true;
                });
            }
            else
            {
                transform.rotation = targetRot;
                if (cardCollider != null) cardCollider.enabled = true;
            }
        }
    }
    public void ReturnToHand()
    {
        isDragging = false;
        PlayerHandCursor_3D.Instance?.NotifyCardReleased();

        if (handManager != null)
        {
            handManager.AddCardToHand(gameObject);
        }
        else
        {
            EnablePhysics();
        }
    }
    private IEnumerator AnimateDrawToHandRoutine()
    {
        isFlipping = true;
        isFaceDown = false;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 targetPos = startPos;
        Quaternion targetRot = Quaternion.identity;

        if (handManager != null)
        {
            int index = handManager.currentHandCards.IndexOf(gameObject);
            int totalCards = handManager.currentHandCards.Count;

            if (index != -1 && totalCards > 0)
            {
                float currentSpread = handManager.widthSpread;
                if (totalCards > 1)
                {
                    float totalCalculatedWidth = (totalCards - 1) * handManager.widthSpread;
                    currentSpread = totalCalculatedWidth > handManager.maxHandWidth ? handManager.maxHandWidth / (totalCards - 1) : handManager.widthSpread;
                }

                float xOffset = (index - (totalCards - 1) / 2.0f) * currentSpread;
                float yOffset = handManager.baseHeightY + (index * handManager.layerOffsetY);

                if (handManager.handAreaAnchor != null)
                {
                    Vector3 localPos = new Vector3(xOffset, yOffset, 0f);
                    targetPos = handManager.handAreaAnchor.position + handManager.handAreaAnchor.TransformDirection(localPos);
                    float rotationAngle = -xOffset * 12f;
                    targetRot = handManager.handAreaAnchor.rotation * Quaternion.Euler(60f, 0f, rotationAngle);
                }
                else
                {
                    targetPos = new Vector3(xOffset, yOffset, -1.5f);
                    targetRot = Quaternion.Euler(60f, 0f, -xOffset * 12f);
                }
            }
        }

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);

            transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);

            yield return null;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        isFlipping = false;
        if (cardCollider != null) cardCollider.enabled = true;
    }

    public void StartDraggingImmediately()
    {
        PrepareForDrag();
    }
    #endregion

    #region Physics Helpers
    public void EnablePhysics()
    {
        if (cardCollider != null) cardCollider.enabled = true;
        if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
    }

    public void DisablePhysics()
    {
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
    }
    #endregion
    private bool isIllegalCardOnTable = false;

    public void MarkAsIllegalOnTable()
    {
        isIllegalCardOnTable = true;
    }

    private void OnMouseDown()
    {
        // If this card was marked as an illegal drop on the table, clicking it lets you pick it back up!
        if (isIllegalCardOnTable && TurnManager_3D.Instance != null)
        {
            PickupIllegalCardFromTable();
        }
    }

    public void PickupIllegalCardFromTable()
    {
        // 1. Add it back to the player's hand manager
        if (RuleManager_3D.Instance != null && RuleManager_3D.Instance.playerHandManager != null)
        {
            RuleManager_3D.Instance.playerHandManager.AddExistingCardVisual(gameObject, cardData);
        }

        // 2. Resolve the illegal card penalty count in TurnManager
        TurnManager_3D.Instance?.ResolveIllegalCardOnTable();

        // 3. Reset state flags
        isIllegalCardOnTable = false;

        // 4. Re-enable dragging physics components
        EnablePhysics(); // or whatever method restores your drag collider/rigidbody settings

        Debug.Log("[CardDraggable3D] Picked up illegal card from table back into hand.");
    }
}