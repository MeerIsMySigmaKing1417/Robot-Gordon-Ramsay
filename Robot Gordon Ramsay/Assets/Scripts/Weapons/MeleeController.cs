using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Handles melee combat functionality for melee weapons
/// Attach this component to melee weapon prefabs alongside WeaponController
/// FIXED: Code-based animation instead of Unity Animator
/// </summary>
public class MeleeController : MonoBehaviour
{
    [Header("Attack Points")]
    [Tooltip("Point where melee attacks originate from")]
    public Transform attackPoint;

    [Tooltip("Point where trail effects spawn")]
    public Transform trailPoint;

    [Header("Code-Based Animation")]
    [Tooltip("Enable code-based weapon swing animation")]
    public bool useCodeAnimation = true;

    [Tooltip("Animation speed multiplier")]
    [Range(0.5f, 3f)]
    public float animationSpeed = 1.5f;

    [Tooltip("How far the weapon swings (degrees)")]
    [Range(30f, 120f)]
    public float swingAngle = 80f;

    [Tooltip("How far forward the weapon moves during swing")]
    [Range(0.1f, 1f)]
    public float swingForwardDistance = 0.3f;

    // COMMENTED OUT ANIMATOR STUFF
    /*
    [Header("Animation")]
    [Tooltip("Animator component for weapon animations")]
    public Animator weaponAnimator;

    [Tooltip("Name of light attack animation trigger")]
    public string lightAttackTrigger = "LightAttack";

    [Tooltip("Name of heavy attack animation trigger")]
    public string heavyAttackTrigger = "HeavyAttack";

    [Tooltip("Name of combo attack animation trigger")]
    public string comboAttackTrigger = "ComboAttack";

    [Tooltip("Name of block animation trigger")]
    public string blockTrigger = "Block";
    */

    [Header("Debug")]
    [Tooltip("Show attack range and arc in scene view")]
    public bool showDebugGizmos = true;

    [Tooltip("Show debug messages for attacks")]
    public bool debugAttacks = true;

    // Events
    [System.Serializable]
    public class MeleeEvents
    {
        public UnityEvent OnAttackStarted;
        public UnityEvent OnAttackHit;
        public UnityEvent OnAttackMissed;
        public UnityEvent OnComboStarted;
        public UnityEvent OnComboEnded;
        public UnityEvent OnBlockStarted;
        public UnityEvent OnBlockEnded;
    }

    [Header("Events")]
    public MeleeEvents meleeEvents;

    // Private variables
    private WeaponController weaponController;
    private WeaponData weaponData;
    private Camera playerCamera;
    private CameraShake cameraShake;

    // Attack state
    private bool isAttacking = false;
    private bool canAttack = true;
    private float lastAttackTime = 0f;
    private int currentCombo = 0;
    private float lastComboTime = 0f;
    private bool isBlocking = false;

    // Hit detection
    private List<Collider> hitTargets = new List<Collider>();
    private GameObject currentTrailEffect;

    // Code-based animation
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private bool isAnimating = false;
    private Coroutine animationCoroutine;

    #region Unity Lifecycle

    void Awake()
    {
        weaponController = GetComponent<WeaponController>();
        if (weaponController != null)
        {
            weaponData = weaponController.weaponData;
        }

        // Find camera and camera shake
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindObjectOfType<Camera>();
        }

        cameraShake = FindObjectOfType<CameraShake>();
        if (cameraShake == null && playerCamera != null)
        {
            cameraShake = playerCamera.gameObject.GetComponent<CameraShake>();
        }

        // Setup attack point if not assigned
        if (attackPoint == null)
        {
            attackPoint = transform;
        }

        if (trailPoint == null)
        {
            trailPoint = attackPoint;
        }

        // Store original transform for animations
        originalPosition = transform.localPosition;
        originalRotation = transform.localEulerAngles;
    }

    void Start()
    {
        if (weaponData == null)
        {
            Debug.LogError($"MeleeController on {gameObject.name} has no WeaponData assigned!", this);
            enabled = false;
            return;
        }

        if (!weaponData.IsMeleeWeapon())
        {
            Debug.LogWarning($"MeleeController on {gameObject.name} is attached to a non-melee weapon!", this);
        }

        Debug.Log($"MeleeController initialized for {weaponData.weaponName}");
    }

    void Update()
    {
        UpdateComboTimer();
        UpdateCooldowns();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Perform a melee attack
    /// </summary>
    public void TryAttack(bool lightAttack, bool heavyAttack)
    {
        if (!canAttack || isAttacking || weaponData == null)
        {
            if (debugAttacks)
            {
                Debug.Log($"Attack blocked - canAttack: {canAttack}, isAttacking: {isAttacking}, weaponData: {weaponData != null}");
            }
            return;
        }

        // Determine attack type
        MeleeAttackType attackType = MeleeAttackType.Light;
        if (heavyAttack)
        {
            attackType = MeleeAttackType.Heavy;
        }
        else if (weaponData.allowCombo && currentCombo > 0)
        {
            attackType = MeleeAttackType.Combo;
        }

        if (debugAttacks)
        {
            Debug.Log($"Starting {attackType} attack with {weaponData.weaponName}");
        }

        StartCoroutine(PerformAttack(attackType));
    }

    /// <summary>
    /// Start blocking with this weapon
    /// </summary>
    public void StartBlock()
    {
        if (!weaponData.canBlock || isAttacking)
            return;

        isBlocking = true;

        // Trigger block animation (code-based)
        if (useCodeAnimation)
        {
            StartBlockAnimation();
        }

        meleeEvents.OnBlockStarted?.Invoke();
        Debug.Log($"Started blocking with {weaponData.weaponName}");
    }

    /// <summary>
    /// Stop blocking
    /// </summary>
    public void StopBlock()
    {
        if (!isBlocking)
            return;

        isBlocking = false;

        // Stop block animation
        if (useCodeAnimation)
        {
            StopBlockAnimation();
        }

        meleeEvents.OnBlockEnded?.Invoke();
        Debug.Log($"Stopped blocking with {weaponData.weaponName}");
    }

    /// <summary>
    /// Check if currently blocking
    /// </summary>
    public bool IsBlocking()
    {
        return isBlocking;
    }

    /// <summary>
    /// Apply damage reduction if blocking
    /// </summary>
    public float ApplyBlockDamageReduction(float incomingDamage)
    {
        if (!isBlocking || !weaponData.canBlock)
            return incomingDamage;

        float reducedDamage = incomingDamage * (1f - weaponData.blockDamageReduction);
        Debug.Log($"Blocked attack! Damage reduced from {incomingDamage} to {reducedDamage}");

        // Apply screen shake for successful block
        if (cameraShake != null)
        {
            cameraShake.Shake(0.2f, 0.1f);
        }

        return reducedDamage;
    }

    /// <summary>
    /// Get current combo count
    /// </summary>
    public int GetComboCount()
    {
        return currentCombo;
    }

    /// <summary>
    /// Reset combo chain
    /// </summary>
    public void ResetCombo()
    {
        if (currentCombo > 0)
        {
            meleeEvents.OnComboEnded?.Invoke();
        }
        currentCombo = 0;
        lastComboTime = 0f;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Perform the actual melee attack
    /// </summary>
    private IEnumerator PerformAttack(MeleeAttackType attackType)
    {
        isAttacking = true;
        canAttack = false;
        hitTargets.Clear();

        // Start attack
        meleeEvents.OnAttackStarted?.Invoke();

        if (debugAttacks)
        {
            Debug.Log($"=== ATTACK STARTED ===");
            Debug.Log($"Attack Type: {attackType}");
            Debug.Log($"Attack Range: {weaponData.meleeRange}");
            Debug.Log($"Attack Arc: {weaponData.meleeArc}");
        }

        // Handle combo logic
        if (attackType == MeleeAttackType.Combo || (weaponData.allowCombo && currentCombo == 0))
        {
            currentCombo++;
            lastComboTime = Time.time;

            if (currentCombo == 1)
            {
                meleeEvents.OnComboStarted?.Invoke();
            }
        }

        // Trigger animation
        TriggerAttackAnimation(attackType);

        // Spawn trail effect
        SpawnTrailEffect();

        // Calculate attack timing
        float attackSpeed = weaponData.GetTimeBetweenShots();
        float hitWindowStart = attackSpeed * 0.3f; // Hit detection starts 30% through attack
        float hitWindowEnd = hitWindowStart + weaponData.meleeHitWindow;

        if (debugAttacks)
        {
            Debug.Log($"Attack timing - Speed: {attackSpeed:F2}s, Hit window: {hitWindowStart:F2}s to {hitWindowEnd:F2}s");
        }

        // Wait for hit window to start
        yield return new WaitForSeconds(hitWindowStart);

        // Perform hit detection during hit window
        bool hitSomething = false;
        float hitCheckInterval = 0.02f; // Check for hits every 20ms during hit window
        float hitWindowDuration = hitWindowEnd - hitWindowStart;
        float hitCheckTime = 0f;

        while (hitCheckTime < hitWindowDuration)
        {
            if (CheckForHits(attackType))
            {
                hitSomething = true;
            }

            hitCheckTime += hitCheckInterval;
            yield return new WaitForSeconds(hitCheckInterval);
        }

        // Wait for attack to complete
        float remainingTime = attackSpeed - hitWindowEnd;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        // Trigger appropriate event
        if (hitSomething)
        {
            meleeEvents.OnAttackHit?.Invoke();
            if (debugAttacks) Debug.Log("Attack HIT something!");
        }
        else
        {
            meleeEvents.OnAttackMissed?.Invoke();
            if (debugAttacks) Debug.Log("Attack MISSED");
        }

        // Cleanup trail effect
        CleanupTrailEffect();

        // End attack
        isAttacking = false;

        // Start cooldown
        StartCoroutine(AttackCooldown());
    }

    /// <summary>
    /// Check for targets within attack range and arc - IMPROVED DEBUG
    /// </summary>
    private bool CheckForHits(MeleeAttackType attackType)
    {
        bool hitSomething = false;
        Vector3 attackOrigin = attackPoint.position;
        Vector3 attackDirection = playerCamera.transform.forward;

        if (debugAttacks)
        {
            Debug.Log($"=== HIT CHECK ===");
            Debug.Log($"Attack Origin: {attackOrigin}");
            Debug.Log($"Attack Direction: {attackDirection}");
            Debug.Log($"Attack Range: {weaponData.meleeRange}");
        }

        // Find all colliders within range
        Collider[] colliders = Physics.OverlapSphere(attackOrigin, weaponData.meleeRange);

        if (debugAttacks)
        {
            Debug.Log($"Found {colliders.Length} colliders in range");
        }

        foreach (Collider col in colliders)
        {
            // Skip if already hit this target in this attack
            if (hitTargets.Contains(col))
                continue;

            // Skip self and player
            if (col.transform.IsChildOf(transform.root))
            {
                if (debugAttacks) Debug.Log($"Skipping {col.name} - is child of player");
                continue;
            }

            // Check if target is within attack arc
            Vector3 directionToTarget = (col.transform.position - attackOrigin).normalized;
            float angleToTarget = Vector3.Angle(attackDirection, directionToTarget);

            if (debugAttacks)
            {
                Debug.Log($"Target: {col.name}, Angle: {angleToTarget:F1}°, Max Angle: {weaponData.meleeArc * 0.5f:F1}°");
            }

            if (angleToTarget <= weaponData.meleeArc * 0.5f)
            {
                // Target is within arc, apply damage
                if (ApplyMeleeDamage(col.gameObject, attackType))
                {
                    hitTargets.Add(col);
                    hitSomething = true;

                    // Spawn impact effect
                    SpawnImpactEffect(col.transform.position, directionToTarget);

                    // Apply knockback
                    ApplyKnockback(col, directionToTarget);

                    if (debugAttacks)
                    {
                        Debug.Log($"HIT: {col.name} - damage applied!");
                    }
                }
                else
                {
                    if (debugAttacks)
                    {
                        Debug.Log($"HIT: {col.name} - but no damage applied (no damage component)");
                    }
                }
            }
            else
            {
                if (debugAttacks)
                {
                    Debug.Log($"MISS: {col.name} - outside attack arc");
                }
            }
        }

        return hitSomething;
    }

    /// <summary>
    /// Apply damage to a target - IMPROVED DEBUG
    /// </summary>
    private bool ApplyMeleeDamage(GameObject target, MeleeAttackType attackType)
    {
        float damage = weaponData.GetMeleeDamage(currentCombo - 1);

        // Apply attack type modifiers
        switch (attackType)
        {
            case MeleeAttackType.Heavy:
                damage *= 1.5f;
                break;
            case MeleeAttackType.Combo:
                // Damage already calculated with combo multiplier
                break;
        }

        if (debugAttacks)
        {
            Debug.Log($"Attempting to apply {damage} damage to {target.name}");
        }

        // Try to find damage components (same logic as WeaponController)
        bool damageApplied = false;

        // Method 1: Try IDamageable interface
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(damage);
            damageApplied = true;
            if (debugAttacks) Debug.Log($"Applied damage via IDamageable interface");
        }
        else
        {
            // Method 2: Try Health component
            Health health = target.GetComponent<Health>();
            if (health != null)
            {
                health.TakeDamage(damage);
                damageApplied = true;
                if (debugAttacks) Debug.Log($"Applied damage via Health component");
            }
            else
            {
                if (debugAttacks)
                {
                    var components = target.GetComponents<Component>();
                    Debug.Log($"No damage component found on {target.name}. Components: {string.Join(", ", System.Array.ConvertAll(components, c => c.GetType().Name))}");
                }
            }
        }

        if (damageApplied)
        {
            Debug.Log($"Melee hit: {target.name} took {damage} damage");

            // Apply screen shake for hits
            if (cameraShake != null)
            {
                cameraShake.Shake(weaponData.meleeShakeIntensity, 0.1f);
            }
        }

        return damageApplied;
    }

    /// <summary>
    /// Apply knockback to hit target
    /// </summary>
    private void ApplyKnockback(Collider target, Vector3 direction)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null && weaponData.meleeKnockbackForce > 0f)
        {
            Vector3 knockbackForce = direction * weaponData.meleeKnockbackForce;
            knockbackForce += Vector3.up * weaponData.meleeKnockbackUpforce;

            targetRb.AddForce(knockbackForce, ForceMode.Impulse);
            Debug.Log($"Applied knockback to {target.name}: {knockbackForce}");
        }
    }

    /// <summary>
    /// Trigger appropriate attack animation - CODE-BASED
    /// </summary>
    private void TriggerAttackAnimation(MeleeAttackType attackType)
    {
        if (!useCodeAnimation)
            return;

        // Stop any existing animation
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        // Start new animation based on attack type
        switch (attackType)
        {
            case MeleeAttackType.Light:
                animationCoroutine = StartCoroutine(PlaySwingAnimation(1f, false));
                break;
            case MeleeAttackType.Heavy:
                animationCoroutine = StartCoroutine(PlaySwingAnimation(0.7f, true)); // Slower, more dramatic
                break;
            case MeleeAttackType.Combo:
                animationCoroutine = StartCoroutine(PlaySwingAnimation(1.2f, false)); // Faster combo
                break;
        }

        /* COMMENTED OUT ANIMATOR VERSION
        if (weaponAnimator == null)
            return;

        string triggerName = lightAttackTrigger;
        switch (attackType)
        {
            case MeleeAttackType.Heavy:
                triggerName = heavyAttackTrigger;
                break;
            case MeleeAttackType.Combo:
                triggerName = comboAttackTrigger;
                break;
        }

        weaponAnimator.SetTrigger(triggerName);
        */
    }

    /// <summary>
    /// CODE-BASED SWING ANIMATION
    /// </summary>
    private IEnumerator PlaySwingAnimation(float speedMultiplier, bool isHeavyAttack)
    {
        isAnimating = true;
        float duration = (1f / speedMultiplier) * animationSpeed;

        // Calculate swing path
        Vector3 startRotation = originalRotation;
        Vector3 midRotation = startRotation + new Vector3(0, -swingAngle * 0.5f, 0); // Swing right to left
        Vector3 endRotation = startRotation + new Vector3(0, swingAngle * 0.5f, 0);

        Vector3 startPosition = originalPosition;
        Vector3 forwardPosition = startPosition + new Vector3(0, 0, swingForwardDistance);

        float elapsed = 0f;

        // Phase 1: Wind up (20% of duration)
        float windupDuration = duration * 0.2f;
        while (elapsed < windupDuration)
        {
            float t = elapsed / windupDuration;
            transform.localEulerAngles = Vector3.Lerp(startRotation, midRotation, t);
            transform.localPosition = Vector3.Lerp(startPosition, forwardPosition, t * 0.5f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2: Swing (60% of duration)
        float swingStart = elapsed;
        float swingDuration = duration * 0.6f;
        while (elapsed < swingStart + swingDuration)
        {
            float t = (elapsed - swingStart) / swingDuration;
            transform.localEulerAngles = Vector3.Lerp(midRotation, endRotation, t);
            transform.localPosition = Vector3.Lerp(forwardPosition, startPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 3: Recovery (20% of duration)
        float recoveryStart = elapsed;
        float recoveryDuration = duration * 0.2f;
        while (elapsed < recoveryStart + recoveryDuration)
        {
            float t = (elapsed - recoveryStart) / recoveryDuration;
            transform.localEulerAngles = Vector3.Lerp(endRotation, startRotation, t);
            transform.localPosition = Vector3.Lerp(startPosition, originalPosition, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure we end at original position
        transform.localEulerAngles = originalRotation;
        transform.localPosition = originalPosition;

        isAnimating = false;
        animationCoroutine = null;
    }

    /// <summary>
    /// Start block animation
    /// </summary>
    private void StartBlockAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        animationCoroutine = StartCoroutine(PlayBlockAnimation(true));
    }

    /// <summary>
    /// Stop block animation
    /// </summary>
    private void StopBlockAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        animationCoroutine = StartCoroutine(PlayBlockAnimation(false));
    }

    /// <summary>
    /// CODE-BASED BLOCK ANIMATION
    /// </summary>
    private IEnumerator PlayBlockAnimation(bool blocking)
    {
        Vector3 startPos = transform.localPosition;
        Vector3 startRot = transform.localEulerAngles;

        Vector3 targetPos = blocking ? originalPosition + new Vector3(-0.2f, 0.1f, -0.3f) : originalPosition;
        Vector3 targetRot = blocking ? originalRotation + new Vector3(-10f, -15f, 5f) : originalRotation;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localPosition = Vector3.Lerp(startPos, targetPos, t);
            transform.localEulerAngles = Vector3.Lerp(startRot, targetRot, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = targetPos;
        transform.localEulerAngles = targetRot;

        animationCoroutine = null;
    }

    /// <summary>
    /// Spawn trail effect for attack
    /// </summary>
    private void SpawnTrailEffect()
    {
        if (weaponData.meleeTrailEffect != null && trailPoint != null)
        {
            currentTrailEffect = Instantiate(weaponData.meleeTrailEffect, trailPoint.position, trailPoint.rotation);
            currentTrailEffect.transform.SetParent(trailPoint);
        }
    }

    /// <summary>
    /// Clean up trail effect
    /// </summary>
    private void CleanupTrailEffect()
    {
        if (currentTrailEffect != null)
        {
            // Detach from weapon and let it finish naturally
            currentTrailEffect.transform.SetParent(null);
            Destroy(currentTrailEffect, 1f);
            currentTrailEffect = null;
        }
    }

    /// <summary>
    /// Spawn impact effect at hit location
    /// </summary>
    private void SpawnImpactEffect(Vector3 position, Vector3 normal)
    {
        GameObject effectPrefab = weaponData.meleeImpactParticles != null ?
            weaponData.meleeImpactParticles : weaponData.impactParticles;

        if (effectPrefab != null)
        {
            Quaternion rotation = Quaternion.LookRotation(normal);
            GameObject effect = Instantiate(effectPrefab, position, rotation);
            Destroy(effect, 2f);
        }
    }

    /// <summary>
    /// Attack cooldown coroutine
    /// </summary>
    private IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(weaponData.meleeCooldown);
        canAttack = true;
    }

    /// <summary>
    /// Update combo timer
    /// </summary>
    private void UpdateComboTimer()
    {
        if (currentCombo > 0 && Time.time - lastComboTime > weaponData.comboWindow)
        {
            ResetCombo();
        }
    }

    /// <summary>
    /// Update various cooldowns
    /// </summary>
    private void UpdateCooldowns()
    {
        // Additional cooldown logic can go here
    }

    #endregion

    #region Debug Gizmos

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || weaponData == null)
            return;

        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;

        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, weaponData.meleeRange);

        // Draw attack arc
        Gizmos.color = Color.yellow;
        float halfArc = weaponData.meleeArc * 0.5f;

        // Draw arc lines
        Vector3 leftBound = Quaternion.AngleAxis(-halfArc, Vector3.up) * forward * weaponData.meleeRange;
        Vector3 rightBound = Quaternion.AngleAxis(halfArc, Vector3.up) * forward * weaponData.meleeRange;

        Gizmos.DrawLine(origin, origin + leftBound);
        Gizmos.DrawLine(origin, origin + rightBound);
        Gizmos.DrawLine(origin, origin + forward * weaponData.meleeRange);

        // Draw arc
        for (int i = 0; i <= 20; i++)
        {
            float angle = Mathf.Lerp(-halfArc, halfArc, i / 20f);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            Vector3 point = origin + direction * weaponData.meleeRange;

            if (i > 0)
            {
                float prevAngle = Mathf.Lerp(-halfArc, halfArc, (i - 1) / 20f);
                Vector3 prevDirection = Quaternion.AngleAxis(prevAngle, Vector3.up) * forward;
                Vector3 prevPoint = origin + prevDirection * weaponData.meleeRange;
                Gizmos.DrawLine(prevPoint, point);
            }
        }

        // Draw attack point
        if (attackPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(attackPoint.position, 0.1f);
        }
    }

    #endregion
}