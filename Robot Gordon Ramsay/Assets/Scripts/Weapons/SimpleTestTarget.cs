using UnityEngine;

/// <summary>
/// Simple target for testing melee weapons
/// Attach this to cubes or other objects you want to hit
/// IMPLEMENTS BOTH IDamageable AND Health FOR MAXIMUM COMPATIBILITY
/// </summary>
public class SimpleTestTarget : MonoBehaviour, IDamageable
{
    [Header("Test Target")]
    public float health = 100f;
    public Color normalColor = Color.white;
    public Color hitColor = Color.red;

    private Renderer rend;
    private float originalHealth;

    void Start()
    {
        rend = GetComponent<Renderer>();
        originalHealth = health;

        // Make sure it has a collider
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }

        // Add rigidbody for knockback
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 10f;
        }

        Debug.Log($"✅ Test target ready: {gameObject.name}");
    }

    public void TakeDamage(float damage)
    {
        health -= damage;

        Debug.Log($"💥 CROWBAR HIT! Target took {damage} damage! Health: {health}");

        // Flash red when hit
        if (rend != null)
        {
            StartCoroutine(FlashRed());
        }

        // Create damage text
        CreateDamageText(damage);

        // Die if health depleted
        if (health <= 0)
        {
            Debug.Log($"💀 Target destroyed!");
            Destroy(gameObject, 1f);
        }
    }

    public bool IsAlive()
    {
        return health > 0;
    }

    private System.Collections.IEnumerator FlashRed()
    {
        rend.material.color = hitColor;
        yield return new WaitForSeconds(0.2f);
        rend.material.color = normalColor;
    }

    private void CreateDamageText(float damage)
    {
        GameObject textObj = new GameObject("DamageText");
        textObj.transform.position = transform.position + Vector3.up * 2f;

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = $"-{damage:F0}";
        textMesh.fontSize = 20;
        textMesh.color = Color.red;
        textMesh.anchor = TextAnchor.MiddleCenter;

        // Face camera
        if (Camera.main != null)
        {
            textObj.transform.LookAt(Camera.main.transform);
            textObj.transform.Rotate(0, 180, 0);
        }

        // Animate upward and destroy
        StartCoroutine(AnimateAndDestroy(textObj));
    }

    private System.Collections.IEnumerator AnimateAndDestroy(GameObject textObj)
    {
        Vector3 startPos = textObj.transform.position;
        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration && textObj != null)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // Move up
            textObj.transform.position = startPos + Vector3.up * progress * 2f;

            // Fade out
            TextMesh textMesh = textObj.GetComponent<TextMesh>();
            if (textMesh != null)
            {
                Color color = textMesh.color;
                color.a = 1f - progress;
                textMesh.color = color;
            }

            yield return null;
        }

        if (textObj != null)
            Destroy(textObj);
    }

    [ContextMenu("Reset Health")]
    public void ResetHealth()
    {
        health = originalHealth;
        if (rend != null)
            rend.material.color = normalColor;
        Debug.Log($"🔄 Health reset to {health}");
    }
}