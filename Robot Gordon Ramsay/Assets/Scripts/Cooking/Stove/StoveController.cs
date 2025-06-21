using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Main stove controller that detects and cooks ingredients
/// Place this on your stove GameObject
/// </summary>
public class StoveController : MonoBehaviour
{
    [Header("Stove Settings")]
    [Tooltip("Is the stove currently turned on?")]
    public bool isStoveOn = false;
    
    [Tooltip("Heat level of the stove (affects cooking speed) - SIMPLIFIED: Always 1.0")]
    [Range(0.1f, 3f)]
    public float heatLevel = 1f;
    
    [Tooltip("Can the player manually control the stove?")]
    public bool playerControllable = true;
    
    [Header("Detection Settings")]
    [Tooltip("Center point for ingredient detection (usually above stove surface)")]
    public Transform detectionCenter;
    
    [Tooltip("Radius of the detection sphere")]
    [Range(0.5f, 5f)]
    public float detectionRadius = 2f;
    
    [Tooltip("Height above detection center to check (prevents ground items from cooking)")]
    [Range(0.1f, 2f)]
    public float detectionHeight = 0.5f;
    
    [Tooltip("Only detect items above this Y position relative to detection center")]
    [Range(-1f, 2f)]
    public float minimumHeightOffset = 0.1f;
    
    [Tooltip("Layer mask for cookable items")]
    public LayerMask cookableLayer = -1;
    
    [Tooltip("How often to check for new items (in seconds)")]
    [Range(0.1f, 2f)]
    public float detectionInterval = 0.5f;
    
    [Header("Visual Feedback")]
    [Tooltip("Show detection sphere in scene view")]
    public bool showDetectionGizmo = true;
    
    [Tooltip("Particle effect when stove is turned on")]
    public GameObject stoveOnParticles;
    
    [Tooltip("Material to use when stove is on (optional)")]
    public Material stoveOnMaterial;
    
    [Tooltip("Material to use when stove is off (optional)")]
    public Material stoveOffMaterial;
    
    [Header("Player Detection")]
    [Tooltip("Radius to detect player for interaction")]
    [Range(1f, 10f)]
    public float playerDetectionRadius = 3f;
    
    [Tooltip("How often to check for player (in seconds)")]
    [Range(0.1f, 1f)]
    public float playerCheckInterval = 0.2f;
    
    [Tooltip("Layer mask for player objects")]
    public LayerMask playerLayer = -1;
    
    [Tooltip("Is the player nearby and can interact?")]
    [SerializeField] private bool playerNearby = false;
    
    // Public property to access player nearby status
    public bool PlayerNearby => playerNearby;
    
    // [Header("Audio")] - COMMENTED OUT FOR NOW
    // [Tooltip("Sound when stove turns on")]
    // public AudioClip stoveOnSound;
    
    // [Tooltip("Sound when stove turns off")]
    // public AudioClip stoveOffSound;
    
    // [Tooltip("Ambient stove sound while on (looping)")]
    // public AudioClip stoveAmbientSound;
    
    [Header("Events")]
    [Tooltip("Called when stove is turned on")]
    public UnityEvent OnStoveOn;
    
    [Tooltip("Called when stove is turned off")]
    public UnityEvent OnStoveOff;
    
    [Tooltip("Called when a new item is placed on the stove")]
    public UnityEvent<CookableItem> OnItemPlaced;
    
    [Tooltip("Called when an item is removed from the stove")]
    public UnityEvent<CookableItem> OnItemRemoved;
    
    [Tooltip("Called when an item finishes cooking")]
    public UnityEvent<CookableItem> OnItemCooked;
    
    [Tooltip("Called when an item burns")]
    public UnityEvent<CookableItem> OnItemBurnt;
    
    [Header("Transition Effects")]
    [Tooltip("How long transition particles last before auto-destroying")]
    [Range(0.5f, 10f)]
    public float transitionParticleDuration = 1.5f;
    
    [Tooltip("Delay before spawning new item (lets particles be visible)")]
    [Range(0.1f, 1f)]
    public float transformationDelay = 0.2f;
    
    // Private variables
    private List<CookableItem> currentItems = new List<CookableItem>();
    private List<CookableItem> previousItems = new List<CookableItem>();
    // private AudioSource audioSource; - COMMENTED OUT FOR NOW
    private GameObject activeParticleEffect;
    private Renderer stoveRenderer;
    private float lastDetectionTime;
    private float lastPlayerCheckTime;
    
    // Track which items we're subscribed to (prevent double subscription)
    private HashSet<CookableItem> subscribedItems = new HashSet<CookableItem>();
    
    // Prevent multiple transformations of the same item
    private HashSet<CookableItem> transformingItems = new HashSet<CookableItem>();
    
    #region Unity Lifecycle
    
    void Awake()
    {
        // Setup audio source - COMMENTED OUT FOR NOW
        // audioSource = GetComponent<AudioSource>();
        // if (audioSource == null)
        // {
        //     audioSource = gameObject.AddComponent<AudioSource>();
        //     audioSource.playOnAwake = false;
        //     audioSource.spatialBlend = 1f; // 3D sound
        // }
        
        // Get renderer for material changes
        stoveRenderer = GetComponent<Renderer>();
        
        // Setup detection center if not assigned
        if (detectionCenter == null)
        {
            detectionCenter = transform;
        }
    }
    
    void Start()
    {
        // Initialize stove state
        UpdateStoveVisuals();
        
        // Start detection coroutine
        InvokeRepeating(nameof(DetectCookableItems), 0f, detectionInterval);
        
        // Subscribe to cooking events for all items
        SubscribeToCookingEvents();
    }
  
    void Update()
    {
        // Cook all items if stove is on
        if (isStoveOn)
        {
            CookAllItems();
        }
    }
    
    void OnDestroy()
    {
        // Clean up
        CancelInvoke();
        UnsubscribeFromCookingEvents();
    }
    
    #endregion
    
    #region Item Transformation System
    
    /// <summary>
    /// Transform an item to its cooked state (called when cooking finishes)
    /// </summary>
    private void TransformItemToCooked(CookableItem rawItem)
    {
        if (rawItem == null || rawItem.recipe == null || rawItem.recipe.cookedPrefab == null)
            return;
        
        // Prevent multiple transformations of the same item
        if (transformingItems.Contains(rawItem))
        {
            Debug.Log($"Item {rawItem.recipe.itemName} is already transforming, skipping duplicate transformation");
            return;
        }
        
        transformingItems.Add(rawItem);
        StartCoroutine(TransformItemCoroutine(rawItem, rawItem.recipe.cookedPrefab, CookingState.Cooked));
    }
    
    /// <summary>
    /// Transform an item to its burnt state (called when item burns)
    /// </summary>
    private void TransformItemToBurnt(CookableItem cookedItem)
    {
        if (cookedItem == null || cookedItem.recipe == null || cookedItem.recipe.burntPrefab == null)
            return;
        
        // Prevent multiple transformations of the same item
        if (transformingItems.Contains(cookedItem))
        {
            Debug.Log($"Item {cookedItem.recipe.itemName} is already transforming, skipping duplicate transformation");
            return;
        }
        
        transformingItems.Add(cookedItem);
        StartCoroutine(TransformItemCoroutine(cookedItem, cookedItem.recipe.burntPrefab, CookingState.Burnt));
    }
    
    /// <summary>
    /// Coroutine that handles the actual transformation with particles
    /// </summary>
    private System.Collections.IEnumerator TransformItemCoroutine(CookableItem oldItem, GameObject newPrefab, CookingState newState)
    {
        Vector3 position = oldItem.transform.position;
        Quaternion rotation = oldItem.transform.rotation;
        CookingRecipe recipe = oldItem.recipe;
        
        // Spawn transformation particles based on the transition
        GameObject transitionParticles = null;
        if (newState == CookingState.Cooked)
        {
            // Raw → Cooked transition
            if (recipe.rawToCookedTransitionParticles != null)
            {
                transitionParticles = Instantiate(recipe.rawToCookedTransitionParticles, position, recipe.rawToCookedTransitionParticles.transform.rotation);
            }
            else if (recipe.cookedParticles != null) // Fallback to legacy
            {
                transitionParticles = Instantiate(recipe.cookedParticles, position, recipe.rawToCookedTransitionParticles.transform.rotation);
            }
        }
        else if (newState == CookingState.Burnt)
        {
            // Cooked → Burnt transition
            if (recipe.cookedToBurntTransitionParticles != null)
            {
                transitionParticles = Instantiate(recipe.cookedToBurntTransitionParticles, position, Quaternion.identity);
            }
            else if (recipe.burntParticles != null) // Fallback to legacy
            {
                transitionParticles = Instantiate(recipe.burntParticles, position, Quaternion.identity);
            }
        }
        
        // Auto-destroy transition particles based on configurable duration
        if (transitionParticles != null)
        {
            Destroy(transitionParticles, transitionParticleDuration);
        }
        
        // Configurable delay for particle effect to be visible
        yield return new WaitForSeconds(transformationDelay);
        
        // Remove old item from tracking
        if (currentItems.Contains(oldItem))
        {
            currentItems.Remove(oldItem);
        }
        UnsubscribeFromItemEvents(oldItem);
        
        // Destroy old item
        Destroy(oldItem.gameObject);
        
        // Instantiate new item
        GameObject newItemObject = Instantiate(newPrefab, position, rotation);
        CookableItem newItem = newItemObject.GetComponent<CookableItem>();
        
        if (newItem != null)
        {
            // Set up the new item
            newItem.recipe = recipe;
            newItem.SetState(newState); // Use SetState method instead of direct access
            newItem.cookingProgress = (newState == CookingState.Cooked) ? 1f : 2f;
            
            // Add to tracking and subscribe to events
            currentItems.Add(newItem);
            SubscribeToItemEvents(newItem);
            
            Debug.Log($"Transformed {recipe.itemName} to {newState} state with transition particles");
        }
    }
    
    #endregion
    
    #region Event Management
    
    /// <summary>
    /// Subscribe to cooking events for all items
    /// </summary>
    private void SubscribeToCookingEvents()
    {
        // This sets up global event handling
        OnItemCooked.AddListener(TransformItemToCooked);
        OnItemBurnt.AddListener(TransformItemToBurnt);
    }
    
    /// <summary>
    /// Unsubscribe from cooking events
    /// </summary>
    private void UnsubscribeFromCookingEvents()
    {
        OnItemCooked.RemoveListener(TransformItemToCooked);
        OnItemBurnt.RemoveListener(TransformItemToBurnt);
    }
    
    /// <summary>
    /// Subscribe to a specific item's events (with duplicate prevention)
    /// </summary>
    private void SubscribeToItemEvents(CookableItem item)
    {
        if (item != null && !subscribedItems.Contains(item))
        {
            item.OnCookingComplete.AddListener(() => OnItemCooked?.Invoke(item));
            item.OnItemBurnt.AddListener(() => OnItemBurnt?.Invoke(item));
            subscribedItems.Add(item);
            Debug.Log($"Subscribed to events for {item.recipe?.itemName}");
        }
    }
    
    /// <summary>
    /// Unsubscribe from a specific item's events
    /// </summary>
    private void UnsubscribeFromItemEvents(CookableItem item)
    {
        if (item != null && subscribedItems.Contains(item))
        {
            item.OnCookingComplete.RemoveListener(() => OnItemCooked?.Invoke(item));
            item.OnItemBurnt.RemoveListener(() => OnItemBurnt?.Invoke(item));
            subscribedItems.Remove(item);
            Debug.Log($"Unsubscribed from events for {item.recipe?.itemName}");
        }
    }
    
    /// <summary>
    /// Ensure an item is subscribed (but don't double-subscribe)
    /// </summary>
    private void EnsureItemSubscription(CookableItem item)
    {
        if (item != null && !subscribedItems.Contains(item))
        {
            SubscribeToItemEvents(item);
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Turn the stove on
    /// </summary>
    public void TurnOn()
    {
        if (!isStoveOn)
        {
            isStoveOn = true;
            UpdateStoveVisuals();
            // PlayStoveSound(stoveOnSound); - COMMENTED OUT FOR NOW
            OnStoveOn?.Invoke();
            
            Debug.Log($"{gameObject.name} turned ON");
        }
    }
    
    /// <summary>
    /// Turn the stove off
    /// </summary>
    public void TurnOff()
    {
        if (isStoveOn)
        {
            isStoveOn = false;
            UpdateStoveVisuals();
            // PlayStoveSound(stoveOffSound); - COMMENTED OUT FOR NOW
            
            // Stop cooking all items
            foreach (CookableItem item in currentItems)
            {
                item.StopCooking();
            }
            
            OnStoveOff?.Invoke();
            
            Debug.Log($"{gameObject.name} turned OFF");
        }
    }
    
    /// <summary>
    /// Toggle stove on/off
    /// </summary>
    public void ToggleStove()
    {
        if (isStoveOn)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }
    
    /// <summary>
    /// Set the heat level of the stove
    /// </summary>
    public void SetHeatLevel(float newHeatLevel)
    {
        heatLevel = Mathf.Clamp(newHeatLevel, 0.1f, 3f);
        Debug.Log($"{gameObject.name} heat level set to {heatLevel:F1}");
    }
    
    /// <summary>
    /// Get all items currently on the stove
    /// </summary>
    public List<CookableItem> GetItemsOnStove()
    {
        return new List<CookableItem>(currentItems);
    }
    
    /// <summary>
    /// Check if a specific item is on this stove
    /// </summary>
    public bool IsItemOnStove(CookableItem item)
    {
        return currentItems.Contains(item);
    }
    
    /// <summary>
    /// Manually add an item to the stove (useful for scripted events)
    /// </summary>
    public void AddItem(CookableItem item)
    {
        if (!currentItems.Contains(item))
        {
            currentItems.Add(item);
            OnItemPlaced?.Invoke(item);
        }
    }
    
    /// <summary>
    /// Manually remove an item from the stove
    /// </summary>
    public void RemoveItem(CookableItem item)
    {
        if (currentItems.Contains(item))
        {
            currentItems.Remove(item);
            item.StopCooking();
            OnItemRemoved?.Invoke(item);
        }
    }
    
    private void DetectCookableItems()
    {
        // Store previous frame's items
        previousItems.Clear();
        previousItems.AddRange(currentItems);
        
        // Clear current items list
        currentItems.Clear();
        
        // Use sphere detection to find all colliders in range
        Collider[] colliders = Physics.OverlapSphere(detectionCenter.position, detectionRadius, cookableLayer);
        
        // Check each collider for CookableItem component
        foreach (Collider col in colliders)
        {
            CookableItem cookable = col.GetComponent<CookableItem>();
            if (cookable != null && cookable.recipe != null)
            {
                // ADDITIONAL CHECK: Only accept items that are ON TOP of the stove
                if (IsItemOnStoveSurface(cookable.transform))
                {
                    currentItems.Add(cookable);
                    
                    // Check if this is a new item
                    if (!previousItems.Contains(cookable))
                    {
                        // Subscribe to this item's cooking events
                        SubscribeToItemEvents(cookable);
                        
                        OnItemPlaced?.Invoke(cookable);
                        Debug.Log($"New item placed on {gameObject.name}: {cookable.recipe.itemName}");
                    }
                }
            }
        }
        
        // Check for removed items
        foreach (CookableItem previousItem in previousItems)
        {
            if (!currentItems.Contains(previousItem))
            {
                // Unsubscribe from this item's events
                UnsubscribeFromItemEvents(previousItem);
                
                previousItem.StopCooking();
                OnItemRemoved?.Invoke(previousItem);
                Debug.Log($"Item removed from {gameObject.name}: {previousItem.recipe.itemName}");
            }
        }
        
        lastDetectionTime = Time.time;
    }
    
    /// <summary>
    /// Check if an item is actually ON the stove surface (not just nearby)
    /// </summary>
    private bool IsItemOnStoveSurface(Transform itemTransform)
    {
        Vector3 detectionPos = detectionCenter.position;
        Vector3 itemPos = itemTransform.position;
        
        // Check if item is above the minimum height threshold
        float heightDifference = itemPos.y - detectionPos.y;
        if (heightDifference < minimumHeightOffset)
        {
            return false; // Item is too low (on ground, not on stove)
        }
        
        // Check if item is within reasonable height above stove
        if (heightDifference > detectionHeight)
        {
            return false; // Item is too high (floating above stove)
        }
        
        // Check horizontal distance (already covered by sphere detection, but can be refined)
        float horizontalDistance = Vector3.Distance(
            new Vector3(detectionPos.x, 0, detectionPos.z),
            new Vector3(itemPos.x, 0, itemPos.z)
        );
        
        // Use a slightly smaller horizontal radius for more precise detection
        float preciseRadius = detectionRadius * 0.8f;
        if (horizontalDistance > preciseRadius)
        {
            return false; // Item is too far horizontally
        }
        
        return true; // Item is properly positioned on stove
    }
    
    private void CookAllItems()
    {
        foreach (CookableItem item in currentItems)
        {
            if (item != null && item.recipe != null)
            {
                // Debug cooking progress to help identify issues
                Debug.Log($"Cooking {item.recipe.itemName}: State={item.GetCurrentState()}, Progress={item.cookingProgress:F2}, CanBurn={item.recipe.canBurn}");
                
                item.StartCooking(heatLevel);
            }
        }
    }

    private void UpdateStoveVisuals()
    {
        // Update particle effects
        if (isStoveOn && stoveOnParticles != null && activeParticleEffect == null)
        {
            // FIX: Preserve the particle prefab's original rotation instead of using Quaternion.identity
            activeParticleEffect = Instantiate(stoveOnParticles, transform.position, stoveOnParticles.transform.rotation);
            activeParticleEffect.transform.SetParent(transform);
        }
        else if (!isStoveOn && activeParticleEffect != null)
        {
            Destroy(activeParticleEffect);
            activeParticleEffect = null;
        }

        // Update materials
        if (stoveRenderer != null)
        {
            if (isStoveOn && stoveOnMaterial != null)
            {
                stoveRenderer.material = stoveOnMaterial;
            }
            else if (!isStoveOn && stoveOffMaterial != null)
            {
                stoveRenderer.material = stoveOffMaterial;
            }
        }

        // Update ambient sound - COMMENTED OUT FOR NOW
        // if (isStoveOn && stoveAmbientSound != null && !audioSource.isPlaying)
        // {
        //     audioSource.clip = stoveAmbientSound;
        //     audioSource.loop = true;
        //     audioSource.Play();
        // }
        // else if (!isStoveOn && audioSource.isPlaying && audioSource.loop)
        // {
        //     audioSource.Stop();
        // }
    }

    // private void PlayStoveSound(AudioClip clip) - COMMENTED OUT FOR NOW
    // {
    //     if (clip != null && audioSource != null)
    //     {
    //         audioSource.PlayOneShot(clip);
    //     }
    // }

    #endregion

    #region Gizmos and Debug

    void OnDrawGizmos()
    {
        if (showDetectionGizmo && detectionCenter != null)
        {
            Vector3 center = detectionCenter.position;
            
            // Draw main detection sphere for ingredients
            Gizmos.color = isStoveOn ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(center, detectionRadius);
            
            // Draw player detection sphere
            Gizmos.color = playerNearby ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, playerDetectionRadius);
            
            // Draw height detection zone for ingredients
            Gizmos.color = Color.cyan;
            // Bottom plane (minimum height)
            Vector3 bottomCenter = center + Vector3.up * minimumHeightOffset;
            DrawCircle(bottomCenter, detectionRadius * 0.8f, Vector3.up);
            
            // Top plane (maximum height)
            Vector3 topCenter = center + Vector3.up * detectionHeight;
            DrawCircle(topCenter, detectionRadius * 0.8f, Vector3.up);
            
            // Side walls of cooking zone
            Gizmos.color = Color.green;
            float preciseRadius = detectionRadius * 0.8f;
            Gizmos.DrawWireCube(
                center + Vector3.up * (minimumHeightOffset + detectionHeight) * 0.5f,
                new Vector3(preciseRadius * 2f, detectionHeight - minimumHeightOffset, preciseRadius * 2f)
            );
            
            // Draw items currently on stove
            if (Application.isPlaying)
            {
                Gizmos.color = Color.green;
                foreach (CookableItem item in currentItems)
                {
                    if (item != null)
                    {
                        Gizmos.DrawLine(center, item.transform.position);
                        Gizmos.DrawWireSphere(item.transform.position, 0.2f);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Helper method to draw circles in gizmos
    /// </summary>
    private void DrawCircle(Vector3 center, float radius, Vector3 normal)
    {
        Vector3 forward = Vector3.Cross(normal, Vector3.right);
        if (forward.magnitude < 0.1f)
            forward = Vector3.Cross(normal, Vector3.up);
        
        Vector3 right = Vector3.Cross(forward, normal);
        
        Vector3 prevPoint = center + forward * radius;
        for (int i = 1; i <= 32; i++)
        {
            float angle = i * Mathf.PI * 2f / 32f;
            Vector3 newPoint = center + (Mathf.Cos(angle) * forward + Mathf.Sin(angle) * right) * radius;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (detectionCenter != null)
        {
            // Draw more detailed info when selected
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(detectionCenter.position, 0.1f);
            
            // Draw heat level as rings
            for (int i = 1; i <= 3; i++)
            {
                float alpha = (heatLevel >= i) ? 1f : 0.3f;
                Gizmos.color = new Color(1f, 0.5f, 0f, alpha);
                Gizmos.DrawWireSphere(detectionCenter.position, detectionRadius * 0.3f * i);
            }
        }
    }
    
    #endregion
}