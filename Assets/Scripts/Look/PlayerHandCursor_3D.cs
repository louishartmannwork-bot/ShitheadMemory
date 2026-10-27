using UnityEngine;

public class PlayerHandCursor_3D : MonoBehaviour
{
    public static PlayerHandCursor_3D Instance;

    public enum HandState { Idle, Hover, Grab, Dragging }

    [Header("References")]
    [SerializeField] private SpriteRenderer handSpriteRenderer;
    [SerializeField] private Transform armTransform;

    [Header("Heights per State")]
    [SerializeField] private float idleHeight = 0.45f;
    [SerializeField] private float hoverHeight = 0.65f;
    [SerializeField] private float dragHeight = 0.85f;

    [Header("Rotation per State (X-Achse Neigung)")]
    [SerializeField] private float idlePitchX = 75f;   // Neigung im Standard
    [SerializeField] private float hoverPitchX = 60f;  // Neigung beim Hovern (z.B. 60 Grad)
    [SerializeField] private float dragPitchX = 75f;   // Neigung beim Ziehen

    [Header("Debug Color Coding")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color hoverColor = Color.yellow;
    [SerializeField] private Color grabColor = Color.green;

    [Header("Movement & Rotation Settings")]
    [SerializeField] private float followSpeed = 30f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private Vector3 mouseOffset = new Vector2(0.2f, -0.2f);

    [Header("Arm Anchor")]
    [SerializeField] private Vector3 screenEdgeAnchor = new Vector3(0f, -1.5f, -3.5f);

    [Header("Arm Reach Limits")]
    [SerializeField] private float maxArmReach = 6.0f;     // Maximale Länge des Arms
    [SerializeField] private float minArmReach = 1.0f;     // Minimale Distanz (damit sie nicht im Anker "versinkt")

    [Header("Camera Lean on Max Reach")]
    [SerializeField] private bool enableCameraLean = true;
    [SerializeField] private float maxCameraPitchAdd = 5.0f; // Max. Neigung nach oben/unten
    [SerializeField] private float maxCameraYawAdd = 5.0f;   // Max. Drehung nach links/rechts
    [SerializeField] private float cameraLeanSpeed = 10f;

    private Quaternion initialCamRotation;

    private Vector3 lastValidTargetPoint;

    private Camera cam;
    private HandState currentState = HandState.Idle;
    private float targetHeightY;
    private float targetPitchX; // Die aktuelle Ziel-Neigung auf der X-Achse

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        cam = Camera.main;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        targetHeightY = idleHeight;
        targetPitchX = idlePitchX;
        SetState(HandState.Idle);

        lastValidTargetPoint = transform.position;

        if (cam != null)
        {
            initialCamRotation = cam.transform.rotation;
        }
    }

    void Update()
    {
        FollowMousePosition();
        UpdateHandRotation();
        UpdateArmTransform();
        HandleInputState();
    }

    private void FollowMousePosition()
    {
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        // TRICK GEGEN DAS ZITTERN: 
        // Wir setzen die Kamera für den Raycast kurz auf ihre Start-Rotation zurück,
        // damit sich Kamera-Neigung und Maus-Berechnung nicht gegenseitig beeinflussen.
        Quaternion tempRot = cam.transform.rotation;
        cam.transform.rotation = initialCamRotation;
        Ray ray = cam.ScreenPointToRay(mousePos);
        cam.transform.rotation = tempRot; // Sofort wiederherstellen

        Plane interactionPlane = new Plane(Vector3.up, new Vector3(0, targetHeightY, 0));

        Vector3 targetPoint = lastValidTargetPoint;

        if (interactionPlane.Raycast(ray, out float enter))
        {
            Vector3 rawHitPoint = ray.GetPoint(enter) + new Vector3(mouseOffset.x, 0, mouseOffset.y);

            Vector3 anchorXZ = new Vector3(screenEdgeAnchor.x, rawHitPoint.y, screenEdgeAnchor.z);
            Vector3 fromAnchorToTarget = rawHitPoint - anchorXZ;

            float distanceFromAnchor = fromAnchorToTarget.magnitude;
            float excessAmount = 0f;
            Vector3 directionToTarget = fromAnchorToTarget.normalized;

            // --- REICHWEITE PRÜFEN ---
            if (distanceFromAnchor > maxArmReach)
            {
                excessAmount = distanceFromAnchor - maxArmReach;
                fromAnchorToTarget = directionToTarget * maxArmReach;
                rawHitPoint = anchorXZ + fromAnchorToTarget;
            }
            else if (distanceFromAnchor < minArmReach)
            {
                fromAnchorToTarget = directionToTarget * minArmReach;
                rawHitPoint = anchorXZ + fromAnchorToTarget;
            }

            targetPoint = rawHitPoint;
            lastValidTargetPoint = targetPoint;

            // --- KAMERA-LEAN BEI MAXIMALER REICHWEITE ---
            if (enableCameraLean && cam != null)
            {
                float targetPitchOffset = 0f;
                float targetYawOffset = 0f;

                if (excessAmount > 0f)
                {
                    float clampedExcess = Mathf.Min(excessAmount, 2f);
                    targetPitchOffset = -directionToTarget.z * clampedExcess * maxCameraPitchAdd;
                    targetYawOffset = directionToTarget.x * clampedExcess * maxCameraYawAdd;
                }

                Quaternion offsetRotation = Quaternion.Euler(targetPitchOffset, targetYawOffset, 0f);
                Quaternion targetCamRotation = initialCamRotation * offsetRotation;

                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetCamRotation, Time.deltaTime * cameraLeanSpeed);
            }
        }
        else
        {
            if (enableCameraLean && cam != null)
            {
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, initialCamRotation, Time.deltaTime * cameraLeanSpeed);
            }
        }

        // Hand bewegt sich flüssig zum Ziel
        transform.position = Vector3.Lerp(transform.position, targetPoint, Time.deltaTime * followSpeed);
    }

    private void UpdateHandRotation()
    {
        // Rotiert die Hand sanft auf der X-Achse zum jeweiligen Ziel-Winkel des States
        Quaternion targetRotation = Quaternion.Euler(targetPitchX, 0f, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    private void HandleInputState()
    {
        if (UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
        {
            if (currentState != HandState.Dragging && currentState != HandState.Grab)
            {
                SetState(HandState.Dragging);
            }
        }
        else if (currentState == HandState.Dragging || currentState == HandState.Grab)
        {
            SetState(HandState.Idle);
        }
    }

    // --- SCHNITTSTELLE FÜR ANDERE SKRIPTE ---

    public void NotifyHoverEnter()
    {
        if (currentState != HandState.Dragging && currentState != HandState.Grab)
        {
            SetState(HandState.Hover);
        }
    }

    public void NotifyHoverExit()
    {
        if (currentState != HandState.Dragging && currentState != HandState.Grab)
        {
            SetState(HandState.Idle);
        }
    }

    public void NotifyCardPickedUp()
    {
        SetState(HandState.Dragging);
    }

    // ----------------------------------------

    private void SetState(HandState newState)
    {
        currentState = newState;
        UpdateStateValues();
        UpdateVisuals();
    }

    private void UpdateStateValues()
    {
        switch (currentState)
        {
            case HandState.Idle:
                targetHeightY = idleHeight;
                targetPitchX = idlePitchX;
                break;
            case HandState.Hover:
                targetHeightY = hoverHeight;
                targetPitchX = hoverPitchX; // Hier greifen die 60 Grad!
                break;
            case HandState.Grab:
            case HandState.Dragging:
                targetHeightY = dragHeight;
                targetPitchX = dragPitchX;
                break;
        }
    }

    private void UpdateArmTransform()
    {
        if (armTransform == null) return;

        Vector3 handPos = transform.position;
        armTransform.position = (screenEdgeAnchor + handPos) / 2f;

        Vector3 direction = handPos - screenEdgeAnchor;
        if (direction != Vector3.zero)
        {
            armTransform.rotation = Quaternion.LookRotation(direction);
        }

        float distance = direction.magnitude;
        Vector3 currentScale = armTransform.localScale;
        armTransform.localScale = new Vector3(currentScale.x, currentScale.y, distance);
    }

    private void UpdateVisuals()
    {
        if (handSpriteRenderer == null) return;

        switch (currentState)
        {
            case HandState.Idle: handSpriteRenderer.color = idleColor; break;
            case HandState.Hover: handSpriteRenderer.color = hoverColor; break;
            case HandState.Grab:
            case HandState.Dragging: handSpriteRenderer.color = grabColor; break;
        }
    }
}