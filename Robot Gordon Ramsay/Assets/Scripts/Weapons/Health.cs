using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Interface for objects that can take damage
/// Implement this for custom damage handling
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
    bool IsAlive();
}

/// <summary>
/// Basic health component for targets, enemies, and destructible objects
/// Attach this to any object that can take damage
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [Tooltip("Maximum health points")]
    [Range(1f, 1000f)]
    public float maxHealth = 100f;

    [Tooltip("Current health points")]
    [SerializeField] private float currentHealth;

    [Tooltip("Can this object be damaged?")]
    public bool invulnerable = false;

    [Tooltip("Automatically destroy when health reaches 0?")]
    public bool destroyOnDeath = true;

    [Tooltip("Delay before destroying object")]
    [Range(0f, 10f)]
    public float destroyDelay = 0f;

    [Header("Visual Feedback")]
    [Tooltip("Effect to spawn when taking damage")]
    public GameObject damageEffect;

    [Tooltip("Effect to spawn when destroyed")]
    public GameObject deathEffect;

    [Tooltip("Material to flash when taking damage")]
    public Material damageMaterial;

    [Tooltip("Duration of damage flash")]
    [Range(0.1f, 1f)]
    public float flashDuration = 0.2f;

    // [Header("Audio")] - COMMENTED OUT FOR NOW
    // [Tooltip("Sound when taking damage")]
    // public AudioClip damageSound;

    // [Tooltip("Sound when destroyed")]
    // public AudioClip deathSound;

    [Header("Events")]
    [Tooltip("Called when damage is taken (damage amount)")]
    public UnityEvent<float> OnDamageTaken;

    [Tooltip("Called when health changes (current, max)")]
    public UnityEvent<float, float> OnHealthChanged;

    [Tooltip("Called when object is destroyed")]
    public UnityEvent OnDeath;

    [Tooltip("Called when health is restored")]
    public UnityEvent<float> OnHealed;

    // Private variables
    private Renderer objectRenderer;
    private Material originalMaterial;
    private bool isDead = false;
    // private AudioSource audioSource; - COMMENTED OUT FOR NOW

    #region Unity Lifecycle

    void Awake()
    {
        // Get renderer for damage flash effect
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material;
        }

        // Setup audio - COMMENTED OUT FOR NOW
        // audioSource = GetComponent<AudioSource>();
        // if (audioSource == null)
        // {
        //     audioSource = gameObject.AddComponent<AudioSource>();
        //     audioSource.playOnAwake = false;
        //     audioSource.spatialBlend = 1f; // 3D sound
        // }
    }

    void Start()
    {
        // Initialize health
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"{gameObject.name} initialized with {maxHealth} health");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Take damage from an attack
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (invulnerable || isDead || damage <= 0)
            return;

        // Apply damage
        currentHealth = Mathf.Max(0f, currentHealth - damage);

        // Trigger effects
        TriggerDamageEffects(damage);

        // Check for death
        if (currentHealth <= 0f && !isDead)
        {
            Die();
        }

        // Trigger events
        OnDamageTaken?.Invoke(damage);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        Debug.Log($"{gameObject.name} took {damage} damage. Health: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Heal the object
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead || amount <= 0)
            return;

        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        if (currentHealth > oldHealth)
        {
            OnHealed?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            Debug.Log($"{gameObject.name} healed for {amount}. Health: {currentHealth}/{maxHealth}");
        }
    }

    /// <summary>
    /// Set health to a specific value
    /// </summary>
    public void SetHealth(float newHealth)
    {
        if (isDead)
            return;

        currentHealth = Mathf.Clamp(newHealth, 0f, maxHealth);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Fully restore health
    /// </summary>
    public void FullHeal()
    {
        Heal(maxHealth);
    }

    /// <summary>
    /// Check if the object is alive
    /// </summary>
    public bool IsAlive()
    {
        return !isDead && currentHealth > 0f;
    }

    /// <summary>
    /// Get current health percentage (0-1)
    /// </summary>
    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    /// <summary>
    /// Get current health values
    /// </summary>
    public void GetHealth(out float current, out float max)
    {
        current = currentHealth;
        max = maxHealth;
    }

    /// <summary>
    /// Set invulnerability status
    /// </summary>
    public void SetInvulnerable(bool invulnerable)
    {
        this.invulnerable = invulnerable;
    }

    /// <summary>
    /// Instantly kill this object
    /// </summary>
    public void Kill()
    {
        if (!isDead)
        {
            currentHealth = 0f;
            Die();
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Handle death
    /// </summary>
    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        // Trigger death effects
        TriggerDeathEffects();

        // Trigger events
        OnDeath?.Invoke();

        Debug.Log($"{gameObject.name} has died!");

        // Destroy object if configured to do so
        if (destroyOnDeath)
        {
            if (destroyDelay > 0f)
            {
                Destroy(gameObject, destroyDelay);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Trigger visual and audio effects for taking damage
    /// </summary>
    private void TriggerDamageEffects(float damage)
    {
        // Spawn damage effect
        if (damageEffect != null)
        {
            GameObject effect = Instantiate(damageEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }

        // Flash damage material
        if (objectRenderer != null && damageMaterial != null && !isDead)
        {
            StartCoroutine(DamageFlash());
        }

        // Play damage sound - COMMENTED OUT FOR NOW
        // PlaySound(damageSound);
    }

    /// <summary>
    /// Trigger effects when object dies
    /// </summary>
    private void TriggerDeathEffects()
    {
        // Spawn death effect
        if (deathEffect != null)
        {
            GameObject effect = Instantiate(deathEffect, transform.position, Quaternion.identity);
            Destroy(effect, 5f);
        }

        // Play death sound - COMMENTED OUT FOR NOW
        // PlaySound(deathSound);
    }

    /// <summary>
    /// Flash damage material briefly
    /// </summary>
    private System.Collections.IEnumerator DamageFlash()
    {
        if (objectRenderer != null && damageMaterial != null)
        {
            objectRenderer.material = damageMaterial;
            yield return new WaitForSeconds(flashDuration);
            objectRenderer.material = originalMaterial;
        }
    }

    // /// <summary>
    // /// Play health-related sound - COMMENTED OUT FOR NOW
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
        // Draw health bar above object
        if (Application.isPlaying)
        {
            Vector3 barPosition = transform.position + Vector3.up * 2f;
            float healthPercentage = GetHealthPercentage();

            // Background bar
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(barPosition, new Vector3(2f, 0.2f, 0.1f));

            // Health bar
            Gizmos.color = isDead ? Color.black : Color.Lerp(Color.red, Color.green, healthPercentage);
            float healthWidth = 2f * healthPercentage;
            Gizmos.DrawCube(barPosition + Vector3.left * (2f - healthWidth) * 0.5f, new Vector3(healthWidth, 0.2f, 0.1f));
        }
    }

    #endregion
}