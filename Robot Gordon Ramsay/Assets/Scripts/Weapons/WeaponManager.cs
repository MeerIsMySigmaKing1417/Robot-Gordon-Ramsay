using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Manages player weapons, switching, and interactions
/// Attach this to the player GameObject
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("Weapon Setup")]
    [Tooltip("Transform where weapons are attached (usually camera or hand)")]
    public Transform weaponHolder;

    [Tooltip("Layer mask for weapon drops")]
    public LayerMask weaponDropMask = -1;

    [Tooltip("Force applied when throwing weapons")]
    [Range(1f, 20f)]
    public float throwForce = 8f;

    [Tooltip("Upward force component for throwing")]
    [Range(0f, 10f)]
    public float throwUpwardForce = 3f;

    [Tooltip("Random spread for throw direction")]
    [Range(0f, 5f)]
    public float throwSpread = 1f;

    [Tooltip("Maximum number of weapons player can carry")]
    [Range(1, 10)]
    public int maxWeapons = 3;

    [Header("Weapon Positions")]
    [Tooltip("Default position offset for held weapons")]
    public Vector3 defaultWeaponPosition = new Vector3(0.5f, -0.3f, 0.8f);

    [Tooltip("Default rotation offset for held weapons")]
    public Vector3 defaultWeaponRotation = new Vector3(0f, 0f, 0f);

    [Tooltip("Default scale for held weapons")]
    public Vector3 defaultWeaponScale = new Vector3(1.5f, 1.5f, 1.5f);

    [Tooltip("Transition speed for weapon switching")]
    [Range(1f, 20f)]
    public float weaponSwitchSpeed = 10f;

    [Header("View Bobbing Integration")]
    [Tooltip("Should weapons bob with camera movement?")]
    public bool enableWeaponBobbing = true;

    [Tooltip("Weapon bobbing intensity (0-1)")]
    [Range(0f, 1f)]
    public float weaponBobbingIntensity = 0.5f;

    [Tooltip("Reference to player movement for bobbing")]
    public Movement playerMovement;

    [Header("Current State")]
    [Tooltip("Currently equipped weapon (read-only)")]
    [SerializeField] private int currentWeaponIndex = -1;

    [Tooltip("List of carried weapons (read-only)")]
    [SerializeField] private List<WeaponController> carriedWeapons = new List<WeaponController>();

    [Header("Input")]
    [Tooltip("Use new Input System?")]
    public bool useNewInputSystem = true;

    [Header("Events")]
    [Tooltip("Called when weapon is equipped")]
    public UnityEvent<WeaponData> OnWeaponEquipped;

    [Tooltip("Called when weapon is unequipped")]
    public UnityEvent<WeaponData> OnWeaponUnequipped;

    [Tooltip("Called when weapon is picked up")]
    public UnityEvent<WeaponData> OnWeaponPickedUp;

    [Tooltip("Called when weapon is dropped")]
    public UnityEvent<WeaponData> OnWeaponDropped;

    [Tooltip("Called when trying to pickup but inventory full")]
    public UnityEvent OnInventoryFull;

    // Private variables
    private PlayerInputActions inputActions;
    private Camera playerCamera;
    private bool fireInput = false;
    private bool fireInputPressed = false;
    private bool reloadInput = false;
    private bool dropInput = false;
    private bool aimInput = false;
    private WeaponController currentWeapon;
    private bool isSwitchingWeapons = false;
    private Vector3 originalWeaponPosition;
    private Vector3 baseWeaponPosition;

    #region Unity Lifecycle

    void Awake()
    {
        // Setup input system
        if (useNewInputSystem)
        {
            inputActions = new PlayerInputActions();
        }

        // Find player camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        // Setup weapon holder if not assigned
        if (weaponHolder == null)
        {
            weaponHolder = playerCamera != null ? playerCamera.transform : transform;
        }
    }

    void OnEnable()
    {
        if (useNewInputSystem && inputActions != null)
        {
            inputActions.Enable();
            inputActions.Player.Enable();

            // Grab input (left mouse) for shooting
            inputActions.Player.Grab.performed += ctx => fireInputPressed = true;
            inputActions.Player.Grab.started += ctx => fireInput = true;
            inputActions.Player.Grab.canceled += ctx => fireInput = false;

            // Right mouse for ADS
            inputActions.Player.Zoom.started += ctx => aimInput = true;
            inputActions.Player.Zoom.canceled += ctx => aimInput = false;

            // R key for reload
            inputActions.Player.Interact.performed += ctx => reloadInput = true;

            // Q key for drop weapon
            inputActions.Player.ScrollDown.performed += ctx => dropInput = true;

            // Number keys for weapon switching
            inputActions.Player.WeaponSlot1.performed += ctx => SwitchToWeapon(0);
            inputActions.Player.WeaponSlot2.performed += ctx => SwitchToWeapon(1);
            inputActions.Player.WeaponSlot3.performed += ctx => SwitchToWeapon(2);
        }
    }

    void OnDisable()
    {
        if (useNewInputSystem && inputActions != null)
        {
            inputActions.Player.Disable();
        }
    }

    void Start()
    {
        // Find player movement component for bobbing
        if (playerMovement == null)
        {
            playerMovement = GetComponent<Movement>();
        }

        Debug.Log("WeaponManager initialized");
    }

    void Update()
    {
        HandleInput();
        HandleWeaponSwitching();
        UpdateWeaponBobbing();
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Handle weapon input
    /// </summary>
    private void HandleInput()
    {
        // Handle firing
        if (currentWeapon != null && !isSwitchingWeapons)
        {
            if (useNewInputSystem)
            {
                currentWeapon.TryFire(fireInputPressed, fireInput);
                fireInputPressed = false; // Reset pressed state

                // Handle ADS
                currentWeapon.SetAiming(aimInput);
            }
            else
            {
                bool mousePressed = Input.GetMouseButtonDown(0);
                bool mouseHeld = Input.GetMouseButton(0);
                bool rightMouseHeld = Input.GetMouseButton(1);

                currentWeapon.TryFire(mousePressed, mouseHeld);
                currentWeapon.SetAiming(rightMouseHeld);
            }
        }

        // Handle reload
        if (reloadInput)
        {
            reloadInput = false;
            if (currentWeapon != null && !isSwitchingWeapons)
            {
                currentWeapon.Reload();
            }
        }

        // Handle drop weapon
        if (dropInput)
        {
            dropInput = false;
            DropCurrentWeapon();
        }

        // Handle legacy input for weapon switching
        if (!useNewInputSystem)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchToWeapon(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchToWeapon(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchToWeapon(2);

            if (Input.GetKeyDown(KeyCode.R) && currentWeapon != null)
            {
                currentWeapon.Reload();
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                DropCurrentWeapon();
            }
        }
    }

    #endregion

    #region Weapon Management

    /// <summary>
    /// Try to pickup a weapon
    /// </summary>
    public bool TryPickupWeapon(PickupableWeapon pickupWeapon)
    {
        if (pickupWeapon == null || pickupWeapon.weaponData == null)
            return false;

        WeaponPickupInfo pickupInfo = pickupWeapon.GetPickupInfo();

        // Check if inventory is full
        if (carriedWeapons.Count >= maxWeapons)
        {
            OnInventoryFull?.Invoke();
            Debug.Log("Cannot pickup weapon: inventory full!");
            return false;
        }

        // IMPORTANT: Destroy the pickup BEFORE creating the held version
        pickupWeapon.PerformPickup();

        // Create held weapon version
        GameObject heldWeaponObject = CreateHeldWeaponFromData(pickupInfo.weaponData);
        if (heldWeaponObject == null)
            return false;

        WeaponController weaponController = heldWeaponObject.GetComponent<WeaponController>();
        if (weaponController == null)
        {
            Destroy(heldWeaponObject);
            return false;
        }

        // Set ammo from pickup
        weaponController.AddAmmo(pickupInfo.reserveAmmo);

        // Add to inventory
        AddWeaponToInventory(weaponController);

        // Equip if it's the first weapon
        if (carriedWeapons.Count == 1)
        {
            SwitchToWeapon(0);
        }

        OnWeaponPickedUp?.Invoke(pickupInfo.weaponData);
        return true;
    }

    /// <summary>
    /// Add weapon to inventory
    /// </summary>
    private void AddWeaponToInventory(WeaponController weapon)
    {
        if (weapon == null)
            return;

        carriedWeapons.Add(weapon);

        // Parent to weapon holder and hide initially
        weapon.transform.SetParent(weaponHolder);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.gameObject.SetActive(false);

        Debug.Log($"Added {weapon.weaponData.weaponName} to inventory. Total weapons: {carriedWeapons.Count}");
    }

    /// <summary>
    /// Create held weapon GameObject from weapon data (different from pickup version)
    /// </summary>
    private GameObject CreateHeldWeaponFromData(WeaponData weaponData)
    {
        if (weaponData.weaponPrefab == null)
        {
            Debug.LogError($"WeaponData {weaponData.weaponName} has no weapon prefab assigned!");
            return null;
        }

        // Create the weapon object
        GameObject weaponObject = Instantiate(weaponData.weaponPrefab);

        // Remove any pickup components (these are for ground weapons only)
        PickupableWeapon pickup = weaponObject.GetComponent<PickupableWeapon>();
        if (pickup != null)
        {
            Destroy(pickup);
        }

        // Remove any InteractableComponent (not needed for held weapons)
        InteractableComponent interactable = weaponObject.GetComponent<InteractableComponent>();
        if (interactable != null)
        {
            Destroy(interactable);
        }

        // Ensure it has WeaponController
        WeaponController weaponController = weaponObject.GetComponent<WeaponController>();
        if (weaponController == null)
        {
            weaponController = weaponObject.AddComponent<WeaponController>();
        }

        // Setup weapon controller
        weaponController.weaponData = weaponData;

        // Setup for holding (disable physics, etc.)
        SetupHeldWeapon(weaponObject);

        return weaponObject;
    }

    /// <summary>
    /// Setup weapon for being held (disable physics, collisions, etc.)
    /// </summary>
    private void SetupHeldWeapon(GameObject weaponObject)
    {
        // Disable rigidbody physics
        Rigidbody rb = weaponObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Disable colliders (weapons shouldn't collide when held)
        Collider[] colliders = weaponObject.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // Ensure proper scale
        weaponObject.transform.localScale = Vector3.one;

        Debug.Log($"Setup held weapon: {weaponObject.name}");
    }

    /// <summary>
    /// Switch to weapon at index
    /// </summary>
    public void SwitchToWeapon(int weaponIndex)
    {
        if (isSwitchingWeapons || weaponIndex < 0 || weaponIndex >= carriedWeapons.Count)
            return;

        if (weaponIndex == currentWeaponIndex)
            return; // Already equipped

        StartCoroutine(SwitchWeaponCoroutine(weaponIndex));
    }

    /// <summary>
    /// Switch weapon coroutine
    /// </summary>
    private System.Collections.IEnumerator SwitchWeaponCoroutine(int newWeaponIndex)
    {
        isSwitchingWeapons = true;

        // Unequip current weapon
        if (currentWeapon != null)
        {
            currentWeapon.StopFiring();
            OnWeaponUnequipped?.Invoke(currentWeapon.weaponData);

            // Animate weapon going down
            yield return StartCoroutine(AnimateWeaponOut(currentWeapon));
            currentWeapon.gameObject.SetActive(false);
        }

        // Equip new weapon
        currentWeaponIndex = newWeaponIndex;
        currentWeapon = carriedWeapons[currentWeaponIndex];
        currentWeapon.gameObject.SetActive(true);

        // Position weapon
        PositionWeapon(currentWeapon);

        // Animate weapon coming up
        yield return StartCoroutine(AnimateWeaponIn(currentWeapon));

        OnWeaponEquipped?.Invoke(currentWeapon.weaponData);
        isSwitchingWeapons = false;

        Debug.Log($"Switched to {currentWeapon.weaponData.weaponName}");
    }

    /// <summary>
    /// Drop current weapon
    /// </summary>
    public void DropCurrentWeapon()
    {
        if (currentWeapon == null || isSwitchingWeapons)
            return;

        WeaponData droppedWeaponData = currentWeapon.weaponData;

        // Get current ammo
        currentWeapon.GetAmmoCount(out int currentAmmo, out int reserveAmmo);

        // Create dropped weapon pickup
        CreateDroppedWeapon(droppedWeaponData, currentAmmo, reserveAmmo);

        // Remove from inventory
        carriedWeapons.RemoveAt(currentWeaponIndex);
        OnWeaponUnequipped?.Invoke(droppedWeaponData);
        Destroy(currentWeapon.gameObject);

        // Switch to next available weapon
        if (carriedWeapons.Count > 0)
        {
            int newIndex = Mathf.Min(currentWeaponIndex, carriedWeapons.Count - 1);
            currentWeaponIndex = -1; // Reset to force switch
            SwitchToWeapon(newIndex);
        }
        else
        {
            currentWeapon = null;
            currentWeaponIndex = -1;
        }

        OnWeaponDropped?.Invoke(droppedWeaponData);
        Debug.Log($"Dropped {droppedWeaponData.weaponName}");
    }

    /// <summary>
    /// Create dropped weapon pickup
    /// </summary>
    private void CreateDroppedWeapon(WeaponData weaponData, int currentAmmo, int reserveAmmo)
    {
        if (weaponData.weaponPrefab == null)
            return;

        // Calculate drop position (slightly in front and up)
        Vector3 dropPosition = playerCamera.transform.position +
                              playerCamera.transform.forward * 1.5f +
                              Vector3.up * 0.2f;

        // Create pickup object from the original prefab
        GameObject droppedWeapon = Instantiate(weaponData.weaponPrefab, dropPosition, playerCamera.transform.rotation);

        // Remove weapon controller (not needed for pickups)
        WeaponController weaponController = droppedWeapon.GetComponent<WeaponController>();
        if (weaponController != null)
        {
            Destroy(weaponController);
        }

        // Setup as pickup weapon
        SetupDroppedWeapon(droppedWeapon, weaponData, currentAmmo, reserveAmmo);

        // Apply realistic throw force
        Rigidbody rb = droppedWeapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Calculate throw direction with some randomness
            Vector3 throwDirection = playerCamera.transform.forward;

            // Add some upward arc
            throwDirection += Vector3.up * throwUpwardForce * 0.1f;

            // Add random spread for realism
            throwDirection += new Vector3(
                Random.Range(-throwSpread, throwSpread) * 0.1f,
                Random.Range(-throwSpread * 0.5f, throwSpread * 0.5f) * 0.1f,
                Random.Range(-throwSpread, throwSpread) * 0.1f
            );

            throwDirection = throwDirection.normalized;

            // Apply force
            rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);

            // Add some random rotation for realism
            rb.AddTorque(new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f)
            ), ForceMode.Impulse);
        }

        Debug.Log($"Dropped weapon {weaponData.weaponName} at {dropPosition}");
    }

    /// <summary>
    /// Setup dropped weapon for pickup
    /// </summary>
    private void SetupDroppedWeapon(GameObject weaponObject, WeaponData weaponData, int currentAmmo, int reserveAmmo)
    {
        // Ensure it has PickupableWeapon component
        PickupableWeapon pickup = weaponObject.GetComponent<PickupableWeapon>();
        if (pickup == null)
        {
            pickup = weaponObject.AddComponent<PickupableWeapon>();
        }

        // Configure pickup
        pickup.weaponData = weaponData;
        pickup.currentAmmo = currentAmmo;
        pickup.reserveAmmo = reserveAmmo;
        pickup.canBePickedUp = true;

        // Ensure proper physics setup
        Rigidbody rb = weaponObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = weaponObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.useGravity = true;

        // Ensure colliders are enabled
        Collider[] colliders = weaponObject.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            // Add a collider if none exist
            BoxCollider boxCol = weaponObject.AddComponent<BoxCollider>();
            boxCol.size = Vector3.one * 0.5f;
        }
        else
        {
            // Enable existing colliders
            foreach (Collider col in colliders)
            {
                col.enabled = true;
            }
        }

        // Reset scale
        weaponObject.transform.localScale = Vector3.one;

        // Ensure it has InteractableComponent for pickup
        InteractableComponent interactable = weaponObject.GetComponent<InteractableComponent>();
        if (interactable == null)
        {
            interactable = weaponObject.AddComponent<InteractableComponent>();
            interactable.interactionText = $"Press E to pickup {weaponData.weaponName}";
            interactable.interactionRange = 3f;
        }

        interactable.SetInteractable(true);
    }

    #endregion

    #region Weapon Positioning

    /// <summary>
    /// Position weapon correctly for first person view
    /// </summary>
    private void PositionWeapon(WeaponController weapon)
    {
        if (weapon == null)
            return;

        Vector3 targetPosition = weapon.weaponData.holdPosition;
        Vector3 targetRotation = weapon.weaponData.holdRotation;
        Vector3 targetScale = Vector3.one;

        // Use default if weapon data doesn't specify
        if (targetPosition == Vector3.zero)
            targetPosition = defaultWeaponPosition;

        if (targetRotation == Vector3.zero)
            targetRotation = defaultWeaponRotation;

        // Apply scale
        targetScale = defaultWeaponScale;

        // Fix weapon facing backwards - add 180 degree Y rotation
        targetRotation.y += 180f;

        weapon.transform.localPosition = targetPosition;
        weapon.transform.localRotation = Quaternion.Euler(targetRotation);
        weapon.transform.localScale = targetScale;

        // Set base position for ADS system AFTER positioning
        weapon.SetBasePosition(targetPosition, targetRotation);

        // Store positions for bobbing
        originalWeaponPosition = targetPosition;
        baseWeaponPosition = targetPosition;

        Debug.Log($"Positioned weapon {weapon.weaponData.weaponName} at {targetPosition} with rotation {targetRotation} and scale {targetScale}");
        Debug.Log($"Base position for ADS: {targetPosition}");
    }

    /// <summary>
    /// Animate weapon going out of view
    /// </summary>
    private System.Collections.IEnumerator AnimateWeaponOut(WeaponController weapon)
    {
        Vector3 startPos = weapon.transform.localPosition;
        Vector3 endPos = startPos + Vector3.down * 2f;

        float elapsed = 0f;
        float duration = 1f / weaponSwitchSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            weapon.transform.localPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
    }

    /// <summary>
    /// Animate weapon coming into view
    /// </summary>
    private System.Collections.IEnumerator AnimateWeaponIn(WeaponController weapon)
    {
        Vector3 targetPos = weapon.weaponData.holdPosition;
        if (targetPos == Vector3.zero)
            targetPos = defaultWeaponPosition;

        Vector3 startPos = targetPos + Vector3.down * 2f;
        weapon.transform.localPosition = startPos;

        float elapsed = 0f;
        float duration = 1f / weaponSwitchSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            weapon.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        weapon.transform.localPosition = targetPos;
    }

    #endregion

    #region Weapon Switching

    /// <summary>
    /// Handle weapon switching logic
    /// </summary>
    private void HandleWeaponSwitching()
    {
        if (isSwitchingWeapons || carriedWeapons.Count <= 1)
            return;

        // Handle scroll wheel weapon switching
        if (useNewInputSystem)
        {
            // ScrollUp (E key) and ScrollDown (Q key) are used for other purposes
            // Mouse wheel switching can be added here if needed
        }
        else
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f)
            {
                SwitchToNextWeapon();
            }
            else if (scroll < 0f)
            {
                SwitchToPreviousWeapon();
            }
        }
    }

    /// <summary>
    /// Switch to next weapon in inventory
    /// </summary>
    public void SwitchToNextWeapon()
    {
        if (carriedWeapons.Count <= 1)
            return;

        int nextIndex = (currentWeaponIndex + 1) % carriedWeapons.Count;
        SwitchToWeapon(nextIndex);
    }

    /// <summary>
    /// Switch to previous weapon in inventory
    /// </summary>
    public void SwitchToPreviousWeapon()
    {
        if (carriedWeapons.Count <= 1)
            return;

        int prevIndex = currentWeaponIndex - 1;
        if (prevIndex < 0)
            prevIndex = carriedWeapons.Count - 1;

        SwitchToWeapon(prevIndex);
    }

    #endregion

    #region Weapon Bobbing

    /// <summary>
    /// Update weapon bobbing to match player movement
    /// </summary>
    private void UpdateWeaponBobbing()
    {
        // DISABLE ALL BOBBING if weapon is aiming
        if (!enableWeaponBobbing || currentWeapon == null || playerMovement == null || currentWeapon.IsAiming())
            return;

        // Get movement input from player
        Vector2 moveInput = playerMovement.moveInput;
        bool isMoving = moveInput.magnitude > 0.1f;

        if (isMoving)
        {
            // Create weapon-specific bobbing (separate from camera bobbing)
            float bobSpeed = 14f; // Walking bob speed
            float bobAmount = 0.02f * weaponBobbingIntensity; // Weapon bob amount

            // Calculate bob based on time and movement
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
            float bobOffsetX = Mathf.Cos(Time.time * bobSpeed * 0.5f) * bobAmount * 0.5f;

            // Apply bobbing to weapon
            Vector3 targetPos = baseWeaponPosition + new Vector3(bobOffsetX, bobOffset, 0f);
            currentWeapon.transform.localPosition = Vector3.Lerp(
                currentWeapon.transform.localPosition,
                targetPos,
                Time.deltaTime * 12f
            );
        }
        else
        {
            // Return to base position when not moving
            currentWeapon.transform.localPosition = Vector3.Lerp(
                currentWeapon.transform.localPosition,
                baseWeaponPosition,
                Time.deltaTime * 8f
            );
        }
    }

    #endregion

    #region Public Getters

    /// <summary>
    /// Get currently equipped weapon
    /// </summary>
    public WeaponController GetCurrentWeapon()
    {
        return currentWeapon;
    }

    /// <summary>
    /// Get all carried weapons
    /// </summary>
    public List<WeaponController> GetCarriedWeapons()
    {
        return new List<WeaponController>(carriedWeapons);
    }

    /// <summary>
    /// Get current weapon index
    /// </summary>
    public int GetCurrentWeaponIndex()
    {
        return currentWeaponIndex;
    }

    /// <summary>
    /// Check if inventory has space
    /// </summary>
    public bool HasInventorySpace()
    {
        return carriedWeapons.Count < maxWeapons;
    }

    /// <summary>
    /// Get inventory count
    /// </summary>
    public int GetInventoryCount()
    {
        return carriedWeapons.Count;
    }

    #endregion

    #region Debug Gizmos

    void OnDrawGizmosSelected()
    {
        // Draw weapon holder position
        if (weaponHolder != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(weaponHolder.position, Vector3.one * 0.1f);

            // Draw default weapon position
            Vector3 weaponPos = weaponHolder.position + weaponHolder.TransformDirection(defaultWeaponPosition);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(weaponPos, Vector3.one * 0.05f);
        }

        // Draw drop trajectory
        if (playerCamera != null)
        {
            Gizmos.color = Color.red;
            Vector3 dropStart = playerCamera.transform.position + playerCamera.transform.forward * 2f;
            Vector3 dropDirection = (playerCamera.transform.forward + Vector3.up * 0.3f) * throwForce * 0.1f;
            Gizmos.DrawRay(dropStart, dropDirection);
        }
    }

    #endregion
}