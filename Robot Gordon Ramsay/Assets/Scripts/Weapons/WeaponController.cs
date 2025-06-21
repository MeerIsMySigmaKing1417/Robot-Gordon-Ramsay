using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Main weapon controller that handles firing, reloading, and weapon behavior - ENHANCED WITH MELEE
/// Attach this to weapon prefabs and assign WeaponData
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

    // ADS system
    private bool isAiming = false;
    private float originalFOV;
    private Vector3 basePosition;
    private Vector3 baseRotation;

    // Recoil system
    private Vector3 currentRecoilOffset = Vector3.zero;
    private Coroutine recoilCoroutine;

    // Effects
    private GameObject currentMuzzleFlash;

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

        // Get melee controller if this is a melee weapon
        meleeController = GetComponent<MeleeController>();

        // Setup fire point if not assigned
        if (firePoint == null)
        {
            firePoint = transform;
        }

        if (muzzlePoint == null)
        {
            muzzlePoint = firePoint;
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

            Debug.Log($"Weapon initialized: {weaponData.weaponName} ({(weaponData.IsMeleeWeapon() ? "Melee" : "Ranged")})");
        }
        else
        {
            Debug.LogError($"WeaponController on {gameObject.name} has no WeaponData assigned!", this);
        }
    }

    void Update()
    {
        // Handle FOV changes
        HandleFOV();

        // Handle weapon position and rotation
        HandleWeaponPosition();
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

    #region ADS System

    /// <summary>
    /// Start or stop aiming down sights / blocking
    /// </summary>
    public void SetAiming(bool aiming)
    {
        if (isAiming != aiming)
        {
            Debug.Log($"SetAiming: {aiming}");
            isAiming = aiming;

            // Handle melee blocking
            if (weaponData != null && weaponData.IsMeleeWeapon() && meleeController != null)
            {
                if (aiming && weaponData.canBlock)
                {
                    meleeController.StartBlock();
                }
                else
                {
                    meleeController.StopBlock();
                }
            }
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
    /// Check if currently transitioning ADS (legacy compatibility)
    /// </summary>
    public bool IsTransitioningADS()
    {
        return false; // We use simple direct transitions now
    }

    /// <summary>
    /// Set base position for ADS calculations (called by WeaponManager)
    /// </summary>
    public void SetBasePosition(Vector3 position, Vector3 rotation)
    {
        // Ensure position is valid and visible
        if (position.z < 0.3f) position.z = 0.5f;
        if (position.z > 2f) position.z = 1f;

        basePosition = position;
        baseRotation = rotation;

        // Immediately set the weapon to the correct rotation
        transform.localEulerAngles = rotation;

        Debug.Log($"Base position set: {position}, rotation: {rotation}");
    }

    /// <summary>
    /// Handle FOV changes for ADS
    /// </summary>
    private void HandleFOV()
    {
        if (playerCamera == null || weaponData == null)
            return;

        // Only handle ADS FOV - don't interfere with sprint FOV
        if (isAiming)
        {
            // Set target FOV for ADS
            float desiredFOV = weaponData.adsFOV > 0f ? weaponData.adsFOV : originalFOV;

            // Only update if we're not already at the desired FOV
            if (Mathf.Abs(playerCamera.fieldOfView - desiredFOV) > 1f)
            {
                // Use a fixed speed for consistent FOV changes
                float fovChangeSpeed = 60f; // Fixed speed in FOV units per second
                playerCamera.fieldOfView = Mathf.MoveTowards(playerCamera.fieldOfView, desiredFOV, fovChangeSpeed * Time.deltaTime);
            }
            else if (Mathf.Abs(playerCamera.fieldOfView - desiredFOV) <= 1f && Mathf.Abs(playerCamera.fieldOfView - desiredFOV) > 0.1f)
            {
                // Snap to final value when very close
                playerCamera.fieldOfView = desiredFOV;
            }
        }
        // Don't restore FOV when not aiming - let other systems (like sprint) handle it
    }

    /// <summary>
    /// Handle weapon position and rotation
    /// </summary>
    private void HandleWeaponPosition()
    {
        if (weaponData == null)
            return;

        float posSpeed = weaponData.adsSpeed * Time.deltaTime;

        if (isAiming)
        {
            // Move to ADS position
            Vector3 targetPos = weaponData.adsPosition + currentRecoilOffset;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, posSpeed);
        }
        else
        {
            // Move to hip fire position
            Vector3 targetPos = basePosition + currentRecoilOffset;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, posSpeed);

            // INSTANTLY snap to correct rotation - no lerping
            transform.localEulerAngles = baseRotation;
        }
    }

    #endregion

    #region Firing System

    /// <summary>
    /// Try to fire the weapon or perform melee attack - ENHANCED FOR MELEE
    /// </summary>
    public void TryFire(bool firePressed, bool fireHeld)
    {
        if (weaponData == null || isFiring)
            return;

        // Handle melee weapons
        if (weaponData.IsMeleeWeapon())
        {
            if (meleeController != null && firePressed)
            {
                // Light attack on primary fire, heavy attack if holding aim
                meleeController.TryAttack(!isAiming, isAiming);
                weaponEvents?.OnMeleeAttack?.Invoke();
            }
            return;
        }

        // Handle ranged weapons (existing logic)
        bool canFire = false;

        switch (weaponData.firingMode)
        {
            case FiringMode.SemiAuto:
                canFire = firePressed;
                break;
            case FiringMode.FullAuto:
                canFire = fireHeld;
                break;
            case FiringMode.Burst:
                canFire = firePressed;
                break;
        }

        // Check fire rate
        if (canFire && Time.time >= lastFireTime + weaponData.GetTimeBetweenShots())
        {
            if (weaponData.firingMode == FiringMode.Burst)
            {
                StartBurstFire();
            }
            else
            {
                FireSingleShot();
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

            Debug.Log($"Firing from camera position: {rayOrigin}, direction: {rayDirection}");
        }
        else
        {
            // Fallback to weapon fire point
            rayOrigin = firePoint.position;
            rayDirection = firePoint.forward;

            Debug.Log($"Firing from weapon position: {rayOrigin}, direction: {rayDirection}");
        }

        // Calculate bullet direction with accuracy
        Vector3 finalDirection = CalculateBulletDirection(rayDirection);

        Debug.Log($"Final bullet direction with spread: {finalDirection}");

        // IMPROVED: Use RaycastAll to get ALL hits, not just the first one
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, finalDirection, weaponData.range, hitLayers);

        if (hits.Length > 0)
        {
            // Sort hits by distance to get the closest one
            System.Array.Sort(hits, (hit1, hit2) => hit1.distance.CompareTo(hit2.distance));

            // Process the closest hit
            RaycastHit hit = hits[0];

            Debug.Log($"RAYCAST HIT: {hit.collider.name} at distance: {hit.distance:F2} meters");
            Debug.Log($"Hit point: {hit.point}");
            Debug.Log($"Hit normal: {hit.normal}");

            // Spawn impact effects
            SpawnImpactEffect(hit.point, hit.normal);

            // IMPROVED DAMAGE SYSTEM - More reliable damage detection
            bool damageApplied = ApplyDamageToTarget(hit.collider.gameObject);

            // Check if we hit an enemy-like object even if no damage applied
            bool hitEnemy = damageApplied || IsEnemyObject(hit.collider.gameObject);

            if (!damageApplied)
            {
                Debug.LogWarning($"No damage method found on {hit.collider.name}. Available components: {string.Join(", ", hit.collider.GetComponents<Component>().Select(c => c.GetType().Name))}");
            }

            // Apply enhanced screen shake for enemy hits
            if (hitEnemy)
            {
                ApplyEnemyHitShake();
            }
        }
        else
        {
            Debug.Log($"RAYCAST MISS: No hit within range {weaponData.range} meters");
        }

        // Debug ray in scene view - ALWAYS show this ray
        Debug.DrawRay(rayOrigin, finalDirection * weaponData.range, Color.red, 2f);
        Debug.Log($"Debug ray drawn from {rayOrigin} in direction {finalDirection} for {weaponData.range} meters");
    }

    /// <summary>
    /// IMPROVED: Apply damage to target with multiple detection methods - FIXED STACK OVERFLOW
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
                Debug.Log($"Applied {damage} damage to {target.name} via IDamageable");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"IDamageable.TakeDamage failed: {e.Message}");
            }
        }

        // Method 2: Try common component names
        string[] damageComponentNames = { "Health", "EnemyHealth", "PlayerHealth", "TargetHealth", "Damageable" };

        foreach (string componentName in damageComponentNames)
        {
            var healthComponent = target.GetComponent(componentName);
            if (healthComponent != null)
            {
                try
                {
                    var takeDamageMethod = healthComponent.GetType().GetMethod("TakeDamage", new System.Type[] { typeof(float) });
                    if (takeDamageMethod != null)
                    {
                        takeDamageMethod.Invoke(healthComponent, new object[] { damage });
                        damageApplied = true;
                        Debug.Log($"Applied {damage} damage to {target.name} via {componentName} component");
                        return true;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"{componentName} component TakeDamage failed: {e.Message}");
                }
            }
        }

        // Method 3: Generic reflection approach (before checking hierarchy)
        try
        {
            var allComponents = target.GetComponents<MonoBehaviour>();
            foreach (var component in allComponents)
            {
                var methods = component.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    if (method.Name.ToLower().Contains("damage") || method.Name == "TakeDamage" || method.Name == "Damage" || method.Name == "ApplyDamage")
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1 && (parameters[0].ParameterType == typeof(float) || parameters[0].ParameterType == typeof(int)))
                        {
                            object damageValue = parameters[0].ParameterType == typeof(int) ? (int)damage : damage;
                            method.Invoke(component, new object[] { damageValue });
                            damageApplied = true;
                            Debug.Log($"Applied {damage} damage to {target.name} via {component.GetType().Name}.{method.Name}");
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

        // Method 4: Check parent object (with recursion protection)
        Transform parent = target.transform.parent;
        if (parent != null && !visited.Contains(parent.gameObject))
        {
            bool parentDamage = ApplyDamageToTargetInternal(parent.gameObject, visited);
            if (parentDamage)
            {
                Debug.Log($"Applied damage to parent object: {parent.name}");
                return true;
            }
        }

        // Method 5: Check immediate child objects only (limit recursion depth)
        int childrenChecked = 0;
        foreach (Transform child in target.transform)
        {
            if (childrenChecked >= 3) break; // Limit to 3 children to prevent excessive recursion

            if (!visited.Contains(child.gameObject))
            {
                bool childDamage = ApplyDamageToTargetInternal(child.gameObject, visited);
                if (childDamage)
                {
                    Debug.Log($"Applied damage to child object: {child.name}");
                    return true;
                }
            }
            childrenChecked++;
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

    #region Pump Shotgun Physics
    /// <summary>
    /// Apply pump shotgun knockback physics - RAYCAST: Check what you're actually aiming at
    /// </summary>
    private void ApplyPumpShotgunKnockback()
    {
        if (!weaponData.HasKnockback())
            return;

        // Find the player's rigidbody for applying forces
        Rigidbody playerRb = GetPlayerRigidbody();
        if (playerRb == null)
        {
            Debug.LogWarning("Cannot apply shotgun knockback: Player rigidbody not found");
            return;
        }

        // Find movement script
        Movement playerMovement = playerRb.GetComponent<Movement>();
        if (playerMovement == null)
            playerMovement = playerRb.GetComponentInParent<Movement>();

        // Calculate knockback direction (opposite of firing direction)
        Vector3 fireDirection = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        Vector3 knockbackDirection = -fireDirection;

        // Calculate horizontal knockback (ignore Y component for pure horizontal push)
        Vector3 horizontalKnockback = new Vector3(knockbackDirection.x, 0f, knockbackDirection.z).normalized;

        // IMPROVED RAYCAST: Better detection for straight down shots
        bool aimingAtGround = false;
        float rayDistance = 25f; // Increased distance for better detection

        // Start raycast from camera position
        Vector3 rayStart = playerCamera.transform.position;

        if (Physics.Raycast(rayStart, fireDirection, out RaycastHit hit, rayDistance))
        {
            // Check if hit point is below player (indicating ground/floor)
            float heightDifference = rayStart.y - hit.point.y;

            // IMPROVED: More lenient ground detection
            bool hitBelowPlayer = heightDifference > 0.1f; // Even small height differences count
            bool surfaceIsFlat = Vector3.Dot(hit.normal, Vector3.up) > 0.1f; // More lenient surface angle

            aimingAtGround = hitBelowPlayer && surfaceIsFlat;

            Debug.Log($"Raycast hit: {hit.collider.name}, Distance: {hit.distance:F2}, Height diff: {heightDifference:F2}, Surface normal: {Vector3.Dot(hit.normal, Vector3.up):F2}, Aiming at ground: {aimingAtGround}");
        }
        else
        {
            // FALLBACK: If raycast misses, check angle as backup for straight down shots
            float angleToGround = Vector3.Angle(fireDirection, Vector3.down);
            if (angleToGround <= weaponData.upwardBoostAngle)
            {
                aimingAtGround = true;
                Debug.Log($"Raycast missed but angle check passed: {angleToGround:F1}° - treating as ground shot");
            }
            else
            {
                Debug.Log("Raycast missed - aiming at air/sky");
            }
        }

        // Check if player is grounded (for air boost limitation)
        bool isGrounded = playerMovement != null ? IsPlayerGrounded(playerMovement) : true;

        // FIXED: Only allow ONE air boost per air time
        bool canUseGroundBoost = aimingAtGround && (isGrounded || (playerMovement != null && !playerMovement.hasUsedAirBoost));

        Vector3 finalVelocity;
        float forceMultiplier = 1f;

        if (canUseGroundBoost)
        {
            // ROCKET JUMP: Angle-based forces when aiming at ground
            float angleToGround = Vector3.Angle(fireDirection, Vector3.down);
            float downwardFactor = 1f - (angleToGround / weaponData.upwardBoostAngle); // 0 to 1 based on angle
            forceMultiplier = 1f + (downwardFactor * downwardFactor * weaponData.maxDownwardForceMultiplier);

            float scaledForce = weaponData.knockbackForce * 0.1f;

            // Calculate upward component (stronger when aiming more straight down)
            Vector3 upwardForce = Vector3.up * scaledForce * weaponData.upwardBoostMultiplier * forceMultiplier;

            // Calculate horizontal component based on angle (more horizontal when aiming at angle)
            float angleBasedHorizontalStrength = (angleToGround / weaponData.upwardBoostAngle); // 0 to 1
            float finalHorizontalStrength = weaponData.baseHorizontalStrength * (0.5f + angleBasedHorizontalStrength * 1.5f); // 0.5x to 2.0x
            Vector3 horizontalForce = horizontalKnockback * scaledForce * weaponData.upwardBoostMultiplier * finalHorizontalStrength;

            // Combine both forces
            finalVelocity = upwardForce + horizontalForce;

            // Mark that air boost was used (if in air)
            if (!isGrounded && playerMovement != null)
            {
                playerMovement.hasUsedAirBoost = true;
                Debug.Log("Used ONE air boost - cannot use again until landing");
            }

            Debug.Log($"ROCKET JUMP (angle: {angleToGround:F1}°): Up={upwardForce.magnitude:F1}, Horizontal={horizontalForce.magnitude:F1}, Total={finalVelocity.magnitude:F1}");
        }
        else
        {
            // MUCH STRONGER FORCES: Very noticeable knockback when NOT aiming at ground
            float scaledForce = weaponData.knockbackForce * 0.1f;

            // Much stronger multiplier for non-ground shots
            float strongMultiplier = 1.0f; // 100% of rocket jump strength - very noticeable!

            if (aimingAtGround && playerMovement != null && playerMovement.hasUsedAirBoost)
            {
                // Trying to use air boost again - still decent
                finalVelocity = knockbackDirection * scaledForce * 0.5f; // 50% strength
                finalVelocity.y = scaledForce * 0.25f; // Good upward component
                Debug.Log("Air boost already used - moderate knockback");
            }
            else
            {
                // Normal shot at enemies/air - MUCH stronger knockback
                finalVelocity = knockbackDirection * scaledForce * strongMultiplier;
                finalVelocity.y += scaledForce * (strongMultiplier * 0.5f); // Strong upward component
                Debug.Log($"STRONG shot (not aiming at ground): {finalVelocity.magnitude:F1}");
            }
        }

        // Apply knockback protection briefly
        if (playerMovement != null)
        {
            playerMovement.OnKnockbackApplied(0.2f);
        }

        // Apply the knockback force
        playerRb.AddForce(finalVelocity, ForceMode.VelocityChange);

        Debug.Log($"Applied knockback - Force: {finalVelocity}, Magnitude: {finalVelocity.magnitude:F1}");

        // Apply enhanced recoil to weapon for better feedback
        ApplyWeaponKnockbackRecoil();

        // Screen shake based on force (scale with actual force applied)
        if (cameraShake != null)
        {
            float shakeIntensity = weaponData.screenShakeIntensity * (finalVelocity.magnitude / (weaponData.knockbackForce * 0.1f));
            cameraShake.Shake(shakeIntensity, weaponData.screenShakeDuration);
        }
    }

    /// <summary>
    /// Check if player is grounded using the Movement component
    /// </summary>
    private bool IsPlayerGrounded(Movement playerMovement)
    {
        // Access the grounded state from the Movement component
        var movementType = playerMovement.GetType();
        var groundedField = movementType.GetField("grounded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (groundedField != null)
        {
            return (bool)groundedField.GetValue(playerMovement);
        }

        // Fallback: assume grounded if we can't detect
        return true;
    }

    /// <summary>
    /// Apply visual recoil to weapon for pump shotgun feedback
    /// </summary>
    private void ApplyWeaponKnockbackRecoil()
    {
        if (weaponData == null)
            return;

        // Apply stronger recoil for pump shotgun
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

    /// <summary>
    /// Enhanced recoil animation for pump shotgun
    /// </summary>
    private System.Collections.IEnumerator PumpShotgunRecoilCoroutine()
    {
        // Much stronger recoil for pump shotgun
        float recoilStrength = 0.6f; // Double normal shotgun recoil
        float recoilDuration = 0.35f; // Longer duration

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

        // More dramatic recoil animation
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
    }

    /// <summary>
    /// Find the player's rigidbody component
    /// </summary>
    private Rigidbody GetPlayerRigidbody()
    {
        // Try to find rigidbody on player object
        Transform current = transform;

        // Search up the hierarchy for a rigidbody
        while (current != null)
        {
            Rigidbody rb = current.GetComponent<Rigidbody>();
            if (rb != null)
                return rb;

            current = current.parent;
        }

        // Try to find by tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            return player.GetComponent<Rigidbody>();
        }

        // Last resort: find any Movement component and get its rigidbody
        Movement playerMovement = FindObjectOfType<Movement>();
        if (playerMovement != null)
        {
            return playerMovement.GetComponent<Rigidbody>();
        }

        return null;
    }

    #endregion

    /// <summary>
    /// Check if a layer is in a layer mask
    /// </summary>
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
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
    }

    #endregion
}