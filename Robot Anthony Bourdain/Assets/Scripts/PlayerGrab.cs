using System.Collections;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tracks surfaces with a glowing sphere based on where the player is looking
/// </summary>
public class PlayerGrab : MonoBehaviour
{
    [Header("Tracking Settings")]
    [Tooltip("Camera to raycast from")]
    public CinemachineCamera playerCamera;

    [Tooltip("Prefab of the tracking sphere")]
    public GameObject trackerSpherePrefab;

    [Tooltip("Layers that can be tracked")]
    public LayerMask trackableLayers = -1;

    [Tooltip("Maximum distance to track surfaces")]
    public float maxTrackDistance = 10f;

    [Tooltip("How far above the surface to place the sphere")]
    public float surfaceOffset = 0.05f;

    [Tooltip("How smoothly the sphere moves")]
    public float moveSpeed = 15f;

    // Private variables
    private GameObject currentTrackerSphere;
    private GameObject hitObject;
    private Transform originalParent;
    private PlayerInputActions inputActions;
    private Vector2 lookInput = Vector2.zero;
    private bool grabInput = false;

    [Header("Grab Settings")]
    [SerializeField] private float grabDamper = 4f;
    [SerializeField] private float grabSpring = 40f;
    [SerializeField] private float grabMassScale = 5f;
    [SerializeField] private float rotationDamping = 1f;
    [SerializeField] private float grabLinearDrag = 1f;
    [SerializeField] private float scrollSensitivity = 3f;
    [SerializeField] private float minGrabDistance = 0.8f;
    [SerializeField] private float maxGrabDistance = 15.0f;
    [SerializeField] private float grabDistance = 5.5f;
    [SerializeField] private float objectMoveSpeed = 15f; // How fast objects can move (editable in inspector)

    private float lightObjectScrollMultiplier = 2.0f;
    private float mediumObjectScrollMultiplier = 1.0f;
    private float heavyObjectScrollMultiplier = 0.4f;
    private float baseSphereSpeed = 35f; // Base speed before weight modification (increased from 25f)
    private float sphereMass = 0.05f; // Very light sphere mass
    private float torqueMultiplier = 50f; // For applying rotational forces at grab point
    private float weightSpeedMultiplier = 2f; // How much weight affects speed
    private float gravityInfluence = 1.5f; // How much gravity affects grabbed objects
    private bool enablePhysicsBasedGrabbing = true; // Enable more realistic physics
    private float orientationSpring = 200f; // Spring force to return to original rotation
    private float orientationDamping = 50f; // Damping for orientation restoration
    private float maxOrientationDeviation = 30f; // Max degrees object can deviate from original
    private bool allowOrientationWobble = true; // Allow natural wobbling during movement

    [HideInInspector] public Rigidbody grabbedObject;
    private SpringJoint grabJoint;
    private Vector3 grabPoint;
    private Vector3 myGrabPoint;
    private Vector3 myHandPoint;
    private float originalAngularDrag;
    private float originalLinearDrag;
    private bool originalUseGravity;
    private float originalMass;
    private float currentGrabDistance;
    private Vector3 targetGrabPosition;
    private Vector3 currentHitPoint;
    private Vector3 lockedSpherePosition;
    private bool isSpherePositionLocked = false;
    private Vector3 grabTargetPosition;
    private Transform originalSphereParent;

    // New variables for proper sphere tracking
    private Vector3 grabPointLocalToObject; // Store the local position on the grabbed object
    private bool isGrabbing = false;

    // Variables for rigidbody-based grab point
    private Rigidbody sphereRigidbody;
    private bool isStabilizing = false;
    private float currentSphereSpeed; // Weight-adjusted sphere speed
    private ObjectWeight currentWeightCategory;
    private Quaternion originalRotation; // Store original rotation when grabbed
    private bool preserveOrientation = true;
    private RigidbodyConstraints originalConstraints; // Store original rigidbody constraints
    private Vector3 lastVelocity; // Track velocity changes for wobble effect
    private bool isScrolling = false; // Track if currently scrolling
    private float lastScrollTime = 0f; // When last scroll input happened

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        currentGrabDistance = grabDistance;
        targetGrabPosition = Vector3.zero;
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Enable();

        // Look input
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        // Grab input
        inputActions.Player.Grab.performed += ctx => grabInput = true;
        inputActions.Player.Grab.canceled += ctx => grabInput = false;

        inputActions.Player.ScrollUp.performed += ctx => HandleScrollWheel(-1f);   // Farther
        inputActions.Player.ScrollDown.performed += ctx => HandleScrollWheel(1f);  // Closer
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    private void Update()
    {
        HandleScrollInput(); // Handle scroll input in Update for better responsiveness
        CheckScrollTimeout(); // Check if we should stop scrolling
    }

    /// <summary>
    /// Check if scrolling should stop based on timeout
    /// </summary>
    private void CheckScrollTimeout()
    {
        if (isScrolling && Time.time - lastScrollTime > 0.1f) // 0.1 second timeout
        {
            isScrolling = false;
            // Immediately stop object movement when scrolling stops
            if (grabbedObject != null)
            {
                grabbedObject.linearVelocity = grabbedObject.linearVelocity * 0.5f; // Reduce velocity quickly
            }
        }
    }

    private void FixedUpdate()
    {
        TrackSurfacePoint();
        HandleGrab();
    }

    /// <summary>
    /// Main tracking logic - raycasts and positions the sphere
    /// </summary>
    private void TrackSurfacePoint()
    {
        // Skip tracking if we're grabbing
        if (isGrabbing)
        {
            UpdateSpherePositionWhileGrabbing();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxTrackDistance, trackableLayers))
        {
            hitObject = hit.collider.gameObject;
            currentHitPoint = hit.point;

            ShowTrackerAt(hit.point, hit.normal, hit.distance);
        }
        else
        {
            hitObject = null;
            HideTracker();
        }
    }

    /// <summary>
    /// Update sphere position while grabbing - sphere stays attached to grabbed object and faces camera
    /// </summary>
    private void UpdateSpherePositionWhileGrabbing()
    {
        if (currentTrackerSphere != null && grabbedObject != null)
        {
            // Convert local position back to world position
            Vector3 worldPosition = grabbedObject.transform.TransformPoint(grabPointLocalToObject);
            currentTrackerSphere.transform.position = worldPosition;

            // Make sphere always face the camera/player
            Vector3 directionToCamera = (playerCamera.transform.position - worldPosition).normalized;
            currentTrackerSphere.transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }

    /// <summary>
    /// Show the tracker sphere at the specified position
    /// </summary>
    private void ShowTrackerAt(Vector3 position, Vector3 normal, float distance)
    {
        if (currentTrackerSphere == null)
        {
            currentTrackerSphere = Instantiate(trackerSpherePrefab);

            // Add rigidbody to the sphere for physics-based grabbing
            if (currentTrackerSphere.GetComponent<Rigidbody>() == null)
            {
                sphereRigidbody = currentTrackerSphere.AddComponent<Rigidbody>();
                sphereRigidbody.mass = sphereMass; // Very light for responsive movement
                sphereRigidbody.isKinematic = true;
                sphereRigidbody.useGravity = false;
                sphereRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // Ensure sphere has low drag for responsiveness
                sphereRigidbody.linearDamping = 0f;
                sphereRigidbody.angularDamping = 0f;
            }
        }
        // Calculate target position (slightly above surface)
        Vector3 targetPosition = position + normal * surfaceOffset;

        // Smooth movement instead of instant teleport
        currentTrackerSphere.transform.position = Vector3.Lerp(currentTrackerSphere.transform.position, targetPosition, Time.deltaTime * moveSpeed);
        // Orient the sphere to the surface normal
        currentTrackerSphere.transform.rotation = Quaternion.LookRotation(normal);

        currentTrackerSphere.SetActive(true);
    }

    /// <summary>
    /// Hide the tracker sphere
    /// </summary>
    private void HideTracker()
    {
        if (currentTrackerSphere != null)
        {
            currentTrackerSphere.SetActive(false);
        }
    }

    private void SetupGrabbedObject(Rigidbody targetRb, Vector3 hitPoint, GrabbableObject grabbableComp)
    {
        if (grabbedObject != null) return;

        grabbedObject = targetRb;
        grabPoint = hitPoint;
        isGrabbing = true;

        // Store the grab point in local coordinates relative to the grabbed object
        grabPointLocalToObject = grabbedObject.transform.InverseTransformPoint(currentTrackerSphere.transform.position);

        // Store original rotation to preserve orientation
        originalRotation = grabbedObject.transform.rotation;

        // Store original rotation to preserve orientation
        originalRotation = grabbedObject.transform.rotation;

        // Store original constraints but DON'T lock rotation - allow natural movement
        originalConstraints = grabbedObject.constraints;
        lastVelocity = Vector3.zero;

        // Pre-stabilization: zero out velocities and apply temporary drag
        grabbedObject.linearVelocity = Vector3.zero;
        grabbedObject.angularVelocity = Vector3.zero;

        // Store original values and apply temporary high drag
        originalLinearDrag = grabbedObject.linearDamping;
        originalAngularDrag = grabbedObject.angularDamping;
        grabbedObject.linearDamping = 15f; // Higher temporary drag for better stability
        grabbedObject.angularDamping = 15f;

        // Get weight category first
        ObjectWeight weightCategory = grabbableComp != null ? grabbableComp.GetWeightCategory() : GetWeightCategoryFromMass(grabbedObject.mass);

        // Reduce mass temporarily for better responsiveness, but keep weight-based differences
        originalMass = grabbedObject.mass;
        float massReduction = GetMassReductionForWeight(weightCategory);
        grabbedObject.mass = Mathf.Max(0.1f, grabbedObject.mass * massReduction);

        // For heavier objects, don't reduce mass as much to maintain "heavy" feeling
        if (weightCategory == ObjectWeight.Heavy || weightCategory == ObjectWeight.VeryHeavy)
        {
            grabbedObject.mass = Mathf.Max(grabbedObject.mass, originalMass * 0.7f); // Keep at least 70% mass
        }

        // Store weight category for later use
        currentWeightCategory = weightCategory;

        // Start stabilization process
        StartCoroutine(StabilizeAndConnect(targetRb, grabbableComp));
    }

    private IEnumerator StabilizeAndConnect(Rigidbody targetRb, GrabbableObject grabbableComp)
    {
        isStabilizing = true;

        // Wait for physics stabilization
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // Initialize grab distance
        float initialDistance = Vector3.Distance(playerCamera.transform.position, grabbedObject.position);
        currentGrabDistance = Mathf.Clamp(initialDistance, minGrabDistance, maxGrabDistance);

        // Use sphere tracker position instead of hit point for distance calculation
        if (currentTrackerSphere != null)
        {
            initialDistance = Vector3.Distance(playerCamera.transform.position, currentTrackerSphere.transform.position);
            currentGrabDistance = Mathf.Clamp(initialDistance, minGrabDistance, maxGrabDistance);
        }

        // Store original physics values
        originalUseGravity = grabbedObject.useGravity;

        // Get weight properties
        ObjectWeight weightCategory = grabbableComp != null ? grabbableComp.GetWeightCategory() : GetWeightCategoryFromMass(grabbedObject.mass);
        float springMultiplier = WeightUtility.GetSpringMultiplier(weightCategory);
        float damperMultiplier = WeightUtility.GetDamperMultiplier(weightCategory);

        // Keep gravity enabled for heavier objects to make them feel heavy
        if (weightCategory == ObjectWeight.Heavy || weightCategory == ObjectWeight.VeryHeavy || weightCategory == ObjectWeight.Immovable)
        {
            grabbedObject.useGravity = true;
        }
        else
        {
            grabbedObject.useGravity = false; // Disable for light objects
        }


        // Calculate weight-adjusted sphere speed with more dramatic differences
        currentSphereSpeed = baseSphereSpeed * GetSpeedMultiplierForWeight(weightCategory) * weightSpeedMultiplier;

        // Apply custom multipliers if available
        if (grabbableComp != null)
        {
            springMultiplier *= grabbableComp.springMultiplier;
            damperMultiplier *= grabbableComp.damperMultiplier;
        }

        // Create and configure spring joint with conservative initial settings
        CreateSpringJoint(springMultiplier * 0.2f, damperMultiplier); // Start with 20% spring force for smoother ramp

        // Apply physics modifications for control
        ApplyGrabPhysics(weightCategory);

        // Initialize smooth positioning
        targetGrabPosition = playerCamera.transform.position + playerCamera.transform.forward * currentGrabDistance;
        myGrabPoint = grabbedObject.position;

        // Call grab event
        if (grabbableComp != null)
            grabbableComp.OnGrabbed();

        if (currentTrackerSphere != null)
        {
            targetGrabPosition = currentTrackerSphere.transform.position;
            UpdateSphereGrabVisuals(true);

            // Make sphere kinematic during grab for stable force application
            if (sphereRigidbody != null)
            {
                sphereRigidbody.isKinematic = true;
            }
        }

        // Gradually ramp up spring forces
        StartCoroutine(RampUpSpringForces(springMultiplier, damperMultiplier));

        isStabilizing = false;
        Debug.Log($"Grabbed: {grabbedObject.name} | Weight Category: {weightCategory}");
    }

    private IEnumerator RampUpSpringForces(float targetSpring, float targetDamper)
    {
        if (grabJoint == null) yield break;

        float rampTime = 0.2f; // Faster ramp for more responsiveness
        float elapsed = 0f;
        float initialSpring = grabJoint.spring;

        while (elapsed < rampTime && grabJoint != null)
        {
            elapsed += Time.fixedDeltaTime;
            float t = elapsed / rampTime;

            grabJoint.spring = Mathf.Lerp(initialSpring, grabSpring * targetSpring, t);

            yield return new WaitForFixedUpdate();
        }

        if (grabJoint != null)
        {
            grabJoint.spring = grabSpring * targetSpring;
        }

        // After spring forces are ramped up, restore some original physics properties gradually
        StartCoroutine(RestorePhysicsGradually());
    }

    private IEnumerator RestorePhysicsGradually()
    {
        yield return new WaitForSeconds(0.1f);

        if (grabbedObject != null)
        {
            // Gradually restore drag values for better control
            grabbedObject.linearDamping = Mathf.Lerp(grabbedObject.linearDamping, originalLinearDrag * 2f, 0.5f);
            grabbedObject.angularDamping = Mathf.Lerp(grabbedObject.angularDamping, originalAngularDrag * 2f, 0.5f);
        }
    }

    private void CreateSpringJoint(float springMultiplier, float damperMultiplier)
    {
        // Always create joint on the grabbed object for distributed forces (Repo-style)
        grabJoint = grabbedObject.gameObject.AddComponent<SpringJoint>();
        grabJoint.autoConfigureConnectedAnchor = false;

        // Connect to the sphere for visual consistency, but forces are distributed
        if (currentTrackerSphere != null && sphereRigidbody != null)
        {
            grabJoint.connectedBody = sphereRigidbody;
        }

        grabJoint.minDistance = 0f;
        grabJoint.maxDistance = GetMaxDistanceForWeight(currentWeightCategory); // Weight-based slack
        grabJoint.damper = grabDamper * damperMultiplier;
        grabJoint.spring = grabSpring * springMultiplier;
        grabJoint.massScale = grabMassScale * GetMassScaleForWeight(currentWeightCategory);
    }

    /// <summary>
    /// Apply physics modifications for grabbed objects
    /// </summary>
    private void ApplyGrabPhysics(ObjectWeight weightCategory)
    {
        if (weightCategory == ObjectWeight.VeryLight || weightCategory == ObjectWeight.Light)
        {
            grabbedObject.angularDamping = rotationDamping * 10f; // Much higher angular damping for light objects
            grabbedObject.linearDamping = grabLinearDrag * 8f;
        }
        else
        {
            // Higher angular damping for all grabbed objects to prevent unwanted rotation
            grabbedObject.angularDamping = rotationDamping * 5f;
        }
    }

    /// <summary>
    /// Update grabbed object position
    /// </summary>
    private void HoldGrab()
    {
        if (grabbedObject == null || isStabilizing)
        {
            return;
        }

        // Only move smoothly when NOT scrolling
        if (!isScrolling)
        {
            // Move the sphere (which has the joint) to follow mouse movement
            if (currentTrackerSphere != null && sphereRigidbody != null)
            {
                Vector3 desiredPosition = playerCamera.transform.position + playerCamera.transform.forward * currentGrabDistance;

                // Use weight-adjusted speed for realistic difficulty - more dramatic difference
                float actualSpeed = currentSphereSpeed;
                Vector3 sphereTargetPos = Vector3.Lerp(currentTrackerSphere.transform.position, desiredPosition, Time.deltaTime * actualSpeed);
                sphereRigidbody.MovePosition(sphereTargetPos);

                // Update sphere rotation to always face the camera
                Vector3 directionToCamera = (playerCamera.transform.position - sphereTargetPos).normalized;
                currentTrackerSphere.transform.rotation = Quaternion.LookRotation(directionToCamera);

                // Apply distributed forces and soft orientation control
                ApplyDistributedForces(desiredPosition);
                ApplySoftOrientationControl();

                // Apply gravity compensation to make heavy objects feel heavy
                ApplyGravityCompensation();
            }
            else if (grabJoint != null)
            {
                // Fallback to original method
                Vector3 desiredPosition = playerCamera.transform.position + playerCamera.transform.forward * currentGrabDistance;
                grabTargetPosition = Vector3.Lerp(grabTargetPosition, desiredPosition, Time.deltaTime * (currentSphereSpeed * 0.6f));
                grabJoint.connectedAnchor = grabTargetPosition;

                // Apply distributed forces and collision checking for fallback method too
                ApplyDistributedForces(desiredPosition);
                ApplySoftOrientationControl();
                ApplyGravityCompensation();
            }
        }
        else
        {
            // When scrolling, still apply forces but don't move smoothly
            Vector3 currentPosition = playerCamera.transform.position + playerCamera.transform.forward * currentGrabDistance;
            ApplyDistributedForces(currentPosition);
            ApplySoftOrientationControl();
            ApplyGravityCompensation();
        }
    }

    /// <summary>
    /// Apply distributed forces across the object (Repo-style) rather than point forces
    /// </summary>
    private void ApplyDistributedForces(Vector3 desiredSpherePosition)
    {
        if (grabbedObject == null || currentTrackerSphere == null) return;

        // Calculate force needed to move object center toward desired position
        Vector3 currentObjectPos = grabbedObject.transform.position;
        Vector3 forceDirection = (desiredSpherePosition - currentObjectPos);

        if (forceDirection.magnitude > 0.1f)
        {
            // Apply weight-based force multiplier
            float weightForceMultiplier = GetForceMultiplierForWeight(currentWeightCategory);

            // Apply distributed force at center of mass (not grab point) for stability
            Vector3 distributedForce = forceDirection * objectMoveSpeed * weightForceMultiplier;
            grabbedObject.AddForce(distributedForce, ForceMode.Force);
        }
    }

    /// <summary>
    /// Apply soft orientation control - allows wobble but returns to original orientation
    /// </summary>
    private void ApplySoftOrientationControl()
    {
        if (grabbedObject == null || !allowOrientationWobble) return;

        // Get object scale to adjust behavior for small objects
        Vector3 objectScale = grabbedObject.transform.localScale;
        float averageScale = (objectScale.x + objectScale.y + objectScale.z) / 3f;

        // Small objects (scale < 0.5) get much more stable behavior
        bool isSmallObject = averageScale < 0.5f;

        // For very small objects, just lock them completely
        if (isSmallObject)
        {
            // Calculate current deviation from original rotation
            Quaternion currentRotation = grabbedObject.transform.rotation;
            float currentDeviation = Quaternion.Angle(currentRotation, originalRotation);

            // Very small tolerance for small objects
            if (currentDeviation > 2f) // Only 2 degrees allowed
            {
                // Force immediate return to original rotation
                grabbedObject.transform.rotation = Quaternion.Slerp(
                    currentRotation,
                    originalRotation,
                    Time.deltaTime * 10f // Fast correction
                );

                // Kill any angular velocity
                grabbedObject.angularVelocity = grabbedObject.angularVelocity * 0.1f;
            }
            return; // Skip the rest of the wobble logic for small objects
        }

        // Normal wobble logic for larger objects
        Quaternion currentRotation2 = grabbedObject.transform.rotation;
        float currentDeviation2 = Quaternion.Angle(currentRotation2, originalRotation);

        // Add wobble based on movement speed and acceleration
        Vector3 currentVelocity = grabbedObject.linearVelocity;
        Vector3 acceleration = (currentVelocity - lastVelocity) / Time.deltaTime;
        lastVelocity = currentVelocity;

        // Allow more deviation for faster movements (natural wobble)
        float velocityFactor = Mathf.Clamp01(currentVelocity.magnitude / 10f);
        float accelerationFactor = Mathf.Clamp01(acceleration.magnitude / 20f);
        float allowedDeviation = maxOrientationDeviation * (0.3f + velocityFactor * 0.4f + accelerationFactor * 0.3f);

        // Apply spring force to return to original orientation
        Quaternion targetRotation = originalRotation;

        // If deviation is too large, apply stronger corrective force
        if (currentDeviation2 > allowedDeviation)
        {
            targetRotation = originalRotation;
        }
        else
        {
            // Allow natural wobbling within limits
            targetRotation = currentRotation2;
        }

        // Calculate spring torque to return to target rotation
        Quaternion rotationDifference = targetRotation * Quaternion.Inverse(currentRotation2);
        rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);

        // Normalize angle
        if (angle > 180f) angle -= 360f;

        // Apply spring-based corrective torque
        if (Mathf.Abs(angle) > 1f)
        {
            float springStrength = GetOrientationSpringForWeight(currentWeightCategory);
            float dampingStrength = GetOrientationDampingForWeight(currentWeightCategory);

            // Spring force proportional to deviation
            Vector3 springTorque = axis * (angle * Mathf.Deg2Rad * orientationSpring * springStrength);

            // Damping force to reduce oscillation
            Vector3 dampingTorque = grabbedObject.angularVelocity * (-orientationDamping * dampingStrength);

            // Apply softer corrective force that allows natural movement
            grabbedObject.AddTorque((springTorque + dampingTorque) * 0.5f, ForceMode.Force);
        }
    }

    /// <summary>
    /// Get rotation speed multiplier based on weight (heavier = slower rotation)
    /// </summary>
    private float GetWeightRotationMultiplier(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 2.0f; // Very fast rotation
            case ObjectWeight.Light:
                return 1.5f; // Fast rotation
            case ObjectWeight.Medium:
                return 1.0f; // Normal rotation
            case ObjectWeight.Heavy:
                return 0.6f; // Slower rotation
            case ObjectWeight.VeryHeavy:
                return 0.3f; // Much slower rotation
            case ObjectWeight.Immovable:
                return 0.1f; // Very slow rotation
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Get maximum allowed deviation based on weight (heavier = more wobble allowed)
    /// </summary>
    private float GetMaxDeviationForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 5f; // Very strict orientation
            case ObjectWeight.Light:
                return 8f; // Strict orientation
            case ObjectWeight.Medium:
                return 15f; // Normal wobble
            case ObjectWeight.Heavy:
                return 25f; // More wobble allowed
            case ObjectWeight.VeryHeavy:
                return 35f; // Lots of wobble
            case ObjectWeight.Immovable:
                return 45f; // Maximum wobble
            default:
                return 15f;
        }
    }

    /// <summary>
    /// Get orientation spring strength based on weight
    /// </summary>
    private float GetOrientationSpringForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 1.5f; // More responsive to orientation correction
            case ObjectWeight.Light:
                return 1.3f;
            case ObjectWeight.Medium:
                return 1.0f;
            case ObjectWeight.Heavy:
                return 0.8f; // Less responsive, more natural sway
            case ObjectWeight.VeryHeavy:
                return 0.6f;
            case ObjectWeight.Immovable:
                return 0.4f;
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Get orientation damping strength based on weight
    /// </summary>
    private float GetOrientationDampingForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 1.2f;
            case ObjectWeight.Light:
                return 1.1f;
            case ObjectWeight.Medium:
                return 1.0f;
            case ObjectWeight.Heavy:
                return 0.9f; // Less damping allows more natural sway
            case ObjectWeight.VeryHeavy:
                return 0.8f;
            case ObjectWeight.Immovable:
                return 0.7f;
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Check if object is currently colliding with environment
    /// </summary>
    private bool IsObjectColliding()
    {
        if (grabbedObject == null) return false;

        // More sensitive collision detection - check for contact with environment
        Collider objectCollider = grabbedObject.GetComponent<Collider>();
        if (objectCollider == null) return false;

        // Check for overlapping colliders (indicating collision)
        Collider[] overlapping = Physics.OverlapBox(
            objectCollider.bounds.center,
            objectCollider.bounds.extents,
            grabbedObject.transform.rotation,
            ~0, // Check all layers
            QueryTriggerInteraction.Ignore
        );

        // Filter out self and tracker sphere
        foreach (Collider col in overlapping)
        {
            if (col != objectCollider && col.gameObject != currentTrackerSphere)
            {
                // Check if it's a significant collision (not just touching)
                if (grabbedObject.linearVelocity.magnitude > 3f ||
                    grabbedObject.angularVelocity.magnitude > 3f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Apply gravity compensation to make heavy objects feel heavy
    /// </summary>
    private void ApplyGravityCompensation()
    {
        if (grabbedObject == null) return;

        // Apply additional downward force for heavy objects
        float gravityMultiplier = GetGravityMultiplierForWeight(currentWeightCategory);

        if (gravityMultiplier > 1.0f)
        {
            Vector3 additionalGravity = Physics.gravity * (gravityMultiplier - 1.0f) * grabbedObject.mass * gravityInfluence;
            grabbedObject.AddForce(additionalGravity, ForceMode.Force);
        }

        // Apply drag based on weight to simulate air resistance
        float dragMultiplier = GetDragMultiplierForWeight(currentWeightCategory);
        Vector3 dragForce = grabbedObject.linearVelocity * (-dragMultiplier * grabbedObject.mass * 0.1f);
        grabbedObject.AddForce(dragForce, ForceMode.Force);
    }

    /// <summary>
    /// Get speed multiplier based on weight for sphere movement
    /// </summary>
    private float GetSpeedMultiplierForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 2.0f; // Double speed
            case ObjectWeight.Light:
                return 1.5f; // 50% faster
            case ObjectWeight.Medium:
                return 1.0f; // Normal speed
            case ObjectWeight.Heavy:
                return 0.5f; // Half speed
            case ObjectWeight.VeryHeavy:
                return 0.3f; // 70% slower
            case ObjectWeight.Immovable:
                return 0.1f; // 90% slower
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Get mass reduction factor based on weight
    /// </summary>
    private float GetMassReductionForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 0.5f; // Reduce to 50%
            case ObjectWeight.Light:
                return 0.6f; // Reduce to 60%
            case ObjectWeight.Medium:
                return 0.7f; // Reduce to 70%
            case ObjectWeight.Heavy:
                return 0.8f; // Reduce to 80%
            case ObjectWeight.VeryHeavy:
                return 0.9f; // Reduce to 90%
            case ObjectWeight.Immovable:
                return 1.0f; // No reduction
            default:
                return 0.7f;
        }
    }

    /// <summary>
    /// Get torque multiplier based on weight
    /// </summary>
    private float GetTorqueMultiplierForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 2.0f;
            case ObjectWeight.Light:
                return 1.5f;
            case ObjectWeight.Medium:
                return 1.0f;
            case ObjectWeight.Heavy:
                return 0.6f;
            case ObjectWeight.VeryHeavy:
                return 0.4f;
            case ObjectWeight.Immovable:
                return 0.2f;
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Get force multiplier based on weight for direct force application
    /// </summary>
    private float GetForceMultiplierForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 0.3f; // Very responsive, less force needed
            case ObjectWeight.Light:
                return 0.5f;
            case ObjectWeight.Medium:
                return 1.0f;
            case ObjectWeight.Heavy:
                return 2.0f; // More force needed but still controllable
            case ObjectWeight.VeryHeavy:
                return 3.5f; // Much more force needed
            case ObjectWeight.Immovable:
                return 5.0f; // Massive force needed
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Get max distance for SpringJoint based on weight (heavier = more slack)
    /// </summary>
    private float GetMaxDistanceForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 0.05f; // Tight connection
            case ObjectWeight.Light:
                return 0.1f;
            case ObjectWeight.Medium:
                return 0.15f;
            case ObjectWeight.Heavy:
                return 0.25f; // More slack, allows sagging
            case ObjectWeight.VeryHeavy:
                return 0.4f; // Lots of slack
            case ObjectWeight.Immovable:
                return 0.6f; // Maximum slack
            default:
                return 0.15f;
        }
    }

    /// <summary>
    /// Get mass scale multiplier based on weight
    /// </summary>
    private float GetMassScaleForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 0.5f;
            case ObjectWeight.Light:
                return 1.0f;
            case ObjectWeight.Medium:
                return 1.5f;
            case ObjectWeight.Heavy:
                return 2.5f; // Higher mass scale for heavier objects
            case ObjectWeight.VeryHeavy:
                return 4.0f;
            case ObjectWeight.Immovable:
                return 6.0f;
            default:
                return 1.5f;
        }
    }

    /// <summary>
    /// Get gravity multiplier based on weight
    /// </summary>
    private float GetGravityMultiplierForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 0.5f; // Less affected by gravity
            case ObjectWeight.Light:
                return 0.8f;
            case ObjectWeight.Medium:
                return 1.0f;
            case ObjectWeight.Heavy:
                return 1.5f; // More affected by gravity
            case ObjectWeight.VeryHeavy:
                return 2.0f;
            case ObjectWeight.Immovable:
                return 2.5f;
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Get drag multiplier based on weight
    /// </summary>
    private float GetDragMultiplierForWeight(ObjectWeight weightCategory)
    {
        switch (weightCategory)
        {
            case ObjectWeight.VeryLight:
                return 0.1f;
            case ObjectWeight.Light:
                return 0.3f;
            case ObjectWeight.Medium:
                return 0.5f;
            case ObjectWeight.Heavy:
                return 0.8f; // More momentum, harder to stop
            case ObjectWeight.VeryHeavy:
                return 1.2f;
            case ObjectWeight.Immovable:
                return 1.5f;
            default:
                return 0.5f;
        }
    }

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

    private void HandleScrollInput()
    {
        if (grabbedObject == null) return;

        if (Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0)
        {
            float scrollInput = Mouse.current.scroll.ReadValue().y;

            // Mark as scrolling and update time
            isScrolling = true;
            lastScrollTime = Time.time;

            // Get weight-based scroll sensitivity
            GrabbableObject grabbableComp = grabbedObject.GetComponent<GrabbableObject>();
            ObjectWeight weightCategory = grabbableComp != null ? grabbableComp.GetWeightCategory() : GetWeightCategoryFromMass(grabbedObject.mass);

            float scrollMultiplier = GetScrollMultiplierForWeight(weightCategory);
            // Make scrolling less slippery by reducing the base sensitivity
            float adjustedSensitivity = scrollSensitivity * 0.3f; // Much less slippery
            float scrollAmount = (scrollInput > 0 ? -1f : 1f) * adjustedSensitivity * scrollMultiplier;
            float oldDistance = currentGrabDistance;
            currentGrabDistance = Mathf.Clamp(currentGrabDistance - scrollAmount, minGrabDistance, maxGrabDistance);

            // IMMEDIATELY update the object position and sphere rotation
            Vector3 newPosition = playerCamera.transform.position + playerCamera.transform.forward * currentGrabDistance;

            if (currentTrackerSphere != null && sphereRigidbody != null)
            {
                sphereRigidbody.MovePosition(newPosition);
                // Update sphere rotation to face camera
                Vector3 directionToCamera = (playerCamera.transform.position - newPosition).normalized;
                currentTrackerSphere.transform.rotation = Quaternion.LookRotation(directionToCamera);
            }

            if (grabJoint != null)
            {
                grabJoint.connectedAnchor = newPosition;
            }

            // Immediately stop object's current movement when scrolling
            grabbedObject.linearVelocity = Vector3.zero;
            grabbedObject.angularVelocity = grabbedObject.angularVelocity * 0.8f; // Reduce but don't completely stop rotation

            string direction = scrollInput > 0 ? "Closer" : "Farther";
            float actualChange = Mathf.Abs(currentGrabDistance - oldDistance);
            Debug.Log($"Distance control: {direction} ({weightCategory}, {scrollMultiplier:F1}x), Old: {oldDistance:F1}m, New: {currentGrabDistance:F1}m, Change: {actualChange:F1}m");
        }
    }

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

        // Reset grab state
        isGrabbing = false;
        grabbedObject = null;
        preserveOrientation = true; // Reset for next grab

        // Reset sphere visuals
        if (currentTrackerSphere != null)
        {
            UpdateSphereGrabVisuals(false);
        }

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
        grabbedObject.constraints = originalConstraints; // Restore original constraints
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

        // Reset sphere rigidbody to non-kinematic for normal tracking
        if (sphereRigidbody != null)
        {
            sphereRigidbody.isKinematic = true; // Keep kinematic for smooth tracking
        }
    }

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

    private void HandleGrab()
    {
        if (grabInput && grabbedObject == null && hitObject != null)
        {
            // Try to grab the object
            Rigidbody hitRb = hitObject.GetComponent<Rigidbody>();
            GrabbableObject grabbableComp = hitObject.GetComponent<GrabbableObject>();

            if (hitRb != null && (grabbableComp == null || grabbableComp.CanBeGrabbed()))
            {
                SetupGrabbedObject(hitRb, currentHitPoint, grabbableComp);
            }
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

    private void UpdateSphereGrabVisuals(bool isAttached)
    {
        if (currentTrackerSphere == null) return;

        Renderer renderer = currentTrackerSphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (isAttached)
            {
                // "Attached" appearance
                renderer.material.color = Color.magenta; // Purple = attached
                currentTrackerSphere.transform.localScale = Vector3.one * 0.25f; // Bigger

                // Optional: Add pulsing effect
                float pulse = Mathf.Sin(Time.time * 5f) * 0.1f + 1f;
                currentTrackerSphere.transform.localScale = Vector3.one * (0.25f * pulse);
            }
            else
            {
                // Normal tracking appearance
                renderer.material.color = Color.green;
                currentTrackerSphere.transform.localScale = Vector3.one * 0.15f;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;

        // Draw the raycast
        Gizmos.color = Color.yellow;
        Vector3 rayStart = playerCamera.transform.position;
        Vector3 rayEnd = rayStart + playerCamera.transform.forward * maxTrackDistance;
        Gizmos.DrawLine(rayStart, rayEnd);

        // Draw max distance sphere
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(rayEnd, 0.1f);
    }
}