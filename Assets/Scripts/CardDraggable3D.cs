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
    private bool isIllegalOnTable = false;
    private bool isCurrentlyHovering = false;

    // --- FACE-DOWN & FLIP STATE ---
    public bool isFaceDown = false;
    private bool isFlipping = false;
    public bool IsFlipping => isFlipping;

    public void SetFaceDown(bool faceDown)
    {
        isFaceDown = faceDown;
    }
    // ----------------------------

    public bool IsDragging => isDragging;

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
            if (handManager != null) handManager.ClearHoveredCard(gameObject);
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

    void HandleDraggingMovement()
    {
        if (PlayerHandCursor_3D.Instance != null)
        {
            Vector3 targetCardPos = PlayerHandCursor_3D.Instance.CardSnapPosition;
            targetCardPos.y = 0.6f;

            float horizontalOffset = 0.63f;
            float forwardOffset = 0.0f;

            targetCardPos -= PlayerHandCursor_3D.Instance.transform.right * horizontalOffset;
            targetCardPos += PlayerHandCursor_3D.Instance.transform.forward * forwardOffset;

            transform.position = targetCardPos;

            Quaternion cursorRot = PlayerHandCursor_3D.Instance.transform.rotation;
            Quaternion pitchOffset = Quaternion.Euler(PlayerHandCursor_3D.Instance.dragPitchX, 0f, 0f);
            transform.rotation = cursorRot * pitchOffset;
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

        if (transform.position.z < -0.8f)
        {
            DropInHand();
            return;
        }

        if (IsDroppedOnPile(out Vector3 targetDropPoint))
        {
            if (RuleManager_3D.Instance != null && RuleManager_3D.Instance.IsMoveLegal(cardData))
            {
                ExecuteLegalDropOnPile();
            }
            else
            {
                ExecuteIllegalDrop("Hey! Die Karte ist zu niedrig, du kannst sie hier nicht ablegen!");
            }
            return;
        }

        ExecuteIllegalDrop("Was soll das werden? Die Karte liegt mitten auf dem Tisch!");
    }

    bool IsDroppedOnPile(out Vector3 targetDropPoint)
    {
        targetDropPoint = transform.position;
        if (pileManager == null && RuleManager_3D.Instance != null) pileManager = RuleManager_3D.Instance.pileManager;

        if (pileManager != null)
        {
            float distance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(pileManager.transform.position.x, 0, pileManager.transform.position.z));
            if (distance < 1.5f)
            {
                targetDropPoint = pileManager.transform.position;
                return true;
            }
        }
        return false;
    }

    void DropInHand()
    {
        DisablePhysics();
        handManager?.AddCardToHand(gameObject);
        PlayerHandCursor_3D.Instance?.NotifyCardReleased();

        if (isIllegalOnTable)
        {
            isIllegalOnTable = false;
            TurnManager_3D.Instance?.ResolveIllegalCardOnTable();
        }

        // --- SMOOTH FLIP OR INSTANT PLACEMENT ---
        if (isFaceDown)
        {
            StartCoroutine(FlipToFaceUpRoutine());
        }
        else
        {
            Quaternion targetRot = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
            transform.rotation = targetRot;
            if (cardCollider != null) cardCollider.enabled = true;
        }
    }

    private IEnumerator FlipToFaceUpRoutine()
    {
        isFlipping = true;
        isFaceDown = false;

        Quaternion startRot = transform.rotation;
        Quaternion targetRot = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

        float duration = 0.3f; // Smooth flip duration in seconds
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        transform.rotation = targetRot;
        isFlipping = false;
        if (cardCollider != null) cardCollider.enabled = true;
    }

    void ExecuteLegalDropOnPile()
    {
        handManager?.RemoveCardFromHand(gameObject);
        PlayerHandCursor_3D.Instance?.NotifyCardReleased();

        if (RuleManager_3D.Instance != null && cardData != null)
        {
            RuleManager_3D.Instance.player1Hand.Remove(cardData);
            RuleManager_3D.Instance.ResetFreeTurn();
        }

        pileManager?.AddCardToPile(cardData, gameObject);
        transform.rotation = Quaternion.Euler(90, Random.Range(-10f, 10f), 0);
        EnablePhysics();

        TurnManager_3D.Instance?.OnPlayerCompletedMove();
        Destroy(this);
    }

    void ExecuteIllegalDrop(string message)
    {
        OpponentDialogueManager_3D.Instance?.ShowComplaint(message);
        EnemyAI_3D.Instance?.RecordPlayerMistake();
        handManager?.RemoveCardFromHand(gameObject);
        PlayerHandCursor_3D.Instance?.NotifyCardReleased();

        if (RuleManager_3D.Instance != null && cardData != null)
        {
            RuleManager_3D.Instance.player1Hand.Remove(cardData);
        }

        if (!isIllegalOnTable)
        {
            isIllegalOnTable = true;
            TurnManager_3D.Instance?.RegisterIllegalCardOnTable();
        }

        transform.rotation = Quaternion.Euler(90, Random.Range(-10f, 10f), 0);
        EnablePhysics();
    }

    void EnablePhysics()
    {
        if (cardCollider != null) cardCollider.enabled = true;
        if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
    }

    void DisablePhysics()
    {
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
    }
}