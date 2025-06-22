using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Main weapon controller that handles firing, reloading, and weapon behavior - ENHANCED WITH MELEE
/// Attach this to weapon prefabs and assign WeaponData
/// FIXED: Better melee setup and input handling + ADS infinite loop fix
/// </summary>
public class WeaponController : MonoBehaviour
{
    #region Public Variables

    [Header("Weapon Configuration")]
    [Tooltip("Weapon data asset that defines this weapon's properties")]
    public WeaponData weaponData;

    [Header("Weapon Points")]
    [Tooltip("Point where bullets are fired from")]
    public Transform firePoint;

    [Tooltip("Point where muzzle flash appears")]
    public Transform muzzlePoint;

    [Header("Targeting")]
    [Tooltip("Layer mask for what can be hit by bullets")]
    public LayerMask hitLayers = -1;

    [Header("Debug")]
    [Tooltip("Show debug information in scene view")]
    public bool showDebugInfo = false;
    #endregion

    #region Public Methods for External Scripts

    /// <summary>
    /// Add ammo to the weapon
    /// </summary>
    public void AddAmmo(int amount)
    {
        if (weaponData == null || weaponData.IsMeleeWeapon())
            return;

        reserveAmmo = Mathf.Min(reserveAmmo + amount, weaponData.maxAmmo);
        OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);

        Debug.Log($"Added {amount} ammo. Reserve ammo: {reserveAmmo}");
    }

    /// <summary>
    /// Stop firing (for external control)
    /// </summary>
    public void StopFiring()
    {
        isFiring = false;

        // Stop burst fire if active
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;
        }

        Debug.Log("Firing stopped externally");
    }

    /// <summary>
    /// Get current ammo count
    /// </summary>
    public int GetAmmoCount()
    {
        if (weaponData != null && weaponData.IsMeleeWeapon())
            return -1; // Melee weapons don't use ammo

        return currentAmmo;
    }

    /// <summary>
    /// Get ammo count with out parameters (for WeaponManager compatibility)
    /// </summary>
    public int GetAmmoCount(out int current, out int reserve)
    {
        if (weaponData != null && weaponData.IsMeleeWeapon())
        {
            current = -1;
            reserve = -1;
            return -1;
        }

        current = currentAmmo;
        reserve = reserveAmmo;
        return currentAmmo;
    }

    /// <summary>
    /// Get reserve ammo count
    /// </summary>
    public int GetReserveAmmo()
    {
        if (weaponData != null && weaponData.IsMeleeWeapon())
            return -1;

        return reserveAmmo;
    }

    /// <summary>
    /// Get total ammo (current + reserve)
    /// </summary>
    public int GetTotalAmmo()
    {
        if (weaponData != null && weaponData.IsMeleeWeapon())
            return -1;

        return currentAmmo + reserveAmmo;
    }

    /// <summary>
    /// Check if weapon can reload
    /// </summary>
    public bool CanReload()
    {
        if (weaponData == null || weaponData.IsMeleeWeapon())
            return false;

        return currentAmmo < weaponData.magazineSize && reserveAmmo > 0;
    }

    /// <summary>
    /// Check if weapon is currently reloading
    /// </summary>
    public bool IsReloading()
    {
        return isFiring; // We use isFiring to block actions during reload
    }

    /// <summary>
    /// Force set ammo (for debugging or special cases)
    /// </summary>
    public void SetAmmo(int current, int reserve)
    {
        if (weaponData == null || weaponData.IsMeleeWeapon())
            return;

        currentAmmo = Mathf.Clamp(current, 0, weaponData.magazineSize);
        reserveAmmo = Mathf.Clamp(reserve, 0, weaponData.maxAmmo);
        OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);

        Debug.Log($"Ammo set to: {currentAmmo}/{reserveAmmo}");
    }

    #endregion

    #region Events

    [System.Serializable]
    public class WeaponEvents
    {
        public UnityEvent OnWeaponFired;
        public UnityEvent OnWeaponReloaded;
        public UnityEvent OnWeaponEmpty;
        public UnityEvent OnMeleeAttack;
        public UnityEvent OnMeleeHit;
    }

    [Header("Events")]
    public WeaponEvents weaponEvents;

    // Static events for UI updates
    public static System.Action<int, int> OnAmmoChanged;
    public static System.Action OnWeaponFired;

    #endregion

    #region Private Variables

    // Core components
    private Camera playerCamera;
    private CrosshairCenter crosshairCenter;
    private CameraShake cameraShake;
    private MeleeController meleeController;

    // Ammo system
    private int currentAmmo;
    private int reserveAmmo;

    // Firing system
    private bool isFiring = false;
    private float lastFireTime;
    private int burstShotsFired = 0;
    private Coroutine burstCoroutine;

    // ADS system - FIXED
    private bool isAiming = false;
    private bool isTransitioningADS = false; // ADDED: Track transition state
    private float originalFOV;
    private Vector3 basePosition;
    private Vector3 baseRotation;
    private Vector3 targetPosition; // ADDED: Current target position
    private Vector3 targetRotation; // ADDED: Current target rotation

    // Recoil system
    private Vector3 currentRecoilOffset = Vector3.zero;
    private Coroutine recoilCoroutine;

    // Effects
    private GameObject currentMuzzleFlash;

    private MeleeController simpleMeleeController;

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        // Find player camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        // Store original FOV immediately
        if (playerCamera != null)
        {
            originalFOV = playerCamera.fieldOfView;
        }

        // Find crosshair center for aiming
        crosshairCenter = FindObjectOfType<CrosshairCenter>();

        // Find camera shake component
        cameraShake = FindObjectOfType<CameraShake>();
        if (cameraShake == null && playerCamera != null)
        {
            cameraShake = playerCamera.gameObject.GetComponent<CameraShake>();
            if (cameraShake == null)
            {
                cameraShake = playerCamera.gameObject.AddComponent<CameraShake>();
            }
        }

        // Setup fire point if not assigned
        if (firePoint == null)
        {
            firePoint = transform;
        }

        if (muzzlePoint == null)
        {
            muzzlePoint = firePoint;
        }

        // CRITICAL FIX: Setup melee controller EARLY in Awake if this is a melee weapon
        if (weaponData != null && weaponData.IsMeleeWeapon())
        {
            SetupSimpleMeleeController();
        }
    }


    void Start()
    {
        // Initialize ammo (only for ranged weapons)
        if (weaponData != null)
        {
            if (!weaponData.IsMeleeWeapon())
            {
                currentAmmo = weaponData.magazineSize;
                reserveAmmo = weaponData.maxAmmo;
                OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
            }
            else
            {
                // Double-check melee controller setup
                EnsureSimpleMeleeControllerSetup();
            }

            Debug.Log($"Weapon initialized: {weaponData.weaponName} ({(weaponData.IsMeleeWeapon() ? "Melee" : "Ranged")})");
        }
        else
        {
            Debug.LogError($"WeaponController on {gameObject.name} has no WeaponData assigned!", this);
        }
    }

    void Update()
    {
        // FIXED: Handle weapon position with proper transition tracking
        HandleWeaponPositionFixed();
    }

    void OnDestroy()
    {
        // Restore original FOV when weapon is destroyed
        if (playerCamera != null && originalFOV > 0f)
        {
            playerCamera.fieldOfView = originalFOV;
        }
    }

    #endregion

    #region Melee Controller Setup - FIXED

    /// <summary>
    /// Setup melee controller with proper attack points
    /// </summary>
    private void SetupMeleeController()
    {
        if (weaponData == null) return;

        Debug.Log($"🔧 Setting up MeleeController for {weaponData.weaponName}");

        // Get the MeleeController component
        MeleeController meleeController = GetComponent<MeleeController>();
        if (meleeController == null)
        {
            Debug.LogError("No MeleeController found! This method should only be called after adding MeleeController.");
            return;
        }

        // Create AttackPoint if missing
        Transform attackPoint = transform.Find("AttackPoint");
        if (attackPoint == null)
        {
            GameObject attackPointObj = new GameObject("AttackPoint");
            attackPointObj.transform.SetParent(transform);

            // Position based on weapon type
            Vector3 attackPos = GetAttackPointPosition(weaponData.weaponType);
            attackPointObj.transform.localPosition = attackPos;
            attackPoint = attackPointObj.transform;

            Debug.Log($"✅ Created AttackPoint at {attackPos}");
        }

        // Create TrailPoint if missing
        Transform trailPoint = transform.Find("TrailPoint");
        if (trailPoint == null)
        {
            GameObject trailPointObj = new GameObject("TrailPoint");
            trailPointObj.transform.SetParent(transform);
            trailPointObj.transform.localPosition = attackPoint.localPosition * 0.7f;
            trailPoint = trailPointObj.transform;

            Debug.Log($"✅ Created TrailPoint");
        }

        // Assign to MeleeController
        meleeController.attackPoint = attackPoint;
        meleeController.trailPoint = trailPoint;

        Debug.Log($"✅ MeleeController setup complete for {weaponData.weaponName}");
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
            case WeaponType.Crowbar: return new Vector3(0f, 0f, 1.3f); // CROWBAR SPECIFIC
            case WeaponType.Fists: return new Vector3(0f, 0f, 0.6f);
            default: return new Vector3(0f, 0f, 1.0f);
        }
    }

    private void ForceCreateAttackPoints()
    {
        // Setup attack point with GUARANTEED creation
        Transform attackPoint = transform.Find("AttackPoint");
        if (attackPoint == null)
        {
            Debug.Log("Creating AttackPoint...");
            GameObject attackPointObj = new GameObject("AttackPoint");
            attackPointObj.transform.SetParent(transform);

            // Better attack point positioning based on weapon type
            Vector3 attackPosition = GetOptimalAttackPointPosition();
            attackPointObj.transform.localPosition = attackPosition;
            attackPoint = attackPointObj.transform;

            Debug.Log($"✅ Created AttackPoint at {attackPosition}");
        }

        // FORCE assignment to MeleeController
        if (meleeController != null)
        {
            meleeController.attackPoint = attackPoint;
            Debug.Log($"✅ Assigned AttackPoint to MeleeController");
        }

        // Setup trail point with GUARANTEED creation
        Transform trailPoint = transform.Find("TrailPoint");
        if (trailPoint == null)
        {
            Debug.Log("Creating TrailPoint...");
            GameObject trailPointObj = new GameObject("TrailPoint");
            trailPointObj.transform.SetParent(transform);
            trailPointObj.transform.localPosition = attackPoint.localPosition * 0.7f; // Closer to handle
            trailPoint = trailPointObj.transform;

            Debug.Log($"✅ Created TrailPoint");
        }

        // FORCE assignment to MeleeController
        if (meleeController != null)
        {
            meleeController.trailPoint = trailPoint;
            Debug.Log($"✅ Assigned TrailPoint to MeleeController");
        }
    }

    /// <summary>
    /// Get optimal attack point position based on weapon type
    /// </summary>
    private Vector3 GetOptimalAttackPointPosition()
    {
        if (weaponData == null) return new Vector3(0f, 0f, 1.0f);

        switch (weaponData.weaponType)
        {
            case WeaponType.Knife:
                return new Vector3(0f, 0f, 0.8f); // Short reach
            case WeaponType.Sword:
                return new Vector3(0f, 0f, 1.8f); // Long reach
            case WeaponType.Axe:
                return new Vector3(0f, 0f, 1.4f); // Medium reach
            case WeaponType.Hammer:
                return new Vector3(0f, 0f, 1.2f); // Medium reach
            case WeaponType.Bat:
                return new Vector3(0f, 0f, 1.6f); // Long reach
            case WeaponType.Crowbar:
                return new Vector3(0f, 0f, 1.3f); // Medium-long reach
            case WeaponType.Fists:
                return new Vector3(0f, 0f, 0.6f); // Very short reach
            default:
                return new Vector3(0f, 0f, 1.0f); // Default
        }
    }

    /// <summary>
    /// Ensure melee controller is properly setup
    /// </summary>
    private void EnsureMeleeControllerSetup()
    {
        if (weaponData == null || !weaponData.IsMeleeWeapon())
            return;

        if (meleeController == null)
        {
            Debug.LogWarning($"⚠️ MeleeController missing for {weaponData.weaponName}");
            SetupMeleeController();
        }
    }

    #endregion

    #region ADS System - COMPLETELY FIXED

    /// <summary>
    /// Start or stop aiming down sights / blocking - FIXED
    /// </summary>
    public void SetAiming(bool aiming)
    {
        if (isAiming != aiming)
        {
            Debug.Log($"SetAiming: {aiming}");
            isAiming = aiming;

            // Handle melee blocking - SIMPLE VERSION
            if (weaponData != null && weaponData.IsMeleeWeapon() && simpleMeleeController != null)
            {
                if (aiming && weaponData.canBlock)
                {
                    simpleMeleeController.StartBlock();
                }
                else
                {
                    simpleMeleeController.StopBlock();
                }
            }
        }
    }

    private void EnsureSimpleMeleeControllerSetup()
    {
        if (weaponData == null || !weaponData.IsMeleeWeapon())
            return;

        if (simpleMeleeController == null)
        {
            Debug.LogWarning($"⚠️ SimpleMeleeController missing for {weaponData.weaponName}");
            SetupSimpleMeleeController();
        }
    }

    private void SetupSimpleMeleeController()
    {
        if (weaponData == null || !weaponData.IsMeleeWeapon())
            return;

        Debug.Log($"🔧 Setting up SimpleMeleeController for {weaponData.weaponName}");

        // Get or add SimpleMeleeController
        simpleMeleeController = GetComponent<MeleeController>();
        if (simpleMeleeController == null)
        {
            simpleMeleeController = gameObject.AddComponent<MeleeController>();
            Debug.Log($"✅ Added SimpleMeleeController component");
        }

        Debug.Log($"✅ SimpleMeleeController configured for {weaponData.weaponName}");
    }

    /// <summary>
    /// Update target positions based on current aiming state
    /// </summary>
    private void UpdateTargetPositions()
    {
        if (weaponData == null)
            return;

        if (isAiming)
        {
            // Set ADS targets
            targetPosition = weaponData.adsPosition + currentRecoilOffset;
            targetRotation = weaponData.adsRotation;
        }
        else
        {
            // Set base targets
            targetPosition = basePosition + currentRecoilOffset;
            targetRotation = baseRotation;
        }
    }

    /// <summary>
    /// Check if currently aiming
    /// </summary>
    public bool IsAiming()
    {
        return isAiming;
    }

    /// <summary>
    /// Check if currently transitioning ADS
    /// </summary>
    public bool IsTransitioningADS()
    {
        return isTransitioningADS;
    }

    /// <summary>
    /// Set base position for ADS calculations (called by WeaponManager)
    /// </summary>
    public void SetBasePosition(Vector3 position, Vector3 rotation)
    {
        // Store the base values
        basePosition = position;
        baseRotation = rotation;

        // CRITICAL FIX: Override ADS positions if they're causing face-shooting
        if (weaponData != null)
        {
            // FIXED ADS positions that won't point at your face
            if (weaponData.IsMeleeWeapon())
            {
                // Melee blocking - move closer to body, slight up angle
                weaponData.adsPosition = new Vector3(0.15f, -0.05f, 0.3f);
                weaponData.adsRotation = new Vector3(-15f, 0f, 0f); // Look down slightly
            }
            else
            {
                // Ranged ADS - center the weapon, move forward slightly
                weaponData.adsPosition = new Vector3(0f, -0.1f, 0.5f);
                weaponData.adsRotation = rotation; // Keep same rotation as base
            }
        }

        // Update target positions
        UpdateTargetPositions();

        Debug.Log($"✅ FIXED base position for {weaponData?.weaponName}: {position}");
        Debug.Log($"   ADS position: {weaponData?.adsPosition}");
        Debug.Log($"   ADS rotation: {weaponData?.adsRotation}");
    }

    [ContextMenu("Fix ADS Pointing At Face")]
    public void FixADSPointingAtFace()
    {
        if (weaponData == null)
        {
            Debug.LogError("No WeaponData to fix!");
            return;
        }

        Debug.Log($"🔧 EMERGENCY FIX: Correcting ADS for {weaponData.weaponName}");

        if (weaponData.IsMeleeWeapon())
        {
            // Melee weapons: blocking position
            weaponData.adsPosition = new Vector3(0.15f, -0.05f, 0.3f);
            weaponData.adsRotation = new Vector3(-15f, 0f, 0f);
            Debug.Log("✅ Fixed MELEE blocking position");
        }
        else
        {
            // Ranged weapons: proper ADS
            weaponData.adsPosition = new Vector3(0f, -0.1f, 0.5f);
            weaponData.adsRotation = new Vector3(0f, 180f, 0f); // Keep the 180 flip
            Debug.Log("✅ Fixed RANGED ADS position");
        }

        // Update immediately if this weapon is equipped
        if (isAiming)
        {
            isAiming = false; // Reset ADS state
            SetAiming(true); // Re-enter ADS with fixed positions
        }

        Debug.Log($"   New ADS position: {weaponData.adsPosition}");
        Debug.Log($"   New ADS rotation: {weaponData.adsRotation}");
    }

    [ContextMenu("Debug Crowbar Setup")]
    public void DebugCrowbarSetup()
    {
        if (weaponData == null)
        {
            Debug.LogError("❌ NO WEAPONDATA!");
            return;
        }

        Debug.Log("=== CROWBAR DEBUG ===");
        Debug.Log($"Weapon Name: {weaponData.weaponName}");
        Debug.Log($"Weapon Type: {weaponData.weaponType}");
        Debug.Log($"Firing Mode: {weaponData.firingMode}");
        Debug.Log($"Is Melee: {weaponData.IsMeleeWeapon()}");
        Debug.Log($"Melee Range: {weaponData.meleeRange}");
        Debug.Log($"Melee Damage: {weaponData.damage}");

        // Check MeleeController
        MeleeController meleeController = GetComponent<MeleeController>();
        Debug.Log($"Has MeleeController: {meleeController != null}");

        if (meleeController != null)
        {
            Debug.Log($"Attack Point: {(meleeController.attackPoint != null ? meleeController.attackPoint.name : "NULL")}");
            Debug.Log($"Trail Point: {(meleeController.trailPoint != null ? meleeController.trailPoint.name : "NULL")}");

            if (meleeController.attackPoint != null)
            {
                Debug.Log($"Attack Point Position: {meleeController.attackPoint.localPosition}");
            }
        }

        // Check if properly configured as melee
        if (weaponData.firingMode != FiringMode.Melee)
        {
            Debug.LogError("❌ CROWBAR NOT SET TO MELEE MODE!");
            Debug.LogError("   Fix: Set firingMode = FiringMode.Melee in WeaponData");
        }
        else
        {
            Debug.Log("✅ Crowbar properly configured as melee weapon");
        }

        Debug.Log("====================");
    }


    /// <summary>
    /// Handle weapon position and rotation - COMPLETELY FIXED
    /// </summary>
    private void HandleWeaponPositionFixed()
    {
        if (weaponData == null)
            return;

        // Update targets when recoil changes
        UpdateTargetPositions();

        // Smooth transition to target position
        float posSpeed = weaponData.adsSpeed * Time.deltaTime;
        Vector3 newPosition = Vector3.Lerp(transform.localPosition, targetPosition, posSpeed);
        Vector3 newRotation = Vector3.Lerp(transform.localEulerAngles, targetRotation, posSpeed);

        // Apply positions
        transform.localPosition = newPosition;
        transform.localRotation = Quaternion.Euler(newRotation);

        // Check if transition is complete
        if (isTransitioningADS)
        {
            float positionDistance = Vector3.Distance(transform.localPosition, targetPosition);
            float rotationDistance = Vector3.Distance(transform.localEulerAngles, targetRotation);

            // FIXED: Use proper threshold for transition completion
            if (positionDistance < 0.01f && rotationDistance < 1f)
            {
                isTransitioningADS = false;
                // Snap to exact target to prevent floating point drift
                transform.localPosition = targetPosition;
                transform.localRotation = Quaternion.Euler(targetRotation);

                Debug.Log($"ADS transition complete for {weaponData.weaponName}");
            }
        }

        // Handle FOV separately and properly
        HandleFOVTransition();
    }

    /// <summary>
    /// Handle FOV transitions properly - FIXED
    /// </summary>
    private void HandleFOVTransition()
    {
        if (playerCamera == null || weaponData == null)
            return;

        float targetFOV;
        if (isAiming && weaponData.adsFOV > 0f)
        {
            targetFOV = weaponData.adsFOV;
        }
        else
        {
            targetFOV = originalFOV;
        }

        // Only change FOV if it's different enough to matter
        if (Mathf.Abs(playerCamera.fieldOfView - targetFOV) > 0.5f)
        {
            float fovSpeed = weaponData.adsSpeed * 10f * Time.deltaTime; // Faster FOV transition
            playerCamera.fieldOfView = Mathf.MoveTowards(playerCamera.fieldOfView, targetFOV, fovSpeed);
        }
    }

    private void PerformDirectMeleeAttack()
    {
        Debug.Log($"🗡️ DIRECT MELEE ATTACK - {weaponData.weaponName}");

        Vector3 attackOrigin = playerCamera.transform.position;
        Vector3 attackDirection = playerCamera.transform.forward;
        float range = weaponData.meleeRange;

        Debug.Log($"Attack from: {attackOrigin} in direction: {attackDirection} with range: {range}");

        // Simple sphere overlap - most reliable
        Vector3 sphereCenter = attackOrigin + attackDirection * (range * 0.5f);
        Collider[] hits = Physics.OverlapSphere(sphereCenter, range);

        Debug.Log($"Found {hits.Length} potential targets");

        bool hitSomething = false;

        foreach (Collider hit in hits)
        {
            if (hit == null || hit.transform.IsChildOf(transform.root))
                continue;

            Debug.Log($"Checking target: {hit.name}");

            // Check if in attack arc
            Vector3 dirToTarget = (hit.transform.position - attackOrigin).normalized;
            float angle = Vector3.Angle(attackDirection, dirToTarget);

            if (angle <= weaponData.meleeArc * 0.5f)
            {
                Debug.Log($"✅ TARGET IN ARC: {hit.name}");

                // Try to damage
                if (TryDamageTarget(hit.gameObject, weaponData.damage))
                {
                    hitSomething = true;
                    Debug.Log($"💥 HIT {hit.name} for {weaponData.damage} damage!");

                    // Apply knockback
                    Rigidbody rb = hit.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 force = dirToTarget * weaponData.meleeKnockbackForce;
                        rb.AddForce(force, ForceMode.Impulse);
                    }
                }
            }
        }

        if (!hitSomething)
        {
            Debug.Log("❌ MELEE ATTACK MISSED");
        }

        // Screen shake
        if (cameraShake != null)
        {
            cameraShake.Shake(0.3f, 0.2f);
        }
    }

    private bool TryDamageTarget(GameObject target, float damage)
    {
        // Try IDamageable
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            return true;
        }

        // Try Health
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage);
            return true;
        }

        Debug.LogWarning($"No damage component on {target.name}");
        return false;
    }

    private bool TryApplyDamage(GameObject target, float damage)
    {
        // Try IDamageable first
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            Debug.Log($"💥 Applied {damage} damage via IDamageable");
            return true;
        }

        // Try Health component
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Debug.Log($"💥 Applied {damage} damage via Health");
            return true;
        }

        // Try any TakeDamage method
        MonoBehaviour[] components = target.GetComponents<MonoBehaviour>();
        foreach (var comp in components)
        {
            var method = comp.GetType().GetMethod("TakeDamage");
            if (method != null && method.GetParameters().Length == 1)
            {
                try
                {
                    var paramType = method.GetParameters()[0].ParameterType;
                    if (paramType == typeof(float))
                    {
                        method.Invoke(comp, new object[] { damage });
                        Debug.Log($"💥 Applied {damage} damage via reflection");
                        return true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Reflection damage failed: {e.Message}");
                }
            }
        }

        Debug.LogWarning($"No damage component found on {target.name}");
        return false;
    }


    #endregion

    #region Firing System - FIXED FOR MELEE

    /// <summary>
    /// Try to fire the weapon or perform melee attack - ENHANCED FOR MELEE
    /// </summary>
    public void TryFire(bool firePressed, bool fireHeld)
    {
        if (weaponData == null)
        {
            Debug.LogWarning("TryFire called but weaponData is null!");
            return;
        }

        Debug.Log($"🔫 WEAPON INPUT: {weaponData.weaponName} - Pressed: {firePressed}, Held: {fireHeld}");

        // MELEE WEAPONS - CROWBAR FIX
        if (weaponData.firingMode == FiringMode.Melee || weaponData.weaponType == WeaponType.Crowbar)
        {
            if (firePressed)
            {
                Debug.Log($"🗡️ MELEE ATTACK STARTING!");
                PerformDirectMeleeAttack();
                return;
            }
        }

        // RANGED WEAPONS - GUN FIX
        if (currentAmmo <= 0)
        {
            Debug.Log("❌ NO AMMO!");
            return;
        }

        bool shouldFire = false;
        switch (weaponData.firingMode)
        {
            case FiringMode.SemiAuto:
                shouldFire = firePressed;
                break;
            case FiringMode.FullAuto:
                shouldFire = fireHeld;
                break;
        }

        if (shouldFire && Time.time >= lastFireTime + weaponData.GetTimeBetweenShots())
        {
            Debug.Log($"🔫 FIRING GUN: {weaponData.weaponName}!");
            FireSingleShot();
        }
    }

    private void PerformSimpleMeleeAttack(bool isHeavyAttack)
    {
        if (weaponData == null)
        {
            Debug.LogError("No weapon data for melee attack!");
            return;
        }

        Debug.Log($"🗡️ PERFORMING SIMPLE MELEE ATTACK with {weaponData.weaponName}");
        Debug.Log($"   Heavy Attack: {isHeavyAttack}");
        Debug.Log($"   Melee Range: {weaponData.meleeRange}");
        Debug.Log($"   Melee Arc: {weaponData.meleeArc}");
        Debug.Log($"   Damage: {weaponData.damage}");

        Vector3 attackOrigin = playerCamera.transform.position;
        Vector3 attackDirection = playerCamera.transform.forward;

        Debug.Log($"   Attack Origin: {attackOrigin}");
        Debug.Log($"   Attack Direction: {attackDirection}");

        // SIMPLE SPHERE OVERLAP - Most reliable method
        Vector3 sphereCenter = attackOrigin + attackDirection * (weaponData.meleeRange * 0.5f);
        Collider[] hitColliders = Physics.OverlapSphere(sphereCenter, weaponData.meleeRange);

        Debug.Log($"   Sphere center: {sphereCenter}");
        Debug.Log($"   Found {hitColliders.Length} colliders in range");

        bool hitSomething = false;
        int targetsHit = 0;

        foreach (Collider col in hitColliders)
        {
            if (col == null)
            {
                Debug.Log("   Skipping null collider");
                continue;
            }

            // Skip self and child objects
            if (col.transform.IsChildOf(transform.root))
            {
                Debug.Log($"   Skipping self/child: {col.name}");
                continue;
            }

            // Check if target is in attack arc
            Vector3 directionToTarget = (col.transform.position - attackOrigin).normalized;
            float angleToTarget = Vector3.Angle(attackDirection, directionToTarget);

            Debug.Log($"   Target: {col.name}");
            Debug.Log($"     Position: {col.transform.position}");
            Debug.Log($"     Direction to target: {directionToTarget}");
            Debug.Log($"     Angle to target: {angleToTarget:F1}°");
            Debug.Log($"     Max allowed angle: {weaponData.meleeArc * 0.5f:F1}°");

            if (angleToTarget <= weaponData.meleeArc * 0.5f)
            {
                Debug.Log($"   ✅ {col.name} is in attack arc - applying damage!");

                // Calculate damage
                float damage = weaponData.damage;
                if (isHeavyAttack) damage *= 1.5f;

                // Try to apply damage
                bool damageApplied = ApplySimpleDamage(col.gameObject, damage);

                if (damageApplied)
                {
                    hitSomething = true;
                    targetsHit++;
                    Debug.Log($"   💥 SUCCESSFULLY HIT {col.name} for {damage} damage!");

                    // Apply knockback
                    ApplyMeleeKnockback(col, directionToTarget);

                    // Screen shake
                    if (cameraShake != null)
                    {
                        cameraShake.Shake(weaponData.meleeShakeIntensity, 0.2f);
                    }
                }
                else
                {
                    Debug.Log($"   ⚠️ Hit {col.name} but couldn't apply damage (no damage component)");
                    // Still count as hit for visual feedback
                    hitSomething = true;
                    targetsHit++;
                    CreateHitFeedback(col.transform.position, damage);
                }
            }
            else
            {
                Debug.Log($"   ❌ {col.name} is outside attack arc");
            }
        }

        // Show debug visualization
        Debug.DrawRay(attackOrigin, attackDirection * weaponData.meleeRange, Color.red, 2f);

        Debug.Log($"=== MELEE ATTACK COMPLETE ===");
        Debug.Log($"Targets hit: {targetsHit}");
        Debug.Log($"Attack successful: {hitSomething}");

        if (!hitSomething)
        {
            Debug.Log("❌ NO TARGETS HIT");
            Debug.Log("   Possible causes:");
            Debug.Log("   - No targets within range (" + weaponData.meleeRange + "m)");
            Debug.Log("   - Targets outside attack arc (" + weaponData.meleeArc + "°)");
            Debug.Log("   - Targets don't have colliders");
            Debug.Log("   Try: Get closer, face target directly, check target has collider");
        }
    }

    private bool ApplySimpleDamage(GameObject target, float damage)
    {
        if (target == null) return false;

        Debug.Log($"Trying to apply {damage} damage to {target.name}...");

        // Method 1: IDamageable interface
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            try
            {
                damageable.TakeDamage(damage);
                Debug.Log($"✅ Applied damage via IDamageable");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"IDamageable failed: {e.Message}");
            }
        }

        // Method 2: Health component
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            try
            {
                health.TakeDamage(damage);
                Debug.Log($"✅ Applied damage via Health component");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Health failed: {e.Message}");
            }
        }

        // Method 3: Any script with TakeDamage method
        MonoBehaviour[] scripts = target.GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script == null) continue;

            var methods = script.GetType().GetMethods();
            foreach (var method in methods)
            {
                if (method.Name == "TakeDamage" && method.GetParameters().Length == 1)
                {
                    try
                    {
                        var paramType = method.GetParameters()[0].ParameterType;
                        if (paramType == typeof(float))
                        {
                            method.Invoke(script, new object[] { damage });
                            Debug.Log($"✅ Applied damage via {script.GetType().Name}.TakeDamage(float)");
                            return true;
                        }
                        else if (paramType == typeof(int))
                        {
                            method.Invoke(script, new object[] { (int)damage });
                            Debug.Log($"✅ Applied damage via {script.GetType().Name}.TakeDamage(int)");
                            return true;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Method invoke failed: {e.Message}");
                    }
                }
            }
        }

        Debug.LogWarning($"No damage component found on {target.name}");
        Debug.LogWarning($"Available components: {string.Join(", ", System.Array.ConvertAll(target.GetComponents<Component>(), c => c.GetType().Name))}");

        return false;
    }

    private void ApplyMeleeKnockback(Collider target, Vector3 direction)
    {
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null && weaponData != null)
        {
            Vector3 knockbackForce = direction * weaponData.meleeKnockbackForce;
            knockbackForce += Vector3.up * weaponData.meleeKnockbackUpforce;
            rb.AddForce(knockbackForce, ForceMode.Impulse);
            Debug.Log($"Applied melee knockback: {knockbackForce}");
        }
    }

    /// <summary>
    /// Create visual feedback when hitting something
    /// </summary>
    private void CreateHitFeedback(Vector3 position, float damage)
    {
        GameObject feedbackObj = new GameObject("MeleeHitFeedback");
        feedbackObj.transform.position = position + Vector3.up;

        TextMesh textMesh = feedbackObj.AddComponent<TextMesh>();
        textMesh.text = $"HIT! -{damage:F0}";
        textMesh.color = Color.red;
        textMesh.fontSize = 25;
        textMesh.anchor = TextAnchor.MiddleCenter;

        if (playerCamera != null)
        {
            feedbackObj.transform.LookAt(playerCamera.transform);
            feedbackObj.transform.Rotate(0, 180, 0);
        }

        Destroy(feedbackObj, 2f);
    }

    public static class DebugExtensions
    {
        public static void DrawWireSphere(Vector3 center, float radius, Color color, float duration)
        {
            // Draw wireframe sphere using multiple circles
            int segments = 16;

            // XY plane
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * Mathf.PI * 2f / segments;
                float angle2 = (i + 1) * Mathf.PI * 2f / segments;

                Vector3 point1 = center + new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, 0);
                Vector3 point2 = center + new Vector3(Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius, 0);

                Debug.DrawLine(point1, point2, color, duration);
            }

            // XZ plane  
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * Mathf.PI * 2f / segments;
                float angle2 = (i + 1) * Mathf.PI * 2f / segments;

                Vector3 point1 = center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 point2 = center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

                Debug.DrawLine(point1, point2, color, duration);
            }

            // YZ plane
            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * Mathf.PI * 2f / segments;
                float angle2 = (i + 1) * Mathf.PI * 2f / segments;

                Vector3 point1 = center + new Vector3(0, Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius);
                Vector3 point2 = center + new Vector3(0, Mathf.Cos(angle2) * radius, Mathf.Sin(angle2) * radius);

                Debug.DrawLine(point1, point2, color, duration);
            }
        }
    }

    /// <summary>
    /// Fire a single shot
    /// </summary>
    private void FireSingleShot()
    {
        if (weaponData == null)
        {
            Debug.LogError("Cannot fire: weaponData is null!");
            return;
        }

        if (currentAmmo <= 0)
        {
            // Play empty sound and trigger events
            try
            {
                weaponEvents?.OnWeaponEmpty?.Invoke();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error triggering empty weapon event: {e.Message}");
            }
            return;
        }

        // Consume ammo
        currentAmmo--;

        try
        {
            OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error updating ammo UI: {e.Message}");
        }

        // Update fire time
        lastFireTime = Time.time;

        // Fire bullets
        for (int i = 0; i < weaponData.bulletsPerShot; i++)
        {
            try
            {
                FireBullet();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error firing bullet {i}: {e.Message}");
            }
        }

        // Trigger effects
        try
        {
            TriggerWeaponEffects();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error triggering weapon effects: {e.Message}");
        }

        isFiring = false;
    }

    /// <summary>
    /// Start burst fire sequence
    /// </summary>
    private void StartBurstFire()
    {
        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
        }

        burstCoroutine = StartCoroutine(BurstFireCoroutine());
    }

    /// <summary>
    /// Burst fire coroutine
    /// </summary>
    private IEnumerator BurstFireCoroutine()
    {
        isFiring = true;
        burstShotsFired = 0;

        while (burstShotsFired < weaponData.burstCount && currentAmmo > 0)
        {
            FireSingleShot();
            burstShotsFired++;

            if (burstShotsFired < weaponData.burstCount)
            {
                yield return new WaitForSeconds(weaponData.GetTimeBetweenShots());
            }
        }

        isFiring = false;
        burstCoroutine = null;
    }

    /// <summary>
    /// Fire a single bullet with raycast - IMPROVED DAMAGE DETECTION
    /// </summary>
    private void FireBullet()
    {
        if (firePoint == null)
        {
            Debug.LogError("FirePoint is null! Cannot fire bullet.");
            return;
        }

        // Get the firing origin and direction
        Vector3 rayOrigin;
        Vector3 rayDirection;

        // Use camera center for accuracy (common FPS approach)
        if (playerCamera != null)
        {
            // Fire from camera position
            rayOrigin = playerCamera.transform.position;
            rayDirection = playerCamera.transform.forward;
        }
        else
        {
            // Fallback to weapon fire point
            rayOrigin = firePoint.position;
            rayDirection = firePoint.forward;
        }

        // Calculate bullet direction with accuracy
        Vector3 finalDirection = CalculateBulletDirection(rayDirection);

        // IMPROVED: Use RaycastAll to get ALL hits, not just the first one
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, finalDirection, weaponData.range, hitLayers);

        if (hits.Length > 0)
        {
            // Sort hits by distance to get the closest one
            System.Array.Sort(hits, (hit1, hit2) => hit1.distance.CompareTo(hit2.distance));

            // Process the closest hit
            RaycastHit hit = hits[0];

            Debug.Log($"RAYCAST HIT: {hit.collider.name} at distance: {hit.distance:F2} meters");

            // Spawn impact effects
            SpawnImpactEffect(hit.point, hit.normal);

            // IMPROVED DAMAGE SYSTEM - More reliable damage detection
            bool damageApplied = ApplyDamageToTarget(hit.collider.gameObject);

            // Check if we hit an enemy-like object even if no damage applied
            bool hitEnemy = damageApplied || IsEnemyObject(hit.collider.gameObject);

            // Apply enhanced screen shake for enemy hits
            if (hitEnemy)
            {
                ApplyEnemyHitShake();
            }
        }

        // Debug ray in scene view - ALWAYS show this ray
        Debug.DrawRay(rayOrigin, finalDirection * weaponData.range, Color.red, 2f);
    }

    /// <summary>
    /// IMPROVED: Apply damage to target with multiple detection methods
    /// </summary>
    private bool ApplyDamageToTarget(GameObject target)
    {
        return ApplyDamageToTargetInternal(target, new HashSet<GameObject>());
    }

    /// <summary>
    /// Internal damage application with recursion protection
    /// </summary>
    private bool ApplyDamageToTargetInternal(GameObject target, HashSet<GameObject> visited)
    {
        if (target == null || visited.Contains(target))
            return false;

        // Add to visited set to prevent infinite recursion
        visited.Add(target);

        bool damageApplied = false;
        float damage = weaponData.damage;

        // Method 1: Try IDamageable interface (highest priority)
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            try
            {
                damageable.TakeDamage(damage);
                damageApplied = true;
                Debug.Log($"💥 Applied {damage} damage to {target.name} via IDamageable");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"IDamageable.TakeDamage failed: {e.Message}");
            }
        }

        // Method 2: Try Health component
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            try
            {
                health.TakeDamage(damage);
                damageApplied = true;
                Debug.Log($"💥 Applied {damage} damage to {target.name} via Health");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Health.TakeDamage failed: {e.Message}");
            }
        }

        // Method 3: Generic reflection approach
        try
        {
            var allComponents = target.GetComponents<MonoBehaviour>();
            foreach (var component in allComponents)
            {
                var methods = component.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    if (method.Name.ToLower().Contains("damage") || method.Name == "TakeDamage")
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1 && (parameters[0].ParameterType == typeof(float) || parameters[0].ParameterType == typeof(int)))
                        {
                            object damageValue = parameters[0].ParameterType == typeof(int) ? (int)damage : damage;
                            method.Invoke(component, new object[] { damageValue });
                            damageApplied = true;
                            Debug.Log($"💥 Applied {damage} damage to {target.name} via {component.GetType().Name}.{method.Name}");
                            return true;
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Generic damage reflection failed: {e.Message}");
        }

        return damageApplied;
    }

    /// <summary>
    /// Check if an object is enemy-like based on name and tags
    /// </summary>
    private bool IsEnemyObject(GameObject obj)
    {
        if (obj == null) return false;

        string objectName = obj.name.ToLower();
        string objectTag = obj.tag.ToLower();

        // Check name patterns
        bool nameMatch = objectName.Contains("enemy") || objectName.Contains("target") ||
                        objectName.Contains("hostile") || objectName.Contains("bot");

        // Check tag patterns
        bool tagMatch = objectTag.Contains("enemy") || objectTag.Contains("target") ||
                       objectTag.Contains("hostile");

        return nameMatch || tagMatch;
    }

    /// <summary>
    /// Calculate bullet direction with accuracy cone
    /// </summary>
    private Vector3 CalculateBulletDirection(Vector3 baseDirection)
    {
        // Apply accuracy cone
        float accuracy = weaponData.accuracy;

        // Improve accuracy when aiming
        if (isAiming)
        {
            accuracy *= weaponData.adsAccuracyMultiplier;
        }

        // Calculate spread
        float spread = (1f - accuracy) * 10f; // Convert to degrees

        // Add random spread
        Vector3 randomDirection = new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            0f
        );

        // Apply spread to base direction
        Quaternion spreadRotation = Quaternion.Euler(randomDirection);
        return spreadRotation * baseDirection;
    }

    #endregion

    #region Reloading

    /// <summary>
    /// Reload the weapon (only for ranged weapons)
    /// </summary>
    public void Reload()
    {
        if (weaponData == null)
        {
            Debug.LogError("Cannot reload: weaponData is null!");
            return;
        }

        if (weaponData.IsMeleeWeapon())
        {
            Debug.Log("Cannot reload: This is a melee weapon!");
            return;
        }

        if (currentAmmo >= weaponData.magazineSize)
        {
            Debug.Log("Magazine is already full!");
            return;
        }

        if (reserveAmmo <= 0)
        {
            Debug.Log("No reserve ammo to reload!");
            return;
        }

        if (isFiring)
        {
            Debug.Log("Already reloading or firing!");
            return;
        }

        Debug.Log("Starting reload...");
        StartCoroutine(ReloadCoroutine());
    }

    /// <summary>
    /// Reload coroutine
    /// </summary>
    private IEnumerator ReloadCoroutine()
    {
        if (weaponData == null)
        {
            Debug.LogError("Cannot reload: weaponData is null!");
            yield break;
        }

        isFiring = true; // Prevent firing during reload

        Debug.Log($"Starting reload. Reload time: {weaponData.reloadTime}");

        // Wait for reload time
        yield return new WaitForSeconds(weaponData.reloadTime);

        // Calculate ammo to reload
        int ammoNeeded = weaponData.magazineSize - currentAmmo;
        int ammoToReload = Mathf.Min(ammoNeeded, reserveAmmo);

        // Update ammo
        currentAmmo += ammoToReload;
        reserveAmmo -= ammoToReload;

        // Update UI safely
        try
        {
            OnAmmoChanged?.Invoke(currentAmmo, reserveAmmo);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error updating ammo UI during reload: {e.Message}");
        }

        // Trigger events safely
        try
        {
            weaponEvents?.OnWeaponReloaded?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error triggering reload event: {e.Message}");
        }

        isFiring = false;

        Debug.Log($"Reload complete. Current ammo: {currentAmmo}, Reserve: {reserveAmmo}");
    }

    #endregion

    #region Effects

    /// <summary>
    /// Trigger visual and audio effects
    /// </summary>
    private void TriggerWeaponEffects()
    {
        // Spawn muzzle flash
        SpawnMuzzleFlash();

        // Apply screen shake
        ApplyScreenShake();

        // Apply weapon recoil
        ApplyWeaponRecoil();

        // Apply pump shotgun knockback if applicable
        ApplyPumpShotgunKnockback();

        // Trigger events safely
        try
        {
            OnWeaponFired?.Invoke();
            weaponEvents?.OnWeaponFired?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error triggering weapon events: {e.Message}");
        }
    }

    /// <summary>
    /// Spawn muzzle flash effect
    /// </summary>
    private void SpawnMuzzleFlash()
    {
        if (weaponData.muzzleFlashParticles == null || muzzlePoint == null)
            return;

        // Clean up previous muzzle flash
        if (currentMuzzleFlash != null)
        {
            Destroy(currentMuzzleFlash);
        }

        // Spawn new muzzle flash
        currentMuzzleFlash = Instantiate(weaponData.muzzleFlashParticles, muzzlePoint.position, muzzlePoint.rotation);

        // Auto-destroy after a short time
        Destroy(currentMuzzleFlash, 0.1f);
    }

    /// <summary>
    /// Spawn impact effect at hit point
    /// </summary>
    private void SpawnImpactEffect(Vector3 position, Vector3 normal)
    {
        if (weaponData.impactParticles == null)
            return;

        // Calculate rotation to face the surface
        Quaternion rotation = Quaternion.LookRotation(normal);

        // Spawn impact effect
        GameObject impact = Instantiate(weaponData.impactParticles, position, rotation);

        // Auto-destroy after a reasonable time
        Destroy(impact, 2f);
    }

    /// <summary>
    /// Apply screen shake when firing
    /// </summary>
    private void ApplyScreenShake()
    {
        if (cameraShake == null || weaponData == null)
            return;

        float intensity = weaponData.screenShakeIntensity;
        float duration = weaponData.screenShakeDuration;

        // Reduce shake when aiming
        if (isAiming)
        {
            intensity *= 0.5f;
        }

        try
        {
            cameraShake.Shake(intensity, duration);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error applying screen shake: {e.Message}");
        }
    }

    /// <summary>
    /// Apply enhanced screen shake when hitting enemies
    /// </summary>
    private void ApplyEnemyHitShake()
    {
        if (cameraShake == null || weaponData == null)
            return;

        // Enhanced shake for enemy hits
        float intensity = weaponData.screenShakeIntensity * 2.5f; // 2.5x stronger
        float duration = weaponData.screenShakeDuration * 1.5f;   // 1.5x longer

        // Reduce shake when aiming (but still stronger than normal)
        if (isAiming)
        {
            intensity *= 0.7f; // Less reduction than normal shake
        }

        try
        {
            cameraShake.Shake(intensity, duration);
            Debug.Log($"Applied enhanced enemy hit shake: intensity={intensity:F2}, duration={duration:F2}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error applying enemy hit shake: {e.Message}");
        }
    }

    /// <summary>
    /// Apply weapon recoil when firing
    /// </summary>
    private void ApplyWeaponRecoil()
    {
        if (weaponData == null)
            return;

        if (recoilCoroutine != null)
        {
            StopCoroutine(recoilCoroutine);
        }

        try
        {
            recoilCoroutine = StartCoroutine(WeaponRecoilCoroutine());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error starting weapon recoil: {e.Message}");
        }
    }

    /// <summary>
    /// Weapon recoil animation
    /// </summary>
    private IEnumerator WeaponRecoilCoroutine()
    {
        // Much stronger and more satisfying recoil
        float recoilStrength = 0.3f;
        float recoilDuration = 0.2f;

        switch (weaponData.weaponType)
        {
            case WeaponType.Pistol:
                recoilStrength = 0.15f;
                recoilDuration = 0.15f;
                break;
            case WeaponType.AssaultRifle:
                recoilStrength = 0.25f;
                recoilDuration = 0.18f;
                break;
            case WeaponType.Shotgun:
                recoilStrength = 0.4f;
                recoilDuration = 0.25f;
                break;
            case WeaponType.Sniper:
                recoilStrength = 0.35f;
                recoilDuration = 0.3f;
                break;
            case WeaponType.SMG:
                recoilStrength = 0.12f;
                recoilDuration = 0.12f;
                break;
        }

        // Reduce recoil when aiming
        if (isAiming)
        {
            recoilStrength *= 0.6f;
            recoilDuration *= 0.8f;
        }

        // Generate recoil direction
        Vector3 recoilDirection = new Vector3(
            Random.Range(-0.5f, 0.5f),  // Horizontal movement
            Random.Range(-0.3f, 0.2f),  // Vertical variation
            -1f                         // Backward movement
        ) * recoilStrength;

        // Snappier recoil animation
        float elapsed = 0f;
        while (elapsed < recoilDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / recoilDuration;

            // More aggressive recoil curve
            float recoilCurve;
            if (progress < 0.3f)
            {
                recoilCurve = progress / 0.3f; // Quick snap to max
            }
            else
            {
                float returnProgress = (progress - 0.3f) / 0.7f;
                recoilCurve = 1f - (returnProgress * returnProgress * returnProgress); // Cubic ease out
            }

            currentRecoilOffset = recoilDirection * recoilCurve;
            yield return null;
        }

        // Ensure we return to zero
        currentRecoilOffset = Vector3.zero;
        recoilCoroutine = null;
    }

    /// <summary>
    /// Apply pump shotgun knockback physics
    /// </summary>
    private void ApplyPumpShotgunKnockback()
    {
        if (!weaponData.HasKnockback())
        {
            Debug.Log("No knockback configured for this weapon");
            return;
        }

        Debug.Log($"🚀 APPLYING KNOCKBACK for {weaponData.weaponName}");
        Debug.Log($"   Knockback Force: {weaponData.knockbackForce}");

        // Find the player's rigidbody
        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null)
        {
            Debug.LogError("❌ NO PLAYER RIGIDBODY FOUND! Add Rigidbody to your player!");
            return;
        }

        Debug.Log($"✅ Found player rigidbody: {playerRb.name}");

        // Calculate knockback direction (opposite of camera forward)
        Vector3 fireDirection = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        Vector3 knockbackDirection = -fireDirection;

        Debug.Log($"Fire direction: {fireDirection}");
        Debug.Log($"Knockback direction: {knockbackDirection}");

        // MUCH STRONGER knockback force
        float baseForce = weaponData.knockbackForce * 3f; // Triple the force!
        Vector3 horizontalKnockback = new Vector3(knockbackDirection.x, 0f, knockbackDirection.z).normalized * baseForce;
        Vector3 upwardKnockback = Vector3.up * (baseForce * 0.3f); // Add upward component

        Vector3 finalKnockback = horizontalKnockback + upwardKnockback;

        Debug.Log($"Horizontal knockback: {horizontalKnockback}");
        Debug.Log($"Upward knockback: {upwardKnockback}");
        Debug.Log($"Final knockback: {finalKnockback} (magnitude: {finalKnockback.magnitude})");

        // Apply the force
        playerRb.AddForce(finalKnockback, ForceMode.VelocityChange);

        Debug.Log($"✅ KNOCKBACK APPLIED! Player should move backward now.");

        // Log player velocity after knockback for debugging
        StartCoroutine(DebugPlayerVelocity(playerRb));

        // Apply weapon recoil for visual feedback
        ApplyWeaponKnockbackRecoil();

        // Screen shake
        if (cameraShake != null)
        {
            float shakeIntensity = weaponData.screenShakeIntensity * 3f; // Stronger shake
            cameraShake.Shake(shakeIntensity, weaponData.screenShakeDuration);
        }
    }

    private System.Collections.IEnumerator DebugPlayerVelocity(Rigidbody playerRb)
    {
        yield return new WaitForFixedUpdate();

        if (playerRb != null)
        {
            Debug.Log($"Player velocity after knockback: {playerRb.linearVelocity}");
            Debug.Log($"Velocity magnitude: {playerRb.linearVelocity.magnitude}");

            if (playerRb.linearVelocity.magnitude < 1f)
            {
                Debug.LogWarning("⚠️ Player velocity is very low - knockback might not be working");
                Debug.LogWarning("   Check: Player has Rigidbody, weapon is PumpShotgun type, knockbackForce > 0");
            }
            else
            {
                Debug.Log("✅ Knockback working - player is moving!");
            }
        }
    }

    private void ApplyWeaponKnockback()
    {
        if (!weaponData.HasKnockback())
            return;

        Debug.Log($"🚀 APPLYING KNOCKBACK: {weaponData.weaponName}");

        // Find player rigidbody
        Rigidbody playerRb = FindPlayerRigidbody();
        if (playerRb == null)
        {
            Debug.LogError("❌ NO PLAYER RIGIDBODY! Add Rigidbody to player!");
            return;
        }

        // Calculate knockback
        Vector3 aimDirection = playerCamera.transform.forward;
        Vector3 knockbackDirection = -aimDirection;

        float force = weaponData.knockbackForce * 2f; // Double for visibility
        Vector3 finalKnockback = knockbackDirection * force;
        finalKnockback += Vector3.up * (force * 0.3f); // Upward component

        Debug.Log($"Player rigidbody: {playerRb.name}");
        Debug.Log($"Knockback force: {finalKnockback} (magnitude: {finalKnockback.magnitude})");

        // Apply force
        playerRb.AddForce(finalKnockback, ForceMode.VelocityChange);

        Debug.Log("✅ Knockback applied!");

        // Log result
        StartCoroutine(LogKnockbackResult(playerRb));
    }

    private Rigidbody FindPlayerRigidbody()
    {
        // Method 1: Up hierarchy
        Transform current = transform;
        for (int i = 0; i < 10 && current != null; i++)
        {
            Rigidbody rb = current.GetComponent<Rigidbody>();
            if (rb != null) return rb;
            current = current.parent;
        }

        // Method 2: Player tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.GetComponent<Rigidbody>();
        }

        // Method 3: Movement component
        Movement movement = FindObjectOfType<Movement>();
        if (movement != null)
        {
            return movement.GetComponent<Rigidbody>();
        }

        return null;
    }

    private System.Collections.IEnumerator LogKnockbackResult(Rigidbody rb)
    {
        yield return new WaitForFixedUpdate();
        Debug.Log($"Player velocity after knockback: {rb.linearVelocity}");

        if (rb.linearVelocity.magnitude < 1f)
        {
            Debug.LogWarning("⚠️ Low velocity - knockback might not be working");
            Debug.LogWarning("Check: Player Rigidbody is not kinematic, has reasonable mass");
        }
    }

    private void ApplyWeaponKnockbackRecoil()
    {
        if (weaponData == null)
            return;

        if (recoilCoroutine != null)
        {
            StopCoroutine(recoilCoroutine);
        }

        try
        {
            recoilCoroutine = StartCoroutine(PumpShotgunRecoilCoroutine());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error starting pump shotgun recoil: {e.Message}");
        }
    }

    private System.Collections.IEnumerator PumpShotgunRecoilCoroutine()
    {
        // Much stronger recoil for pump shotgun
        float recoilStrength = 0.8f; // Very strong recoil
        float recoilDuration = 0.4f; // Longer duration

        // Reduce recoil when aiming
        if (isAiming)
        {
            recoilStrength *= 0.7f;
            recoilDuration *= 0.9f;
        }

        // Generate strong recoil direction
        Vector3 recoilDirection = new Vector3(
            Random.Range(-0.3f, 0.3f),  // Horizontal movement
            Random.Range(-0.2f, 0.1f),  // Slight vertical variation
            -1f                         // Strong backward movement
        ) * recoilStrength;

        Debug.Log($"Weapon recoil direction: {recoilDirection}");

        // More dramatic recoil animation for pump shotgun
        float elapsed = 0f;
        while (elapsed < recoilDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / recoilDuration;

            // More dramatic recoil curve for pump shotgun
            float recoilCurve;
            if (progress < 0.2f)
            {
                recoilCurve = progress / 0.2f; // Very quick snap to max
            }
            else
            {
                float returnProgress = (progress - 0.2f) / 0.8f;
                recoilCurve = 1f - (returnProgress * returnProgress); // Quadratic ease out
            }

            currentRecoilOffset = recoilDirection * recoilCurve;
            yield return null;
        }

        // Ensure we return to zero
        currentRecoilOffset = Vector3.zero;
        recoilCoroutine = null;

        Debug.Log("Weapon recoil animation complete");
    }

    public void TestKnockbackNow()
    {
        if (weaponData == null)
        {
            Debug.LogError("No weapon data to test!");
            return;
        }

        Debug.Log($"=== TESTING KNOCKBACK FOR {weaponData.weaponName} ===");
        Debug.Log($"Weapon Type: {weaponData.weaponType}");
        Debug.Log($"Has Knockback: {weaponData.HasKnockback()}");
        Debug.Log($"Knockback Force: {weaponData.knockbackForce}");

        if (weaponData.HasKnockback())
        {
            ApplyPumpShotgunKnockback();
        }
        else
        {
            Debug.LogWarning("This weapon doesn't have knockback enabled!");
            Debug.LogWarning("Check: weaponType = PumpShotgun, knockbackForce > 0");
        }
    }

    public void DebugPlayerRigidbody()
    {
        Debug.Log("=== PLAYER RIGIDBODY DEBUG ===");

        Rigidbody playerRb = GetPlayerRigidbody();

        if (playerRb != null)
        {
            Debug.Log($"✅ Player Rigidbody Found: {playerRb.name}");
            Debug.Log($"   Mass: {playerRb.mass}");
            Debug.Log($"   Linear Drag: {playerRb.linearDamping}");
            Debug.Log($"   Angular Drag: {playerRb.angularDamping}");
            Debug.Log($"   Use Gravity: {playerRb.useGravity}");
            Debug.Log($"   Is Kinematic: {playerRb.isKinematic}");
            Debug.Log($"   Current Velocity: {playerRb.linearVelocity}");

            if (playerRb.isKinematic)
            {
                Debug.LogWarning("⚠️ Player rigidbody is KINEMATIC - knockback won't work!");
                Debug.LogWarning("   Solution: Set 'Is Kinematic' to FALSE in player's Rigidbody");
            }
        }
        else
        {
            Debug.LogError("❌ NO PLAYER RIGIDBODY FOUND!");
            Debug.LogError("   Solution: Add Rigidbody component to your Player GameObject");

            // List all rigidbodies in scene for debugging
            Rigidbody[] allRigidbodies = FindObjectsOfType<Rigidbody>();
            Debug.Log($"All Rigidbodies in scene: {allRigidbodies.Length}");
            foreach (var rb in allRigidbodies)
            {
                Debug.Log($"   - {rb.name} (on {rb.gameObject.name})");
            }
        }

        Debug.Log("=============================");
    }

    /// <summary>
    /// Find the player's rigidbody component
    /// </summary>
    private Rigidbody GetPlayerRigidbody()
    {
        Debug.Log("Searching for player rigidbody...");

        // Method 1: Search up the hierarchy from weapon
        Transform current = transform;
        int maxSearchDepth = 10; // Prevent infinite loops
        int searchDepth = 0;

        while (current != null && searchDepth < maxSearchDepth)
        {
            Rigidbody rb = current.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.Log($"Found rigidbody on {current.name} (search depth: {searchDepth})");
                return rb;
            }

            current = current.parent;
            searchDepth++;
        }

        // Method 2: Find by Player tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.Log($"Found rigidbody via Player tag: {player.name}");
                return rb;
            }
            else
            {
                Debug.LogError($"Player GameObject '{player.name}' found but has no Rigidbody!");
            }
        }

        // Method 3: Find Movement component and get its rigidbody
        Movement playerMovement = FindObjectOfType<Movement>();
        if (playerMovement != null)
        {
            Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Debug.Log($"Found rigidbody via Movement component: {playerMovement.name}");
                return rb;
            }
        }

        // Method 4: Find any rigidbody in scene (last resort)
        Rigidbody[] allRigidbodies = FindObjectsOfType<Rigidbody>();
        foreach (var rb in allRigidbodies)
        {
            // Skip weapon rigidbodies and other objects
            if (rb.name.ToLower().Contains("player") || rb.GetComponent<Movement>() != null)
            {
                Debug.Log($"Found rigidbody (last resort): {rb.name}");
                return rb;
            }
        }

        Debug.LogError("❌ NO RIGIDBODY FOUND! Your player MUST have a Rigidbody component for knockback to work!");
        Debug.LogError("   Solution: Select your Player GameObject and Add Component → Physics → Rigidbody");

        return null;
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Debug Weapon State")]
    public void DebugWeaponState()
    {
        Debug.Log("=== WEAPON DEBUG INFO ===");
        Debug.Log($"Weapon Name: {(weaponData != null ? weaponData.weaponName : "NULL")}");
        Debug.Log($"Weapon Type: {(weaponData != null ? weaponData.weaponType.ToString() : "NULL")}");
        Debug.Log($"Firing Mode: {(weaponData != null ? weaponData.firingMode.ToString() : "NULL")}");
        Debug.Log($"Is Melee: {(weaponData != null ? weaponData.IsMeleeWeapon() : false)}");
        Debug.Log($"Is Firing: {isFiring}");
        Debug.Log($"Current Ammo: {currentAmmo}");
        Debug.Log($"Reserve Ammo: {reserveAmmo}");
        Debug.Log($"Has MeleeController: {meleeController != null}");
        if (meleeController != null && weaponData != null && weaponData.IsMeleeWeapon())
        {
            Debug.Log($"Attack Point: {(meleeController.attackPoint != null ? meleeController.attackPoint.name : "NULL")}");
            Debug.Log($"Trail Point: {(meleeController.trailPoint != null ? meleeController.trailPoint.name : "NULL")}");
        }
        Debug.Log("========================");
    }

    #endregion

    #region Debug Gizmos

    void OnDrawGizmosSelected()
    {
        if (!showDebugInfo || weaponData == null)
            return;

        // Draw weapon range
        if (firePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(firePoint.position, 0.1f);
            Gizmos.DrawRay(firePoint.position, firePoint.forward * weaponData.range);
        }

        // Draw muzzle point
        if (muzzlePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(muzzlePoint.position, 0.05f);
        }

        // Draw melee attack points
        if (weaponData.IsMeleeWeapon() && meleeController != null)
        {
            if (meleeController.attackPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(meleeController.attackPoint.position, 0.15f);
                Gizmos.DrawWireSphere(meleeController.attackPoint.position, weaponData.meleeRange);
            }

            if (meleeController.trailPoint != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(meleeController.trailPoint.position, 0.1f);
            }
        }
    }

    #endregion
}