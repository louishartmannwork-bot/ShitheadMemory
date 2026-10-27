using UnityEngine;
using ShitheadMemory;

public class CardDraggable3D : MonoBehaviour
{
    private Camera cam;
    private Plane dragPlane;
    private Vector2 lastMousePos;
    private Collider cardCollider;
    private Rigidbody rb;

    private HandManager_3D handManager;
    private DeckManager_3D deckManager;
    public Card cardData;

    [Header("References")]
    public PileManager_3D pileManager;

    [Header("State Tracking")]
    private bool isDragging = false;
    private bool isIllegalOnTable = false;

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
        dragPlane = new Plane(Vector3.up, new Vector3(0, 0.6f, 0));
        cardCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        DisablePhysics();
    }

    void Update()
    {
        if (!isDragging)
        {
            CheckForClickToDrag();
        }
        else
        {
            HandleDraggingMovement();
            CheckForMouseRelease();
        }
    }

    private void OnMouseEnter()
    {
        if (!isDragging && handManager != null)
        {
            handManager.SetHoveredCard(gameObject);
        }

        if (PlayerHandCursor_3D.Instance != null)
        {
            PlayerHandCursor_3D.Instance.NotifyHoverEnter();
        }
    }

    private void OnMouseExit()
    {
        if (handManager != null)
        {
            handManager.ClearHoveredCard(gameObject);
        }

        if (PlayerHandCursor_3D.Instance != null)
        {
            PlayerHandCursor_3D.Instance.NotifyHoverExit();
        }
    }

    public void StartDraggingImmediately() => PrepareForDrag();

    void CheckForClickToDrag()
    {
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider != null && hit.collider.gameObject == gameObject)
                {
                    PrepareForDrag();
                }
            }
        }
    }

    void PrepareForDrag()
    {
        if (TurnManager_3D.Instance != null && !TurnManager_3D.Instance.IsPlayerTurn())
        {
            Debug.Log("Warte, bis du am Zug bist!");
            return;
        }

        DisablePhysics();
        handManager?.OnCardPickedUp(gameObject);

        isDragging = true;
        lastMousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        Vector3 pos = transform.position;
        pos.y = 0.6f;
        transform.position = pos;

        if (PlayerHandCursor_3D.Instance != null)
        {
            PlayerHandCursor_3D.Instance.NotifyCardPickedUp();
        }
    }

    void HandleDraggingMovement()
    {
        Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Vector2 mouseDelta = mouseScreenPos - lastMousePos;
        lastMousePos = mouseScreenPos;

        if (PlayerHandCursor_3D.Instance != null)
        {
            Vector3 handPos = PlayerHandCursor_3D.Instance.transform.position;
            Vector3 targetCardPos = handPos;
            targetCardPos.y = 0.6f;

            transform.position = targetCardPos;
        }

        float tiltZ = Mathf.Clamp(-mouseDelta.x * 0.1f, -15f, 15f);
        transform.rotation = Quaternion.Euler(75f, 0f, tiltZ);
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
                ExecuteLegalDropOnPile(targetDropPoint);
            }
            else
            {
                ExecuteIllegalDrop("Hey! Die Karte ist zu niedrig, du kannst sie hier nicht ablegen!");
            }
            return;
        }

        ExecuteIllegalDrop("Was soll das werden? Die Karte liegt mitten auf dem Tisch, nimm sie wieder auf!");
    }

    bool IsDroppedOnPile(out Vector3 targetDropPoint)
    {
        targetDropPoint = transform.position;

        Vector3 rayStartPos = new Vector3(transform.position.x, 5f, transform.position.z);
        RaycastHit[] hits = Physics.RaycastAll(new Ray(rayStartPos, Vector3.down), 10f);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;

            PileManager_3D foundPile = hit.collider.GetComponentInParent<PileManager_3D>();
            if (foundPile != null)
            {
                if (pileManager == null) pileManager = foundPile;
                targetDropPoint = hit.point;
                return true;
            }
        }

        if (pileManager == null && RuleManager_3D.Instance != null)
        {
            pileManager = RuleManager_3D.Instance.pileManager;
        }

        if (pileManager != null)
        {
            float distanceToPile = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(pileManager.transform.position.x, 0, pileManager.transform.position.z)
            );

            if (distanceToPile < 1.5f)
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

        if (isIllegalOnTable)
        {
            isIllegalOnTable = false;
            TurnManager_3D.Instance?.ResolveIllegalCardOnTable();
        }

        if (cardCollider != null) cardCollider.enabled = true;
        Debug.Log("Karte erfolgreich in der Hand einsortiert.");
    }

    void ExecuteLegalDropOnPile(Vector3 hitPoint)
    {
        handManager?.RemoveCardFromHand(gameObject);

        if (RuleManager_3D.Instance != null && cardData != null)
        {
            RuleManager_3D.Instance.player1Hand.Remove(cardData);

            // Freien Zug nach erfolgreichem Ablegen beenden
            RuleManager_3D.Instance.ResetFreeTurn();
        }

        pileManager?.AddCardToPile(cardData, gameObject);

        transform.rotation = Quaternion.Euler(90, Random.Range(-10f, 10f), 0);
        EnablePhysics();

        // Dem TurnManager signalisieren, dass die Karte gelegt wurde 
        // (Er wechselt nun intern in 'WaitingForPlayerDraw', falls Hand < 3 ist)
        TurnManager_3D.Instance?.OnPlayerCompletedMove();

        Destroy(this);
    }

    void ExecuteIllegalDrop(string message)
    {
        OpponentDialogueManager_3D.Instance?.ShowComplaint(message);
        EnemyAI_3D.Instance?.RecordPlayerMistake();
        handManager?.RemoveCardFromHand(gameObject);

        // --- AUCH BEI FEHLERN WIRD SIE AUS DER HAND ENTFERNT ---
        if (RuleManager_3D.Instance != null && cardData != null)
        {
            RuleManager_3D.Instance.player1Hand.Remove(cardData);
        }
        // ------------------------------------------------------

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
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    void DisablePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }
}