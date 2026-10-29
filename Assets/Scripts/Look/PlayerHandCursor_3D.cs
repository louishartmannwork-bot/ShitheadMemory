using UnityEngine;
using ShitheadMemory;

public class PlayerHandCursor_3D : MonoBehaviour
{
    public static PlayerHandCursor_3D Instance;

    public enum HandState { Idle, HandHover, PileHover, GrabEmpty, Dragging }

    [Header("References")]
    [SerializeField] private Transform armRotatePivot;
    [SerializeField] private Transform handRotatePivot;
    [SerializeField] private Transform cursorHotspot;
    [SerializeField] private SpriteRenderer handSpriteRenderer;

    [Header("Sprites per State")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite handHoverSprite;
    [SerializeField] private Sprite pileHoverSprite;
    [SerializeField] private Sprite grabEmptySprite;
    [SerializeField] private Sprite dragSprite;

    [Header("Height Offsets per State (Added to Scene Baselines)")]
    [SerializeField] private float idleHeightOffset = 0f;
    [SerializeField] private float handHoverHeightOffset = 0.55f;
    [SerializeField] private float pileHoverHeightOffset = 0.1f;
    [SerializeField] private float grabEmptyHeightOffset = 0.05f;
    [SerializeField] private float dragHeightOffset = 0.3f;

    [Header("Hand Pitch X-Rotation per State")]
    [SerializeField] private float idlePitchX = 90f;
    [SerializeField] private float handHoverPitchX = 65f;
    [SerializeField] private float pileHoverPitchX = 80f;
    [SerializeField] private float grabEmptyPitchX = 90f;
    [SerializeField] public float dragPitchX = 80f;

    [Header("Arm Pitch Offsets per State")]
    [SerializeField] private float armIdlePitchOffset = 0f;
    [SerializeField] private float armHandHoverPitchOffset = 0f;
    [SerializeField] private float armPileHoverPitchOffset = 40.85f;
    [SerializeField] private float armGrabEmptyPitchOffset = 0f;
    [SerializeField] private float armDragPitchOffset = 0f;

    [Header("Movement & Cursor Alignment")]
    [SerializeField] private float followSpeed = 25f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private Vector3 screenEdgeAnchor = new Vector3(2f, 0f, -3f);
    [SerializeField] private float maxArmReach = 4f;
    [SerializeField] private float minArmReach = 1f;

    [Header("Camera Lean & Follow Settings")]
    [SerializeField] private bool enableCameraLean = true;
    [SerializeField] private float insideCamFollowWeight = 0.15f;
    [SerializeField] private float maxCameraPitchAdd = 8.0f;
    [SerializeField] private float maxCameraYawAdd = 8.0f;
    [SerializeField] private float cameraLeanSpeed = 10f;

    public Vector3 CardSnapPosition => cursorHotspot != null ? cursorHotspot.position : transform.position;

    private Camera cam;
    private HandState currentState = HandState.Idle;

    private float targetHeightOffset;
    private float currentHeightOffset;
    private float targetPitchX;
    private float targetArmPitchOffset;
    private Vector3 lastValidTargetPoint;
    private Quaternion initialCamRotation;

    private Vector3 initialArmLocalPos;
    private Quaternion initialArmLocalRot;
    private Vector3 initialHandLocalPos;
    private Quaternion initialHandLocalRot;
    private float initialHandPitchX;

    private int handHoverCount = 0;
    private int pileHoverCount = 0;
    private bool isHoldingCard = false;

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

        if (armRotatePivot != null)
        {
            initialArmLocalPos = armRotatePivot.localPosition;
            initialArmLocalRot = armRotatePivot.localRotation;
        }
        if (handRotatePivot != null)
        {
            initialHandLocalPos = handRotatePivot.localPosition;
            initialHandLocalRot = handRotatePivot.localRotation;
            initialHandPitchX = handRotatePivot.localEulerAngles.x;
        }

        targetHeightOffset = idleHeightOffset;
        currentHeightOffset = idleHeightOffset;
        targetPitchX = idlePitchX;
        targetArmPitchOffset = armIdlePitchOffset;
        SetState(HandState.Idle);

        lastValidTargetPoint = transform.position;
        if (cam != null) initialCamRotation = cam.transform.rotation;
    }

    void Update()
    {
        FollowMousePosition();
        UpdateHeights();
        UpdateRotations();
        HandleInputState();
    }

    private void FollowMousePosition()
    {
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();

        Quaternion tempRot = cam.transform.rotation;
        cam.transform.rotation = initialCamRotation;
        Ray ray = cam.ScreenPointToRay(mousePos);
        cam.transform.rotation = tempRot;

        // Plane locked to current height offset to prevent perspective parallax offset
        Plane tablePlane = new Plane(Vector3.up, new Vector3(0f, currentHeightOffset, 0f));
        Vector3 targetPoint = lastValidTargetPoint;

        if (tablePlane.Raycast(ray, out float enter))
        {
            Vector3 rawHitPoint = ray.GetPoint(enter);
            Vector3 anchorXZ = new Vector3(screenEdgeAnchor.x, 0f, screenEdgeAnchor.z);
            Vector3 fromAnchorToTarget = rawHitPoint - anchorXZ;

            float distanceFromAnchor = fromAnchorToTarget.magnitude;
            float excessAmount = 0f;
            Vector3 directionToTarget = fromAnchorToTarget.normalized;

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

            targetPoint = new Vector3(rawHitPoint.x, currentHeightOffset, rawHitPoint.z);

            if (cursorHotspot != null)
            {
                Vector3 hotspotOffset = cursorHotspot.position - transform.position;
                targetPoint -= new Vector3(hotspotOffset.x, 0f, hotspotOffset.z);
            }

            lastValidTargetPoint = targetPoint;

            if (enableCameraLean && cam != null)
            {
                float targetPitchOffset = 0f;
                float targetYawOffset = 0f;

                float normalReachFactor = Mathf.Clamp01(distanceFromAnchor / maxArmReach);
                targetPitchOffset += -directionToTarget.z * normalReachFactor * insideCamFollowWeight * maxCameraPitchAdd;
                targetYawOffset += directionToTarget.x * normalReachFactor * insideCamFollowWeight * maxCameraYawAdd;

                if (excessAmount > 0f)
                {
                    float clampedExcess = Mathf.Min(excessAmount, 3f);
                    targetPitchOffset += -directionToTarget.z * clampedExcess * maxCameraPitchAdd;
                    targetYawOffset += directionToTarget.x * clampedExcess * maxCameraYawAdd;
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

        transform.position = Vector3.Lerp(transform.position, targetPoint, Time.deltaTime * followSpeed);
    }

    private void UpdateHeights()
    {
        currentHeightOffset = Mathf.Lerp(currentHeightOffset, targetHeightOffset, Time.deltaTime * followSpeed);

        if (armRotatePivot != null)
        {
            Vector3 targetPos = initialArmLocalPos;
            targetPos.y += currentHeightOffset;
            armRotatePivot.localPosition = Vector3.Lerp(armRotatePivot.localPosition, targetPos, Time.deltaTime * followSpeed);
        }

        if (handRotatePivot != null)
        {
            Vector3 targetPos = initialHandLocalPos;
            targetPos.y += currentHeightOffset;
            handRotatePivot.localPosition = Vector3.Lerp(handRotatePivot.localPosition, targetPos, Time.deltaTime * followSpeed);
        }
    }

    private void UpdateRotations()
    {
        Vector3 directionToAnchor = transform.position - screenEdgeAnchor;
        directionToAnchor.y = 0f;
        if (directionToAnchor != Vector3.zero)
        {
            Quaternion lookAtRotation = Quaternion.LookRotation(directionToAnchor);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookAtRotation, Time.deltaTime * rotationSpeed);
        }

        if (armRotatePivot != null)
        {
            Quaternion targetArmRot = initialArmLocalRot * Quaternion.Euler(targetArmPitchOffset, 0f, 0f);
            armRotatePivot.localRotation = Quaternion.Slerp(armRotatePivot.localRotation, targetArmRot, Time.deltaTime * rotationSpeed);
        }

        if (handRotatePivot != null)
        {
            float pitchDelta = targetPitchX - initialHandPitchX;
            Quaternion targetHandRot = initialHandLocalRot * Quaternion.Euler(pitchDelta, 0f, 0f);
            handRotatePivot.localRotation = Quaternion.Slerp(handRotatePivot.localRotation, targetHandRot, Time.deltaTime * rotationSpeed);
        }
    }

    private void HandleInputState()
    {
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (isHoldingCard) SetState(HandState.Dragging);
            else SetState(HandState.GrabEmpty);
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (!isHoldingCard)
            {
                EvaluateHoverState();
            }
        }
    }

    public void NotifyHoverEnter() => NotifyPileHoverEnter();
    public void NotifyHoverExit() => NotifyPileHoverExit();

    public void NotifyHandHoverEnter()
    {
        handHoverCount++;
        EvaluateHoverState();
    }

    public void NotifyHandHoverExit()
    {
        handHoverCount = Mathf.Max(0, handHoverCount - 1);
        Debug.Log($"[Hand Hover Exit] New handHoverCount: {handHoverCount}");
        EvaluateHoverState();
    }

    public void NotifyPileHoverEnter()
    {
        pileHoverCount++;
        EvaluateHoverState();
    }

    public void NotifyPileHoverExit()
    {
        pileHoverCount = Mathf.Max(0, pileHoverCount - 1);
        EvaluateHoverState();
    }

    private void EvaluateHoverState()
    {
        if (mouseButtonDown || isHoldingCard || currentState == HandState.Dragging) return;
        ForceEvaluateHoverState();
    }

    private void ForceEvaluateHoverState()
    {
        if (isHoldingCard) return;

        if (handHoverCount > 0) SetState(HandState.HandHover);
        else if (pileHoverCount > 0) SetState(HandState.PileHover);
        else SetState(HandState.Idle);
    }

    private bool mouseButtonDown => UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;

    public void NotifyCardPickedUp()
    {
        isHoldingCard = true;
        SetState(HandState.Dragging);
    }

    public void NotifyCardReleased()
    {
        isHoldingCard = false;

        // Clean reset on release. Natural OnMouseEnter events will instantly 
        // re-trigger the correct hover state on the very next frame if the mouse is over a card.
        handHoverCount = 0;
        pileHoverCount = 0;
        ForceEvaluateHoverState();
    }

    private void SetState(HandState newState)
    {
        if (currentState != newState)
        {
            Debug.Log($"[Cursor State Change] Changing from {currentState} to {newState} | HandCount: {handHoverCount} | PileCount: {pileHoverCount}");
        }

        currentState = newState;
        UpdateStateValues();
        UpdateVisuals();
    }

    private void UpdateStateValues()
    {
        switch (currentState)
        {
            case HandState.Idle:
                targetHeightOffset = idleHeightOffset;
                targetPitchX = idlePitchX;
                targetArmPitchOffset = armIdlePitchOffset;
                break;
            case HandState.HandHover:
                targetHeightOffset = handHoverHeightOffset;
                targetPitchX = handHoverPitchX;
                targetArmPitchOffset = armHandHoverPitchOffset;
                break;
            case HandState.PileHover:
                targetHeightOffset = pileHoverHeightOffset;
                targetPitchX = pileHoverPitchX;
                targetArmPitchOffset = armPileHoverPitchOffset;
                break;
            case HandState.GrabEmpty:
                targetHeightOffset = grabEmptyHeightOffset;
                targetPitchX = grabEmptyPitchX;
                targetArmPitchOffset = armGrabEmptyPitchOffset;
                break;
            case HandState.Dragging:
                targetHeightOffset = dragHeightOffset;
                targetPitchX = dragPitchX;
                targetArmPitchOffset = armDragPitchOffset;
                break;
        }
    }

    private void UpdateVisuals()
    {
        if (handSpriteRenderer == null) return;

        switch (currentState)
        {
            case HandState.Idle:
                if (idleSprite != null) handSpriteRenderer.sprite = idleSprite;
                break;
            case HandState.HandHover:
                if (handHoverSprite != null) handSpriteRenderer.sprite = handHoverSprite;
                else if (idleSprite != null) handSpriteRenderer.sprite = idleSprite;
                break;
            case HandState.PileHover:
                if (pileHoverSprite != null) handSpriteRenderer.sprite = pileHoverSprite;
                else if (idleSprite != null) handSpriteRenderer.sprite = idleSprite;
                break;
            case HandState.GrabEmpty:
                if (grabEmptySprite != null) handSpriteRenderer.sprite = grabEmptySprite;
                else if (idleSprite != null) handSpriteRenderer.sprite = idleSprite;
                break;
            case HandState.Dragging:
                if (dragSprite != null) handSpriteRenderer.sprite = dragSprite;
                else if (handHoverSprite != null) handSpriteRenderer.sprite = handHoverSprite;
                break;
        }
    }
}