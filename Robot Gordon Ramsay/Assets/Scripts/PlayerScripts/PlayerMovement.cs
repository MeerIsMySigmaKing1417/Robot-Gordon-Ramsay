using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person player movement controller with physics-based grab system
/// Handles movement, jumping, crouching, looking, and object grabbing
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Movement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Walking speed")]
    public float walkSpeed = 8f;

    [Tooltip("Running/sprinting speed")]
    public float runSpeed = 14f;

    [Tooltip("Speed multiplier when crouching")]
    public float crouchSpeedMultiplier = 0.5f;

    [Tooltip("Maximum horizontal speed limit")]
    public float maxHorizontalSpeed = 20f;

    [Header("Acceleration Settings")]
    [Tooltip("How fast you accelerate on ground")]
    public float groundAcceleration = 50f;

    [Tooltip("How fast you slow down on ground")]
    public float groundDeceleration = 60f;

    [Tooltip("How fast sprint speed changes")]
    public float sprintTransitionSpeed = 8f;

    [Tooltip("How much momentum is kept when changing speeds (0-1)")]
    public float momentumRetention = 0.85f;

    [Header("Jump Settings")]
    [Tooltip("Jump height in meters")]
    public float jumpHeight = 1.2f;

    [Tooltip("Gravity strength (negative value)")]
    public float gravity = -35f;

    [Tooltip("Air control while jumping/falling")]
    public float airControl = 0.2f;

    [Header("Ground Check")]
    [Tooltip("Transform used for ground detection")]
    public Transform groundChecker;

    [Tooltip("Layer mask for ground objects")]
    public LayerMask groundMask;

    [Tooltip("Distance to check for ground")]
    public float groundDistance = 0.3f;

    [Header("Camera Settings")]
    [Tooltip("Player camera transform")]
    public Transform playerCam;

    [Tooltip("Main camera component")]
    public Camera mainCam;

    [Tooltip("Default field of view")]
    public float defaultFOV = 60f;

    [Tooltip("Field of view when sprinting")]
    public float sprintFOV = 75f;

    [Tooltip("Field of view when crouching")]
    public float crouchFOV = 55f;

    [Tooltip("FOV transition speed")]
    public float fovTransitionSpeed = 6f;

    [Tooltip("Mouse sensitivity X-axis")]
    public float mouseSensitivityX = 1f;

    [Tooltip("Mouse sensitivity Y-axis")]
    public float mouseSensitivityY = 1f;

    [Header("View Bobbing")]
    [Tooltip("Head bob speed when walking")]
    public float walkBobSpeed = 14f;

    [Tooltip("Head bob amount when walking")]
    public float walkBobAmount = 0.08f;

    [Tooltip("Head bob speed when sprinting")]
    public float sprintBobSpeed = 18f;

    [Tooltip("Head bob amount when sprinting")]
    public float sprintBobAmount = 0.15f;

    [Tooltip("Head bob speed when crouching")]
    public float crouchBobSpeed = 10f;

    [Tooltip("Head bob amount when crouching")]
    public float crouchBobAmount = 0.04f;

    [Header("Crouch Settings")]
    [Tooltip("Player height when crouching")]
    public float crouchHeight = 0.5f;

    [Tooltip("Player height when standing")]
    public float standHeight = 1.5f;

    [Tooltip("Crouch transition speed")]
    public float crouchTransitionSpeed = 12f;

    [Header("Grab Settings")]
    [Tooltip("Layer mask for grabbable objects")]
    public LayerMask grabbableMask = -1;

    [Tooltip("Maximum grab range")]
    public float grabRange = 8f;

    [Tooltip("Distance to hold grabbed objects")]
    public float grabDistance = 5.5f;

    [Tooltip("Spring force for grabbed objects")]
    public float grabSpring = 40f;

    [Tooltip("Damping force for grabbed objects")]
    public float grabDamper = 4f;

    [Tooltip("Mass scaling for grabbed objects")]
    public float grabMassScale = 5f;

    [Tooltip("Angular drag while grabbed")]
    public float grabAngularDrag = 5f;

    [Tooltip("Linear drag while grabbed")]
    public float grabLinearDrag = 1f;

    [Header("Distance Control")]
    [Tooltip("Closest distance for grabbed objects")]
    [SerializeField] public float minGrabDistance = 0.8f;

    [Tooltip("Furthest distance for grabbed objects")]
    [SerializeField] public float maxGrabDistance = 15f;

    [Tooltip("Scroll wheel sensitivity")]
    [SerializeField] public float scrollSensitivity = 3f;

    [Tooltip("Scroll multiplier for light objects")]
    [SerializeField] public float lightObjectScrollMultiplier = 2f;

    [Tooltip("Scroll multiplier for medium objects")]
    [SerializeField] public float mediumObjectScrollMultiplier = 1f;

    [Tooltip("Scroll multiplier for heavy objects")]
    [SerializeField] public float heavyObjectScrollMultiplier = 0.4f;

    [Header("Slope Settings")]
    [Tooltip("Maximum walkable slope angle")]
    [SerializeField] public float maxSlopeAngle = 45f;

    [Tooltip("Extra force for walking up slopes")]
    [SerializeField] public float slopeForceMultiplier = 2f;

    [Header("Stability Settings")]
    [Tooltip("Stability multiplier for grabbed objects")]
    public float stabilityMultiplier = 1f;

    [Tooltip("Rotation damping for grabbed objects")]
    public float rotationDamping = 1f;

    [Tooltip("Position smoothing for grabbed objects")]
    public float positionSmoothing = 10f;

    [Tooltip("Extra stabilizer for light objects")]
    public float lightObjectStabilizer = 1f;

    [Header("Precision Control")]
    [Tooltip("Extra damping for light objects")]
    public float lightObjectDamping = 8f;

    [Tooltip("Distance threshold for precision mode")]
    public float precisionDistance = 0.5f;

    [Tooltip("Force reduction when very close")]
    public float precisionMultiplier = 0.3f;

    [Header("Weapon Integration")]
    [Tooltip("Weapon Manager component")]
    public WeaponManager weaponManager;

    [Tooltip("Interaction Manager component")]
    public InteractionManager interactionManager;

    [HideInInspector] public bool isBeingKnockedBack = false;
    [HideInInspector] public bool recentKnockback = false;
    [HideInInspector] public float knockbackEndTime = 0f;
    [HideInInspector] public bool allowVerticalOverride = true;
    [HideInInspector] public bool hasUsedAirBoost = false; // Track if boost was used in air

    // Input system and movement variables
    private PlayerInputActions inputActions;
    private Rigidbody rb;
    [HideInInspector] public Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpInput;
    private bool sprintInput;
    private bool crouchInput;
    private bool grabInput;

    // Physics and state variables
    private bool grounded;
    private Vector3 originalCamPos;
    private Vector3 targetCrouchPosition = Vector3.zero;
    private float xRotation = 0f;
    private bool isCrouching = false;
    private Vector3 targetScale;
    private float verticalVelocity = 0f;
    private float currentMaxSpeed = 0f;
    private float currentGrabDistance = 0f;
    private Vector3 targetGrabPosition;
    private Vector3 groundNormal = Vector3.up;

    // Grab system variables
    [HideInInspector] public Rigidbody grabbedObject;
    private SpringJoint grabJoint;
    private LineRenderer grabLineRenderer;
    private Vector3 grabPoint;
    private Vector3 myGrabPoint;
    private Vector3 myHandPoint;
    private float originalAngularDrag;
    private float originalLinearDrag;
    private bool originalUseGravity;
    private float originalMass;
    private CrosshairCenter crosshairCenter;

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        // Create inputActions if it doesn't exist
        if (inputActions == null)
        {
            inputActions = new PlayerInputActions();
        }

        inputActions.Enable();
        inputActions.Player.Enable();

        // Movement input
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Look input
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        // Jump input
        inputActions.Player.Jump.performed += ctx => jumpInput = true;
        inputActions.Player.Jump.canceled += ctx => jumpInput = false;

        // Sprint input
        inputActions.Player.Sprint.performed += ctx => sprintInput = true;
        inputActions.Player.Sprint.canceled += ctx => sprintInput = false;

        // Crouch input
        inputActions.Player.Crouch.performed += ctx => crouchInput = true;
        inputActions.Player.Crouch.canceled += ctx => crouchInput = false;

        // Grab input
        inputActions.Player.Grab.performed += ctx => grabInput = true;
        inputActions.Player.Grab.canceled += ctx => grabInput = false;

        inputActions.Player.ScrollUp.performed += ctx => HandleWeaponPickup();

        // Distance control input (scroll wheel)
        inputActions.Player.ScrollUp.performed += ctx => HandleScrollWheel(-1f);
        inputActions.Player.ScrollDown.performed += ctx => HandleScrollWheel(1f);
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();

        if (inputActions != null)
        {
            inputActions.Player.Disable();
            inputActions.UI.Disable();
            inputActions.Disable();
        }
    }

    private void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
            inputActions = null;
        }
    }

    private void Start()
    {
        // Initialize cursor and camera
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        originalCamPos = playerCam.localPosition;

        // Initialize camera settings
        if (mainCam == null) mainCam = Camera.main;
        mainCam.fieldOfView = defaultFOV;
        targetScale = transform.localScale;

        // Initialize grab system
        currentGrabDistance = grabDistance;
        targetGrabPosition = Vector3.zero;

        // Find or create crosshair center component
        crosshairCenter = FindFirstObjectByType<CrosshairCenter>();
        if (crosshairCenter == null)
        {
            GameObject centerGO = new GameObject("CrosshairCenter");
            crosshairCenter = centerGO.AddComponent<CrosshairCenter>();
        }

        if (weaponManager == null)
        {
            weaponManager = GetComponent<WeaponManager>();
            if (weaponManager == null)
            {
                weaponManager = gameObject.AddComponent<WeaponManager>();
                Debug.Log("Added WeaponManager to player");
            }
        }

        if (interactionManager == null)
        {
            interactionManager = GetComponent<InteractionManager>();
            if (interactionManager == null)
            {
                interactionManager = gameObject.AddComponent<InteractionManager>();
                Debug.Log("Added InteractionManager to player");
            }
        }

        if (playerCam != null)
        {
            originalCamPos = playerCam.localPosition;
        }
    }

    private void Update()
    {
        // Check if grounded
        grounded = Physics.CheckSphere(groundChecker.position, groundDistance, groundMask);

        // Reset air boost when landing
        if (grounded && hasUsedAirBoost)
        {
            hasUsedAirBoost = false;
            Debug.Log("Landed - air boost reset");
        }

        // Update ground normal for slope detection
        UpdateGroundNormal();

        // Handle input
        HandleJump();
        HandleCrouch();
        HandleGrab();
        HandleScrollWheelFallback();
        HandleWeaponInteractions();

        // Update visual effects
        SmoothCrouchTransition();
        UpdateFOV();
        ViewBobbing();
        DrawGrabbing();
    }

    private void FixedUpdate()
    {
        HandleMovement();
    }

    private void LateUpdate()
    {
        HandleLook();
    }

    #endregion

    #region Movement System

    /// <summary>
    /// Handle weapon pickup when E key is pressed
    /// </summary>
    private void HandleWeaponPickup()
    {
        Debug.Log("E key pressed - attempting weapon pickup");

        // Method 1: Try interaction manager first
        if (interactionManager != null)
        {
            InteractableComponent hoveredObject = interactionManager.GetCurrentHoveredObject();
            if (hoveredObject != null)
            {
                PickupableWeapon pickupWeapon = hoveredObject.GetComponent<PickupableWeapon>();
                if (pickupWeapon != null && weaponManager != null)
                {
                    Debug.Log($"Attempting pickup via InteractionManager: {pickupWeapon.name}");
                    weaponManager.TryPickupWeapon(pickupWeapon);
                    return;
                }
            }
        }

        // Method 2: Direct raycast backup
        if (playerCam != null && weaponManager != null)
        {
            Ray ray = new Ray(playerCam.position, playerCam.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 5f))
            {
                PickupableWeapon pickup = hit.collider.GetComponent<PickupableWeapon>();
                if (pickup != null)
                {
                    Debug.Log($"Attempting pickup via direct raycast: {pickup.name}");
                    weaponManager.TryPickupWeapon(pickup);
                    return;
                }
            }
        }

        Debug.Log("No weapon found to pickup");
    }

    private void HandleWeaponInteractions()
    {
        // Auto-find weapon manager if not assigned
        if (weaponManager == null)
        {
            weaponManager = GetComponent<WeaponManager>();
            if (weaponManager == null)
            {
                Debug.LogWarning("WeaponManager not found on player!");
            }
        }

        // Auto-find interaction manager if not assigned
        if (interactionManager == null)
        {
            interactionManager = GetComponent<InteractionManager>();
            if (interactionManager == null)
            {
                Debug.LogWarning("InteractionManager not found on player!");
            }
        }
    }

    /// <summary>
    /// Handle player movement physics
    /// </summary>
    private void HandleMovement()
    {
        Vector3 moveDir = transform.forward * moveInput.y + transform.right * moveInput.x;

        // Determine target speed based on current state
        float targetSpeed = isCrouching ? walkSpeed * crouchSpeedMultiplier : (sprintInput ? runSpeed : walkSpeed);

        // Smoothly transition between different max speeds
        currentMaxSpeed = Mathf.Lerp(currentMaxSpeed, targetSpeed, Time.fixedDeltaTime * sprintTransitionSpeed);

        // Get current horizontal velocity
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float currentSpeed = horizontalVelocity.magnitude;

        // Calculate desired velocity
        Vector3 desiredVelocity = moveDir.normalized * currentMaxSpeed;

        // KNOCKBACK INTEGRATION: Reduce movement forces during/after knockback
        bool isInKnockbackPeriod = recentKnockback && Time.time < knockbackEndTime;

        if (isInKnockbackPeriod)
        {
            // During knockback period: only allow air control, no ground-based forces
            if (!grounded && moveInput.magnitude > 0.1f)
            {
                // Light air control - work WITH existing velocity instead of against it
                Vector3 airControlForce = desiredVelocity * airControl * 0.3f; // Much gentler
                rb.AddForce(new Vector3(airControlForce.x, 0f, airControlForce.z), ForceMode.Force);
            }
            // Don't apply ground forces during knockback period
            return;
        }

        // NORMAL MOVEMENT: Apply movement forces when not in knockback
        if (moveInput.magnitude > 0.1f)
        {
            ApplyMovementForces(horizontalVelocity, desiredVelocity, currentSpeed);
        }
        else
        {
            ApplyDeceleration(horizontalVelocity, currentSpeed);
        }

        // Clamp horizontal speed
        ClampHorizontalSpeed();

        // Handle slopes
        HandleSlopeMovement();
    }

    public void OnKnockbackApplied(float duration = 0.6f)
    {
        knockbackEndTime = Time.time + duration;
        Debug.Log($"Knockback applied - Y velocity override disabled for {duration} seconds");
    }

    /// <summary>
    /// Apply movement forces when player is actively moving
    /// </summary>
    private void ApplyMovementForces(Vector3 horizontalVelocity, Vector3 desiredVelocity, float currentSpeed)
    {
        Vector3 velocityChange = desiredVelocity - horizontalVelocity;

        if (grounded)
        {
            float accelerationRate = groundAcceleration;

            // Handle momentum retention when slowing down
            if (currentSpeed > currentMaxSpeed)
            {
                accelerationRate = groundDeceleration * (1f - momentumRetention);

                Vector3 currentDirection = horizontalVelocity.normalized;
                Vector3 targetDirection = desiredVelocity.normalized;

                float directionAlignment = Vector3.Dot(currentDirection, targetDirection);
                if (directionAlignment > 0.5f)
                {
                    float momentumSpeed = Mathf.Lerp(currentSpeed, currentMaxSpeed, Time.fixedDeltaTime * accelerationRate);
                    desiredVelocity = targetDirection * momentumSpeed;
                    velocityChange = desiredVelocity - horizontalVelocity;
                }
            }

            // Apply velocity change with acceleration limiting
            float maxVelocityChange = accelerationRate * Time.fixedDeltaTime;
            velocityChange = Vector3.ClampMagnitude(velocityChange, maxVelocityChange);

            // Apply slope force if needed
            Vector3 finalVelocityChange = IsOnSlope() ? ProjectOntoSlope(velocityChange) * slopeForceMultiplier : velocityChange;
            rb.AddForce(new Vector3(finalVelocityChange.x, 0f, finalVelocityChange.z), ForceMode.VelocityChange);
        }
        else
        {
            // Limited air control
            velocityChange = velocityChange * airControl;
            rb.AddForce(new Vector3(velocityChange.x, 0f, velocityChange.z), ForceMode.VelocityChange);
        }
    }

    /// <summary>
    /// Apply deceleration when player stops moving
    /// </summary>
    private void ApplyDeceleration(Vector3 horizontalVelocity, float currentSpeed)
    {
        if (grounded && currentSpeed > 0.1f)
        {
            Vector3 deceleration = -horizontalVelocity.normalized * groundDeceleration * Time.fixedDeltaTime;

            if (deceleration.magnitude > currentSpeed)
            {
                deceleration = -horizontalVelocity;
            }

            rb.AddForce(new Vector3(deceleration.x, 0f, deceleration.z), ForceMode.VelocityChange);
        }
    }

    /// <summary>
    /// Clamp horizontal speed to maximum
    /// </summary>
    private void ClampHorizontalSpeed()
    {
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (flatVel.magnitude > maxHorizontalSpeed)
        {
            Vector3 limitedVel = flatVel.normalized * maxHorizontalSpeed;
            rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
        }
    }

    /// <summary>
    /// Handle movement on slopes
    /// </summary>
    private void HandleSlopeMovement()
    {
        if (grounded && moveInput.magnitude < 0.1f && IsOnSlope())
        {
            // Prevent sliding down slopes when not moving
            Vector3 antiSlideForce = Vector3.ProjectOnPlane(Vector3.down, groundNormal) * -10f;
            rb.AddForce(antiSlideForce, ForceMode.Force);
        }
    }

    #endregion

    #region Slope Detection

    /// <summary>
    /// Update ground normal for slope calculations
    /// </summary>
    private void UpdateGroundNormal()
    {
        if (grounded)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, groundDistance + 0.5f, groundMask))
            {
                groundNormal = hit.normal;
            }
            else
            {
                groundNormal = Vector3.up;
            }
        }
        else
        {
            groundNormal = Vector3.up;
        }
    }

    /// <summary>
    /// Check if player is on a slope
    /// </summary>
    private bool IsOnSlope()
    {
        float angle = Vector3.Angle(groundNormal, Vector3.up);
        return angle > 1f && angle <= maxSlopeAngle;
    }

    /// <summary>
    /// Project movement vector onto slope
    /// </summary>
    private Vector3 ProjectOntoSlope(Vector3 movement)
    {
        return Vector3.ProjectOnPlane(movement, groundNormal);
    }

    #endregion

    #region Input Handlers

    /// <summary>
    /// Handle jump input and physics
    /// </summary>
    private void HandleJump()
    {
        if (grounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            if (jumpInput)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpInput = false;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // SIMPLE FIX: Only override Y velocity if not in knockback period
        bool knockbackActive = Time.time < knockbackEndTime;

        if (!knockbackActive)
        {
            // Normal behavior
            Vector3 vel = rb.linearVelocity;
            vel.y = verticalVelocity;
            rb.linearVelocity = vel;
        }
        else
        {
            // During knockback - sync our verticalVelocity with actual rigidbody
            // This prevents jarring transitions when knockback ends
            verticalVelocity = rb.linearVelocity.y;
        }
    }
    /// <summary>
    /// Handle crouch input
    /// </summary>
    private void HandleCrouch()
    {
        if (crouchInput && !isCrouching)
        {
            StartCrouch();
        }
        else if (!crouchInput && isCrouching)
        {
            StopCrouch();
        }
    }

    /// <summary>
    /// Handle grab input
    /// </summary>
    private void HandleGrab()
    {
        if (grabInput && grabbedObject == null)
        {
            StartGrab();
        }
        else if (grabInput && grabbedObject != null)
        {
            HoldGrab();
        }
        else if (!grabInput && grabbedObject != null)
        {
            StopGrab();
        }
    }

    /// <summary>
    /// Fallback scroll wheel detection using direct mouse input
    /// </summary>
    private void HandleScrollWheelFallback()
    {
        if (grabbedObject != null)
        {
            if (Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0)
            {
                float scrollInput = Mouse.current.scroll.ReadValue().y;
                HandleScrollWheel(scrollInput > 0 ? -1f : 1f);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Reset air boost when touching ground
        if (grounded)
        {
            hasUsedAirBoost = false;
        }
    }


    #endregion

    #region Grab System

    /// <summary>
    /// Start grabbing an object
    /// </summary>
    private void StartGrab()
    {
        RaycastHit[] hits = Physics.RaycastAll(playerCam.position, playerCam.forward, grabRange, grabbableMask);

        if (hits.Length < 1)
            return;

        foreach (RaycastHit hit in hits)
        {
            GrabbableObject grabbableComp = hit.transform.GetComponent<GrabbableObject>();
            Rigidbody hitRb = hit.transform.GetComponent<Rigidbody>();

            if (hitRb != null)
            {
                if (grabbableComp != null && !grabbableComp.CanBeGrabbed())
                {
                    Debug.Log($"Object {hit.transform.name} cannot be grabbed!");
                    continue;
                }

                SetupGrabbedObject(hitRb, hit.point, grabbableComp);
                break;
            }
        }
    }

    /// <summary>
    /// Setup a grabbed object with proper physics
    /// </summary>
    private void SetupGrabbedObject(Rigidbody targetRb, Vector3 hitPoint, GrabbableObject grabbableComp)
    {
        grabbedObject = targetRb;
        grabPoint = hitPoint;

        // Initialize grab distance
        float initialDistance = Vector3.Distance(playerCam.position, grabbedObject.position);
        currentGrabDistance = Mathf.Clamp(initialDistance, minGrabDistance, maxGrabDistance);

        // Store original physics values
        originalAngularDrag = grabbedObject.angularDamping;
        originalLinearDrag = grabbedObject.linearDamping;
        originalUseGravity = grabbedObject.useGravity;
        originalMass = grabbedObject.mass;

        // Get weight properties
        ObjectWeight weightCategory = grabbableComp != null ? grabbableComp.GetWeightCategory() : GetWeightCategoryFromMass(grabbedObject.mass);
        float springMultiplier = WeightUtility.GetSpringMultiplier(weightCategory);
        float damperMultiplier = WeightUtility.GetDamperMultiplier(weightCategory);

        // Apply custom multipliers if available
        if (grabbableComp != null)
        {
            springMultiplier *= grabbableComp.springMultiplier;
            damperMultiplier *= grabbableComp.damperMultiplier;
        }

        // Create and configure spring joint
        CreateSpringJoint(springMultiplier, damperMultiplier);

        // Apply physics modifications for control
        ApplyGrabPhysics(weightCategory);

        // Create visual feedback
        CreateGrabLineRenderer(grabbableComp, weightCategory);

        // Initialize smooth positioning
        targetGrabPosition = playerCam.position + playerCam.forward * currentGrabDistance;
        myGrabPoint = grabbedObject.position;
        myHandPoint = crosshairCenter.GetCrosshairWorldPosition(2f);

        // Call grab event
        if (grabbableComp != null)
            grabbableComp.OnGrabbed();

        Debug.Log($"Grabbed: {grabbedObject.name} | Weight Category: {weightCategory}");
    }

    /// <summary>
    /// Create spring joint for grabbed object
    /// </summary>
    private void CreateSpringJoint(float springMultiplier, float damperMultiplier)
    {
        grabJoint = grabbedObject.gameObject.AddComponent<SpringJoint>();
        grabJoint.autoConfigureConnectedAnchor = false;
        grabJoint.minDistance = 0f;
        grabJoint.maxDistance = 0f;
        grabJoint.damper = grabDamper * damperMultiplier;
        grabJoint.spring = grabSpring * springMultiplier;
        grabJoint.massScale = grabMassScale;
    }

    /// <summary>
    /// Apply physics modifications for grabbed objects
    /// </summary>
    private void ApplyGrabPhysics(ObjectWeight weightCategory)
    {
        if (weightCategory == ObjectWeight.VeryLight || weightCategory == ObjectWeight.Light)
        {
            grabbedObject.angularDamping = rotationDamping * 3f;
            grabbedObject.linearDamping = grabLinearDrag * 8f;
        }
    }

    /// <summary>
    /// Update grabbed object position
    /// </summary>
    private void HoldGrab()
    {
        if (grabbedObject == null || grabJoint == null)
            return;

        // Update target position smoothly
        Vector3 desiredPosition = playerCam.position + playerCam.forward * currentGrabDistance;
        targetGrabPosition = Vector3.Lerp(targetGrabPosition, desiredPosition, Time.deltaTime * positionSmoothing);

        // Update joint connection
        grabJoint.connectedAnchor = targetGrabPosition;

        // Update line renderer
        if (grabLineRenderer != null)
        {
            grabLineRenderer.startWidth = 0f;
            grabLineRenderer.endWidth = 0.0075f * Mathf.Clamp(grabbedObject.linearVelocity.magnitude, 0f, 5f);
        }
    }

    /// <summary>
    /// Handle scroll wheel distance control
    /// </summary>
    private void HandleScrollWheel(float scrollValue)
    {
        if (grabbedObject == null)
            return;

        // Get weight-based scroll sensitivity
        GrabbableObject grabbableComp = grabbedObject.GetComponent<GrabbableObject>();
        ObjectWeight weightCategory = grabbableComp != null ? grabbableComp.GetWeightCategory() : GetWeightCategoryFromMass(grabbedObject.mass);

        float scrollMultiplier = GetScrollMultiplierForWeight(weightCategory);
        float scrollAmount = scrollValue * scrollSensitivity * scrollMultiplier;
        float oldDistance = currentGrabDistance;
        currentGrabDistance = Mathf.Clamp(currentGrabDistance - scrollAmount, minGrabDistance, maxGrabDistance);

        string direction = scrollValue > 0 ? "Closer" : "Farther";
        float actualChange = Mathf.Abs(currentGrabDistance - oldDistance);
        Debug.Log($"Distance control: {direction} ({weightCategory}, {scrollMultiplier:F1}x), Old: {oldDistance:F1}m, New: {currentGrabDistance:F1}m, Change: {actualChange:F1}m");
    }

    /// <summary>
    /// Stop grabbing the current object
    /// </summary>
    private void StopGrab()
    {
        if (grabbedObject == null)
            return;

        // Call release event
        GrabbableObject grabbableComp = grabbedObject.GetComponent<GrabbableObject>();
        if (grabbableComp != null)
            grabbableComp.OnReleased();

        // Restore original physics
        RestoreOriginalPhysics();

        // Add gentle release force
        grabbedObject.AddForce(Vector3.down * 0.5f, ForceMode.Impulse);

        // Clean up components
        CleanupGrabComponents();

        grabbedObject = null;
        Debug.Log("Object released with restored physics properties");
    }

    /// <summary>
    /// Restore original physics properties
    /// </summary>
    private void RestoreOriginalPhysics()
    {
        grabbedObject.angularDamping = originalAngularDrag;
        grabbedObject.linearDamping = originalLinearDrag;
        grabbedObject.useGravity = originalUseGravity;
        grabbedObject.mass = originalMass;
    }

    /// <summary>
    /// Clean up grab-related components
    /// </summary>
    private void CleanupGrabComponents()
    {
        if (grabJoint != null)
        {
            Destroy(grabJoint);
            grabJoint = null;
        }

        if (grabLineRenderer != null)
        {
            Destroy(grabLineRenderer);
            grabLineRenderer = null;
        }
    }

    /// <summary>
    /// Create line renderer for grab visualization
    /// </summary>
    private void CreateGrabLineRenderer(GrabbableObject grabbableComp = null, ObjectWeight weightCategory = ObjectWeight.Medium)
    {
        if (grabbedObject == null)
        {
            Debug.LogWarning("Cannot create line renderer - no grabbed object!");
            return;
        }

        grabLineRenderer = grabbedObject.gameObject.AddComponent<LineRenderer>();
        grabLineRenderer.positionCount = 2;

        float lineWidth = 0.1f;
        grabLineRenderer.startWidth = lineWidth;
        grabLineRenderer.endWidth = lineWidth;

        // Setup material
        Material lineMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        if (lineMaterial.shader == null)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }
        if (lineMaterial.shader == null)
        {
            lineMaterial = new Material(Shader.Find("Standard"));
        }

        lineMaterial.color = Color.red;
        grabLineRenderer.material = lineMaterial;

        // Configure line renderer settings
        grabLineRenderer.enabled = true;
        grabLineRenderer.useWorldSpace = true;
        grabLineRenderer.sortingOrder = 100;
        grabLineRenderer.receiveShadows = false;
        grabLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        grabLineRenderer.numCapVertices = 2;
        grabLineRenderer.numCornerVertices = 2;

        Debug.Log($"Line renderer created! Width: {lineWidth}, Color: {lineMaterial.color}, Shader: {lineMaterial.shader.name}");
    }

    /// <summary>
    /// Update grab line visualization
    /// </summary>
    private void DrawGrabbing()
    {
        if (grabbedObject == null || grabLineRenderer == null)
            return;

        // Get crosshair world position
        Vector3 crosshairWorldPos = crosshairCenter.GetCrosshairWorldPosition(2f);

        // Smooth interpolation for visual appeal
        myGrabPoint = Vector3.Lerp(myGrabPoint, grabbedObject.position, Time.deltaTime * 45f);
        myHandPoint = Vector3.Lerp(myHandPoint, crosshairWorldPos, Time.deltaTime * 45f);

        // Update line positions
        grabLineRenderer.SetPosition(0, myHandPoint);
        grabLineRenderer.SetPosition(1, myGrabPoint);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get weight category from rigidbody mass (fallback)
    /// </summary>
    private ObjectWeight GetWeightCategoryFromMass(float mass)
    {
        if (mass < 1f) return ObjectWeight.VeryLight;
        if (mass < 5f) return ObjectWeight.Light;
        if (mass < 15f) return ObjectWeight.Medium;
        if (mass < 30f) return ObjectWeight.Heavy;
        if (mass < 50f) return ObjectWeight.VeryHeavy;
        return ObjectWeight.Immovable;
    }

    /// <summary>
    /// Get scroll multiplier based on weight category
    /// </summary>
    private float GetScrollMultiplierForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
            case ObjectWeight.Light:
                return lightObjectScrollMultiplier; // 2.0x - very easy
            case ObjectWeight.Medium:
                return mediumObjectScrollMultiplier; // 1.0x - normal
            case ObjectWeight.Heavy:
            case ObjectWeight.VeryHeavy:
                return heavyObjectScrollMultiplier; // 0.4x - harder
            default:
                return mediumObjectScrollMultiplier;
        }
    }

    #endregion

    #region Visual Effects

    /// <summary>
    /// Handle view bobbing effect
    /// </summary>
    private void ViewBobbing()
    {
        if (!grounded || moveInput == Vector2.zero)
        {
            playerCam.localPosition = Vector3.Lerp(playerCam.localPosition, originalCamPos, Time.deltaTime * 5f);
            return;
        }

        float bobSpeed = walkBobSpeed;
        float bobAmount = walkBobAmount;

        if (sprintInput && moveInput.magnitude > 0.1f)
        {
            bobSpeed = sprintBobSpeed;
            bobAmount = sprintBobAmount;
        }
        else if (isCrouching)
        {
            bobSpeed = crouchBobSpeed;
            bobAmount = crouchBobAmount;
        }

        Vector3 bobPos = originalCamPos;
        bobPos.y += Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        playerCam.localPosition = Vector3.Lerp(playerCam.localPosition, bobPos, Time.deltaTime * 8f);
    }

    /// <summary>
    /// Update field of view based on movement state
    /// </summary>
    private void UpdateFOV()
    {
        float targetFOV = defaultFOV;

        if (sprintInput && moveInput.magnitude > 0.1f)
        {
            targetFOV = sprintFOV;
        }
        else if (isCrouching)
        {
            targetFOV = crouchFOV;
        }

        mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFOV, Time.deltaTime * fovTransitionSpeed);
    }

    #endregion

    #region Crouch System - FIXED TO USE POSITION INSTEAD OF SCALE

    /// <summary>
    /// Start crouching - FIXED: Use position instead of scale
    /// </summary>
    private void StartCrouch()
    {
        isCrouching = true;

        // Use position-based crouching instead of scale to avoid weapon squishing
        Vector3 currentPos = transform.position;
        float heightDifference = (standHeight - crouchHeight) * 0.5f;

        // Lower the player by moving them down
        targetCrouchPosition = currentPos - Vector3.up * heightDifference;
    }

    /// <summary>
    /// Stop crouching - FIXED: Use position instead of scale
    /// </summary>
    private void StopCrouch()
    {
        if (!CanStandUp())
        {
            // Can't stand up due to ceiling - stay crouched
            return;
        }

        isCrouching = false;

        // Return to standing position
        Vector3 currentPos = transform.position;
        float heightDifference = (standHeight - crouchHeight) * 0.5f;

        // Raise the player by moving them up
        targetCrouchPosition = currentPos + Vector3.up * heightDifference;
    }

    /// <summary>
    /// Check if player can stand up (no ceiling blocking)
    /// </summary>
    private bool CanStandUp()
    {
        // Check for ceiling above player
        float checkHeight = standHeight - crouchHeight + 0.1f; // Add small buffer
        Vector3 checkPosition = transform.position + Vector3.up * checkHeight;

        return !Physics.CheckSphere(checkPosition, 0.3f, groundMask);
    }

    /// <summary>
    /// Smooth crouch transition - FIXED: Use position instead of scale
    /// </summary>
    private void SmoothCrouchTransition()
    {
        if (targetCrouchPosition != Vector3.zero)
        {
            // Smoothly move to target crouch position
            transform.position = Vector3.Lerp(
                transform.position,
                targetCrouchPosition,
                Time.deltaTime * crouchTransitionSpeed
            );

            // Check if we're close enough to target
            if (Vector3.Distance(transform.position, targetCrouchPosition) < 0.01f)
            {
                transform.position = targetCrouchPosition;
                targetCrouchPosition = Vector3.zero; // Reset target
            }
        }

        // Update camera position for crouching
        UpdateCameraForCrouch();
    }

    /// <summary>
    /// Update camera position for crouching
    /// </summary>
    private void UpdateCameraForCrouch()
    {
        if (playerCam == null) return;

        // Calculate target camera height based on crouch state
        float targetCameraY = isCrouching ?
            originalCamPos.y * (crouchHeight / standHeight) :
            originalCamPos.y;

        // Smoothly adjust camera height
        Vector3 currentCamPos = playerCam.localPosition;
        currentCamPos.y = Mathf.Lerp(currentCamPos.y, targetCameraY, Time.deltaTime * crouchTransitionSpeed);
        playerCam.localPosition = currentCamPos;
    }

    #endregion

    #region Look System

    /// <summary>
    /// Handle mouse look rotation
    /// </summary>
    private void HandleLook()
    {
        float mouseX = lookInput.x * mouseSensitivityX;
        float mouseY = lookInput.y * mouseSensitivityY;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Smoothly interpolate camera rotation
        playerCam.localRotation = Quaternion.Lerp(playerCam.localRotation, Quaternion.Euler(xRotation, 0f, 0f), Time.deltaTime * 30f);
        transform.Rotate(Vector3.up * mouseX);
    }

    #endregion
}