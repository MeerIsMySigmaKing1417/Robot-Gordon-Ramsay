using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SIMPLIFIED MeleeController - No event system errors, just pure functionality
/// This version focuses on getting melee attacks working reliably
/// Replace your existing MeleeController with this one
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

    [Header("Debug")]
    [Tooltip("Show attack range and arc in scene view")]
    public bool showDebugGizmos = true;

    [Tooltip("Show debug messages for attacks")]
    public bool debugAttacks = true;

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

    // Animation
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private bool isAnimating = false;
    private Coroutine animationCoroutine;

    #region Unity Lifecycle

    void Awake()
    {
        InitializeComponents();
    }

    void Start()
    {
        EnsureProperSetup();
    }

    void Update()
    {
        if (weaponData == null) return;

        UpdateComboTimer();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        Debug.Log($"🔧 SimpleMeleeController: Initializing for {gameObject.name}");

        // Get weapon controller and data
        weaponController = GetComponent<WeaponController>();
        if (weaponController != null)
        {
            weaponData = weaponController.weaponData;
        }

        // Find player camera
        playerCamera = Camera.main ?? FindObjectOfType<Camera>();

        // Find camera shake
        cameraShake = FindObjectOfType<CameraShake>();
        if (cameraShake == null && playerCamera != null)
        {
            cameraShake = playerCamera.gameObject.GetComponent<CameraShake>();
        }

        // Setup attack points
        SetupAttackPoints();

        // Store original transform for animations
        originalPosition = transform.localPosition;
        originalRotation = transform.localEulerAngles;
    }

    private void SetupAttackPoints()
    {
        // Setup attack point
        if (attackPoint == null)
        {
            Transform existingAttackPoint = transform.Find("AttackPoint");
            if (existingAttackPoint != null)
            {
                attackPoint = existingAttackPoint;
            }
            else
            {
                CreateAttackPoint();
            }
        }

        // Setup trail point
        if (trailPoint == null)
        {
            Transform existingTrailPoint = transform.Find("TrailPoint");
            if (existingTrailPoint != null)
            {
                trailPoint = existingTrailPoint;
            }
            else
            {
                trailPoint = attackPoint ?? transform;
            }
        }
    }

    private void CreateAttackPoint()
    {
        GameObject attackPointObj = new GameObject("AttackPoint");
        attackPointObj.transform.SetParent(transform);

        Vector3 attackPosition = GetAttackPointPosition();
        attackPointObj.transform.localPosition = attackPosition;
        attackPoint = attackPointObj.transform;

        Debug.Log($"✅ Created AttackPoint at {attackPosition}");
    }

    private Vector3 GetAttackPointPosition()
    {
        if (weaponData == null) return new Vector3(0f, 0f, 1.0f);

        switch (weaponData.weaponType)
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

    private void EnsureProperSetup()
    {
        bool hasWeaponData = weaponData != null;
        bool hasCamera = playerCamera != null;
        bool hasAttackPoint = attackPoint != null;

        if (!hasWeaponData)
        {
            Debug.LogError("❌ No WeaponData found!");
            enabled = false;
            return;
        }

        if (!hasCamera)
        {
            Debug.LogError("❌ No Camera found!");
            return;
        }

        if (!hasAttackPoint)
        {
            CreateAttackPoint();
        }

        Debug.Log($"✅ SimpleMeleeController ready for {weaponData.weaponName}");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Perform a melee attack - SIMPLIFIED VERSION
    /// </summary>
    public void TryAttack(bool lightAttack, bool heavyAttack)
    {
        if (!CanAttack()) return;

        if (debugAttacks)
        {
            Debug.Log($"🔨 SimpleMeleeController: Starting attack with {weaponData.weaponName}");
        }

        // Determine attack type
        MeleeAttackType attackType = heavyAttack ? MeleeAttackType.Heavy : MeleeAttackType.Light;

        lastAttackTime = Time.time;
        StartCoroutine(PerformAttackCoroutine(attackType));
    }

    private bool CanAttack()
    {
        if (weaponData == null || playerCamera == null || attackPoint == null)
        {
            if (debugAttacks) Debug.LogError("Missing required components for attack!");
            return false;
        }

        if (isAttacking)
        {
            if (debugAttacks) Debug.Log("Already attacking!");
            return false;
        }

        if (!canAttack)
        {
            if (debugAttacks) Debug.Log("Attack on cooldown!");
            return false;
        }

        return true;
    }

    #endregion

    #region Attack System

    private IEnumerator PerformAttackCoroutine(MeleeAttackType attackType)
    {
        isAttacking = true;
        canAttack = false;
        hitTargets.Clear();

        if (debugAttacks)
        {
            Debug.Log($"=== ATTACK STARTED: {attackType} ===");
        }

        // Handle combo
        if (weaponData.allowCombo)
        {
            currentCombo++;
            lastComboTime = Time.time;
        }

        // Trigger animation
        if (useCodeAnimation)
        {
            TriggerAttackAnimation(attackType);
        }

        // Spawn trail effect
        SpawnTrailEffect();

        // Calculate timing
        float attackSpeed = weaponData.GetTimeBetweenShots();
        float hitWindowStart = attackSpeed * 0.3f;
        float hitWindowDuration = weaponData.meleeHitWindow;

        // Wait for hit window
        yield return new WaitForSeconds(hitWindowStart);

        // Hit detection
        bool hitSomething = CheckForHits(attackType);

        if (debugAttacks)
        {
            Debug.Log(hitSomething ? "✅ Attack HIT!" : "❌ Attack MISSED");
        }

        // Wait for attack to complete
        float remainingTime = attackSpeed - hitWindowStart - hitWindowDuration;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        // Cleanup
        CleanupTrailEffect();
        isAttacking = false;
        StartCoroutine(AttackCooldown());
    }

    private bool CheckForHits(MeleeAttackType attackType)
    {
        Vector3 attackOrigin = attackPoint.position;
        Vector3 attackDirection = playerCamera.transform.forward;

        if (debugAttacks)
        {
            Debug.Log($"Hit check: Origin={attackOrigin}, Range={weaponData.meleeRange}");
        }

        // Find colliders in range
        Collider[] colliders = Physics.OverlapSphere(attackOrigin, weaponData.meleeRange);
        bool hitSomething = false;

        foreach (Collider col in colliders)
        {
            if (!IsValidTarget(col, attackOrigin, attackDirection)) continue;

            if (ApplyDamage(col.gameObject, attackType))
            {
                hitTargets.Add(col);
                hitSomething = true;

                // Screen shake
                if (cameraShake != null)
                {
                    cameraShake.Shake(weaponData.meleeShakeIntensity, 0.1f);
                }

                // Spawn impact effect
                SpawnImpactEffect(col.transform.position, (col.transform.position - attackOrigin).normalized);

                // Knockback
                ApplyKnockback(col, (col.transform.position - attackOrigin).normalized);
            }
        }

        return hitSomething;
    }

    private bool IsValidTarget(Collider col, Vector3 attackOrigin, Vector3 attackDirection)
    {
        if (col == null) return false;
        if (hitTargets.Contains(col)) return false;
        if (col.transform.IsChildOf(transform.root)) return false;

        // Check angle
        Vector3 directionToTarget = (col.transform.position - attackOrigin).normalized;
        float angleToTarget = Vector3.Angle(attackDirection, directionToTarget);

        bool inArc = angleToTarget <= weaponData.meleeArc * 0.5f;

        if (debugAttacks && inArc)
        {
            Debug.Log($"Valid target: {col.name} (angle: {angleToTarget:F1}°)");
        }

        return inArc;
    }

    private bool ApplyDamage(GameObject target, MeleeAttackType attackType)
    {
        if (target == null) return false;

        float damage = weaponData.GetMeleeDamage(currentCombo - 1);

        // Apply attack type modifiers
        if (attackType == MeleeAttackType.Heavy)
        {
            damage *= 1.5f;
        }

        // Try IDamageable interface
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            try
            {
                damageable.TakeDamage(damage);
                Debug.Log($"💥 Applied {damage} damage to {target.name} via IDamageable");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error applying damage via IDamageable: {e.Message}");
            }
        }

        // Try Health component
        Health health = target.GetComponent<Health>();
        if (health != null)
        {
            try
            {
                health.TakeDamage(damage);
                Debug.Log($"💥 Applied {damage} damage to {target.name} via Health");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error applying damage via Health: {e.Message}");
            }
        }

        if (debugAttacks)
        {
            Debug.Log($"No damage component found on {target.name}");
        }

        return false;
    }

    private void ApplyKnockback(Collider target, Vector3 direction)
    {
        if (target == null || weaponData.meleeKnockbackForce <= 0) return;

        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 knockbackForce = direction * weaponData.meleeKnockbackForce;
            knockbackForce += Vector3.up * weaponData.meleeKnockbackUpforce;
            targetRb.AddForce(knockbackForce, ForceMode.Impulse);

            if (debugAttacks)
            {
                Debug.Log($"Applied knockback to {target.name}: {knockbackForce}");
            }
        }
    }

    private void SpawnImpactEffect(Vector3 position, Vector3 normal)
    {
        if (weaponData == null) return;

        GameObject effectPrefab = weaponData.meleeImpactParticles != null ?
            weaponData.meleeImpactParticles : weaponData.impactParticles;

        if (effectPrefab != null)
        {
            Quaternion rotation = Quaternion.LookRotation(normal);
            GameObject effect = Instantiate(effectPrefab, position, rotation);
            Destroy(effect, 2f);
        }
    }

    private void SpawnTrailEffect()
    {
        if (weaponData == null || trailPoint == null) return;

        if (weaponData.meleeTrailEffect != null)
        {
            currentTrailEffect = Instantiate(weaponData.meleeTrailEffect, trailPoint.position, trailPoint.rotation);
            currentTrailEffect.transform.SetParent(trailPoint);
        }
    }

    private void CleanupTrailEffect()
    {
        if (currentTrailEffect != null)
        {
            currentTrailEffect.transform.SetParent(null);
            Destroy(currentTrailEffect, 1f);
            currentTrailEffect = null;
        }
    }

    private IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(weaponData.meleeCooldown);
        canAttack = true;
    }

    private void UpdateComboTimer()
    {
        if (currentCombo > 0 && Time.time - lastComboTime > weaponData.comboWindow)
        {
            currentCombo = 0;
            if (debugAttacks) Debug.Log("Combo reset");
        }
    }

    #endregion

    #region Animation System

    private void TriggerAttackAnimation(MeleeAttackType attackType)
    {
        if (!useCodeAnimation) return;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        switch (attackType)
        {
            case MeleeAttackType.Light:
                animationCoroutine = StartCoroutine(PlaySwingAnimation(1f, false));
                break;
            case MeleeAttackType.Heavy:
                animationCoroutine = StartCoroutine(PlaySwingAnimation(0.7f, true));
                break;
            case MeleeAttackType.Combo:
                animationCoroutine = StartCoroutine(PlaySwingAnimation(1.2f, false));
                break;
        }
    }

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

    #endregion

    #region Blocking System

    public void StartBlock()
    {
        if (weaponData == null || !weaponData.canBlock || isAttacking) return;
        isBlocking = true;
        if (debugAttacks) Debug.Log($"Started blocking with {weaponData.weaponName}");
    }

    public void StopBlock()
    {
        if (!isBlocking) return;
        isBlocking = false;
        if (debugAttacks) Debug.Log($"Stopped blocking with {weaponData.weaponName}");
    }

    public bool IsBlocking()
    {
        return isBlocking;
    }

    public float ApplyBlockDamageReduction(float incomingDamage)
    {
        if (!isBlocking || weaponData == null || !weaponData.canBlock)
            return incomingDamage;

        float reducedDamage = incomingDamage * (1f - weaponData.blockDamageReduction);
        Debug.Log($"Blocked! Damage: {incomingDamage} → {reducedDamage}");
        return reducedDamage;
    }

    public int GetComboCount()
    {
        return currentCombo;
    }

    public void ResetCombo()
    {
        currentCombo = 0;
        lastComboTime = 0f;
    }

    #endregion

    #region Debug Gizmos

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || weaponData == null) return;

        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;

        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin, weaponData.meleeRange);

        // Draw attack arc
        Gizmos.color = Color.yellow;
        float halfArc = weaponData.meleeArc * 0.5f;

        Vector3 leftBound = Quaternion.AngleAxis(-halfArc, Vector3.up) * forward * weaponData.meleeRange;
        Vector3 rightBound = Quaternion.AngleAxis(halfArc, Vector3.up) * forward * weaponData.meleeRange;

        Gizmos.DrawLine(origin, origin + leftBound);
        Gizmos.DrawLine(origin, origin + rightBound);
        Gizmos.DrawLine(origin, origin + forward * weaponData.meleeRange);

        // Draw attack point
        if (attackPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(attackPoint.position, 0.1f);
        }
    }

    #endregion
}