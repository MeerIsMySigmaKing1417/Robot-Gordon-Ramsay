using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base interactable component that can be clicked/activated by the player
/// Attach this to any object you want to be clickable (dials, buttons, switches, etc.)
/// </summary>
public class InteractableComponent : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Can this object be interacted with?")]
    public bool isInteractable = true;

    [Tooltip("Maximum distance from which this can be interacted with")]
    [Range(1f, 20f)]
    public float interactionRange = 5f;

    [Tooltip("Layer mask for what can interact with this (usually player)")]
    public LayerMask interactorLayerMask = -1;

    [Header("Visual Feedback")]
    [Tooltip("Material to use when highlighted/hovered")]
    public Material highlightMaterial;

    [Tooltip("Color tint when highlighted (if no highlight material provided)")]
    public Color highlightColor = Color.yellow;

    [Tooltip("Custom interaction text for this object")]
    public string interactionText = "Click to interact";

    [Header("Events")]
    [Tooltip("Called when interaction starts (mouse down)")]
    public UnityEvent OnInteractionStart;

    [Tooltip("Called when interaction completes (mouse up/click)")]
    public UnityEvent OnInteractionComplete;

    [Tooltip("Called when mouse enters this object")]
    public UnityEvent OnHoverEnter;

    [Tooltip("Called when mouse exits this object")]
    public UnityEvent OnHoverExit;

    // Private variables for visual feedback
    private Renderer objectRenderer;
    private Material originalMaterial;
    private Color originalColor;
    private bool isHighlighted = false;
    private bool isBeingInteracted = false;

    #region Unity Lifecycle

    void Awake()
    {
        // Get renderer for visual feedback
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
            originalColor = objectRenderer.material.color;
        }
    }

    void Start()
    {
        // Validate that we have a collider for mouse detection
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning($"InteractableComponent on {gameObject.name} has no Collider! Mouse detection won't work.", this);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Check if this object can be interacted with by the given interactor
    /// </summary>
    public bool CanInteract(Transform interactor)
    {
        if (!isInteractable)
            return false;

        // Check distance
        float distance = Vector3.Distance(transform.position, interactor.position);
        if (distance > interactionRange)
            return false;

        // Check layer mask
        int interactorLayer = 1 << interactor.gameObject.layer;
        if ((interactorLayerMask & interactorLayer) == 0)
            return false;

        return true;
    }

    /// <summary>
    /// Start interaction (called when mouse down begins)
    /// </summary>
    public void StartInteraction(Transform interactor)
    {
        if (!CanInteract(interactor) || isBeingInteracted)
            return;

        isBeingInteracted = true;
        OnInteractionStart?.Invoke();

        Debug.Log($"Started interacting with {gameObject.name}");
    }

    /// <summary>
    /// Complete interaction (called when mouse up/click completes)
    /// </summary>
    public void CompleteInteraction(Transform interactor)
    {
        if (!CanInteract(interactor) || !isBeingInteracted)
            return;

        isBeingInteracted = false;
        OnInteractionComplete?.Invoke();

        Debug.Log($"Completed interaction with {gameObject.name}");
    }

    /// <summary>
    /// Cancel ongoing interaction
    /// </summary>
    public void CancelInteraction()
    {
        if (isBeingInteracted)
        {
            isBeingInteracted = false;
            Debug.Log($"Cancelled interaction with {gameObject.name}");
        }
    }

    /// <summary>
    /// Highlight this object (visual feedback for hover)
    /// </summary>
    public void Highlight()
    {
        if (isHighlighted || objectRenderer == null)
            return;

        isHighlighted = true;

        // Apply highlight visual
        if (highlightMaterial != null)
        {
            objectRenderer.material = highlightMaterial;
        }
        else
        {
            objectRenderer.material.color = highlightColor;
        }

        OnHoverEnter?.Invoke();
    }

    /// <summary>
    /// Remove highlight from this object
    /// </summary>
    public void RemoveHighlight()
    {
        if (!isHighlighted || objectRenderer == null)
            return;

        isHighlighted = false;

        // Restore original visual
        if (highlightMaterial != null)
        {
            objectRenderer.material = originalMaterial;
        }
        else
        {
            objectRenderer.material.color = originalColor;
        }

        OnHoverExit?.Invoke();
    }

    /// <summary>
    /// Get the interaction prompt text
    /// </summary>
    public string GetInteractionText()
    {
        return interactionText;
    }

    /// <summary>
    /// Enable/disable interaction
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;

        if (!interactable)
        {
            RemoveHighlight();
            CancelInteraction();
        }
    }

    #endregion

    #region Debug Gizmos

    void OnDrawGizmosSelected()
    {
        // Draw interaction range
        Gizmos.color = isInteractable ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Draw interaction status
        if (Application.isPlaying)
        {
            if (isHighlighted)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(transform.position, transform.localScale * 1.1f);
            }

            if (isBeingInteracted)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(transform.position, transform.localScale * 1.2f);
            }
        }
    }

    #endregion
}