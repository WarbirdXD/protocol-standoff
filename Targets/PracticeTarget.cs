using UnityEngine;
using TMPro;

/// <summary>
/// Advanced practice target with health display, respawn, and statistics
/// Perfect for aim training and weapon testing
/// </summary>
public class PracticeTarget : MonoBehaviour
{
    [Header("Target Settings")]
    public float maxHealth = 150f;
    public bool autoRespawn = true;
    public float respawnDelay = 2f;
    
    [Header("Visual Feedback")]
    public Color normalColor = Color.white;
    public Color hitColor = Color.red;
    public Color headshotColor = Color.yellow;
    public Color lowHealthColor = new Color(1f, 0.5f, 0f); // Orange
    
    [Header("UI (Optional)")]
    public TextMeshPro healthText;          // Shows HP above target
    public bool showHealthBar = true;
    
    [Header("Statistics")]
    public bool trackStats = true;
    
    private Renderer targetRenderer;
    private float currentHealth;
    private Vector3 startPosition;
    private Quaternion startRotation;
    
    // Statistics
    private int totalHits = 0;
    private int totalHeadshots = 0;
    private float totalDamage = 0f;
    
    private void Start()
    {
        currentHealth = maxHealth;
        targetRenderer = GetComponent<Renderer>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        
        if (targetRenderer != null)
        {
            targetRenderer.material.color = normalColor;
        }
        
        UpdateHealthDisplay();
    }
    
    public void TakeDamage(float damage, bool wasHeadshot = false)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        // Track statistics
        if (trackStats)
        {
            totalHits++;
            if (wasHeadshot) totalHeadshots++;
            totalDamage += damage;
        }
        
        // Debug log
        Debug.Log($"Target hit! Damage: {damage:F1}, Headshot: {wasHeadshot}, HP: {currentHealth:F1}/{maxHealth}");
        
        // Visual feedback
        if (targetRenderer != null)
        {
            StopAllCoroutines();
            StartCoroutine(FlashColor(wasHeadshot ? headshotColor : hitColor));
        }
        
        UpdateHealthDisplay();
        
        if (currentHealth <= 0f)
        {
            Die();
        }
    }
    
    private void Die()
    {
        // Print statistics
        if (trackStats)
        {
            float headshotPercent = totalHits > 0 ? (totalHeadshots / (float)totalHits) * 100f : 0f;
            Debug.Log($"<color=cyan>Target Destroyed!</color>");
            Debug.Log($"Total Hits: {totalHits}");
            Debug.Log($"Headshots: {totalHeadshots} ({headshotPercent:F1}%)");
            Debug.Log($"Total Damage: {totalDamage:F1}");
            Debug.Log($"Average Damage: {(totalHits > 0 ? totalDamage / totalHits : 0):F1}");
        }
        
        if (autoRespawn)
        {
            Invoke(nameof(Respawn), respawnDelay);
            gameObject.SetActive(false);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Respawn()
    {
        // Reset position and health
        transform.position = startPosition;
        transform.rotation = startRotation;
        currentHealth = maxHealth;
        
        // Reset statistics
        totalHits = 0;
        totalHeadshots = 0;
        totalDamage = 0f;
        
        // Reset visuals
        if (targetRenderer != null)
        {
            targetRenderer.material.color = normalColor;
        }
        
        UpdateHealthDisplay();
        gameObject.SetActive(true);
        
        Debug.Log("Target respawned!");
    }
    
    private void UpdateHealthDisplay()
    {
        if (healthText != null && showHealthBar)
        {
            healthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
            
            // Color based on health percentage
            float healthPercent = currentHealth / maxHealth;
            if (healthPercent <= 0.3f)
            {
                healthText.color = Color.red;
            }
            else if (healthPercent <= 0.6f)
            {
                healthText.color = lowHealthColor;
            }
            else
            {
                healthText.color = Color.white;
            }
        }
        
        // Change target color based on health
        if (targetRenderer != null && currentHealth > 0)
        {
            float healthPercent = currentHealth / maxHealth;
            if (healthPercent <= 0.3f)
            {
                targetRenderer.material.color = Color.Lerp(Color.red, normalColor, 0.5f);
            }
        }
    }
    
    private System.Collections.IEnumerator FlashColor(Color flashColor)
    {
        if (targetRenderer != null)
        {
            Color originalColor = targetRenderer.material.color;
            targetRenderer.material.color = flashColor;
            yield return new WaitForSeconds(0.1f);
            
            // Return to health-based color
            float healthPercent = currentHealth / maxHealth;
            if (healthPercent <= 0.3f && currentHealth > 0)
            {
                targetRenderer.material.color = Color.Lerp(Color.red, normalColor, 0.5f);
            }
            else
            {
                targetRenderer.material.color = normalColor;
            }
        }
    }
    
    // Public method to get statistics
    public void PrintStats()
    {
        float headshotPercent = totalHits > 0 ? (totalHeadshots / (float)totalHits) * 100f : 0f;
        Debug.Log($"=== Target Statistics ===");
        Debug.Log($"Total Hits: {totalHits}");
        Debug.Log($"Headshots: {totalHeadshots} ({headshotPercent:F1}%)");
        Debug.Log($"Total Damage: {totalDamage:F1}");
        Debug.Log($"Average Damage: {(totalHits > 0 ? totalDamage / totalHits : 0):F1}");
        Debug.Log($"Current HP: {currentHealth:F1}/{maxHealth}");
    }
}
