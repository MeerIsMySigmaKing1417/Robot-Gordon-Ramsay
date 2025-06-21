using System.Collections;
using UnityEngine;

/// <summary>
/// Camera shake system for weapon effects and other impacts
/// Attach this to your main camera
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [Tooltip("Global shake intensity multiplier")]
    [Range(0f, 2f)]
    public float globalIntensityMultiplier = 1f;

    [Tooltip("Maximum shake distance")]
    [Range(0.1f, 2f)]
    public float maxShakeDistance = 0.5f;

    [Tooltip("Shake frequency (higher = faster shaking)")]
    [Range(10f, 100f)]
    public float shakeFrequency = 50f;

    [Header("Decay Settings")]
    [Tooltip("How quickly shake fades out")]
    [Range(1f, 10f)]
    public float decayRate = 3f;

    [Tooltip("Use smooth decay curve")]
    public bool useSmoothDecay = true;

    [Header("Debug")]
    [Tooltip("Show shake debug info")]
    public bool showDebugInfo = false;

    // Private variables
    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;
    private float currentShakeIntensity = 0f;
    private float currentShakeDuration = 0f;
    private bool isShaking = false;

    #region Unity Lifecycle

    void Awake()
    {
        // Store original camera position
        originalPosition = transform.localPosition;
    }

    void Start()
    {
        Debug.Log("CameraShake initialized");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Trigger camera shake with specified intensity and duration
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        // Stop any existing shake
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        // Apply global multiplier
        intensity *= globalIntensityMultiplier;

        // Clamp values
        intensity = Mathf.Clamp(intensity, 0f, maxShakeDistance);
        duration = Mathf.Max(0.01f, duration);

        if (intensity > 0f && duration > 0f)
        {
            currentShakeIntensity = intensity;
            currentShakeDuration = duration;
            shakeCoroutine = StartCoroutine(ShakeCoroutine(intensity, duration));

            if (showDebugInfo)
            {
                Debug.Log($"Camera shake: Intensity={intensity:F3}, Duration={duration:F3}");
            }
        }
    }

    /// <summary>
    /// Stop any current shake and return camera to original position
    /// </summary>
    public void StopShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        StartCoroutine(ReturnToOriginalPosition());
        isShaking = false;
        currentShakeIntensity = 0f;
    }

    /// <summary>
    /// Quick shake for weapon firing
    /// </summary>
    public void WeaponFireShake(float intensity = 0.1f)
    {
        Shake(intensity, 0.1f);
    }

    /// <summary>
    /// Medium shake for explosions
    /// </summary>
    public void ExplosionShake(float intensity = 0.5f)
    {
        Shake(intensity, 0.3f);
    }

    /// <summary>
    /// Strong shake for large impacts
    /// </summary>
    public void ImpactShake(float intensity = 0.8f)
    {
        Shake(intensity, 0.5f);
    }

    /// <summary>
    /// Check if camera is currently shaking
    /// </summary>
    public bool IsShaking()
    {
        return isShaking;
    }

    /// <summary>
    /// Get current shake intensity
    /// </summary>
    public float GetCurrentIntensity()
    {
        return currentShakeIntensity;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Main shake coroutine
    /// </summary>
    private IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        isShaking = true;
        float elapsed = 0f;
        Vector3 originalPos = originalPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Calculate current shake intensity with decay
            float currentIntensity;
            if (useSmoothDecay)
            {
                // Smooth exponential decay
                float normalizedTime = elapsed / duration;
                currentIntensity = intensity * Mathf.Exp(-decayRate * normalizedTime);
            }
            else
            {
                // Linear decay
                currentIntensity = intensity * (1f - (elapsed / duration));
            }

            // Generate random shake offset
            Vector3 shakeOffset = GenerateShakeOffset(currentIntensity);

            // Apply shake to camera position
            transform.localPosition = originalPos + shakeOffset;

            // Update current intensity for external queries
            currentShakeIntensity = currentIntensity;

            yield return null;
        }

        // Return to original position
        yield return StartCoroutine(ReturnToOriginalPosition());

        isShaking = false;
        currentShakeIntensity = 0f;
        shakeCoroutine = null;
    }

    /// <summary>
    /// Generate random shake offset
    /// </summary>
    private Vector3 GenerateShakeOffset(float intensity)
    {
        // Use Perlin noise for smoother, more natural shake
        float time = Time.time * shakeFrequency;

        float x = (Mathf.PerlinNoise(time, 0f) - 0.5f) * 2f * intensity;
        float y = (Mathf.PerlinNoise(0f, time) - 0.5f) * 2f * intensity;
        float z = (Mathf.PerlinNoise(time, time) - 0.5f) * 2f * intensity * 0.5f; // Less Z movement

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Smoothly return camera to original position
    /// </summary>
    private IEnumerator ReturnToOriginalPosition()
    {
        float returnSpeed = 10f;

        while (Vector3.Distance(transform.localPosition, originalPosition) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                originalPosition,
                Time.deltaTime * returnSpeed
            );

            yield return null;
        }

        // Ensure exact position
        transform.localPosition = originalPosition;
    }

    #endregion

    #region Debug

    void OnGUI()
    {
        if (showDebugInfo && isShaking)
        {
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 300, 60),
                $"Camera Shake Debug:\n" +
                $"Intensity: {currentShakeIntensity:F3}\n" +
                $"Duration: {currentShakeDuration:F3}");
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw shake range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, maxShakeDistance);

        // Draw original position
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.1f);
    }

    #endregion
}