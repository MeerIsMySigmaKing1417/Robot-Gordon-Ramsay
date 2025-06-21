using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Component for weapons that can be picked up from the ground
/// Enhanced with Minecraft-style physics-to-floating transition
/// </summary>
public class PickupableWeapon : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Weapon data for this pickupable weapon")]
    public WeaponData weaponData;

    [Tooltip("Current ammo in this weapon when picked up")]
    [Range(0, 100)]
    public int currentAmmo = 0;

    [Tooltip("Reserve ammo that comes with this weapon")]
    [Range(0, 500)]
    public int reserveAmmo = 60;

    [Tooltip("Can this weapon be picked up?")]
    public bool canBePickedUp = true;

    [Header("Visual Feedback")]
    [Tooltip("Highlight material when player is nearby")]
    public Material highlightMaterial;

    [Tooltip("Pickup effect to spawn when collected")]
    public GameObject pickupEffect;

    [Header("Minecraft-Style Physics")]
    [Tooltip("Time for physics to settle before floating starts")]
    [Range(1f, 5f)]
    public float physicsSettleTime = 2f;

    [Tooltip("Delay after physics settle before floating begins")]
    [Range(0f, 1f)]
    public float floatStartDelay = 0.3f;

    [Tooltip("Should the weapon bob up and down after physics settle?")]
    public bool enableBobbing = true;

    [Tooltip("Bobbing speed")]
    [Range(0.5f, 5f)]
    public float bobbingSpeed = 1f;

    [Tooltip("Bobbing height")]
    [Range(0.1f, 1f)]
    public float bobbingHeight = 0.3f;

    [Header("Physics Layers")]
    [Tooltip("Physics layer for dropped weapons (should ignore Player layer)")]
    public string droppedWeaponLayer = "DroppedWeapons";

    [Tooltip("Should weapon colliders be disabled during floating? (Prevents interaction with environment while floating)")]
    public bool disableCollidersWhenFloating = false;

    [Tooltip("Directly ignore collisions with player colliders (backup method)")]
    public bool forceIgnorePlayerCollisions = true;

    [Header("Rotation")]
    [Tooltip("Should the weapon rotate slowly?")]
    public bool enableRotation = true;

    [Tooltip("Rotation speed (degrees per second)")]
    [Range(10f, 180f)]
    public float rotationSpeed = 45f;

    [Header("Drop Input (New Input System)")]
    [Tooltip("Input Action Asset reference for drop action")]
    public InputActionReference dropActionReference;

    [Header("Events")]
    [Tooltip("Called when weapon is picked up")]
    public UnityEvent<WeaponData> OnWeaponPickedUp;

    [Tooltip("Called when player enters pickup range")]
    public UnityEvent OnPlayerEnterRange;

    [Tooltip("Called when player exits pickup range")]
    public UnityEvent OnPlayerExitRange;

    [Tooltip("Called when weapon is dropped")]
    public UnityEvent OnWeaponDropped;

    // Private variables
    private Vector3 startPosition;
    private Renderer weaponRenderer;
    private Material originalMaterial;
    private InteractableComponent interactableComponent;

    // Physics and floating state
    private Rigidbody rb;
    private Collider[] weaponColliders;
    private bool isPhysicsActive = true;
    private bool isFloating = false;
    private float dropTime;
    private Vector3 settledPosition;

    // Input handling
    private InputAction dropAction;
    private bool playerInRange = false;
    private WeaponManager playerWeaponManager;

    #region Unity Lifecycle

    void Awake()
    {
        // Get components
        weaponRenderer = GetComponent<Renderer>();
        rb = GetComponent<Rigidbody>();
        weaponColliders = GetComponentsInChildren<Collider>();

        if (weaponRenderer != null)
        {
            originalMaterial = weaponRenderer.material;
        }

        // Get or add InteractableComponent
        interactableComponent = GetComponent<InteractableComponent>();
        if (interactableComponent == null)
        {
            interactableComponent = gameObject.AddComponent<InteractableComponent>();
            SetupInteractableComponent();
        }

        // Setup New Input System for dropping
        SetupDropInput();

        // Setup physics layers for dropped weapon
        SetupPhysicsLayers();
    }

    void Start()
    {
        // Record drop time for physics settling
        dropTime = Time.time;

        // Store starting position for bobbing (will be updated when physics settle)
        startPosition = transform.position;

        // Validate setup
        if (weaponData == null)
        {
            Debug.LogWarning($"PickupableWeapon on {gameObject.name} has no WeaponData assigned!", this);
        }

        // Initialize ammo if not set
        if (currentAmmo == 0 && weaponData != null)
        {
            currentAmmo = weaponData.magazineSize;
        }

        if (reserveAmmo == 0 && weaponData != null)
        {
            reserveAmmo = weaponData.maxAmmo / 2;
        }

        // Setup interaction events
        SetupInteractionEvents();

        // Schedule physics-to-floating transition
        Invoke(nameof(BeginFloatingTransition), physicsSettleTime);
    }

    void Update()
    {
        if (!canBePickedUp)
            return;

        // Handle floating animation (only when not using physics)
        if (!isPhysicsActive && isFloating && enableBobbing)
        {
            UpdateBobbing();
        }

        // Handle rotation animation (always active)
        if (enableRotation)
        {
            UpdateRotation();
        }

        // Handle drop input when player is nearby
        if (playerInRange && dropAction != null && dropAction.WasPressedThisFrame())
        {
            HandleDropInput();
        }
    }

    void OnDestroy()
    {
        // Clean up input
        if (dropAction != null)
        {
            dropAction.Disable();
            dropAction.Dispose();
        }

        // Clean up event subscriptions
        CleanupInteractionEvents();
    }

    #endregion

    #region Setup Methods

    private void SetupDropInput()
    {
        // Setup drop action from InputActionReference or create fallback
        if (dropActionReference != null)
        {
            dropAction = dropActionReference.action;
        }
        else
        {
            // Fallback: create drop action manually
            dropAction = new InputAction("Drop", InputActionType.Button, "<Keyboard>/q");
        }

        if (dropAction != null)
        {
            dropAction.Enable();
        }
    }

    private void SetupPhysicsLayers()
    {
        // Set weapon to DroppedWeapons layer to ignore player collisions
        int droppedLayer = LayerMask.NameToLayer(droppedWeaponLayer);

        if (droppedLayer == -1)
        {
            Debug.LogWarning($"Layer '{droppedWeaponLayer}' doesn't exist! Please create it in Project Settings > Tags and Layers. Using Default layer instead.");
            droppedLayer = 0; // Default layer
        }

        // Set layer for this object and all children
        SetLayerRecursively(gameObject, droppedLayer);

        // BACKUP METHOD: Directly ignore collisions with player colliders
        if (forceIgnorePlayerCollisions)
        {
            StartCoroutine(IgnorePlayerCollisionsCoroutine());
        }

        Debug.Log($"Set weapon {gameObject.name} to layer: {droppedWeaponLayer} (ID: {droppedLayer})");
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>
    /// Coroutine to find and ignore all player colliders (backup method)
    /// </summary>
    private System.Collections.IEnumerator IgnorePlayerCollisionsCoroutine()
    {
        yield return new WaitForSeconds(0.1f); // Wait for everything to initialize

        // Refresh weapon colliders in case they changed
        weaponColliders = GetComponentsInChildren<Collider>();

        // Find all player colliders
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        int ignoredCollisions = 0;

        foreach (GameObject player in players)
        {
            Collider[] playerColliders = player.GetComponentsInChildren<Collider>();

            foreach (Collider playerCol in playerColliders)
            {
                foreach (Collider weaponCol in weaponColliders)
                {
                    if (playerCol != null && weaponCol != null)
                    {
                        Physics.IgnoreCollision(playerCol, weaponCol, true);
                        ignoredCollisions++;
                        Debug.Log($"Ignored collision between {weaponCol.name} and player {playerCol.name}");
                    }
                }
            }
        }

        Debug.Log($"Set up {ignoredCollisions} collision ignores for weapon {gameObject.name}");
    }

    private void SetupInteractableComponent()
    {
        if (interactableComponent != null && weaponData != null)
        {
            interactableComponent.interactionText = $"Press E to pickup {weaponData.weaponName}";
            interactableComponent.interactionRange = 3f;
            interactableComponent.highlightColor = Color.cyan;
        }
    }

    private void SetupInteractionEvents()
    {
        if (interactableComponent != null)
        {
            try
            {
                if (interactableComponent.OnInteractionComplete != null)
                    interactableComponent.OnInteractionComplete.AddListener(OnPickupInteraction);

                if (interactableComponent.OnHoverEnter != null)
                    interactableComponent.OnHoverEnter.AddListener(OnPlayerEnterPickupRange);

                if (interactableComponent.OnHoverExit != null)
                    interactableComponent.OnHoverExit.AddListener(OnPlayerExitPickupRange);

                Debug.Log($"Successfully setup events for {gameObject.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error setting up PickupableWeapon events: {e.Message}", this);
            }
        }
    }

    private void CleanupInteractionEvents()
    {
        if (interactableComponent != null)
        {
            try
            {
                if (interactableComponent.OnInteractionComplete != null)
                    interactableComponent.OnInteractionComplete.RemoveListener(OnPickupInteraction);

                if (interactableComponent.OnHoverEnter != null)
                    interactableComponent.OnHoverEnter.RemoveListener(OnPlayerEnterPickupRange);

                if (interactableComponent.OnHoverExit != null)
                    interactableComponent.OnHoverExit.RemoveListener(OnPlayerExitPickupRange);
            }
            catch (System.Exception)
            {
                // Silently handle cleanup errors during destruction
            }
        }
    }

    #endregion

    #region Minecraft-Style Physics Transition

    /// <summary>
    /// Begin transition from physics to floating (Minecraft style)
    /// </summary>
    private void BeginFloatingTransition()
    {
        if (this == null || !canBePickedUp) return;

        Debug.Log($"Weapon {weaponData?.weaponName} beginning floating transition");

        // Disable physics
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Store settled position as new baseline for floating
        settledPosition = transform.position;
        startPosition = settledPosition;
        isPhysicsActive = false;

        // Start floating after delay
        Invoke(nameof(StartFloating), floatStartDelay);
    }

    /// <summary>
    /// Start the floating animation
    /// </summary>
    private void StartFloating()
    {
        if (this == null || !canBePickedUp) return;

        isFloating = true;

        // Optionally disable colliders during floating to prevent environmental interference
        if (disableCollidersWhenFloating)
        {
            SetCollidersEnabled(false);
            Debug.Log($"Disabled colliders for floating weapon {weaponData?.weaponName}");
        }

        Debug.Log($"Weapon {weaponData?.weaponName} started floating");
    }

    /// <summary>
    /// Enable or disable all weapon colliders
    /// </summary>
    private void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider col in weaponColliders)
        {
            if (col != null)
            {
                col.enabled = enabled;
            }
        }
    }

    #endregion

    #region Animation Methods

    /// <summary>
    /// Update bobbing animation (Minecraft-style floating) - Only floats upward to prevent floor clipping
    /// </summary>
    private void UpdateBobbing()
    {
        // Use Mathf.Abs to make sine wave only positive (0 to 1) so it only floats upward
        float yOffset = Mathf.Abs(Mathf.Sin(Time.time * bobbingSpeed)) * bobbingHeight;
        float newY = startPosition.y + yOffset;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }

    /// <summary>
    /// Update rotation animation
    /// </summary>
    private void UpdateRotation()
    {
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Handle drop input (Q key)
    /// </summary>
    private void HandleDropInput()
    {
        if (playerWeaponManager != null)
        {
            // Tell the weapon manager to drop current weapon
            playerWeaponManager.DropCurrentWeapon();
            Debug.Log("Drop weapon requested via Q key");
        }
    }

    #endregion

    #region Interaction Handlers

    /// <summary>
    /// Called when player interacts with the weapon (E key)
    /// </summary>
    private void OnPickupInteraction()
    {
        Debug.Log($"OnPickupInteraction called for {gameObject.name}");

        if (!canBePickedUp)
        {
            Debug.Log("Cannot be picked up!");
            return;
        }

        // Find the player's weapon manager
        WeaponManager weaponManager = FindPlayerWeaponManager();

        if (weaponManager != null)
        {
            Debug.Log("Found WeaponManager, attempting pickup...");

            // Try to pickup the weapon
            bool pickupSuccessful = weaponManager.TryPickupWeapon(this);

            if (!pickupSuccessful)
            {
                Debug.Log("Pickup failed - weapon remains on ground");
            }
        }
        else
        {
            Debug.LogWarning("Could not find WeaponManager on player!");
        }
    }

    /// <summary>
    /// Called when player enters pickup range
    /// </summary>
    private void OnPlayerEnterPickupRange()
    {
        if (!canBePickedUp)
            return;

        playerInRange = true;
        playerWeaponManager = FindPlayerWeaponManager();

        ApplyHighlight(true);
        OnPlayerEnterRange?.Invoke();

        Debug.Log($"Player entered range of {gameObject.name}");
    }

    /// <summary>
    /// Called when player exits pickup range
    /// </summary>
    private void OnPlayerExitPickupRange()
    {
        playerInRange = false;
        playerWeaponManager = null;

        ApplyHighlight(false);
        OnPlayerExitRange?.Invoke();

        Debug.Log($"Player exited range of {gameObject.name}");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialize weapon as freshly dropped (call this when dropping weapons)
    /// </summary>
    public void InitializeAsDropped(Vector3 dropVelocity = default)
    {
        dropTime = Time.time;
        isPhysicsActive = true;
        isFloating = false;

        // Apply drop velocity if provided
        if (rb != null && dropVelocity != Vector3.zero)
        {
            rb.isKinematic = false;
            rb.AddForce(dropVelocity, ForceMode.Impulse);
        }

        // IMPORTANT: Re-setup physics layers and collision ignoring for newly dropped weapon
        SetupPhysicsLayers();

        // Schedule physics-to-floating transition
        CancelInvoke(); // Cancel any existing invokes
        Invoke(nameof(BeginFloatingTransition), physicsSettleTime);

        OnWeaponDropped?.Invoke();
        Debug.Log($"Weapon {weaponData?.weaponName} dropped with physics and collision ignoring re-setup");
    }

    /// <summary>
    /// Perform the pickup (called by WeaponManager)
    /// </summary>
    public void PerformPickup()
    {
        // Re-enable colliders if they were disabled
        if (disableCollidersWhenFloating && isFloating)
        {
            SetCollidersEnabled(true);
        }

        // Trigger pickup effects
        TriggerPickupEffects();

        // Trigger events
        OnWeaponPickedUp?.Invoke(weaponData);

        Debug.Log($"Picked up {weaponData.weaponName} with {currentAmmo}/{reserveAmmo} ammo");

        // Disable components immediately to prevent double-pickup
        canBePickedUp = false;

        if (interactableComponent != null)
        {
            interactableComponent.SetInteractable(false);
        }

        // Disable colliders to prevent further interaction
        SetCollidersEnabled(false);

        // Destroy immediately
        Destroy(gameObject);
    }

    /// <summary>
    /// Set pickup availability
    /// </summary>
    public void SetPickupable(bool pickupable)
    {
        canBePickedUp = pickupable;

        if (interactableComponent != null)
        {
            interactableComponent.SetInteractable(pickupable);
        }
    }

    /// <summary>
    /// Get weapon info for pickup
    /// </summary>
    public WeaponPickupInfo GetPickupInfo()
    {
        return new WeaponPickupInfo
        {
            weaponData = weaponData,
            currentAmmo = currentAmmo,
            reserveAmmo = reserveAmmo
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Apply or remove highlight effect
    /// </summary>
    private void ApplyHighlight(bool highlight)
    {
        if (weaponRenderer == null)
            return;

        if (highlight && highlightMaterial != null)
        {
            weaponRenderer.material = highlightMaterial;
        }
        else if (originalMaterial != null)
        {
            weaponRenderer.material = originalMaterial;
        }
    }

    /// <summary>
    /// Trigger pickup effects
    /// </summary>
    private void TriggerPickupEffects()
    {
        // Spawn pickup effect
        if (pickupEffect != null)
        {
            GameObject effect = Instantiate(pickupEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    /// <summary>
    /// Find the player's weapon manager
    /// </summary>
    private WeaponManager FindPlayerWeaponManager()
    {
        // Look for player by tag first
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            WeaponManager weaponManager = player.GetComponent<WeaponManager>();
            if (weaponManager != null)
                return weaponManager;
        }

        // Fallback: find any WeaponManager in scene
        return FindObjectOfType<WeaponManager>();
    }

    #endregion

    #region Debug Gizmos

    void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = canBePickedUp ? Color.green : Color.red;
        if (interactableComponent != null)
        {
            Gizmos.DrawWireSphere(transform.position, interactableComponent.interactionRange);
        }

        // Draw bobbing path
        if (enableBobbing && Application.isPlaying && isFloating)
        {
            Gizmos.color = Color.yellow;
            Vector3 minPos = new Vector3(startPosition.x, startPosition.y - bobbingHeight, startPosition.z);
            Vector3 maxPos = new Vector3(startPosition.x, startPosition.y + bobbingHeight, startPosition.z);
            Gizmos.DrawLine(minPos, maxPos);
        }

        // Show physics state
        if (Application.isPlaying)
        {
            Gizmos.color = isPhysicsActive ? Color.red : (isFloating ? Color.blue : Color.yellow);
            Gizmos.DrawSphere(transform.position, 0.1f);
        }
    }

    #endregion
}

/// <summary>
/// Data structure for weapon pickup information
/// </summary>
[System.Serializable]
public struct WeaponPickupInfo
{
    public WeaponData weaponData;
    public int currentAmmo;
    public int reserveAmmo;
}