using UnityEngine;

/// <summary>
/// Example integration showing how to use the Firebase-enhanced spawn system.
/// This demonstrates the complete flow from spawn to death tracking.
/// </summary>
public class SpawnSystemIntegrationExample : MonoBehaviour
{
    [Header("References")]
    public DynamicSpawnSystem spawnSystem;
    
    private SpawnQualityAnalyzer qualityAnalyzer;
    private TemporalSafetyTracker safetyTracker;
    
    void Start()
    {
        // Get components (they're auto-added by DynamicSpawnSystem)
        if (spawnSystem == null)
        {
            spawnSystem = FindFirstObjectByType<DynamicSpawnSystem>();
        }
        
        qualityAnalyzer = spawnSystem.GetComponent<SpawnQualityAnalyzer>();
        safetyTracker = spawnSystem.GetComponent<TemporalSafetyTracker>();
        
        Debug.Log("✅ Spawn system with Firebase integration ready!");
    }
    
    /// <summary>
    /// Call this when a player spawns
    /// </summary>
    public void OnPlayerSpawn(GameObject player)
    {
        // The DynamicSpawnSystem.SpawnPlayer() already handles this,
        // but if you're spawning manually, you need to track it:
        
        Vector3 spawnPosition = player.transform.position;
        
        // Track with quality analyzer
        if (qualityAnalyzer != null)
        {
            qualityAnalyzer.RegisterSpawn(player, spawnPosition);
        }
        
        // Track spawn time for spawn kill detection
        if (safetyTracker != null)
        {
            safetyTracker.TrackPlayerSpawn(player);
        }
        
        Debug.Log($"📍 Player {player.name} spawned at {spawnPosition}");
    }
    
    /// <summary>
    /// Call this when a player takes damage
    /// </summary>
    public void OnPlayerDamaged(GameObject player, float damage)
    {
        // Track first damage for spawn quality analysis
        if (qualityAnalyzer != null)
        {
            qualityAnalyzer.RegisterDamage(player);
        }
    }
    
    /// <summary>
    /// Call this when a player dies
    /// </summary>
    public void OnPlayerDeath(GameObject player, Vector3 deathPosition)
    {
        // 1. Track death with quality analyzer (completes spawn tracking)
        if (qualityAnalyzer != null)
        {
            qualityAnalyzer.RegisterDeath(player);
        }
        
        // 2. Track spawn kill if it happened within 3 seconds
        // Note: Spawn kill detection is handled automatically by TemporalSafetyTracker
        // when you call RegisterDeath on the spawn system
        
        // 3. Register death location with spawn system
        if (spawnSystem != null)
        {
            spawnSystem.RegisterDeath(deathPosition);
        }
        
        Debug.Log($"💀 Player {player.name} died at {deathPosition}");
        
        // Data is automatically uploaded to Firebase!
    }
    
    /// <summary>
    /// Call this when a player gets a kill
    /// </summary>
    public void OnPlayerKill(GameObject killer, Vector3 killPosition)
    {
        // Register kill location for combat zone tracking
        if (spawnSystem != null)
        {
            spawnSystem.RegisterKill(killPosition);
        }
        
        // Track combat event
        if (safetyTracker != null)
        {
            safetyTracker.RegisterCombatEvent(killPosition, TemporalSafetyTracker.CombatEventType.Gunfire, 0.8f);
        }
    }
    
    /// <summary>
    /// Get analytics report (for debugging/research)
    /// </summary>
    public void ShowAnalyticsReport()
    {
        if (qualityAnalyzer != null)
        {
            Debug.Log(qualityAnalyzer.GetAnalyticsReport());
        }
        
        var firebaseAnalytics = spawnSystem.GetComponent<FirebaseSpawnAnalytics>();
        if (firebaseAnalytics != null)
        {
            Debug.Log(firebaseAnalytics.GetCloudAnalyticsReport());
        }
        
        var firebaseSafety = spawnSystem.GetComponent<FirebaseTemporalSafety>();
        if (firebaseSafety != null)
        {
            Debug.Log(firebaseSafety.GetCloudSafetyReport());
        }
    }
}
