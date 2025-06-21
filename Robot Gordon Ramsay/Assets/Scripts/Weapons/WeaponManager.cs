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

    [Tooltip("Auto-switch to newly picked up weapons")]
    public bool autoSwitchOnPickup = true;

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

    // Prevent unwanted firing when picking up weapons
    private float lastWeaponSwitchTime = 0f;
    private const float weaponSwitchCooldown = 0.2f;

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

            // Number keys for weapon switching - IMPROVED FOR 3+ WEAPONS
            inputActions.Player.WeaponSlot1.performed += ctx => HandleWeaponSlotInput(1);
            inputActions.Player.WeaponSlot2.performed += ctx => HandleWeaponSlotInput(2);
            inputActions.Player.WeaponSlot3.performed += ctx => HandleWeaponSlotInput(3);
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
    /// Handle weapon input - FIXED TO PREVENT UNWANTED FIRING AFTER PICKUP
    /// </summary>
    private void HandleInput()
    {
        // Prevent firing immediately after weapon switch/pickup
        bool canFire = Time.time >= lastWeaponSwitchTime + weaponSwitchCooldown;

        // Handle firing only if cooldown has passed
        if (currentWeapon != null && !isSwitchingWeapons && canFire)
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
        else if (!canFire)
        {
            // Clear input states during cooldown to prevent queuing
            fireInputPressed = false;
            fireInput = false;
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
            if (Input.GetKeyDown(KeyCode.Alpha1)) HandleWeaponSlotInput(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) HandleWeaponSlotInput(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) HandleWeaponSlotInput(3);

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

    /// <summary>
    /// Handle weapon slot input (1, 2, 3) with smart cycling for multiple weapons
    /// </summary>
    private void HandleWeaponSlotInput(int slotNumber)
    {
        int targetIndex = slotNumber - 1; // Convert to 0-based index

        if (targetIndex >= 0 && targetIndex < carriedWeapons.Count)
        {
            // Direct weapon switch if within range
            SwitchToWeapon(targetIndex);
        }
        else if (carriedWeapons.Count > 0)
        {
            // If more weapons than slots, cycle through them
            // This allows accessing weapon 4, 5, 6+ by repeatedly pressing 3
            if (slotNumber == 3 && carriedWeapons.Count > 3)
            {
                // Cycle through weapons beyond slot 3
                int nextIndex = currentWeaponIndex + 1;
                if (nextIndex >= carriedWeapons.Count || nextIndex < 2) // Wrap back to slot 3 (index 2)
                {
                    nextIndex = 2;
                }
                SwitchToWeapon(nextIndex);
                Debug.Log($"Cycling through extra weapons: switched to weapon {nextIndex + 1}");
            }
        }
    }

    #endregion

    #region Weapon Management

    /// <summary>
    /// Try to pickup a weapon - UPDATED FOR MULTIPLE WEAPONS AND AUTO-SWITCH
    /// </summary>
    public bool TryPickupWeapon(PickupableWeapon pickupWeapon)
    {
        if (pickupWeapon == null || pickupWeapon.weaponData == null)
        {
            Debug.Log("Cannot pickup: Invalid weapon or weapon data");
            return false;
        }

        WeaponPickupInfo pickupInfo = pickupWeapon.GetPickupInfo();

        // Check if inventory is full
        if (carriedWeapons.Count >= maxWeapons)
        {
            // Inventory is full - offer weapon swap for current weapon
            if (currentWeapon != null)
            {
                Debug.Log($"Inventory full! Press F to swap {currentWeapon.weaponData.weaponName} for {pickupInfo.weaponData.weaponName}");

                // Store the pickup weapon for potential swapping - DON'T DESTROY IT YET
                StartCoroutine(HandleWeaponSwapPrompt(pickupWeapon));
                return false; // Return false but don't destroy the pickup
            }
            else
            {
                OnInventoryFull?.Invoke();
                Debug.Log("Cannot pickup weapon: inventory full and no current weapon to swap!");
                return false;
            }
        }

        // REMOVED: No longer check for duplicate weapons - allow multiple of same type
        // Normal pickup - inventory has space
        return PerformWeaponPickup(pickupWeapon, pickupInfo);
    }

    /// <summary>
    /// Handle weapon swap prompt when inventory is full - FIXED TO NOT DESTROY PICKUP
    /// </summary>
    private System.Collections.IEnumerator HandleWeaponSwapPrompt(PickupableWeapon pickupWeapon)
    {
        float promptDuration = 3f; // Show prompt for 3 seconds
        float elapsed = 0f;
        bool swapPerformed = false;

        while (elapsed < promptDuration && !swapPerformed)
        {
            // Check if F key is pressed for weapon swap
            if (Input.GetKeyDown(KeyCode.F))
            {
                swapPerformed = PerformWeaponSwap(pickupWeapon);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!swapPerformed)
        {
            Debug.Log("Weapon swap prompt expired - pickup weapon remains on ground");
        }
    }

    /// <summary>
    /// Perform weapon swap - drop current weapon and pickup new one - FIXED LOGIC
    /// </summary>
    private bool PerformWeaponSwap(PickupableWeapon newWeapon)
    {
        if (currentWeapon == null || newWeapon == null)
        {
            Debug.LogError("Cannot perform weapon swap: missing weapon reference");
            return false;
        }

        Debug.Log($"Swapping {currentWeapon.weaponData.weaponName} for {newWeapon.weaponData.weaponName}");

        // Get the position of the new weapon for dropping the old one there
        Vector3 swapPosition = newWeapon.transform.position;
        WeaponPickupInfo newWeaponInfo = newWeapon.GetPickupInfo();

        // Get current weapon info before dropping it
        WeaponData oldWeaponData = currentWeapon.weaponData;
        currentWeapon.GetAmmoCount(out int oldCurrentAmmo, out int oldReserveAmmo);

        // Remove current weapon from inventory but don't destroy it yet
        carriedWeapons.RemoveAt(currentWeaponIndex);
        OnWeaponUnequipped?.Invoke(oldWeaponData);

        GameObject oldWeaponObj = currentWeapon.gameObject;
        currentWeapon = null;
        currentWeaponIndex = -1;

        // NOW destroy the pickup weapon since we're swapping
        newWeapon.PerformPickup();

        // Create the new held weapon
        GameObject heldWeaponObject = CreateHeldWeaponFromData(newWeaponInfo.weaponData);

        if (heldWeaponObject != null)
        {
            WeaponController weaponController = heldWeaponObject.GetComponent<WeaponController>();
            if (weaponController != null)
            {
                weaponController.SetAmmo(newWeaponInfo.currentAmmo, newWeaponInfo.reserveAmmo);
                AddWeaponToInventory(weaponController);
                SwitchToWeapon(carriedWeapons.Count - 1); // Equip the new weapon

                // Now drop the old weapon at the swap position
                CreateDroppedWeaponAtPosition(oldWeaponData, oldCurrentAmmo, oldReserveAmmo, swapPosition);

                // Destroy the old held weapon object
                Destroy(oldWeaponObj);

                Debug.Log($"Weapon swap completed successfully");
                return true;
            }
        }

        Debug.LogError("Failed to create new weapon during swap");

        // If swap failed, try to restore the old weapon
        carriedWeapons.Insert(currentWeaponIndex >= 0 ? currentWeaponIndex : 0, currentWeapon);
        return false;
    }

    /// <summary>
    /// Create dropped weapon at specific position (for swapping)
    /// </summary>
    private void CreateDroppedWeaponAtPosition(WeaponData weaponData, int currentAmmo, int reserveAmmo, Vector3 position)
    {
        if (weaponData.weaponPrefab == null)
            return;

        // Create pickup object from the original prefab at the specified position
        GameObject droppedWeapon = Instantiate(weaponData.weaponPrefab, position, Quaternion.identity);

        // Remove weapon controller (not needed for pickups)
        WeaponController weaponController = droppedWeapon.GetComponent<WeaponController>();
        if (weaponController != null)
        {
            Destroy(weaponController);
        }

        // Setup as pickup weapon
        SetupDroppedWeapon(droppedWeapon, weaponData, currentAmmo, reserveAmmo);

        // Don't apply throwing physics for swapped weapons - just place them
        Rigidbody rb = droppedWeapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.mass = 2f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.8f;

            // Just a small downward force to make it settle
            rb.AddForce(Vector3.down * 2f, ForceMode.Impulse);
        }

        Debug.Log($"Dropped {weaponData.weaponName} at swap position");
    }

    /// <summary>
    /// Perform the actual weapon pickup - UPDATED WITH AUTO-SWITCH
    /// </summary>
    private bool PerformWeaponPickup(PickupableWeapon pickupWeapon, WeaponPickupInfo pickupInfo)
    {
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
        weaponController.SetAmmo(pickupInfo.currentAmmo, pickupInfo.reserveAmmo);

        // Add to inventory
        AddWeaponToInventory(weaponController);

        // Auto-switch behavior
        if (autoSwitchOnPickup)
        {
            // Always switch to newly picked up weapon
            SwitchToWeapon(carriedWeapons.Count - 1);
            Debug.Log($"Auto-switched to newly picked up {pickupInfo.weaponData.weaponName}");
        }
        else if (carriedWeapons.Count == 1)
        {
            // Equip if it's the first weapon (even if auto-switch is disabled)
            SwitchToWeapon(0);
        }

        OnWeaponPickedUp?.Invoke(pickupInfo.weaponData);
        Debug.Log($"Successfully picked up {pickupInfo.weaponData.weaponName} (Total weapons: {carriedWeapons.Count}/{maxWeapons})");
        return true;
    }

    /// <summary>
    /// Add weapon to inventory - FIXED TO PREVENT CORRUPTION
    /// </summary>
    private void AddWeaponToInventory(WeaponController weapon)
    {
        if (weapon == null)
        {
            Debug.LogError("Cannot add null weapon to inventory!");
            return;
        }

        // Validate weapon has required components
        if (weapon.weaponData == null)
        {
            Debug.LogError($"Weapon {weapon.name} has no WeaponData! Cannot add to inventory.");
            Destroy(weapon.gameObject);
            return;
        }

        carriedWeapons.Add(weapon);

        // Parent to weapon holder and hide initially
        weapon.transform.SetParent(weaponHolder);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.gameObject.SetActive(false);

        Debug.Log($"Added {weapon.weaponData.weaponName} to inventory. Total weapons: {carriedWeapons.Count}/{maxWeapons}");

        // Log current inventory for debugging
        LogCurrentInventory();
    }

    /// <summary>
    /// Debug method to log current inventory state
    /// </summary>
    private void LogCurrentInventory()
    {
        Debug.Log("=== CURRENT INVENTORY ===");
        for (int i = 0; i < carriedWeapons.Count; i++)
        {
            if (carriedWeapons[i] != null && carriedWeapons[i].weaponData != null)
            {
                string status = (i == currentWeaponIndex) ? "[EQUIPPED]" : "[STORED]";
                Debug.Log($"Slot {i}: {carriedWeapons[i].weaponData.weaponName} {status}");
            }
            else
            {
                Debug.LogError($"Slot {i}: NULL OR CORRUPTED WEAPON!");
            }
        }
        Debug.Log($"Current weapon index: {currentWeaponIndex}");
        Debug.Log("========================");
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
    /// Switch to weapon at index - FIXED TO HANDLE CORRUPTED INVENTORY
    /// </summary>
    public void SwitchToWeapon(int weaponIndex)
    {
        // Validate input
        if (weaponIndex < 0 || weaponIndex >= carriedWeapons.Count)
        {
            Debug.LogWarning($"Cannot switch to weapon index {weaponIndex}: out of range (0-{carriedWeapons.Count - 1})");
            return;
        }

        // Check if weapon at index is valid
        if (carriedWeapons[weaponIndex] == null)
        {
            Debug.LogError($"Weapon at index {weaponIndex} is null! Cleaning up inventory...");
            CleanupInventory();
            return;
        }

        if (isSwitchingWeapons)
        {
            Debug.Log("Already switching weapons, ignoring request");
            return;
        }

        if (weaponIndex == currentWeaponIndex)
        {
            Debug.Log($"Already equipped weapon at index {weaponIndex}");
            return; // Already equipped
        }

        Debug.Log($"Switching to weapon at index {weaponIndex}: {carriedWeapons[weaponIndex].weaponData.weaponName}");
        StartCoroutine(SwitchWeaponCoroutine(weaponIndex));
    }

    /// <summary>
    /// Clean up corrupted inventory entries
    /// </summary>
    private void CleanupInventory()
    {
        Debug.Log("Cleaning up corrupted inventory...");

        // Remove null entries
        for (int i = carriedWeapons.Count - 1; i >= 0; i--)
        {
            if (carriedWeapons[i] == null || carriedWeapons[i].weaponData == null)
            {
                Debug.LogWarning($"Removing corrupted weapon at index {i}");
                carriedWeapons.RemoveAt(i);

                // Adjust current weapon index if necessary
                if (currentWeaponIndex >= i)
                {
                    currentWeaponIndex--;
                }
            }
        }

        // Validate current weapon index
        if (currentWeaponIndex >= carriedWeapons.Count)
        {
            currentWeaponIndex = carriedWeapons.Count - 1;
        }

        if (currentWeaponIndex < 0 && carriedWeapons.Count > 0)
        {
            currentWeaponIndex = 0;
        }

        // Update current weapon reference
        if (carriedWeapons.Count > 0 && currentWeaponIndex >= 0)
        {
            currentWeapon = carriedWeapons[currentWeaponIndex];
        }
        else
        {
            currentWeapon = null;
            currentWeaponIndex = -1;
        }

        Debug.Log($"Inventory cleanup complete. Remaining weapons: {carriedWeapons.Count}");
        LogCurrentInventory();
    }

    /// <summary>
    /// Switch weapon coroutine - UPDATED TO PREVENT UNWANTED FIRING
    /// </summary>
    private System.Collections.IEnumerator SwitchWeaponCoroutine(int newWeaponIndex)
    {
        isSwitchingWeapons = true;
        lastWeaponSwitchTime = Time.time; // Set cooldown timer

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
    /// Drop current weapon - FIXED TO PREVENT WEAPON DISAPPEARING BUG
    /// </summary>
    public void DropCurrentWeapon()
    {
        if (currentWeapon == null || isSwitchingWeapons)
        {
            Debug.Log("Cannot drop weapon: no current weapon or switching in progress");
            return;
        }

        WeaponData droppedWeaponData = currentWeapon.weaponData;

        // Get current ammo
        currentWeapon.GetAmmoCount(out int currentAmmo, out int reserveAmmo);

        // Create dropped weapon pickup with improved physics
        CreateDroppedWeaponWithPhysics(droppedWeaponData, currentAmmo, reserveAmmo);

        // Remove from inventory - CRITICAL FIX: Update indices correctly
        GameObject weaponToDestroy = currentWeapon.gameObject;
        OnWeaponUnequipped?.Invoke(droppedWeaponData);

        // Remove from list BEFORE destroying to prevent null references
        carriedWeapons.RemoveAt(currentWeaponIndex);

        // Update current weapon references
        currentWeapon = null;

        // Destroy the weapon object
        Destroy(weaponToDestroy);

        // Switch to next available weapon or clear current
        if (carriedWeapons.Count > 0)
        {
            // Adjust index if needed
            if (currentWeaponIndex >= carriedWeapons.Count)
                currentWeaponIndex = carriedWeapons.Count - 1;

            // Switch to the weapon at the adjusted index
            currentWeaponIndex = -1; // Reset to force switch
            SwitchToWeapon(currentWeaponIndex >= 0 ? currentWeaponIndex : 0);
        }
        else
        {
            // No weapons left
            currentWeaponIndex = -1;
        }

        OnWeaponDropped?.Invoke(droppedWeaponData);
        Debug.Log($"Dropped {droppedWeaponData.weaponName}. Remaining weapons: {carriedWeapons.Count}");
    }

    /// <summary>
    /// Create dropped weapon pickup with improved physics
    /// </summary>
    private void CreateDroppedWeaponWithPhysics(WeaponData weaponData, int currentAmmo, int reserveAmmo)
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

        // IMPROVED PHYSICS: Apply realistic throw force with proper physics
        Rigidbody rb = droppedWeapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Ensure physics are enabled
            rb.isKinematic = false;
            rb.useGravity = true;

            // Set appropriate mass
            rb.mass = 2f; // Realistic weapon weight

            // Set drag for more realistic movement
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.8f;

            // Calculate throw direction with improved physics
            Vector3 throwDirection = playerCamera.transform.forward;

            // Add significant upward arc for better throw
            throwDirection += Vector3.up * (throwUpwardForce * 0.2f);

            // Add random spread for realism
            throwDirection += new Vector3(
                Random.Range(-throwSpread, throwSpread) * 0.1f,
                Random.Range(-throwSpread * 0.5f, throwSpread * 0.5f) * 0.1f,
                Random.Range(-throwSpread, throwSpread) * 0.1f
            );

            throwDirection = throwDirection.normalized;

            // Apply forward force (stronger than before)
            rb.AddForce(throwDirection * throwForce * 1.5f, ForceMode.Impulse);

            // Add realistic rotation for tumbling effect
            Vector3 randomTorque = new Vector3(
                Random.Range(-8f, 8f),
                Random.Range(-8f, 8f),
                Random.Range(-8f, 8f)
            );
            rb.AddTorque(randomTorque, ForceMode.Impulse);

            Debug.Log($"Applied throw force: {throwDirection * throwForce * 1.5f} with torque: {randomTorque}");
        }

        Debug.Log($"Dropped weapon {weaponData.weaponName} at {dropPosition} with improved physics");
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

    #region Weapon Bobbing - IMPROVED AIR CHECK

    /// <summary>
    /// Update weapon bobbing to match player movement - FIXED FOR AIRBORNE PLAYERS
    /// </summary>
    private void UpdateWeaponBobbing()
    {
        // DISABLE ALL BOBBING if weapon is aiming OR player is in the air
        if (!enableWeaponBobbing || currentWeapon == null || playerMovement == null ||
            currentWeapon.IsAiming())
        {
            return;
        }

        // CHECK IF PLAYER IS GROUNDED - if not, no bobbing
        bool isGrounded = IsPlayerGrounded();
        if (!isGrounded)
        {
            // Return to base position when in air
            currentWeapon.transform.localPosition = Vector3.Lerp(
                currentWeapon.transform.localPosition,
                baseWeaponPosition,
                Time.deltaTime * 8f
            );
            return;
        }

        // Get movement input from player
        Vector2 moveInput = playerMovement.moveInput;
        bool isMoving = moveInput.magnitude > 0.1f;

        if (isMoving && isGrounded)
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

    /// <summary>
    /// Check if player is grounded using the Movement component
    /// </summary>
    private bool IsPlayerGrounded()
    {
        if (playerMovement == null)
            return true; // Fallback to assume grounded if no movement component

        // Access the grounded state from the Movement component
        // We need to use reflection since the grounded field is private
        var movementType = playerMovement.GetType();
        var groundedField = movementType.GetField("grounded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (groundedField != null)
        {
            return (bool)groundedField.GetValue(playerMovement);
        }

        // Alternative method: Check if player is close to ground using physics
        return Physics.CheckSphere(
            playerMovement.transform.position + Vector3.down * 0.1f,
            0.3f,
            LayerMask.GetMask("Ground", "Default")
        );
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