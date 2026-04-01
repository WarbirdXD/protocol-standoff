using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows damage feedback with red vignette around screen edges
/// Like COD/Battlefield - no health bar, just visual feedback
/// </summary>
public class DamageOverlay : MonoBehaviour
{
    [Header("References")]
    public Image damageOverlay;             // Full-screen red overlay
    public PlayerHealth playerHealth;       // Reference to player health
    
    [Header("Damage Feedback")]
    public Color damageColor = new Color(1f, 0f, 0f, 0.5f); // Red, semi-transparent
    public float maxOverlayAlpha = 0.7f;    // Max opacity when near death
    public float damageFlashIntensity = 0.3f; // Flash intensity when hit
    public float damageFlashDuration = 0.2f; // How long flash lasts
    
    [Header("Recovery")]
    public float fadeSpeed = 2f;            // How fast overlay fades
    public float lowHealthThreshold = 0.3f; // Below 30% HP = danger
    public float criticalHealthPulse = 1.5f; // Pulse speed when critical
    
    [Header("Vignette Effect")]
    public bool useVignette = true;         // Use vignette instead of full overlay
    public float vignetteSize = 0.5f;       // How much of screen is affected
    
    private float currentAlpha = 0f;
    private float targetAlpha = 0f;
    private float damageFlashTimer = 0f;
    private float pulseTimer = 0f;
    
    private void Start()
    {
        if (damageOverlay == null)
        {
            Debug.LogError("DamageOverlay: No overlay image assigned!");
            return;
        }
        
        // Find player health if not assigned
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }
        
        // Subscribe to damage events
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged.AddListener(OnHealthChanged);
        }
        
        // Set initial alpha to 0
        SetOverlayAlpha(0f);
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged.RemoveListener(OnHealthChanged);
        }
    }
    
    private void Update()
    {
        if (playerHealth == null || damageOverlay == null) return;
        
        // Calculate target alpha based on health
        float healthPercent = playerHealth.CurrentHealth / playerHealth.maxHealth;
        
        // Base alpha increases as health decreases
        targetAlpha = Mathf.Lerp(0f, maxOverlayAlpha, 1f - healthPercent);
        
        // Add pulse effect when health is critical
        if (healthPercent <= lowHealthThreshold)
        {
            pulseTimer += Time.deltaTime * criticalHealthPulse;
            float pulse = (Mathf.Sin(pulseTimer) + 1f) * 0.5f; // 0 to 1
            targetAlpha += pulse * 0.2f; // Add pulsing
        }
        
        // Handle damage flash
        if (damageFlashTimer > 0f)
        {
            damageFlashTimer -= Time.deltaTime;
            float flashAlpha = (damageFlashTimer / damageFlashDuration) * damageFlashIntensity;
            currentAlpha = Mathf.Max(currentAlpha, targetAlpha + flashAlpha);
        }
        else
        {
            // Smooth fade to target
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }
        
        // Apply alpha
        SetOverlayAlpha(currentAlpha);
    }
    
    /// <summary>
    /// Called when player health changes
    /// </summary>
    private void OnHealthChanged(float newHealth)
    {
        // Trigger damage flash if health decreased
        if (newHealth < playerHealth.CurrentHealth)
        {
            damageFlashTimer = damageFlashDuration;
        }
    }
    
    /// <summary>
    /// Set overlay alpha
    /// </summary>
    private void SetOverlayAlpha(float alpha)
    {
        if (damageOverlay == null) return;
        
        Color color = damageColor;
        color.a = Mathf.Clamp01(alpha);
        damageOverlay.color = color;
    }
    
    /// <summary>
    /// Manually trigger damage flash (for testing)
    /// </summary>
    public void TriggerDamageFlash()
    {
        damageFlashTimer = damageFlashDuration;
    }
}
