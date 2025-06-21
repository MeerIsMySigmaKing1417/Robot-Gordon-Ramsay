using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple test script to debug particle direction issues
/// Attach this to any GameObject and assign your particle prefab
/// Press SPACE to spawn particles and see debug info
/// </summary>
public class ParticleDebugTest : MonoBehaviour
{
    [Header("Debug Test")]
    [Tooltip("The particle prefab you're having trouble with")]
    public GameObject particlePrefab;

    [Tooltip("Test spawn position offset")]
    public Vector3 spawnOffset = Vector3.up;

    // Input system variables
    private PlayerInputActions inputActions;
    private bool testInput;

    void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Enable();

        // Use Jump input (Space key) for testing
        inputActions.Player.Jump.performed += ctx => testInput = true;
        inputActions.Player.Jump.canceled += ctx => testInput = false;
    }

    void OnDisable()
    {
        inputActions.Player.Disable();
    }

    void Update()
    {
        if (testInput)
        {
            TestParticleSpawn();
            testInput = false; // Reset to prevent spam
        }
    }

    void TestParticleSpawn()
    {
        if (particlePrefab == null)
        {
            Debug.LogError("No particle prefab assigned!");
            return;
        }

        Vector3 spawnPos = transform.position + spawnOffset;

        Debug.Log("=== PARTICLE DEBUG TEST ===");
        Debug.Log($"Prefab Name: {particlePrefab.name}");
        Debug.Log($"Prefab Rotation: {particlePrefab.transform.rotation.eulerAngles}");
        Debug.Log($"Spawn Position: {spawnPos}");

        // Test 1: Spawn with prefab's original rotation
        GameObject test1 = Instantiate(particlePrefab, spawnPos, particlePrefab.transform.rotation);
        test1.name = "Test1_OriginalRotation";

        // Test 2: Spawn with identity rotation  
        GameObject test2 = Instantiate(particlePrefab, spawnPos + Vector3.right * 2f, Quaternion.identity);
        test2.name = "Test2_IdentityRotation";

        // Test 3: Spawn with forced upward rotation
        GameObject test3 = Instantiate(particlePrefab, spawnPos + Vector3.left * 2f, Quaternion.Euler(-90, 0, 0));
        test3.name = "Test3_ForcedUpward";

        // Test 4: Spawn with different forced rotations
        GameObject test4 = Instantiate(particlePrefab, spawnPos + Vector3.forward * 2f, Quaternion.Euler(0, 0, 0));
        test4.name = "Test4_NoRotation";

        // Check particle system settings on each
        CheckParticleSystem(test1, "Test1");
        CheckParticleSystem(test2, "Test2");
        CheckParticleSystem(test3, "Test3");
        CheckParticleSystem(test4, "Test4");

        // Auto-destroy after 10 seconds
        Destroy(test1, 10f);
        Destroy(test2, 10f);
        Destroy(test3, 10f);
        Destroy(test4, 10f);

        Debug.Log("4 test particles spawned! Watch which one goes upward.");
    }

    void CheckParticleSystem(GameObject particleObj, string testName)
    {
        ParticleSystem ps = particleObj.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError($"{testName}: No ParticleSystem component found!");
            return;
        }

        var main = ps.main;
        var shape = ps.shape;
        var velocityOverLifetime = ps.velocityOverLifetime;
        var emission = ps.emission;

        Debug.Log($"{testName} Settings:");
        Debug.Log($"  Object Rotation: {particleObj.transform.rotation.eulerAngles}");
        Debug.Log($"  Object Up Vector: {particleObj.transform.up}");
        Debug.Log($"  Object Forward Vector: {particleObj.transform.forward}");
        Debug.Log($"  Simulation Space: {main.simulationSpace}");
        Debug.Log($"  Start Speed: {main.startSpeed.constant}");
        Debug.Log($"  Start Lifetime: {main.startLifetime.constant}");
        Debug.Log($"  Gravity Modifier: {main.gravityModifier.constant}");
        Debug.Log($"  Max Particles: {main.maxParticles}");
        Debug.Log($"  Prewarm: {main.prewarm}");
        Debug.Log($"  Shape Enabled: {shape.enabled}");
        Debug.Log($"  Shape Type: {shape.shapeType}");
        Debug.Log($"  Shape Angle: {shape.angle}");
        Debug.Log($"  Shape Radius: {shape.radius}");
        Debug.Log($"  Emission Rate: {emission.rateOverTime.constant}");
        Debug.Log($"  Velocity Over Lifetime Enabled: {velocityOverLifetime.enabled}");

        // NOW FIX THE PARTICLE SYSTEM SETTINGS BY FORCE
        Debug.Log($"  FORCING {testName} TO GO UPWARD...");

        // Set to world space
        var mainModule = ps.main;
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
        mainModule.startSpeed = 3f; // Force speed
        mainModule.gravityModifier = 0f; // No gravity pulling down

        // Force shape to emit upward
        var shapeModule = ps.shape;
        shapeModule.enabled = true;
        shapeModule.shapeType = ParticleSystemShapeType.Circle;
        shapeModule.angle = 0f; // Straight cone
        shapeModule.radius = 0.1f;

        // Force velocity upward
        var velocityModule = ps.velocityOverLifetime;
        velocityModule.enabled = true;
        velocityModule.space = ParticleSystemSimulationSpace.World;

        // Try setting radial velocity (this often works)
        velocityModule.radial = new ParticleSystem.MinMaxCurve(2f); // Outward force

        Debug.Log($"  Applied force fixes to {testName}");
        Debug.Log($"---");
    }

    void OnDrawGizmos()
    {
        // Draw spawn positions
        Gizmos.color = Color.green;
        Vector3 spawnPos = transform.position + spawnOffset;

        Gizmos.DrawWireSphere(spawnPos, 0.2f); // Test1
        Gizmos.DrawWireSphere(spawnPos + Vector3.right * 2f, 0.2f); // Test2
        Gizmos.DrawWireSphere(spawnPos + Vector3.left * 2f, 0.2f); // Test3
        Gizmos.DrawWireSphere(spawnPos + Vector3.forward * 2f, 0.2f); // Test4

        // Draw labels
        if (Application.isPlaying)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(spawnPos, Vector3.up);
            Gizmos.DrawRay(spawnPos + Vector3.right * 2f, Vector3.up);
            Gizmos.DrawRay(spawnPos + Vector3.left * 2f, Vector3.up);
            Gizmos.DrawRay(spawnPos + Vector3.forward * 2f, Vector3.up);
        }
    }
}