using UnityEngine;

public class CardDraggable3D : MonoBehaviour
{
    private Camera cam;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool isDragging = false;
    private Plane dragPlane;
    private Vector3 lastMousePos;
    private Collider cardCollider;

    private HandManager_3D handManager;
    private DeckManager_3D deckManager;
    private ShitheadMemory.Card cardData;

    [Header("References")]
    public PileManager_3D pileManager;

    // NEU: Merkt sich, ob die Karte frisch gezogen wurde und noch nicht "gesichert" ist
    private bool isNewlyDrawn = true;

    public bool IsDragging => isDragging;

    public void Init(HandManager_3D hand, DeckManager_3D deck, ShitheadMemory.Card data)
    {
        handManager = hand;
        deckManager = deck;
        cardData = data;
        isNewlyDrawn = true; // Frisch erstellt = neu gezogen
    }

    void Start()
    {
        cam = Camera.main;
        dragPlane = new Plane(Vector3.up, new Vector3(0, 0.6f, 0));
        cardCollider = GetComponent<Collider>();
    }
    private void OnMouseEnter()
    {
        // Nur hoovern, wenn nicht gerade gezockt/gezogen wird
        if (!isDragging && handManager != null)
        {
            handManager.SetHoveredCard(gameObject);
        }
    }

    private void OnMouseExit()
    {
        if (handManager != null)
        {
            handManager.ClearHoveredCard(gameObject);
        }
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

    public void StartDraggingImmediately()
    {
        PrepareForDrag();
    }

    void CheckForClickToDrag()
    {
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = cam.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
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
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        isDragging = true;
        lastMousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        Vector3 pos = transform.position;
        pos.y = 0.6f;
        transform.position = pos;
    }

    void HandleDraggingMovement()
    {
        Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Vector3 mouseDelta = (Vector3)mouseScreenPos - lastMousePos;
        lastMousePos = mouseScreenPos;

        Ray ray = cam.ScreenPointToRay(mouseScreenPos);
        float enter;
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 targetPoint = ray.GetPoint(enter);
            targetPoint.y = 0.6f;
            transform.position = targetPoint;
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
        if (cardCollider != null) cardCollider.enabled = false;

        Vector3 rayStartPos = new Vector3(transform.position.x, 5f, transform.position.z);
        Ray ray = new Ray(rayStartPos, Vector3.down);

        RaycastHit hit;
        bool isValidDrop = false;

        if (Physics.Raycast(ray, out hit, 10f))
        {
            string hitObjectName = hit.collider.gameObject.name;
            bool hitAnotherCard = hit.collider.GetComponent<CardDraggable3D>() != null;

            if (hitObjectName == "PileArea_3D" || hitObjectName.Contains("HandArea") || hitAnotherCard)
            {
                isValidDrop = true;

                if (hitObjectName == "PileArea_3D")
                {
                    DropOnPile(hit.point);
                }
                else
                {
                    DropInHand();
                }
                return;
            }
        }

        // Wenn KEINE gültige Drop-Zone getroffen wurde:
        if (!isValidDrop)
        {
            if (isNewlyDrawn)
            {
                // Fall 1: Karte wurde frisch gezogen und noch nie in der Hand abgelegt -> Zurück ins Deck!
                ReturnToDeckAndDestroy();
            }
            else
            {
                // Fall 2: Karte war bereits in der Hand -> Fliegt/gleitet zurück in die Hand!
                ReturnToHand();
            }
        }
    }

    void DropOnPile(Vector3 hitPoint)
    {
        if (handManager != null)
        {
            handManager.RemoveCardFromHand(gameObject);
        }

        // Dem PileManager Bescheid geben, welche Karte gespielt wurde!
        if (pileManager != null)
        {
            pileManager.AddCardToPile(cardData, gameObject);
        }

        Vector3 targetPos = new Vector3(hitPoint.x, 0.16f, hitPoint.z);
        float randomTilt = Random.Range(-10f, 10f);
        Quaternion targetRot = Quaternion.Euler(90f, 0f, randomTilt);

        if (cardCollider != null) cardCollider.enabled = false;

        CardAnimation_3D anim = GetComponent<CardAnimation_3D>();
        if (anim != null)
        {
            anim.PlayTween(targetPos, targetRot, 0.25f, () => {
                Destroy(this); // Nur das Drag-Skript zerstören, das GameObject bleibt als Ablage liegen!
            });
        }
        else
        {
            transform.position = targetPos;
            transform.rotation = targetRot;
            Destroy(this);
        }
    }

    void DropInHand()
    {
        // Ab hier gilt die Karte als offiziell in der Hand aufgenommen
        isNewlyDrawn = false;

        if (handManager != null)
        {
            handManager.OnCardDroppedInHand(gameObject);
        }

        if (cardCollider != null) cardCollider.enabled = true;
    }

    // NEU: Bringt eine bereits existierende Handkarte sauber zurück in den Fächer, wenn sie ins Leere gezogen wurde
    void ReturnToHand()
    {
        if (cardCollider != null) cardCollider.enabled = true;

        if (handManager != null)
        {
            // Zwingt den HandManager, die Positionen und den Fächer für diese Karte neu zu berechnen (gleitet geschmeidig zurück)
            handManager.RedistributeHandCards();
        }
        Debug.Log("Karte außerhalb losgelassen – fliegt zurück in die Hand.");
    }

    void ReturnToDeckAndDestroy()
    {
        // 1. Aus der Hand-Liste austragen
        if (handManager != null)
        {
            handManager.RemoveCardFromHand(gameObject);
        }

        if (cardCollider != null) cardCollider.enabled = false;

        // 2. Zielposition definieren (Dort, wo der Nachziehstapel liegt, z. B. X = 1.4, Y = 0.2, Z = 0)
        Vector3 deckTargetPos = new Vector3(1.4f, 0.2f, 0f);
        Quaternion deckTargetRot = Quaternion.Euler(75f, 0f, 0f);

        // 3. Über das Animations-Skript sanft zum Deck gleiten lassen, erst danach ins Deck einsortieren & zerstören
        CardAnimation_3D anim = GetComponent<CardAnimation_3D>();
        if (anim != null)
        {
            anim.PlayTween(deckTargetPos, deckTargetRot, 0.25f, () => {
                if (deckManager != null && cardData != null)
                {
                    deckManager.ReturnCardToDeck(cardData);
                }
                Destroy(gameObject);
            });
        }
        else
        {
            // Fallback, falls kein Animator da ist
            if (deckManager != null && cardData != null)
            {
                deckManager.ReturnCardToDeck(cardData);
            }
            Destroy(gameObject);
        }

        Debug.Log("Frisch gezogene Karte gleitet geschmeidig zurück ins Deck.");
    }
}