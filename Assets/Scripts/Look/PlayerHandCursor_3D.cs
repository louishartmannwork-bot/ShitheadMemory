using UnityEngine;

public class PlayerHandCursor_3D : MonoBehaviour
{
    public static PlayerHandCursor_3D Instance;

    public enum HandState { Idle, Hover, Grab, Dragging }

    [Header("References")]
    [SerializeField] private SpriteRenderer handSpriteRenderer;
    [SerializeField] private SpriteRenderer armSpriteRenderer; // Neu: SpriteRenderer für den Arm

    [Header("Sprites per State")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite hoverSprite;
    [SerializeField] private Sprite grabSprite;
    [SerializeField] private Sprite dragSprite;

    [Header("Heights per State")]
    [SerializeField] private float idleHeight = 0.45f;
    [SerializeField] private float hoverHeight = 0.65f;
    [SerializeField] private float dragHeight = 0.85f;

    [Header("Rotation per State (X-Achse Neigung)")]
    [SerializeField] private float idlePitchX = 75f;
    [SerializeField] private float hoverPitchX = 60f;
    [SerializeField] private float dragPitchX = 75f;

    [Header("Movement & Rotation Settings")]
    [SerializeField] private float followSpeed = 30f;
    [SerializeField] private float rotationSpeed = 15f;
    [SerializeField] private Vector3 mouseOffset = new Vector2(0.2f, -0.2f);

    [Header("Arm Anchor")]
    [SerializeField] private Vector3 screenEdgeAnchor = new Vector3(0f, -1.5f, -3.5f);

    [Header("Arm Reach Limits")]
    [SerializeField] private float maxArmReach = 6.0f;
    [SerializeField] private float minArmReach = 1.0f;

    [Header("Camera Lean on Max Reach")]
    [SerializeField] private bool enableCameraLean = true;
    [SerializeField] private float maxCameraPitchAdd = 5.0f;
    [SerializeField] private float maxCameraYawAdd = 5.0f;
    [SerializeField] private float cameraLeanSpeed = 10f;

    private Quaternion initialCamRotation;
    private Vector3 lastValidTargetPoint;
    private Camera cam;
    private HandState currentState = HandState.Idle;
    private float targetHeightY;
    private float targetPitchX;

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

        Quaternion tempRot = cam.transform.rotation;
        cam.transform.rotation = initialCamRotation;
        Ray ray = cam.ScreenPointToRay(mousePos);
        cam.transform.rotation = tempRot;

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

        transform.position = Vector3.Lerp(transform.position, targetPoint, Time.deltaTime * followSpeed);
    }

    private void UpdateHandRotation()
    {
        // Combine the arm's direction rotation with the hand's local state tilt (targetPitchX)
        Vector3 direction = transform.position - screenEdgeAnchor;
        if (direction != Vector3.zero)
        {
            Quaternion lookAtRotation = Quaternion.LookRotation(direction);
            Quaternion stateTilt = Quaternion.Euler(targetPitchX, 0f, 0f);

            // This makes the hand face the mouse/direction while keeping its state tilt
            Quaternion targetRotation = lookAtRotation * stateTilt;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
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
                targetPitchX = hoverPitchX;
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
        if (armSpriteRenderer == null) return;

        Transform armTransform = armSpriteRenderer.transform;
        Vector3 handPos = transform.position; // Current position of the hand

        // 1. Position the arm right at the shoulder anchor point
        armTransform.position = screenEdgeAnchor;

        // 2. Calculate direction and distance from anchor to hand
        Vector3 direction = handPos - screenEdgeAnchor;
        float distance = direction.magnitude;

        if (direction != Vector3.zero)
        {
            // Rotate the arm to point directly at the hand
            armTransform.rotation = Quaternion.LookRotation(direction);

            // If your arm sprite needs a base rotation offset (like your X: 90 rotation), 
            // you can multiply it here, e.g.: armTransform.rotation *= Quaternion.Euler(90, 0, 0);
        }

        // 3. Stretch the arm dynamically based on the distance to the hand
        Vector3 currentScale = armTransform.localScale;
        // Adjust currentScale.z (or .y depending on your sprite setup) to match the distance
        armTransform.localScale = new Vector3(currentScale.x, currentScale.y, distance);
    }


    private void UpdateVisuals()
    {
        if (handSpriteRenderer == null) return;

        switch (currentState)
        {
            case HandState.Idle:
                if (idleSprite != null) handSpriteRenderer.sprite = idleSprite;
                break;
            case HandState.Hover:
                if (hoverSprite != null) handSpriteRenderer.sprite = hoverSprite;
                break;
            case HandState.Grab:
                if (grabSprite != null) handSpriteRenderer.sprite = grabSprite;
                break;
            case HandState.Dragging:
                Sprite spriteToUse = (dragSprite != null) ? dragSprite : grabSprite;
                if (spriteToUse != null) handSpriteRenderer.sprite = spriteToUse;
                break;
        }
    }
}