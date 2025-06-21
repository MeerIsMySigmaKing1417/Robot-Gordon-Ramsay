using UnityEngine;

/// <summary>
/// ScriptableObject that defines how an ingredient cooks
/// Create new recipes: Right-click → Create → Cooking → Recipe
/// </summary>
[CreateAssetMenu(fileName = "New Cooking Recipe", menuName = "Cooking/Recipe")]
public class CookingRecipe : ScriptableObject
{
    [Header("Item Identification")]
    [Tooltip("Display name for this ingredient")]
    public string itemName = "New Ingredient";
    
    [Tooltip("Description of the ingredient")]
    [TextArea(2, 4)]
    public string description = "";
    
    [Header("Prefab References")]
    [Tooltip("The raw/initial version of this ingredient")]
    public GameObject rawPrefab;
    
    [Tooltip("The cooked version that replaces the raw ingredient")]
    public GameObject cookedPrefab;
    
    [Tooltip("The burnt version if overcooked (optional)")]
    public GameObject burntPrefab;
    
    [Header("Cooking Timing")]
    [Tooltip("Time in seconds to go from Raw to Cooked")]
    [Range(1f, 60f)]
    public float cookTime = 5f;
    
    [Tooltip("Additional time in seconds to go from Cooked to Burnt")]
    [Range(1f, 30f)]
    public float burnTime = 3f;
    
    [Tooltip("Can this ingredient burn? (if false, stops at cooked)")]
    public bool canBurn = true;
    
    [Header("Visual Effects")]
    [Tooltip("Particle effect to spawn while cooking (continuous)")]
    public GameObject cookingParticles;
    
    [Tooltip("Particle effect for Raw → Cooked transition (quick burst)")]
    public GameObject rawToCookedTransitionParticles;
    
    [Tooltip("Particle effect for Cooked → Burnt transition (quick burst)")]
    public GameObject cookedToBurntTransitionParticles;
    
    [Tooltip("LEGACY: Particle effect to spawn when item finishes cooking (burst)")]
    public GameObject cookedParticles;
    
    [Tooltip("LEGACY: Particle effect to spawn when item burns (burst)")]
    public GameObject burntParticles;
    
    // [Header("Audio")] - COMMENTED OUT FOR NOW
    // [Tooltip("Sound to play while cooking (looping)")]
    // public AudioClip cookingSound;
    
    // [Tooltip("Sound to play when cooking is complete")]
    // public AudioClip cookedSound;
    
    // [Tooltip("Sound to play when item burns")]
    // public AudioClip burntSound;
    
    [Header("Advanced Settings")]
    [Tooltip("Heat sensitivity - higher values cook faster")]
    [Range(0.1f, 3f)]
    public float heatSensitivity = 1f;
    
    [Tooltip("Quality of the cooked result (for scoring/gameplay)")]
    [Range(1, 5)]
    public int cookingQuality = 3;
    
    /// <summary>
    /// Get the appropriate prefab for a given cooking state
    /// </summary>
    public GameObject GetPrefabForState(CookingState state)
    {
        switch (state)
        {
            case CookingState.Raw:
                return rawPrefab;
            case CookingState.Cooked:
                return cookedPrefab;
            case CookingState.Burnt:
                return burntPrefab;
            default:
                return rawPrefab;
        }
    }
    
    /// <summary>
    /// Get the appropriate particles for a cooking transition
    /// </summary>
    public GameObject GetParticlesForTransition(CookingState fromState, CookingState toState)
    {
        // New transition particles (preferred)
        if (fromState == CookingState.Cooking && toState == CookingState.Cooked)
        {
            return rawToCookedTransitionParticles != null ? rawToCookedTransitionParticles : cookedParticles;
        }
        else if (fromState == CookingState.Cooked && toState == CookingState.Burnt)
        {
            return cookedToBurntTransitionParticles != null ? cookedToBurntTransitionParticles : burntParticles;
        }
        else if (toState == CookingState.Cooking)
        {
            return cookingParticles;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get the appropriate sound for a cooking transition - COMMENTED OUT FOR NOW
    /// </summary>
    // public AudioClip GetSoundForTransition(CookingState fromState, CookingState toState)
    // {
    //     if (fromState == CookingState.Cooking && toState == CookingState.Cooked)
    //         return cookedSound;
    //     else if (toState == CookingState.Burnt)
    //         return burntSound;
    //     else if (toState == CookingState.Cooking)
    //         return cookingSound;
    //     
    //     return null;
    // }
}