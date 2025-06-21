using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Enum for different weapon types
/// </summary>
public enum WeaponType
{
    Pistol,
    AssaultRifle,
    Shotgun,
    Sniper,
    SMG
}

/// <summary>
/// Enum for weapon firing modes
/// </summary>
public enum FiringMode
{
    SemiAuto,    // One shot per trigger pull
    FullAuto,    // Continuous fire while held
    Burst        // Multiple shots per trigger pull
}

/// <summary>
/// ScriptableObject that defines weapon properties
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
    [Tooltip("Damage per bullet")]
    [Range(1f, 100f)]
    public float damage = 25f;

    [Tooltip("Maximum range of the weapon")]
    [Range(5f, 200f)]
    public float range = 50f;

    [Tooltip("Rate of fire (shots per minute)")]
    [Range(60f, 1200f)]
    public float fireRate = 300f;

    [Tooltip("Accuracy (1.0 = perfect, 0.0 = completely inaccurate)")]
    [Range(0f, 1f)]
    public float accuracy = 0.85f;

    [Header("Ammunition")]
    [Tooltip("Magazine size")]
    [Range(1, 100)]
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

    [Header("Visual Effects")]
    [Tooltip("Muzzle flash particle effect")]
    public GameObject muzzleFlashParticles;

    [Tooltip("Impact effect for hitting targets")]
    public GameObject impactParticles;

    [Tooltip("Bullet trail effect (optional)")]
    public GameObject bulletTrailPrefab;

    // [Header("Audio Effects")] - COMMENTED OUT FOR NOW
    // [Tooltip("Sound when firing")]
    // public AudioClip fireSound;

    // [Tooltip("Sound when reloading")]
    // public AudioClip reloadSound;

    // [Tooltip("Sound when out of ammo")]
    // public AudioClip emptySound;

    [Header("Weapon Model")]
    [Tooltip("3D model/prefab for the weapon")]
    public GameObject weaponPrefab;

    [Tooltip("Position offset when held by player")]
    public Vector3 holdPosition = new Vector3(0.5f, -0.3f, 0.8f);

    [Tooltip("Rotation offset when held by player")]
    public Vector3 holdRotation = Vector3.zero;

    [Header("ADS (Aim Down Sights)")]
    [Tooltip("Position when aiming down sights")]
    public Vector3 adsPosition = new Vector3(0f, -0.15f, 0.4f);

    [Tooltip("Rotation when aiming down sights")]
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

    [Header("Screen Shake")]
    [Tooltip("Screen shake intensity when firing")]
    [Range(0f, 2f)]
    public float screenShakeIntensity = 0.3f;

    [Tooltip("Screen shake duration when firing")]
    [Range(0.05f, 0.5f)]
    public float screenShakeDuration = 0.1f;

    /// <summary>
    /// Calculate time between shots based on fire rate
    /// </summary>
    public float GetTimeBetweenShots()
    {
        return 60f / fireRate;
    }
}