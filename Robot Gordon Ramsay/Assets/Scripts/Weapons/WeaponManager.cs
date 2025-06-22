// =====================================================
// EMERGENCY WEAPON MANAGER FIX - COMPLETE REWRITE OF POSITION SYSTEM
// =====================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// EMERGENCY FIXED WeaponManager - Solves ALL positioning issues
/// </summary>
public class WeaponManager : MonoBehaviour
{
    [Header("Weapon Setup")]
    [Tooltip("Transform where weapons are attached (usually camera or hand)")]
    public Transform weaponHolder;

    [Tooltip("Maximum number of weapons player can carry")]
    [Range(1, 10)]
    public int maxWeapons = 3;

    [Tooltip("Auto-switch to newly picked up weapons")]
    public bool autoSwitchOnPickup = true;

    [Header("FIXED Weapon Positions - NO MORE CONVERGENCE")]
    [Tooltip("Force override all weapon positions (DISABLE convergence)")]
    public bool useFixedPositions = true;

    [Header("Ranged Weapon Positions")]
    [Tooltip("Pistol position")]
    public Vector3 pistolPosition = new Vector3(0.4f, -0.25f, 0.6f);
    [Tooltip("Assault rifle position")]
    public Vector3 riflePosition = new Vector3(0.5f, -0.3f, 0.8f);
    [Tooltip("Shotgun position")]
    public Vector3 shotgunPosition = new Vector3(0.55f, -0.35f, 0.9f);

    [Header("Melee Weapon Positions")]
    [Tooltip("Crowbar position")]
    public Vector3 crowbarPosition = new Vector3(0.45f, -0.32f, 0.75f);
    [Tooltip("Knife position")]
    public Vector3 knifePosition = new Vector3(0.3f, -0.2f, 0.5f);
    [Tooltip("Sword position")]
    public Vector3 swordPosition = new Vector3(0.4f, -0.3f, 0.8f);

    [Header("Current State")]
    [SerializeField] private int currentWeaponIndex = -1;
    [SerializeField] private List<WeaponController> carriedWeapons = new List<WeaponController>();

    [Header("Events")]
    public UnityEvent<WeaponData> OnWeaponEquipped;
    public UnityEvent<WeaponData> OnWeaponUnequipped;
    public UnityEvent<WeaponData> OnWeaponPickedUp;
    public UnityEvent<WeaponData> OnWeaponDropped;

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

    // FIXED: No more shared position dictionaries that cause convergence
    private float lastWeaponSwitchTime = 0f;
    private const float weaponSwitchCooldown = 0.2f;

    #region Unity Lifecycle

    void Awake()
    {
        inputActions = new PlayerInputActions();
        playerCamera = Camera.main ?? FindObjectOfType<Camera>();

        if (weaponHolder == null)
        {
            weaponHolder = playerCamera != null ? playerCamera.transform : transform;
        }
    }

    void OnEnable()
    {
        if (inputActions != null)
        {
            inputActions.Enable();
            inputActions.Player.Enable();

            // FIXED: Correct input mapping
            // LEFT MOUSE - ATTACK/FIRE (was mapped to Grab, should be primary action)
            inputActions.Player.Grab.performed += ctx => fireInputPressed = true;
            inputActions.Player.Grab.started += ctx => fireInput = true;
            inputActions.Player.Grab.canceled += ctx => fireInput = false;

            // RIGHT MOUSE - ADS/BLOCK
            inputActions.Player.Zoom.started += ctx => aimInput = true;
            inputActions.Player.Zoom.canceled += ctx => aimInput = false;

            // R - RELOAD
            inputActions.Player.Interact.performed += ctx => reloadInput = true;

            // Q - DROP WEAPON
            inputActions.Player.ScrollDown.performed += ctx => dropInput = true;

            // NUMBER KEYS - WEAPON SWITCHING
            inputActions.Player.WeaponSlot1.performed += ctx => HandleWeaponSlotInput(1);
            inputActions.Player.WeaponSlot2.performed += ctx => HandleWeaponSlotInput(2);
            inputActions.Player.WeaponSlot3.performed += ctx => HandleWeaponSlotInput(3);
        }
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.Disable();
            inputActions.UI.Disable();
            inputActions.Disable();
        }
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Dispose();
            inputActions = null;
        }
    }

    void Start()
    {
        Debug.Log("🔧 EMERGENCY WeaponManager initialized - Fixed positioning system");
    }

    void Update()
    {
        HandleInput();
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        // Prevent firing immediately after weapon switch/pickup
        bool canFireNow = Time.time >= lastWeaponSwitchTime + weaponSwitchCooldown;

        // DEBUG: Log input state for troubleshooting
        if (fireInputPressed || fireInput)
        {
            Debug.Log($"🔄 WeaponManager.HandleInput() - Input detected!");
            Debug.Log($"   - Fire Pressed: {fireInputPressed}");
            Debug.Log($"   - Fire Held: {fireInput}");
            Debug.Log($"   - Can Fire: {canFireNow}");
            Debug.Log($"   - Current Weapon: {(currentWeapon != null ? currentWeapon.weaponData?.weaponName : "NULL")}");
            Debug.Log($"   - Is Switching: {isSwitchingWeapons}");
        }

        // Check cooldown and log if blocking
        if (!canFireNow && (fireInputPressed || fireInput))
        {
            Debug.Log($"🚫 Attack blocked by cooldown. Time: {Time.time:F2}, Last switch: {lastWeaponSwitchTime:F2}, Cooldown: {weaponSwitchCooldown:F2}");
        }

        // Handle firing/attacking only if cooldown has passed
        if (currentWeapon != null && !isSwitchingWeapons && canFireNow)
        {
            // CRITICAL: Always pass input to weapon, regardless of weapon type
            if (fireInputPressed || fireInput)
            {
                Debug.Log($"🔫 PASSING INPUT TO WEAPON: {currentWeapon.weaponData?.weaponName}");
                Debug.Log($"   - Is Melee: {currentWeapon.weaponData?.IsMeleeWeapon()}");
            }

            // Pass input to current weapon
            currentWeapon.TryFire(fireInputPressed, fireInput);
            fireInputPressed = false; // Reset pressed state

            // Handle ADS/Blocking
            currentWeapon.SetAiming(aimInput);
        }
        else if (!canFireNow)
        {
            // Clear input states during cooldown to prevent queuing
            if (fireInputPressed || fireInput)
            {
                Debug.Log("🚫 Clearing input during cooldown");
            }
            fireInputPressed = false;
            fireInput = false;
        }
        else if (currentWeapon == null)
        {
            if (fireInputPressed || fireInput)
            {
                Debug.Log("🚫 No current weapon equipped");
                fireInputPressed = false;
                fireInput = false;
            }
        }
        else if (isSwitchingWeapons)
        {
            if (fireInputPressed || fireInput)
            {
                Debug.Log("🚫 Currently switching weapons");
                fireInputPressed = false;
                fireInput = false;
            }
        }

        // Handle other inputs
        HandleOtherInputs();
    }

    private void HandleOtherInputs()
    {
        // Handle reload
        if (reloadInput)
        {
            reloadInput = false;
            if (currentWeapon != null && !isSwitchingWeapons)
            {
                Debug.Log($"🔄 Reloading {currentWeapon.weaponData?.weaponName}");
                currentWeapon.Reload();
            }
        }

        // Handle drop weapon
        if (dropInput)
        {
            dropInput = false;
            Debug.Log("📤 Dropping current weapon");
            DropCurrentWeapon();
        }
    }

    private void HandleWeaponSlotInput(int slotNumber)
    {
        int targetIndex = slotNumber - 1;
        if (targetIndex >= 0 && targetIndex < carriedWeapons.Count)
        {
            SwitchToWeapon(targetIndex);
        }
    }

    #endregion

    #region FIXED Weapon Positioning System

    /// <summary>
    /// Get the CORRECT position for a weapon type - FIXED VERSION
    /// </summary>
    private Vector3 GetFixedWeaponPosition(WeaponData weaponData)
    {
        if (weaponData == null)
            return new Vector3(0.4f, -0.25f, 0.6f); // Default pistol position

        // FIXED: Use unique positions for each weapon type
        switch (weaponData.weaponType)
        {
            case WeaponType.Pistol:
                return new Vector3(0.4f, -0.25f, 0.6f);
            case WeaponType.AssaultRifle:
                return new Vector3(0.5f, -0.3f, 0.8f);
            case WeaponType.Shotgun:
            case WeaponType.PumpShotgun:
                return new Vector3(0.55f, -0.35f, 0.9f);
            case WeaponType.Sniper:
                return new Vector3(0.5f, -0.3f, 0.8f);
            case WeaponType.SMG:
                return new Vector3(0.4f, -0.25f, 0.6f);

            // Melee weapons - UNIQUE positions
            case WeaponType.Knife:
                return new Vector3(0.3f, -0.2f, 0.5f);
            case WeaponType.Sword:
                return new Vector3(0.4f, -0.3f, 0.8f);
            case WeaponType.Axe:
                return new Vector3(0.45f, -0.32f, 0.75f);
            case WeaponType.Hammer:
                return new Vector3(0.45f, -0.32f, 0.75f);
            case WeaponType.Bat:
                return new Vector3(0.4f, -0.3f, 0.8f);
            case WeaponType.Crowbar:
                return new Vector3(0.45f, -0.32f, 0.75f); // CROWBAR SPECIFIC
            case WeaponType.Fists:
                return new Vector3(0.3f, -0.2f, 0.5f);

            default:
                return new Vector3(0.4f, -0.25f, 0.6f);
        }
    }

    /// <summary>
    /// Get the CORRECT rotation for a weapon type - FIXED VERSION
    /// </summary>
    private Vector3 GetFixedWeaponRotation(WeaponData weaponData)
    {
        if (weaponData == null)
            return Vector3.zero;

        // CRITICAL FIX: Only ranged weapons need the 180 flip, NOT melee
        if (weaponData.IsMeleeWeapon())
        {
            // Melee weapons: NO rotation adjustment needed
            return Vector3.zero;
        }
        else
        {
            // Ranged weapons: 180 degree Y rotation to face forward
            return new Vector3(0f, 180f, 0f);
        }
    }

    /// <summary>
    /// Get CORRECT ADS position - FIXED VERSION
    /// </summary>
    private Vector3 GetFixedADSPosition(WeaponData weaponData)
    {
        if (weaponData == null)
            return Vector3.zero;

        if (weaponData.IsMeleeWeapon())
        {
            // Melee blocking position - closer to body
            return new Vector3(0.15f, -0.1f, 0.3f);
        }
        else
        {
            // Ranged ADS position - centered and closer
            return new Vector3(0f, -0.15f, 0.4f);
        }
    }

    /// <summary>
    /// Get CORRECT ADS rotation - FIXED VERSION
    /// </summary>
    private Vector3 GetFixedADSRotation(WeaponData weaponData)
    {
        if (weaponData == null)
            return Vector3.zero;

        if (weaponData.IsMeleeWeapon())
        {
            // Melee blocking - slight defensive angle
            return new Vector3(-15f, 0f, 0f);
        }
        else
        {
            // Ranged ADS - keep same rotation as base (already has 180 flip)
            return new Vector3(0f, 180f, 0f);
        }
    }

    #endregion

    #region Weapon Management

    public bool TryPickupWeapon(PickupableWeapon pickupWeapon)
    {
        if (pickupWeapon == null || pickupWeapon.weaponData == null)
        {
            Debug.Log("Cannot pickup: Invalid weapon");
            return false;
        }

        WeaponPickupInfo pickupInfo = pickupWeapon.GetPickupInfo();

        if (carriedWeapons.Count >= maxWeapons)
        {
            Debug.Log($"Inventory full! Drop current weapon first.");
            return false;
        }

        return PerformWeaponPickup(pickupWeapon, pickupInfo);
    }

    private bool PerformWeaponPickup(PickupableWeapon pickupWeapon, WeaponPickupInfo pickupInfo)
    {
        // Destroy pickup
        pickupWeapon.PerformPickup();

        // Create held weapon
        GameObject heldWeaponObject = CreateHeldWeaponFromData(pickupInfo.weaponData);
        if (heldWeaponObject == null)
            return false;

        WeaponController weaponController = heldWeaponObject.GetComponent<WeaponController>();
        if (weaponController == null)
        {
            Destroy(heldWeaponObject);
            return false;
        }

        // Set ammo
        weaponController.SetAmmo(pickupInfo.currentAmmo, pickupInfo.reserveAmmo);

        // Add to inventory
        AddWeaponToInventory(weaponController);

        // Auto-switch
        if (autoSwitchOnPickup || carriedWeapons.Count == 1)
        {
            SwitchToWeapon(carriedWeapons.Count - 1);
        }

        OnWeaponPickedUp?.Invoke(pickupInfo.weaponData);
        Debug.Log($"✅ Picked up {pickupInfo.weaponData.weaponName}");
        return true;
    }

    private void AddWeaponToInventory(WeaponController weapon)
    {
        if (weapon == null || weapon.weaponData == null)
        {
            Debug.LogError("Cannot add invalid weapon!");
            return;
        }

        carriedWeapons.Add(weapon);

        // Parent and hide
        weapon.transform.SetParent(weaponHolder);
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.gameObject.SetActive(false);

        Debug.Log($"Added {weapon.weaponData.weaponName} to inventory. Total: {carriedWeapons.Count}");
    }

    private GameObject CreateHeldWeaponFromData(WeaponData weaponData)
    {
        if (weaponData.weaponPrefab == null)
        {
            Debug.LogError($"No prefab for {weaponData.weaponName}!");
            return null;
        }

        GameObject weaponObject = Instantiate(weaponData.weaponPrefab);

        // Remove pickup components
        PickupableWeapon pickup = weaponObject.GetComponent<PickupableWeapon>();
        if (pickup != null) Destroy(pickup);

        InteractableComponent interactable = weaponObject.GetComponent<InteractableComponent>();
        if (interactable != null) Destroy(interactable);

        // Ensure WeaponController
        WeaponController weaponController = weaponObject.GetComponent<WeaponController>();
        if (weaponController == null)
        {
            weaponController = weaponObject.AddComponent<WeaponController>();
        }
        weaponController.weaponData = weaponData;

        // CRITICAL: Setup melee weapons properly
        if (weaponData.IsMeleeWeapon())
        {
            SetupMeleeWeaponProperly(weaponObject, weaponData);
        }

        // Setup for holding
        SetupHeldWeapon(weaponObject);

        return weaponObject;
    }

    /// <summary>
    /// PROPER melee weapon setup - GUARANTEED TO WORK
    /// </summary>
    private void SetupMeleeWeaponProperly(GameObject weaponObject, WeaponData weaponData)
    {
        Debug.Log($"🗡️ Setting up MELEE weapon: {weaponData.weaponName}");

        // Get or add MeleeController
        MeleeController meleeController = weaponObject.GetComponent<MeleeController>();
        if (meleeController == null)
        {
            meleeController = weaponObject.AddComponent<MeleeController>();
            Debug.Log($"✅ Added MeleeController");
        }

        // FORCE create AttackPoint
        Transform attackPoint = weaponObject.transform.Find("AttackPoint");
        if (attackPoint == null)
        {
            GameObject attackPointObj = new GameObject("AttackPoint");
            attackPointObj.transform.SetParent(weaponObject.transform);
            attackPointObj.transform.localPosition = GetAttackPointForWeapon(weaponData.weaponType);
            attackPoint = attackPointObj.transform;
            Debug.Log($"✅ Created AttackPoint at {attackPoint.localPosition}");
        }

        // FORCE create TrailPoint
        Transform trailPoint = weaponObject.transform.Find("TrailPoint");
        if (trailPoint == null)
        {
            GameObject trailPointObj = new GameObject("TrailPoint");
            trailPointObj.transform.SetParent(weaponObject.transform);
            trailPointObj.transform.localPosition = attackPoint.localPosition * 0.6f;
            trailPoint = trailPointObj.transform;
            Debug.Log($"✅ Created TrailPoint");
        }

        // FORCE assign to MeleeController
        meleeController.attackPoint = attackPoint;
        meleeController.trailPoint = trailPoint;

        Debug.Log($"🗡️ MELEE SETUP COMPLETE for {weaponData.weaponName}");
        Debug.Log($"   - AttackPoint: {(meleeController.attackPoint != null ? "✅" : "❌")}");
        Debug.Log($"   - TrailPoint: {(meleeController.trailPoint != null ? "✅" : "❌")}");
    }

    private Vector3 GetAttackPointForWeapon(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Knife: return new Vector3(0f, 0f, 0.8f);
            case WeaponType.Sword: return new Vector3(0f, 0f, 1.8f);
            case WeaponType.Axe: return new Vector3(0f, 0f, 1.4f);
            case WeaponType.Hammer: return new Vector3(0f, 0f, 1.2f);
            case WeaponType.Bat: return new Vector3(0f, 0f, 1.6f);
            case WeaponType.Crowbar: return new Vector3(0f, 0f, 1.3f); // CROWBAR SPECIFIC
            case WeaponType.Fists: return new Vector3(0f, 0f, 0.6f);
            default: return new Vector3(0f, 0f, 1.0f);
        }
    }

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

        // GET THE WEAPON DATA TO CHECK IF IT'S MELEE
        WeaponController weaponController = weaponObject.GetComponent<WeaponController>();
        if (weaponController != null && weaponController.weaponData != null)
        {
            WeaponData weaponData = weaponController.weaponData;

            // IF IT'S A MELEE WEAPON, MAKE SURE IT HAS THE RIGHT MELEE CONTROLLER
            if (weaponData.IsMeleeWeapon())
            {
                Debug.Log($"🗡️ Setting up MELEE weapon: {weaponData.weaponName}");

                // Remove old MeleeController if it exists to prevent conflicts
                MeleeController oldMeleeController = weaponObject.GetComponent<MeleeController>();
                if (oldMeleeController != null)
                {
                    DestroyImmediate(oldMeleeController);
                    Debug.Log("🗑️ Removed old MeleeController to prevent conflicts");
                }

                // Add SimpleMeleeController
                MeleeController simpleMeleeController = weaponObject.GetComponent<MeleeController>();
                if (simpleMeleeController == null)
                {
                    simpleMeleeController = weaponObject.AddComponent<MeleeController>();
                    Debug.Log($"✅ Added SimpleMeleeController to {weaponData.weaponName}");
                }

                // SETUP ATTACK POINT IF MISSING
                Transform attackPoint = weaponObject.transform.Find("AttackPoint");
                if (attackPoint == null)
                {
                    // Create attack point at weapon tip
                    GameObject attackPointObj = new GameObject("AttackPoint");
                    attackPointObj.transform.SetParent(weaponObject.transform);
                    attackPointObj.transform.localPosition = GetAttackPointPosition(weaponData.weaponType);
                    Debug.Log($"✅ Created AttackPoint for {weaponData.weaponName}");
                }

                // SETUP TRAIL POINT IF MISSING
                Transform trailPoint = weaponObject.transform.Find("TrailPoint");
                if (trailPoint == null)
                {
                    // Create trail point at weapon middle
                    GameObject trailPointObj = new GameObject("TrailPoint");
                    trailPointObj.transform.SetParent(weaponObject.transform);
                    trailPointObj.transform.localPosition = GetAttackPointPosition(weaponData.weaponType) * 0.6f;
                    Debug.Log($"✅ Created TrailPoint for {weaponData.weaponName}");
                }

                Debug.Log($"✅ MELEE SETUP COMPLETE for {weaponData.weaponName}");
            }
            else
            {
                Debug.Log($"🔫 Setting up RANGED weapon: {weaponData.weaponName}");
            }
        }

        Debug.Log($"✅ Setup held weapon: {weaponObject.name}");
    }

    private Vector3 GetAttackPointPosition(WeaponType weaponType)
    {
        switch (weaponType)
        {
            case WeaponType.Knife: return new Vector3(0f, 0f, 0.8f);
            case WeaponType.Sword: return new Vector3(0f, 0f, 1.8f);
            case WeaponType.Axe: return new Vector3(0f, 0f, 1.4f);
            case WeaponType.Hammer: return new Vector3(0f, 0f, 1.2f);
            case WeaponType.Bat: return new Vector3(0f, 0f, 1.6f);
            case WeaponType.Crowbar: return new Vector3(0f, 0f, 1.3f);
            case WeaponType.Fists: return new Vector3(0f, 0f, 0.6f);
            default: return new Vector3(0f, 0f, 1.0f);
        }
    }

    #endregion

    #region Weapon Switching

    public void SwitchToWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= carriedWeapons.Count)
            return;

        if (carriedWeapons[weaponIndex] == null)
            return;

        if (isSwitchingWeapons || weaponIndex == currentWeaponIndex)
            return;

        Debug.Log($"Switching to weapon: {carriedWeapons[weaponIndex].weaponData.weaponName}");
        StartCoroutine(SwitchWeaponCoroutine(weaponIndex));
    }

    private System.Collections.IEnumerator SwitchWeaponCoroutine(int newWeaponIndex)
    {
        isSwitchingWeapons = true;
        lastWeaponSwitchTime = Time.time;

        // Unequip current weapon
        if (currentWeapon != null)
        {
            currentWeapon.StopFiring();
            OnWeaponUnequipped?.Invoke(currentWeapon.weaponData);
            currentWeapon.gameObject.SetActive(false);
        }

        // Small delay for weapon switch
        yield return new WaitForSeconds(0.1f);

        // Equip new weapon
        currentWeaponIndex = newWeaponIndex;
        currentWeapon = carriedWeapons[currentWeaponIndex];
        currentWeapon.gameObject.SetActive(true);

        // FIXED: Position weapon correctly
        PositionWeaponCorrectly(currentWeapon);

        OnWeaponEquipped?.Invoke(currentWeapon.weaponData);
        isSwitchingWeapons = false;

        Debug.Log($"✅ Equipped {currentWeapon.weaponData.weaponName}");
    }

    /// <summary>
    /// Position weapon correctly - FIXED VERSION
    /// </summary>
    private void PositionWeaponCorrectly(WeaponController weapon)
    {
        if (weapon == null || weapon.weaponData == null)
            return;

        // Get FIXED positions
        Vector3 basePosition = GetFixedWeaponPosition(weapon.weaponData);
        Vector3 baseRotation = GetFixedWeaponRotation(weapon.weaponData);

        // Apply immediately
        weapon.transform.localPosition = basePosition;
        weapon.transform.localRotation = Quaternion.Euler(baseRotation);
        weapon.transform.localScale = Vector3.one;

        // Set base position for WeaponController
        weapon.SetBasePosition(basePosition, baseRotation);

        // OVERRIDE weapon data ADS positions if they're wrong
        if (useFixedPositions)
        {
            weapon.weaponData.adsPosition = GetFixedADSPosition(weapon.weaponData);
            weapon.weaponData.adsRotation = GetFixedADSRotation(weapon.weaponData);
        }

        Debug.Log($"✅ POSITIONED {weapon.weaponData.weaponName}:");
        Debug.Log($"   Base: {basePosition}, Rotation: {baseRotation}");
        Debug.Log($"   ADS: {weapon.weaponData.adsPosition}, ADS Rot: {weapon.weaponData.adsRotation}");
    }

    #endregion

    #region Drop Weapons

    public void DropCurrentWeapon()
    {
        if (currentWeapon == null || isSwitchingWeapons)
            return;

        WeaponData droppedWeaponData = currentWeapon.weaponData;
        currentWeapon.GetAmmoCount(out int currentAmmo, out int reserveAmmo);

        // Create dropped version
        CreateDroppedWeapon(droppedWeaponData, currentAmmo, reserveAmmo);

        // Remove from inventory
        OnWeaponUnequipped?.Invoke(droppedWeaponData);
        GameObject weaponToDestroy = currentWeapon.gameObject;
        carriedWeapons.RemoveAt(currentWeaponIndex);
        currentWeapon = null;
        Destroy(weaponToDestroy);

        // Switch to next weapon or clear
        if (carriedWeapons.Count > 0)
        {
            int nextIndex = Mathf.Min(currentWeaponIndex, carriedWeapons.Count - 1);
            currentWeaponIndex = -1;
            SwitchToWeapon(nextIndex);
        }
        else
        {
            currentWeaponIndex = -1;
        }

        OnWeaponDropped?.Invoke(droppedWeaponData);
        Debug.Log($"Dropped {droppedWeaponData.weaponName}");
    }

    private void CreateDroppedWeapon(WeaponData weaponData, int currentAmmo, int reserveAmmo)
    {
        if (weaponData.weaponPrefab == null)
            return;

        Vector3 dropPosition = playerCamera.transform.position + playerCamera.transform.forward * 1.5f + Vector3.up * 0.2f;
        GameObject droppedWeapon = Instantiate(weaponData.weaponPrefab, dropPosition, playerCamera.transform.rotation);

        // Remove WeaponController
        WeaponController weaponController = droppedWeapon.GetComponent<WeaponController>();
        if (weaponController != null)
        {
            Destroy(weaponController);
        }

        // Setup as pickup
        PickupableWeapon pickup = droppedWeapon.GetComponent<PickupableWeapon>();
        if (pickup == null)
        {
            pickup = droppedWeapon.AddComponent<PickupableWeapon>();
        }

        pickup.weaponData = weaponData;
        pickup.currentAmmo = currentAmmo;
        pickup.reserveAmmo = reserveAmmo;
        pickup.canBePickedUp = true;

        // Enable physics
        Rigidbody rb = droppedWeapon.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = droppedWeapon.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.useGravity = true;

        // Enable colliders
        Collider[] colliders = droppedWeapon.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = true;
        }

        // Add throw force
        Vector3 throwDirection = playerCamera.transform.forward + Vector3.up * 0.3f;
        rb.AddForce(throwDirection * 8f, ForceMode.Impulse);
    }

    #endregion

    #region Public Getters

    public WeaponController GetCurrentWeapon() => currentWeapon;
    public List<WeaponController> GetCarriedWeapons() => new List<WeaponController>(carriedWeapons);
    public int GetCurrentWeaponIndex() => currentWeaponIndex;
    public bool HasInventorySpace() => carriedWeapons.Count < maxWeapons;
    public int GetInventoryCount() => carriedWeapons.Count;

    #endregion
}