using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug script to test weapon pickup - attach to Player
/// This helps identify where the pickup system is failing
/// </summary>
public class DebugPickupSystem : MonoBehaviour
{
    [Header("Debug Settings")]
    [Tooltip("Enable debug logging")]
    public bool enableDebugLogs = true;

    [Tooltip("Debug pickup range")]
    [Range(1f, 10f)]
    public float debugRange = 5f;

    [Tooltip("Layer mask for pickupable weapons")]
    public LayerMask weaponLayerMask = -1;

    private PlayerInputActions inputActions;
    private Camera playerCamera;
    private WeaponManager weaponManager;
    private InteractionManager interactionManager;

    void Awake()
    {
        inputActions = new PlayerInputActions();
        playerCamera = Camera.main;
        if (playerCamera == null)
            playerCamera = FindObjectOfType<Camera>();
    }

    void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Enable();

        // Listen for E key (ScrollUp in your input map)
        inputActions.Player.ScrollUp.performed += OnPickupInput;
    }

    void OnDisable()
    {
        if (inputActions != null)
        {
            inputActions.Player.ScrollUp.performed -= OnPickupInput;
            inputActions.Player.Disable();
        }
    }

    void Start()
    {
        weaponManager = GetComponent<WeaponManager>();
        interactionManager = GetComponent<InteractionManager>();

        if (enableDebugLogs)
        {
            Debug.Log("=== DEBUG PICKUP SYSTEM INITIALIZED ===");
            Debug.Log($"WeaponManager found: {weaponManager != null}");
            Debug.Log($"InteractionManager found: {interactionManager != null}");
            Debug.Log($"Player Camera found: {playerCamera != null}");
        }
    }

    void Update()
    {
        if (enableDebugLogs)
        {
            DebugNearbyWeapons();
        }
    }

    private void OnPickupInput(InputAction.CallbackContext context)
    {
        if (enableDebugLogs)
        {
            Debug.Log("=== E KEY PRESSED ===");
        }

        // Method 1: Check InteractionManager
        CheckInteractionManager();

        // Method 2: Direct raycast check
        CheckDirectRaycast();

        // Method 3: Sphere overlap check
        CheckSphereOverlap();
    }

    private void CheckInteractionManager()
    {
        if (interactionManager != null)
        {
            InteractableComponent hoveredObject = interactionManager.GetCurrentHoveredObject();
            if (enableDebugLogs)
            {
                Debug.Log($"InteractionManager hovered object: {(hoveredObject != null ? hoveredObject.name : "NULL")}");
            }

            if (hoveredObject != null)
            {
                PickupableWeapon pickup = hoveredObject.GetComponent<PickupableWeapon>();
                if (pickup != null && weaponManager != null)
                {
                    Debug.Log($"Attempting pickup via InteractionManager: {pickup.name}");
                    weaponManager.TryPickupWeapon(pickup);
                }
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log("InteractionManager is NULL!");
        }
    }

    private void CheckDirectRaycast()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, debugRange, weaponLayerMask))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Direct raycast hit: {hit.collider.name}");
            }

            PickupableWeapon pickup = hit.collider.GetComponent<PickupableWeapon>();
            if (pickup != null && weaponManager != null)
            {
                Debug.Log($"Attempting pickup via direct raycast: {pickup.name}");
                weaponManager.TryPickupWeapon(pickup);
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log("Direct raycast hit nothing");
        }
    }

    private void CheckSphereOverlap()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, debugRange, weaponLayerMask);

        if (enableDebugLogs)
        {
            Debug.Log($"Sphere overlap found {nearbyColliders.Length} objects");
        }

        foreach (Collider col in nearbyColliders)
        {
            PickupableWeapon pickup = col.GetComponent<PickupableWeapon>();
            if (pickup != null)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance <= debugRange && weaponManager != null)
                {
                    Debug.Log($"Attempting pickup via sphere overlap: {pickup.name} (distance: {distance:F2})");
                    weaponManager.TryPickupWeapon(pickup);
                    break; // Only pickup one weapon
                }
            }
        }
    }

    private void DebugNearbyWeapons()
    {
        // Only check every 30 frames to reduce spam
        if (Time.frameCount % 30 != 0) return;

        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, debugRange);
        int weaponCount = 0;

        foreach (Collider col in nearbyColliders)
        {
            if (col.GetComponent<PickupableWeapon>() != null)
            {
                weaponCount++;
            }
        }

        if (weaponCount > 0)
        {
            Debug.Log($"Found {weaponCount} nearby weapons");
        }
    }

    void OnDrawGizmos()
    {
        // Draw debug range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, debugRange);

        // Draw camera forward ray
        if (playerCamera != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * debugRange);
        }
    }
}