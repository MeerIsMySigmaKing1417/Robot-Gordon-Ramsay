using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Inspector-friendly cooking states (excludes internal "Cooking" state)
/// </summary>
public enum InspectorCookingState
{
    Raw,        // Initial state - ready to cook
    Cooked,     // Finished cooking - ready to eat or can burn  
    Burnt       // Overcooked - usually bad result
}

/// <summary>
/// Component that goes on any ingredient that can be cooked
/// Handles the cooking state and progress for individual items
/// </summary>
public class CookableItem : MonoBehaviour
{
    [Header("Cooking Setup")]
    [Tooltip("The recipe that defines how this item cooks")]
    public CookingRecipe recipe;

    [Tooltip("Initial cooking state of this item")]
    [SerializeField] private InspectorCookingState inspectorState = InspectorCookingState.Raw;

    [Tooltip("Current cooking state (READ ONLY - managed automatically)")]
    [SerializeField] private CookingState currentState = CookingState.Raw;

    [Header("Cooking Progress")]
    [Tooltip("Current cooking progress (0-1 for each state)")]
    [Range(0f, 2f)]
    public float cookingProgress = 0f;

    [Tooltip("Is this item currently being heated?")]
    public bool isBeingCooked = false;

    [Header("Visual Feedback")]
    [Tooltip("Show cooking progress above the item")]
    public bool showProgressBar = true;

    [Tooltip("Color of progress bar when cooking")]
    public Color cookingColor = Color.yellow;

    [Tooltip("Color of progress bar when cooked")]
    public Color cookedColor = Color.green;

    [Tooltip("Color of progress bar when burning")]
    public Color burningColor = Color.red;

    [Header("Events")]
    [Tooltip("Called when cooking starts")]
    public UnityEvent OnCookingStarted;

    [Tooltip("Called when item finishes cooking")]
    public UnityEvent OnCookingComplete;

    [Tooltip("Called when item burns")]
    public UnityEvent OnItemBurnt;

    [Tooltip("Called when cooking state changes")]
    public UnityEvent<CookingState> OnStateChanged;

    // Private variables
    private GameObject currentParticleEffect;
    private float stateStartTime;

    #region Unity Lifecycle

    void Awake()
    {
        stateStartTime = Time.time;
    }

    void Start()
    {
        // Validate setup
        if (recipe == null)
        {
            Debug.LogWarning($"CookableItem on {gameObject.name} has no recipe assigned!", this);
        }

        // Set initial state from inspector value
        currentState = ConvertInspectorStateToCookingState(inspectorState);
        stateStartTime = Time.time;

        // Initialize based on current state
        UpdateVisualState();
    }

    void Update()
    {
        // Update progress bar if enabled
        if (showProgressBar && isBeingCooked)
        {
            UpdateProgressDisplay();
        }
    }

    #endregion

    #region State Conversion Helpers

    /// <summary>
    /// Convert inspector state to internal cooking state
    /// </summary>
    private CookingState ConvertInspectorStateToCookingState(InspectorCookingState inspectorState)
    {
        switch (inspectorState)
        {
            case InspectorCookingState.Raw:
                return CookingState.Raw;
            case InspectorCookingState.Cooked:
                return CookingState.Cooked;
            case InspectorCookingState.Burnt:
                return CookingState.Burnt;
            default:
                return CookingState.Raw;
        }
    }

    /// <summary>
    /// Convert internal cooking state to inspector state
    /// </summary>
    private InspectorCookingState ConvertCookingStateToInspectorState(CookingState cookingState)
    {
        switch (cookingState)
        {
            case CookingState.Raw:
            case CookingState.Cooking: // Cooking state maps back to Raw for inspector
                return InspectorCookingState.Raw;
            case CookingState.Cooked:
                return InspectorCookingState.Cooked;
            case CookingState.Burnt:
                return InspectorCookingState.Burnt;
            default:
                return InspectorCookingState.Raw;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Start cooking this item with the given heat level
    /// </summary>
    public void StartCooking(float heatLevel = 1f)
    {
        if (recipe == null || currentState == CookingState.Burnt)
            return;

        // Start cooking if raw
        if (currentState == CookingState.Raw)
        {
            ChangeState(CookingState.Cooking);
            OnCookingStarted?.Invoke();
        }

        isBeingCooked = true;

        // Apply heat to cooking progress
        float progressRate = heatLevel * recipe.heatSensitivity;

        if (currentState == CookingState.Cooking)
        {
            // Normal cooking process
            cookingProgress += Time.deltaTime * progressRate / recipe.cookTime;

            // Check if finished cooking
            if (cookingProgress >= 1f)
            {
                FinishCooking();
            }
        }
        else if (currentState == CookingState.Cooked && recipe.canBurn)
        {
            // Continue cooking = burning (progress goes from 1f to 2f)
            cookingProgress += Time.deltaTime * progressRate / recipe.burnTime;

            // Check if burnt
            if (cookingProgress >= 2f)
            {
                BurnItem();
            }
        }
    }

    /// <summary>
    /// Stop cooking this item
    /// </summary>
    public void StopCooking()
    {
        isBeingCooked = false;

        // Stop cooking particles
        if (currentParticleEffect != null && currentState == CookingState.Cooking)
        {
            Destroy(currentParticleEffect);
            currentParticleEffect = null;
        }
    }

    /// <summary>
    /// Get the cooking progress as a percentage (0-100)
    /// </summary>
    public float GetCookingProgressPercent()
    {
        if (currentState == CookingState.Raw)
            return 0f;
        else if (currentState == CookingState.Cooking)
            return cookingProgress * 100f;
        else if (currentState == CookingState.Cooked)
            return 100f;
        else if (currentState == CookingState.Burnt)
            return 100f;

        return 0f;
    }

    /// <summary>
    /// Get the current cooking state (read-only access)
    /// </summary>
    public CookingState GetCurrentState()
    {
        return currentState;
    }

    /// <summary>
    /// Set the inspector state (for prefab setup)
    /// </summary>
    public void SetInspectorState(InspectorCookingState newState)
    {
        inspectorState = newState;
        currentState = ConvertInspectorStateToCookingState(newState);
        UpdateVisualState();
    }

    /// <summary>
    /// Check if this item is perfectly cooked
    /// </summary>
    public bool IsPerfectlyCooked()
    {
        return currentState == CookingState.Cooked;
    }

    /// <summary>
    /// Force set the cooking state (useful for testing)
    /// </summary>
    public void SetState(CookingState newState)
    {
        ChangeState(newState);

        // Update inspector state to match (if not cooking)
        if (newState != CookingState.Cooking)
        {
            inspectorState = ConvertCookingStateToInspectorState(newState);
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Complete the cooking process
    /// </summary>
    private void FinishCooking()
    {
        cookingProgress = 1f;
        ChangeState(CookingState.Cooked);
        OnCookingComplete?.Invoke();

        // Spawn cooked particles
        SpawnTransitionParticles(CookingState.Cooking, CookingState.Cooked);
    }

    /// <summary>
    /// Burn the item from overcooking
    /// </summary>
    private void BurnItem()
    {
        cookingProgress = 2f;
        ChangeState(CookingState.Burnt);
        OnItemBurnt?.Invoke();

        // Spawn burnt particles
        SpawnTransitionParticles(CookingState.Cooked, CookingState.Burnt);
    }

    /// <summary>
    /// Change the cooking state and update visuals
    /// </summary>
    private void ChangeState(CookingState newState)
    {
        CookingState oldState = currentState;
        currentState = newState;
        stateStartTime = Time.time;

        UpdateVisualState();
        OnStateChanged?.Invoke(newState);

        Debug.Log($"{gameObject.name} changed from {oldState} to {newState}");
    }

    /// <summary>
    /// Update visual appearance based on state
    /// </summary>
    private void UpdateVisualState()
    {
        if (currentState == CookingState.Cooking)
        {
            StartCookingEffects();
        }
        else
        {
            StopCookingEffects();
        }
    }

    /// <summary>
    /// Start visual effects for cooking
    /// </summary>
    private void StartCookingEffects()
    {
        if (recipe == null)
        {
            Debug.Log("StartCookingEffects: No recipe found!");
            return;
        }

        Debug.Log($"StartCookingEffects called for {gameObject.name}");
        Debug.Log($"Recipe: {recipe.name}");
        Debug.Log($"Recipe.cookingParticles: {(recipe.cookingParticles != null ? recipe.cookingParticles.name : "NULL")}");
        Debug.Log($"currentParticleEffect: {(currentParticleEffect != null ? "Already exists" : "NULL")}");

        // DEBUG RAYS - Remove these after testing
        Debug.DrawRay(transform.position, Vector3.up * 2f, Color.green, 5f);
        Debug.DrawRay(transform.position, transform.up * 2f, Color.red, 5f);

        // Start cooking particles
        if (recipe.cookingParticles != null && currentParticleEffect == null)
        {
            Debug.Log("Attempting to instantiate particle system...");

            // PRESERVE the particle prefab's original rotation (-90 on X axis)
            currentParticleEffect = Instantiate(recipe.cookingParticles, transform.position, recipe.cookingParticles.transform.rotation);

            if (currentParticleEffect == null)
            {
                Debug.LogError("Failed to instantiate particle system!");
                return;
            }

            Debug.Log($"Successfully created particle effect: {currentParticleEffect.name}");
            Debug.Log($"Particle rotation: {currentParticleEffect.transform.rotation.eulerAngles}");

            // DON'T parent it to avoid inheriting food item rotation
            // currentParticleEffect.transform.SetParent(transform);

            // Just position it at the food location but keep its original rotation
            currentParticleEffect.transform.position = transform.position;

            Debug.Log("Particle system spawned with preserved prefab rotation");
        }
        else
        {
            if (recipe.cookingParticles == null)
                Debug.Log("No cooking particles assigned to recipe!");
            if (currentParticleEffect != null)
                Debug.Log("Particle effect already exists, not creating new one");
        }
    }

    /// <summary>
    /// Stop visual effects for cooking
    /// </summary>
    private void StopCookingEffects()
    {
        // Stop particles
        if (currentParticleEffect != null)
        {
            Destroy(currentParticleEffect);
            currentParticleEffect = null;
        }
    }

    /// <summary>
    /// Spawn transition particles for state changes
    /// </summary>
    private void SpawnTransitionParticles(CookingState fromState, CookingState toState)
    {
        if (recipe == null) return;

        GameObject particlesToSpawn = recipe.GetParticlesForTransition(fromState, toState);
        if (particlesToSpawn != null)
        {
            GameObject particles = Instantiate(particlesToSpawn, transform.position, Quaternion.identity);
            // Auto-destroy particles after 5 seconds
            Destroy(particles, 5f);
        }
    }

    /// <summary>
    /// Update progress bar visualization
    /// </summary>
    private void UpdateProgressDisplay()
    {
        // Determine progress color based on state
        Color progressColor = cookingColor;
        if (currentState == CookingState.Cooked)
            progressColor = cookedColor;
        else if (currentState == CookingState.Cooking && cookingProgress > 0.8f)
            progressColor = Color.Lerp(cookingColor, burningColor, (cookingProgress - 0.8f) * 5f);

        // Draw progress in scene view (for debugging)
        Debug.DrawRay(transform.position + Vector3.up * 2f, Vector3.right * cookingProgress, progressColor);
    }

    #endregion

    #region Debug Gizmos

    void OnDrawGizmosSelected()
    {
        // Draw cooking progress as a bar above the object
        if (Application.isPlaying && showProgressBar)
        {
            Vector3 barPosition = transform.position + Vector3.up * 2f;

            // Background bar
            Gizmos.color = Color.grey;
            Gizmos.DrawWireCube(barPosition, new Vector3(2f, 0.1f, 0.1f));

            // Progress bar
            if (currentState == CookingState.Cooking)
                Gizmos.color = cookingColor;
            else if (currentState == CookingState.Cooked)
                Gizmos.color = cookedColor;
            else if (currentState == CookingState.Burnt)
                Gizmos.color = burningColor;

            float progressWidth = 2f * (cookingProgress / (currentState == CookingState.Cooked && recipe.canBurn ? 2f : 1f));
            Gizmos.DrawCube(barPosition + Vector3.left * (2f - progressWidth) * 0.5f, new Vector3(progressWidth, 0.1f, 0.1f));
        }
    }

    #endregion
}