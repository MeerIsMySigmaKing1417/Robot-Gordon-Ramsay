using UnityEngine;

/// <summary>
/// Enum for different weapon types - ENHANCED WITH MELEE
/// </summary>
public enum WeaponType
{
    // Ranged weapons
    Pistol,
    AssaultRifle,
    Shotgun,
    PumpShotgun,
    Sniper,
    SMG,

    // Melee weapons
    Knife,
    Sword,
    Axe,
    Hammer,
    Bat,
    Crowbar,
    Fists
}

/// <summary>
/// Enum for weapon firing modes - ENHANCED WITH MELEE
/// </summary>
public enum FiringMode
{
    SemiAuto,    // One shot per trigger pull
    FullAuto,    // Continuous fire while held
    Burst,       // Multiple shots per trigger pull
    Melee        // Melee attack mode
}

/// <summary>
/// Enum for melee attack types
/// </summary>
public enum MeleeAttackType
{
    Light,       // Fast, low damage attacks
    Heavy,       // Slow, high damage attacks
    Combo        // Combination attacks
}

/// <summary>
/// ScriptableObject that defines weapon properties - ENHANCED WITH MELEE
/// Create new weapons: Right-click → Create → Weapons → Weapon Data
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "Weapons/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Display name for this weapon")]
    public string weaponName = "New Weapon";

    [Tooltip("Type of weapon")]
    public WeaponType weaponType = WeaponType.Pistol;

    [Tooltip("Description of the weapon")]
    [TextArea(2, 4)]
    public string description = "";

    [Header("Combat Stats")]
    [Tooltip("Damage per bullet/hit")]
    [Range(1f, 200f)]
    public float damage = 25f;

    [Tooltip("Maximum range of the weapon")]
    [Range(1f, 200f)]
    public float range = 50f;

    [Tooltip("Rate of fire (shots per minute) or attack speed for melee")]
    [Range(30f, 1200f)]
    public float fireRate = 300f;

    [Tooltip("Accuracy (1.0 = perfect, 0.0 = completely inaccurate)")]
    [Range(0f, 1f)]
    public float accuracy = 0.85f;

    [Header("Ammunition (Ranged Weapons Only)")]
    [Tooltip("Magazine size")]
    [Range(0, 100)]
    public int magazineSize = 15;

    [Tooltip("Total ammo capacity")]
    [Range(0, 500)]
    public int maxAmmo = 120;

    [Tooltip("Reload time in seconds")]
    [Range(0.5f, 5f)]
    public float reloadTime = 2f;

    [Header("Firing Behavior")]
    [Tooltip("Firing mode of the weapon")]
    public FiringMode firingMode = FiringMode.SemiAuto;

    [Tooltip("Number of bullets per shot (for shotguns)")]
    [Range(1, 12)]
    public int bulletsPerShot = 1;

    [Tooltip("Burst count (for burst fire weapons)")]
    [Range(2, 5)]
    public int burstCount = 3;

    [Header("Melee Combat Settings")]
    [Tooltip("Type of melee attack this weapon performs")]
    public MeleeAttackType meleeAttackType = MeleeAttackType.Light;

    [Tooltip("Attack range for melee weapons")]
    [Range(0.5f, 5f)]
    public float meleeRange = 2f;

    [Tooltip("Angle of attack arc for melee weapons (degrees)")]
    [Range(15f, 180f)]
    public float meleeArc = 60f;

    [Tooltip("Time window for successful hits (seconds)")]
    [Range(0.1f, 1f)]
    public float meleeHitWindow = 0.3f;

    [Tooltip("Time before next attack can be performed")]
    [Range(0.1f, 3f)]
    public float meleeCooldown = 0.5f;

    [Tooltip("Can this melee weapon perform combo attacks?")]
    public bool allowCombo = false;

    [Tooltip("Maximum combo hits before reset")]
    [Range(2, 5)]
    public int maxComboHits = 3;

    [Tooltip("Time window for combo continuation")]
    [Range(0.5f, 2f)]
    public float comboWindow = 1f;

    [Tooltip("Damage multiplier for combo attacks")]
    [Range(1f, 3f)]
    public float comboDamageMultiplier = 1.2f;

    [Tooltip("Knockback force applied to hit targets")]
    [Range(0f, 50f)]
    public float meleeKnockbackForce = 10f;

    [Tooltip("Upward component of knockback")]
    [Range(0f, 20f)]
    public float meleeKnockbackUpforce = 2f;

    [Header("Pump Shotgun Physics")]
    [Tooltip("Knockback force applied to player when firing (for pump shotguns) - Instant VelocityChange")]
    [Range(0f, 25f)]
    public float knockbackForce = 12f;

    [Tooltip("Base horizontal knockback strength (how much you get pushed sideways)")]
    [Range(0f, 5f)]
    public float baseHorizontalStrength = 2f;

    [Tooltip("Minimum angle (degrees) to ground for upward boost")]
    [Range(0f, 90f)]
    public float upwardBoostAngle = 70f;

    [Tooltip("Multiplier for upward force when aiming at ground")]
    [Range(1f, 6f)]
    public float upwardBoostMultiplier = 3f;

    [Tooltip("Maximum force multiplier when aiming straight down (higher = more rocket jump power)")]
    [Range(1f, 8f)]
    public float maxDownwardForceMultiplier = 5f;

    [Header("Visual Effects")]
    [Tooltip("Muzzle flash particle effect")]
    public GameObject muzzleFlashParticles;

    [Tooltip("Impact effect for hitting targets")]
    public GameObject impactParticles;

    [Tooltip("Melee impact effect for hitting targets")]
    public GameObject meleeImpactParticles;

    [Tooltip("Bullet trail effect (optional)")]
    public GameObject bulletTrailPrefab;

    [Tooltip("Melee swing trail effect")]
    public GameObject meleeTrailEffect;

    [Header("Weapon Model")]
    [Tooltip("3D model/prefab for the weapon")]
    public GameObject weaponPrefab;

    [Tooltip("Position offset when held by player")]
    public Vector3 holdPosition = new Vector3(0.5f, -0.3f, 0.8f);

    [Tooltip("Rotation offset when held by player")]
    public Vector3 holdRotation = Vector3.zero;

    [Header("ADS (Aim Down Sights) / Melee Block")]
    [Tooltip("Position when aiming down sights or blocking with melee")]
    public Vector3 adsPosition = new Vector3(0f, -0.15f, 0.4f);

    [Tooltip("Rotation when aiming down sights or blocking")]
    public Vector3 adsRotation = Vector3.zero;

    [Tooltip("Field of view when aiming (0 = no change)")]
    [Range(0f, 90f)]
    public float adsFOV = 40f;

    [Tooltip("ADS transition speed")]
    [Range(1f, 20f)]
    public float adsSpeed = 8f;

    [Tooltip("Accuracy multiplier when aiming (1.0 = same, 2.0 = twice as accurate)")]
    [Range(1f, 5f)]
    public float adsAccuracyMultiplier = 2f;

    [Tooltip("Can this melee weapon block attacks?")]
    public bool canBlock = false;

    [Tooltip("Damage reduction when blocking (0-1)")]
    [Range(0f, 1f)]
    public float blockDamageReduction = 0.5f;

    [Header("Screen Shake")]
    [Tooltip("Screen shake intensity when firing")]
    [Range(0f, 2f)]
    public float screenShakeIntensity = 0.3f;

    [Tooltip("Screen shake duration when firing")]
    [Range(0.05f, 0.5f)]
    public float screenShakeDuration = 0.1f;

    [Tooltip("Screen shake intensity for melee hits")]
    [Range(0f, 2f)]
    public float meleeShakeIntensity = 0.4f;

    /// <summary>
    /// Calculate time between shots/attacks based on fire rate
    /// </summary>
    public float GetTimeBetweenShots()
    {
        return 60f / fireRate;
    }

    /// <summary>
    /// Check if this weapon has knockback physics
    /// </summary>
    public bool HasKnockback()
    {
        return weaponType == WeaponType.PumpShotgun && knockbackForce > 0f;
    }

    /// <summary>
    /// IMPROVED: Check if this weapon is a melee weapon - More comprehensive detection
    /// </summary>
    public bool IsMeleeWeapon()
    {
        // PRIMARY CHECK: Weapon type
        bool isMeleeType = weaponType == WeaponType.Knife ||
                          weaponType == WeaponType.Sword ||
                          weaponType == WeaponType.Axe ||
                          weaponType == WeaponType.Hammer ||
                          weaponType == WeaponType.Bat ||
                          weaponType == WeaponType.Crowbar ||
                          weaponType == WeaponType.Fists;

        // SECONDARY CHECK: Firing mode
        bool isMeleeMode = firingMode == FiringMode.Melee;

        // TERTIARY CHECK: Has melee-specific settings configured
        bool hasMeleeSettings = meleeRange > 0f && meleeArc > 0f;

        // Return true if ANY condition is met (most comprehensive)
        bool result = isMeleeType || isMeleeMode || hasMeleeSettings;

        // DEBUG LOG for troubleshooting
        if (result)
        {
            Debug.Log($"✅ {weaponName} detected as MELEE weapon:");
            Debug.Log($"   - Type Check: {isMeleeType} (type: {weaponType})");
            Debug.Log($"   - Mode Check: {isMeleeMode} (mode: {firingMode})");
            Debug.Log($"   - Settings Check: {hasMeleeSettings} (range: {meleeRange}, arc: {meleeArc})");
        }

        return result;
    }

    /// <summary>
    /// Get melee damage with combo multiplier
    /// </summary>
    public float GetMeleeDamage(int comboCount = 0)
    {
        if (!allowCombo || comboCount <= 0)
            return damage;

        float multiplier = 1f + (comboCount * (comboDamageMultiplier - 1f));
        return damage * multiplier;
    }

    /// <summary>
    /// AUTO-CONFIGURE for melee weapons - Call this to automatically set up melee weapons
    /// </summary>
    [ContextMenu("Auto-Configure as Melee Weapon")]
    public void AutoConfigureAsMeleeWeapon()
    {
        // Set firing mode to melee
        firingMode = FiringMode.Melee;

        // Configure melee-appropriate settings based on weapon type
        switch (weaponType)
        {
            case WeaponType.Knife:
                meleeRange = 1.5f;
                meleeArc = 45f;
                fireRate = 180f; // Fast attacks
                damage = 35f;
                meleeHitWindow = 0.2f;
                meleeCooldown = 0.3f;
                allowCombo = true;
                maxComboHits = 3;
                break;

            case WeaponType.Sword:
                meleeRange = 2.5f;
                meleeArc = 90f;
                fireRate = 120f; // Medium speed
                damage = 65f;
                meleeHitWindow = 0.4f;
                meleeCooldown = 0.6f;
                allowCombo = true;
                maxComboHits = 4;
                canBlock = true;
                blockDamageReduction = 0.7f;
                break;

            case WeaponType.Axe:
                meleeRange = 2.0f;
                meleeArc = 70f;
                fireRate = 90f; // Slow, heavy attacks
                damage = 85f;
                meleeHitWindow = 0.5f;
                meleeCooldown = 0.8f;
                allowCombo = false;
                meleeKnockbackForce = 20f;
                meleeKnockbackUpforce = 5f;
                break;

            case WeaponType.Hammer:
                meleeRange = 1.8f;
                meleeArc = 60f;
                fireRate = 80f; // Very slow, very heavy
                damage = 100f;
                meleeHitWindow = 0.6f;
                meleeCooldown = 1.0f;
                allowCombo = false;
                meleeKnockbackForce = 25f;
                meleeKnockbackUpforce = 8f;
                break;

            case WeaponType.Bat:
                meleeRange = 2.2f;
                meleeArc = 80f;
                fireRate = 150f; // Medium speed
                damage = 50f;
                meleeHitWindow = 0.3f;
                meleeCooldown = 0.4f;
                allowCombo = true;
                maxComboHits = 2;
                meleeKnockbackForce = 15f;
                break;

            case WeaponType.Crowbar:
                meleeRange = 2.0f;
                meleeArc = 65f;
                fireRate = 110f; // Medium-slow
                damage = 70f;
                meleeHitWindow = 0.4f;
                meleeCooldown = 0.7f;
                allowCombo = false;
                meleeKnockbackForce = 18f;
                canBlock = true;
                blockDamageReduction = 0.4f;
                break;

            case WeaponType.Fists:
                meleeRange = 1.2f;
                meleeArc = 50f;
                fireRate = 240f; // Very fast
                damage = 20f;
                meleeHitWindow = 0.15f;
                meleeCooldown = 0.2f;
                allowCombo = true;
                maxComboHits = 5;
                comboDamageMultiplier = 1.1f;
                break;
        }

        // Set screen shake for melee
        meleeShakeIntensity = 0.4f;
        screenShakeIntensity = 0.2f;
        screenShakeDuration = 0.1f;

        Debug.Log($"✅ Auto-configured {weaponName} as {weaponType} melee weapon");
    }
}