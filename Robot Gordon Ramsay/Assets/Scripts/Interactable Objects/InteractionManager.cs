using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages mouse interactions with InteractableComponent objects
/// Handles raycasting, hovering, and clicking on interactable objects
/// Attach this to your player GameObject or main camera
/// FIXED: Uses New Input System properly
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Maximum distance for interaction raycast")]
    [Range(1f, 50f)]
    public float maxInteractionDistance = 20f;

    [Tooltip("Layer mask for interactable objects")]
    public LayerMask interactableLayerMask = -1;

    [Tooltip("Camera to use for raycasting (leave empty to auto-find)")]
    public Camera interactionCamera;

    [Header("Input Settings")]
    [Tooltip("Use the new Input System? (disable for legacy Input Manager)")]
    public bool useNewInputSystem = true;

    // Private variables for interaction state
    private Mouse mouse;
    private InteractableComponent currentHoveredObject;
    private InteractableComponent currentInteractingObject;
    private bool isMousePressed = false;

    #region Unity Lifecycle

    void Awake()
    {
        // Auto-find camera if not assigned
        if (interactionCamera == null)
        {
            interactionCamera = Camera.main;
            if (interactionCamera == null)
            {
                interactionCamera = FindObjectOfType<Camera>();
            }
        }

        // Setup input system
        if (useNewInputSystem)
        {
            mouse = Mouse.current;
        }
    }

    void Start()
    {
        // Validate setup
        if (interactionCamera == null)
        {
            Debug.LogError("InteractionManager: No camera found! Please assign a camera.", this);
            enabled = false;
            return;
        }

        Debug.Log("InteractionManager initialized successfully");
    }

    void Update()
    {
        HandleMouseInput();
        HandleInteractionDetection();
    }

    #endregion

    #region Mouse Input Handling

    /// <summary>
    /// Handle mouse button presses and releases
    /// </summary>
    private void HandleMouseInput()
    {
        bool mouseDown = false;
        bool mouseUp = false;

        // Get input based on selected system
        if (useNewInputSystem && mouse != null)
        {
            mouseDown = mouse.leftButton.wasPressedThisFrame;
            mouseUp = mouse.leftButton.wasReleasedThisFrame;
        }
        else
        {
            // Legacy fallback
            mouseDown = Input.GetMouseButtonDown(0);
            mouseUp = Input.GetMouseButtonUp(0);
        }

        // Handle mouse down - start interaction
        if (mouseDown && currentHoveredObject != null)
        {
            currentInteractingObject = currentHoveredObject;
            currentInteractingObject.StartInteraction(transform);
            isMousePressed = true;
        }

        // Handle mouse up - complete or cancel interaction
        if (mouseUp && isMousePressed)
        {
            if (currentInteractingObject != null)
            {
                // Complete interaction if still hovering over same object
                if (currentHoveredObject == currentInteractingObject)
                {
                    currentInteractingObject.CompleteInteraction(transform);
                }
                else
                {
                    // Cancel if mouse moved away
                    currentInteractingObject.CancelInteraction();
                }

                currentInteractingObject = null;
            }

            isMousePressed = false;
        }
    }

    /// <summary>
    /// Handle detecting what object the mouse is hovering over
    /// </summary>
    private void HandleInteractionDetection()
    {
        // Get mouse position using NEW INPUT SYSTEM
        Vector2 mousePos = Vector2.zero;

        if (useNewInputSystem && mouse != null)
        {
            mousePos = mouse.position.ReadValue();
        }
        else
        {
            // Legacy fallback - but only if not using new input system
            mousePos = (Vector2)Input.mousePosition;
        }

        // Cast ray from camera through mouse position
        Ray ray = interactionCamera.ScreenPointToRay(mousePos);
        InteractableComponent hitInteractable = null;

        // Perform raycast to find interactable objects
        if (Physics.Raycast(ray, out RaycastHit hit, maxInteractionDistance, interactableLayerMask))
        {
            hitInteractable = hit.collider.GetComponent<InteractableComponent>();

            // Verify the object can actually be interacted with
            if (hitInteractable != null && !hitInteractable.CanInteract(transform))
            {
                hitInteractable = null;
            }
        }

        // Handle hover state changes
        if (hitInteractable != currentHoveredObject)
        {
            // Remove highlight from previous object
            if (currentHoveredObject != null)
            {
                currentHoveredObject.RemoveHighlight();
            }

            // Update current hovered object
            currentHoveredObject = hitInteractable;

            // Highlight new object
            if (currentHoveredObject != null)
            {
                currentHoveredObject.Highlight();
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Get the currently hovered interactable object
    /// </summary>
    public InteractableComponent GetCurrentHoveredObject()
    {
        return currentHoveredObject;
    }

    /// <summary>
    /// Get the object currently being interacted with
    /// </summary>
    public InteractableComponent GetCurrentInteractingObject()
    {
        return currentInteractingObject;
    }

    /// <summary>
    /// Force cancel current interaction
    /// </summary>
    public void CancelCurrentInteraction()
    {
        if (currentInteractingObject != null)
        {
            currentInteractingObject.CancelInteraction();
            currentInteractingObject = null;
        }

        isMousePressed = false;
    }

    #endregion

    #region Debug Gizmos - FIXED FOR NEW INPUT SYSTEM

    void OnDrawGizmos()
    {
        if (interactionCamera == null)
            return;

        // Get mouse position for debug ray - FIXED
        Vector2 mousePos = Vector2.zero;

        if (Application.isPlaying)
        {
            if (useNewInputSystem && mouse != null)
            {
                mousePos = mouse.position.ReadValue();
            }
            else
            {
                // Only use legacy input if explicitly disabled new input system
                if (!useNewInputSystem)
                {
                    mousePos = (Vector2)Input.mousePosition;
                }
                else
                {
                    // Show center ray when new input system is enabled but mouse not available
                    mousePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                }
            }
        }
        else
        {
            // Show center ray when not playing
            mousePos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        // Draw interaction ray
        Ray ray = interactionCamera.ScreenPointToRay(mousePos);
        Gizmos.color = currentHoveredObject != null ? Color.green : Color.red;
        Gizmos.DrawRay(ray.origin, ray.direction * maxInteractionDistance);

        // Draw highlight around hovered object
        if (currentHoveredObject != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(currentHoveredObject.transform.position, 0.3f);
        }
    }

    #endregion
}