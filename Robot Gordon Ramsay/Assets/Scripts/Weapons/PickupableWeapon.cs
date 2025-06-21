using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Component for weapons that can be picked up from the ground
/// Attach this to weapon prefabs that should be collectible
/// </summary>
public class PickupableWeapon : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Weapon data for this pickupable weapon")]
    public WeaponData weaponData;

    [Tooltip("Current ammo in this weapon when picked up")]
    [Range(0, 100)]
    public int currentAmmo = 0;

    [Tooltip("Reserve ammo that comes with this weapon")]
    [Range(0, 500)]
    public int reserveAmmo = 60;

    [Tooltip("Can this weapon be picked up?")]
    public bool canBePickedUp = true;

    [Header("Visual Feedback")]
    [Tooltip("Highlight material when player is nearby")]
    public Material highlightMaterial;

    [Tooltip("Pickup effect to spawn when collected")]
    public GameObject pickupEffect;

    [Tooltip("Should the weapon bob up and down?")]
    public bool enableBobbing = true;

    [Tooltip("Bobbing speed")]
    [Range(0.5f, 5f)]
    public float bobbingSpeed = 1f;

    [Tooltip("Bobbing height")]
    [Range(0.1f, 1f)]
    public float bobbingHeight = 0.3f;

    [Header("Rotation")]
    [Tooltip("Should the weapon rotate slowly?")]
    public bool enableRotation = true;

    [Tooltip("Rotation speed (degrees per second)")]
    [Range(10f, 180f)]
    public float rotationSpeed = 45f;

    // [Header("Audio")] - COMMENTED OUT FOR NOW
    // [Tooltip("Sound when picked up")]
    // public AudioClip pickupSound;

    [Header("Events")]
    [Tooltip("Called when weapon is picked up")]
    public UnityEvent<WeaponData> OnWeaponPickedUp;

    [Tooltip("Called when player enters pickup range")]
    public UnityEvent OnPlayerEnterRange;

    [Tooltip("Called when player exits pickup range")]
    public UnityEvent OnPlayerExitRange;

    // Private variables
    private Vector3 startPosition;
    private Renderer weaponRenderer;
    private Material originalMaterial;
    private InteractableComponent interactableComponent;
    // private AudioSource audioSource; - COMMENTED OUT FOR NOW
    // Removed playerInRange as it's not being used

    #region Unity Lifecycle

    void Awake()
    {
        // Get renderer for highlight effect
        weaponRenderer = GetComponent<Renderer>();
        if (weaponRenderer != null)
        {
            originalMaterial = weaponRenderer.material;
        }

        // Setup audio - COMMENTED OUT FOR NOW
        // audioSource = GetComponent<AudioSource>();
        // if (audioSource == null)
        // {
        //     audioSource = gameObject.AddComponent<AudioSource>();
        //     audioSource.playOnAwake = false;
        //     audioSource.spatialBlend = 1f; // 3D sound
        // }

        // Get or add InteractableComponent
        interactableComponent = GetComponent<InteractableComponent>();
        if (interactableComponent == null)
        {
            interactableComponent = gameObject.AddComponent<InteractableComponent>();
            SetupInteractableComponent();
        }
    }

    void Start()
    {
        // Store starting position for bobbing
        startPosition = transform.position;

        // Validate setup
        if (weaponData == null)
        {
            Debug.LogWarning($"PickupableWeapon on {gameObject.name} has no WeaponData assigned!", this);
        }

        // Initialize ammo if not set
        if (currentAmmo == 0 && weaponData != null)
        {
            currentAmmo = weaponData.magazineSize;
        }

        if (reserveAmmo == 0 && weaponData != null)
        {
            reserveAmmo = weaponData.maxAmmo / 2; // Start with half max ammo
        }

        // Setup interaction events safely
        if (interactableComponent != null)
        {
            try
            {
                // Check if the component is valid before subscribing
                if (interactableComponent.OnInteractionComplete != null)
                    interactableComponent.OnInteractionComplete.AddListener(OnPickupInteraction);

                if (interactableComponent.OnHoverEnter != null)
                    interactableComponent.OnHoverEnter.AddListener(OnPlayerEnterPickupRange);

                if (interactableComponent.OnHoverExit != null)
                    interactableComponent.OnHoverExit.AddListener(OnPlayerExitPickupRange);

                Debug.Log($"Successfully setup events for {gameObject.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error setting up PickupableWeapon events: {e.Message}", this);
            }
        }
        else
        {
            Debug.LogWarning($"PickupableWeapon on {gameObject.name} has no InteractableComponent!", this);
        }
    }

    void Update()
    {
        if (!canBePickedUp)
            return;

        // Handle bobbing animation
        if (enableBobbing)
        {
            UpdateBobbing();
        }

        // Handle rotation animation
        if (enableRotation)
        {
            UpdateRotation();
        }
    }

    void OnDestroy()
    {
        // Clean up event subscriptions safely
        if (interactableComponent != null)
        {
            try
            {
                // Check if events exist before unsubscribing
                if (interactableComponent.OnInteractionComplete != null)
                    interactableComponent.OnInteractionComplete.RemoveListener(OnPickupInteraction);

                if (interactableComponent.OnHoverEnter != null)
                    interactableComponent.OnHoverEnter.RemoveListener(OnPlayerEnterPickupRange);

                if (interactableComponent.OnHoverExit != null)
                    interactableComponent.OnHoverExit.RemoveListener(OnPlayerExitPickupRange);
            }
            catch (System.Exception)
            {
                // Silently handle cleanup errors during destruction
                // Don't log errors during destruction as objects may be partially destroyed
            }
        }
    }

    #endregion

    #region Setup Methods

    /// <summary>
    /// Setup the InteractableComponent with appropriate settings
    /// </summary>
    private void SetupInteractableComponent()
    {
        if (interactableComponent != null && weaponData != null)
        {
            interactableComponent.interactionText = $"Press E to pickup {weaponData.weaponName}";
            interactableComponent.interactionRange = 3f;
            interactableComponent.highlightColor = Color.cyan;
        }
    }

    #endregion

    #region Interaction Handlers

    /// <summary>
    /// Called when player interacts with the weapon (E key)
    /// </summary>
    private void OnPickupInteraction()
    {
        Debug.Log($"OnPickupInteraction called for {gameObject.name}");

        if (!canBePickedUp)
        {
            Debug.Log("Cannot be picked up!");
            return;
        }

        // Find the player's weapon manager
        WeaponManager playerWeaponManager = FindPlayerWeaponManager();

        if (playerWeaponManager != null)
        {
            Debug.Log("Found WeaponManager, attempting pickup...");
            // Try to pickup the weapon
            if (playerWeaponManager.TryPickupWeapon(this))
            {
                PerformPickup();
            }
            else
            {
                Debug.Log("TryPickupWeapon returned false");
            }
        }
        else
        {
            Debug.LogWarning("Could not find WeaponManager on player!");
        }
    }

    /// <summary>
    /// Called when player enters pickup range
    /// </summary>
    private void OnPlayerEnterPickupRange()
    {
        if (!canBePickedUp)
            return;

        ApplyHighlight(true);
        OnPlayerEnterRange?.Invoke();

        Debug.Log($"Player entered range of {gameObject.name}");
    }

    /// <summary>
    /// Called when player exits pickup range
    /// </summary>
    private void OnPlayerExitPickupRange()
    {
        ApplyHighlight(false);
        OnPlayerExitRange?.Invoke();

        Debug.Log($"Player exited range of {gameObject.name}");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Perform the pickup (called by WeaponManager)
    /// </summary>
    public void PerformPickup()
    {
        // Trigger pickup effects
        TriggerPickupEffects();

        // Trigger events
        OnWeaponPickedUp?.Invoke(weaponData);

        Debug.Log($"Picked up {weaponData.weaponName} with {currentAmmo}/{reserveAmmo} ammo");

        // Disable components immediately to prevent double-pickup
        canBePickedUp = false;

        if (interactableComponent != null)
        {
            interactableComponent.SetInteractable(false);
        }

        // Disable colliders to prevent further interaction
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // Destroy immediately
        Destroy(gameObject);
    }

    /// <summary>
    /// Set pickup availability
    /// </summary>
    public void SetPickupable(bool pickupable)
    {
        canBePickedUp = pickupable;

        if (interactableComponent != null)
        {
            interactableComponent.SetInteractable(pickupable);
        }
    }

    /// <summary>
    /// Get weapon info for pickup
    /// </summary>
    public WeaponPickupInfo GetPickupInfo()
    {
        return new WeaponPickupInfo
        {
            weaponData = weaponData,
            currentAmmo = currentAmmo,
            reserveAmmo = reserveAmmo
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Update bobbing animation
    /// </summary>
    private void UpdateBobbing()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * bobbingSpeed) * bobbingHeight;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }

    /// <summary>
    /// Update rotation animation
    /// </summary>
    private void UpdateRotation()
    {
        transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Apply or remove highlight effect
    /// </summary>
    private void ApplyHighlight(bool highlight)
    {
        if (weaponRenderer == null)
            return;

        if (highlight && highlightMaterial != null)
        {
            weaponRenderer.material = highlightMaterial;
        }
        else if (originalMaterial != null)
        {
            weaponRenderer.material = originalMaterial;
        }
    }

    /// <summary>
    /// Trigger pickup effects
    /// </summary>
    private void TriggerPickupEffects()
    {
        // Spawn pickup effect
        if (pickupEffect != null)
        {
            GameObject effect = Instantiate(pickupEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // Play pickup sound - COMMENTED OUT FOR NOW
        // PlaySound(pickupSound);
    }

    /// <summary>
    /// Find the player's weapon manager
    /// </summary>
    private WeaponManager FindPlayerWeaponManager()
    {
        // Look for player by tag first
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            WeaponManager weaponManager = player.GetComponent<WeaponManager>();
            if (weaponManager != null)
                return weaponManager;
        }

        // Fallback: find any WeaponManager in scene
        return FindObjectOfType<WeaponManager>();
    }

    // /// <summary>
    // /// Play pickup sound - COMMENTED OUT FOR NOW
    // /// </summary>
    // private void PlaySound(AudioClip clip)
    // {
    //     if (clip != null && audioSource != null)
    //     {
    //         audioSource.PlayOneShot(clip);
    //     }
    // }

    #endregion

    #region Debug Gizmos

    void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = canBePickedUp ? Color.green : Color.red;
        if (interactableComponent != null)
        {
            Gizmos.DrawWireSphere(transform.position, interactableComponent.interactionRange);
        }

        // Draw bobbing path
        if (enableBobbing && Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Vector3 minPos = new Vector3(startPosition.x, startPosition.y - bobbingHeight, startPosition.z);
            Vector3 maxPos = new Vector3(startPosition.x, startPosition.y + bobbingHeight, startPosition.z);
            Gizmos.DrawLine(minPos, maxPos);
        }
    }

    #endregion
}

/// <summary>
/// Data structure for weapon pickup information
/// </summary>
[System.Serializable]
public struct WeaponPickupInfo
{
    public WeaponData weaponData;
    public int currentAmmo;
    public int reserveAmmo;
}