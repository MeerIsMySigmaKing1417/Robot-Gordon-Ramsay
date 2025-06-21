using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Generic controller for cooking appliances (stoves, ovens, grills, etc.)
/// Handles turning appliances on/off via clickable controls
/// Works with existing cooking systems like StoveController
/// </summary>
public class CookingApplianceController : MonoBehaviour
{
    [Header("Appliance Settings")]
    [Tooltip("Type of cooking appliance")]
    public CookingApplianceType applianceType = CookingApplianceType.Stove;

    [Tooltip("Is the appliance currently turned on?")]
    public bool isApplianceOn = false;

    [Tooltip("Heat level of the appliance (affects cooking speed)")]
    [Range(0.1f, 3f)]
    public float heatLevel = 1f;

    [Tooltip("Can the player manually control this appliance?")]
    public bool playerControllable = true;

    [Header("Control Objects")]
    [Tooltip("The dial/button/switch that controls this appliance")]
    public InteractableComponent controlDial;

    [Tooltip("Additional control objects for complex appliances")]
    public InteractableComponent[] additionalControls;

    [Header("Visual Feedback")]
    [Tooltip("Material to use when appliance is on")]
    public Material applianceOnMaterial;

    [Tooltip("Material to use when appliance is off")]
    public Material applianceOffMaterial;

    [Tooltip("Light component to control (for indicator lights, flames, etc.)")]
    public Light applianceLight;

    [Header("Cooking Integration")]
    [Tooltip("The StoveController component (auto-found if this is a stove)")]
    public StoveController stoveController;

    [Tooltip("Custom cooking components for other appliance types")]
    public MonoBehaviour[] customCookingComponents;

    [Header("Events")]
    [Tooltip("Called when appliance is turned on")]
    public UnityEvent OnApplianceOn;

    [Tooltip("Called when appliance is turned off")]
    public UnityEvent OnApplianceOff;

    [Tooltip("Called when any control is interacted with")]
    public UnityEvent OnControlInteraction;

    // Private variables for visual management
    private Renderer applianceRenderer;

    #region Unity Lifecycle

    void Awake()
    {
        // Get renderer for material changes
        applianceRenderer = GetComponent<Renderer>();
    }

    void Start()
    {
        // Setup main control dial
        SetupControlDial();

        // Setup additional controls
        SetupAdditionalControls();

        // Auto-find StoveController if this is a stove
        if (applianceType == CookingApplianceType.Stove && stoveController == null)
        {
            stoveController = GetComponent<StoveController>();
        }

        // Initialize appliance state
        UpdateApplianceVisuals();
        UpdateCookingComponents();
    }

    void OnDestroy()
    {
        // Clean up event subscriptions
        CleanupControlEvents();
    }

    #endregion

    #region Setup Methods

    /// <summary>
    /// Setup the main control dial interaction
    /// </summary>
    private void SetupControlDial()
    {
        if (controlDial != null)
        {
            // Subscribe to interaction event
            controlDial.OnInteractionComplete.AddListener(OnControlDialInteracted);

            // Set appropriate interaction text
            if (string.IsNullOrEmpty(controlDial.interactionText))
            {
                controlDial.interactionText = $"Click to toggle {applianceType.ToString().ToLower()}";
            }
        }
        else
        {
            Debug.LogWarning($"CookingApplianceController on {gameObject.name} has no control dial assigned!", this);
        }
    }

    /// <summary>
    /// Setup additional control interactions
    /// </summary>
    private void SetupAdditionalControls()
    {
        foreach (var control in additionalControls)
        {
            if (control != null)
            {
                control.OnInteractionComplete.AddListener(OnAdditionalControlInteracted);
            }
        }
    }

    /// <summary>
    /// Clean up all control event subscriptions
    /// </summary>
    private void CleanupControlEvents()
    {
        if (controlDial != null)
        {
            controlDial.OnInteractionComplete.RemoveListener(OnControlDialInteracted);
        }

        foreach (var control in additionalControls)
        {
            if (control != null)
            {
                control.OnInteractionComplete.RemoveListener(OnAdditionalControlInteracted);
            }
        }
    }

    #endregion

    #region Interaction Handlers

    /// <summary>
    /// Called when the main control dial is clicked
    /// </summary>
    private void OnControlDialInteracted()
    {
        if (!playerControllable)
            return;

        ToggleAppliance();
        OnControlInteraction?.Invoke();
    }

    /// <summary>
    /// Called when additional controls are clicked
    /// </summary>
    private void OnAdditionalControlInteracted()
    {
        if (!playerControllable)
            return;

        // Can be customized for specific control types (heat adjustment, timers, etc.)
        OnControlInteraction?.Invoke();
        Debug.Log($"Additional control interacted with on {gameObject.name}");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Toggle the appliance on/off
    /// </summary>
    public void ToggleAppliance()
    {
        if (isApplianceOn)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }

    /// <summary>
    /// Turn the appliance on
    /// </summary>
    public void TurnOn()
    {
        if (!isApplianceOn)
        {
            isApplianceOn = true;
            UpdateApplianceVisuals();
            UpdateCookingComponents();
            OnApplianceOn?.Invoke();

            Debug.Log($"{applianceType} {gameObject.name} turned ON");
        }
    }

    /// <summary>
    /// Turn the appliance off
    /// </summary>
    public void TurnOff()
    {
        if (isApplianceOn)
        {
            isApplianceOn = false;
            UpdateApplianceVisuals();
            UpdateCookingComponents();
            OnApplianceOff?.Invoke();

            Debug.Log($"{applianceType} {gameObject.name} turned OFF");
        }
    }

    /// <summary>
    /// Set the heat level of the appliance
    /// </summary>
    public void SetHeatLevel(float newHeatLevel)
    {
        heatLevel = Mathf.Clamp(newHeatLevel, 0.1f, 3f);

        // Update cooking components with new heat level
        if (stoveController != null)
        {
            stoveController.SetHeatLevel(heatLevel);
        }

        Debug.Log($"{applianceType} {gameObject.name} heat level set to {heatLevel:F1}");
    }

    /// <summary>
    /// Enable/disable player control of this appliance
    /// </summary>
    public void SetPlayerControllable(bool controllable)
    {
        playerControllable = controllable;

        // Update all control interactability
        if (controlDial != null)
        {
            controlDial.SetInteractable(controllable);
        }

        foreach (var control in additionalControls)
        {
            if (control != null)
            {
                control.SetInteractable(controllable);
            }
        }
    }

    /// <summary>
    /// Get current appliance status for debugging
    /// </summary>
    public string GetApplianceStatus()
    {
        return $"{applianceType} is {(isApplianceOn ? "ON" : "OFF")} - Heat Level: {heatLevel:F1}";
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Update visual effects based on appliance state
    /// </summary>
    private void UpdateApplianceVisuals()
    {
        // Update materials
        if (applianceRenderer != null)
        {
            if (isApplianceOn && applianceOnMaterial != null)
            {
                applianceRenderer.material = applianceOnMaterial;
            }
            else if (!isApplianceOn && applianceOffMaterial != null)
            {
                applianceRenderer.material = applianceOffMaterial;
            }
        }

        // Update lights (indicator lights, flame effects, etc.)
        if (applianceLight != null)
        {
            applianceLight.enabled = isApplianceOn;
        }
    }

    /// <summary>
    /// Update cooking components based on appliance state
    /// </summary>
    private void UpdateCookingComponents()
    {
        // Update StoveController if this is a stove
        if (stoveController != null)
        {
            if (isApplianceOn)
            {
                stoveController.TurnOn();
                stoveController.SetHeatLevel(heatLevel);
            }
            else
            {
                stoveController.TurnOff();
            }
        }

        // Update custom cooking components
        // Add custom logic here for other appliance types as needed
        foreach (var component in customCookingComponents)
        {
            if (component != null)
            {
                // Example for future oven/grill controllers:
                // if (component is OvenController oven)
                // {
                //     if (isApplianceOn) oven.TurnOn(); else oven.TurnOff();
                // }
            }
        }
    }

    #endregion
}

/// <summary>
/// Enum for different types of cooking appliances
/// </summary>
public enum CookingApplianceType
{
    Stove,
    Oven,
    Grill,
    Microwave,
    Fryer,
    Steamer,
    Smoker,
    Toaster,
    CoffeeMaker,
    Other
}