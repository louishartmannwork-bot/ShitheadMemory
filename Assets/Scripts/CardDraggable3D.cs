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

    // --- FACE-DOWN & FLIP STATE ---
    public bool isFaceDown = false;
    private bool isFlipping = false;
    public bool IsFlipping => isFlipping;

    public void SetFaceDown(bool faceDown) => isFaceDown = faceDown;
    public bool IsDragging => isDragging;
    // ----------------------------

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

        bool isInHand = handManager != null && handManager.IsCardInHand(gameObject);

        if (isInHand)
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
        bool isInHand = handManager != null && handManager.IsCardInHand(gameObject);

        if (isInHand)
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

    public void StartDraggingImmediately() => PrepareForDrag();

    void CheckForClickToDrag()
    {
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider != null && hit.collider.gameObject == gameObject)
            {
                // IF THIS CARD IS STILL SITTING IN THE DECK STACK:
                if (deckManager != null && deckManager.physicalCardStack.Contains(gameObject))
                {
                    // Trigger official deck draw logic so lists update 1-for-1!
                    deckManager.OnDeckCardClicked(gameObject);
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

        PlayerHandCursor_3D.Instance?.NotifyHandHoverExit();

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

            float horizontalOffset = 0.63f;
            targetCardPos -= PlayerHandCursor_3D.Instance.transform.right * horizontalOffset;

            transform.position = targetCardPos;

            // Get standard cursor rotation used for normal dragging
            Quaternion cursorRot = PlayerHandCursor_3D.Instance.transform.rotation;
            Quaternion pitchOffset = Quaternion.Euler(PlayerHandCursor_3D.Instance.dragPitchX, 0f, 0f);
            Quaternion normalDragRot = cursorRot * pitchOffset;

            if (isFaceDown)
            {
                // Follow the hand rotation, but offset by 180 degrees to keep it face-down
                transform.rotation = normalDragRot * Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                // Normal right-side up rotation
                transform.rotation = normalDragRot;
            }
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

        // 1. Dropped back down into the hand area
        if (transform.position.z < -0.8f)
        {
            DropInHand();
            return;
        }

        // 2. Dropped on or near the discard pile
        if (IsDroppedOnPile())
        {
            // Delegate the rule processing to RuleManager
            if (RuleManager_3D.Instance != null)
            {
                RuleManager_3D.Instance.HandleCardDroppedOnPile(cardData, gameObject);
            }
            return;
        }

        // 3. Dropped somewhere else on the table illegally
        if (RuleManager_3D.Instance != null)
        {
            RuleManager_3D.Instance.HandleIllegalTableDrop(cardData, gameObject);
        }
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
            // Standard drop handling for cards already in hand
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

    private IEnumerator AnimateDrawToHandRoutine()
    {
        isFlipping = true;
        isFaceDown = false;

        // 1. Capture starting state right where the player dropped it
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;
        Vector3 startScale = transform.localScale;

        // 2. Figure out where the hand manager WANTS this card to end up by querying the layout math
        // (We can temporarily let hand manager evaluate the target position for this card)
        Vector3 targetPos = startPos;
        Quaternion targetRot = Quaternion.identity;
        Vector3 targetScale = startScale;

        if (handManager != null)
        {
            // Find our index in the hand list
            int index = handManager.currentHandCards.IndexOf(gameObject);
            int totalCards = handManager.currentHandCards.Count;

            if (index != -1 && totalCards > 0)
            {
                // Replicate the hand layout calculation to find our exact final slot
                // (Matches your CalculateSpread and HandAreaAnchor logic from HandManager_3D)
                float currentSpread = handManager.widthSpread;
                if (totalCards > 1)
                {
                    float totalCalculatedWidth = (totalCards - 1) * handManager.widthSpread;
                    currentSpread = totalCalculatedWidth > handManager.maxHandWidth ? handManager.maxHandWidth / (totalCards - 1) : handManager.widthSpread;
                }

                float xOffset = (index - (totalCards - 1) / 2.0f) * currentSpread;
                float yOffset = handManager.baseHeightY + (index * handManager.layerOffsetY);
                float zOffset = 0f;

                if (handManager.handAreaAnchor != null)
                {
                    Vector3 localPos = new Vector3(xOffset, yOffset, zOffset);
                    targetPos = handManager.handAreaAnchor.position + handManager.handAreaAnchor.TransformDirection(localPos);

                    float rotationAngle = -xOffset * 12f;
                    Quaternion localRot = Quaternion.Euler(60f, 0f, rotationAngle);
                    // Final upright rotation matching hand fan
                    targetRot = handManager.handAreaAnchor.rotation * localRot;
                }
                else
                {
                    targetPos = new Vector3(xOffset, yOffset, -1.5f + zOffset);
                    float rotationAngle = -xOffset * 12f;
                    targetRot = Quaternion.Euler(60f, 0f, rotationAngle);
                }
            }
        }

        // 3. Lerp everything smoothly over 0.3 seconds
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth ease-out curve for motion
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
}