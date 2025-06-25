using UnityEngine;

[System.Serializable]
public enum ObjectWeight
{
    VeryLight,    // 0-1kg (papers, cups, small tools)
    Light,        // 1-5kg (books, bottles, small boxes)
    Medium,       // 5-15kg (chairs, medium boxes, weapons)
    Heavy,        // 15-30kg (furniture, large boxes, engines)
    VeryHeavy,    // 30-50kg (heavy machinery, big furniture)
    Immovable     // 50kg+ (cannot be grabbed)
}

/// <summary>
/// Component that defines how an object behaves when grabbed.
/// Attach this to any object you want to be grabbable.
/// </summary>
public class GrabbableObject : MonoBehaviour
{
    [Header("Weight Settings")]
    [Tooltip("Override the rigidbody mass for grab calculations")]
    public bool useCustomWeight = false;
    [Tooltip("Custom weight category for this object")]
    public ObjectWeight weightCategory = ObjectWeight.Medium;
    [Tooltip("Exact weight in kg (if using custom weight)")]
    public float customWeight = 5f;

    [Header("Grab Properties")]
    [Tooltip("Can this object be grabbed at all?")]
    public bool isGrabbable = true;
    [Tooltip("Custom spring force multiplier for this specific object")]
    public float springMultiplier = 1f;
    [Tooltip("Custom damper multiplier for this specific object")]
    public float damperMultiplier = 1f;

    // [Header("Audio")]
    // [Tooltip("Sound to play when grabbed")]
    // public AudioClip grabSound;
    // [Tooltip("Sound to play when released")]
    // public AudioClip releaseSound;

    private Rigidbody rb;
    // private AudioSource audioSource;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        // audioSource = GetComponent<AudioSource>();

        // Auto-setup rigidbody mass based on weight category if not using custom
        if (!useCustomWeight)
        {
            SetMassFromWeightCategory();
        }
    }

    /// <summary>
    /// Get the effective weight for grab calculations
    /// </summary>
    public float GetEffectiveWeight()
    {
        if (useCustomWeight)
            return customWeight;

        return rb != null ? rb.mass : GetWeightFromCategory(weightCategory);
    }

    /// <summary>
    /// Get the weight category of this object
    /// </summary>
    public ObjectWeight GetWeightCategory()
    {
        if (useCustomWeight)
            return GetCategoryFromWeight(customWeight);

        return weightCategory;
    }

    /// <summary>
    /// Check if this object can be grabbed
    /// </summary>
    public bool CanBeGrabbed()
    {
        return isGrabbable && GetWeightCategory() != ObjectWeight.Immovable;
    }

    /// <summary>
    /// Called when the object is grabbed
    /// </summary>
    public void OnGrabbed()
    {
        // PlaySound(grabSound);

        // Add any custom grab behavior here
        // Example: Change material, add particle effects, etc.
    }

    /// <summary>
    /// Called when the object is released
    /// </summary>
    public void OnReleased()
    {
        // PlaySound(releaseSound);

        // Add any custom release behavior here
    }

    // private void PlaySound(AudioClip clip)
    // {
    //     if (clip != null && audioSource != null)
    //     {
    //         audioSource.PlayOneShot(clip);
    //     }
    // }

    private void SetMassFromWeightCategory()
    {
        if (rb != null)
        {
            rb.mass = GetWeightFromCategory(weightCategory);
        }
    }

    private float GetWeightFromCategory(ObjectWeight category)
    {
        switch (category)
        {
            case ObjectWeight.VeryLight: return 0.5f;
            case ObjectWeight.Light: return 2.5f;
            case ObjectWeight.Medium: return 10f;
            case ObjectWeight.Heavy: return 22.5f;
            case ObjectWeight.VeryHeavy: return 40f;
            case ObjectWeight.Immovable: return 100f;
            default: return 10f;
        }
    }

    private ObjectWeight GetCategoryFromWeight(float weight)
    {
        if (weight < 1f) return ObjectWeight.VeryLight;
        if (weight < 5f) return ObjectWeight.Light;
        if (weight < 15f) return ObjectWeight.Medium;
        if (weight < 30f) return ObjectWeight.Heavy;
        if (weight < 50f) return ObjectWeight.VeryHeavy;
        return ObjectWeight.Immovable;
    }

    // Editor helper - updates mass in real-time when changing weight category
    private void OnValidate()
    {
        if (Application.isPlaying && !useCustomWeight)
        {
            SetMassFromWeightCategory();
        }
    }
}

/// <summary>
/// Static utility class for weight calculations
/// </summary>
public static class WeightUtility
{
    public static float GetSpringMultiplier(ObjectWeight weight)
    {
        switch (weight)
        {
            case ObjectWeight.VeryLight: return 0.6f;  // Reduced for better control
            case ObjectWeight.Light: return 0.8f;      // Reduced for better control
            case ObjectWeight.Medium: return 1.0f;
            case ObjectWeight.Heavy: return 0.5f;
            case ObjectWeight.VeryHeavy: return 0.3f;
            default: return 0f;
        }
    }

    public static float GetDamperMultiplier(ObjectWeight weight)
    {
        switch (weight)
        {
            case ObjectWeight.VeryLight: return 2.5f;  // High damping for control
            case ObjectWeight.Light: return 2.0f;      // High damping for control
            case ObjectWeight.Medium: return 1.0f;
            case ObjectWeight.Heavy: return 1.5f;
            case ObjectWeight.VeryHeavy: return 2.0f;
            default: return 1.0f;
        }
    }
}