using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// RESEARCH TOOL 2: Temporal Safety Tracker
/// Prevents spawn kills through temporal analysis and decaying safe zones.
/// Based on research: Call of Duty's combat intensity tracking with 8-second decay
/// </summary>
public class TemporalSafetyTracker : MonoBehaviour
{
    [Header("Temporal Safety Settings")]
    [Tooltip("Enable temporal safety tracking")]
    public bool enableSafety = true;
    
    [Tooltip("Time window to consider recent (seconds)")]
    public float recentTimeWindow = 8f;
    
    [Tooltip("Radius around combat events")]
    public float combatEventRadius = 15f;
    
    [Tooltip("Decay rate for combat intensity (per second)")]
    public float decayRate = 0.125f; // 1/8 = full decay in 8 seconds
    
    [Header("Spawn Kill Prevention")]
    [Tooltip("Minimum safe time after spawn (seconds)")]
    public float minimumSafeTime = 3f;
    
    [Tooltip("Penalty multiplier for locations with spawn kill history")]
    public float spawnKillPenalty = 200f;
    
    [Header("Visualization")]
    public bool showSafetyZones = true;
    public Color safeZoneColor = new Color(0f, 1f, 0f, 0.2f);
    public Color dangerZoneColor = new Color(1f, 0f, 0f, 0.3f);
    
    // Temporal tracking data
    private List<CombatEvent> combatEvents = new List<CombatEvent>();
    private Dictionary<Vector3, SpawnKillHistory> spawnKillLocations = new Dictionary<Vector3, SpawnKillHistory>();
    
    // Firebase integration
    private FirebaseTemporalSafety firebaseSafety;
    private Dictionary<GameObject, float> playerSpawnTimes = new Dictionary<GameObject, float>();
    
    /// <summary>
    /// Combat event (death, damage, etc.)
    /// </summary>
    private class CombatEvent
    {
        public Vector3 position;
        public float timestamp;
        public CombatEventType type;
        public float intensity; // 0-1, decays over time
        
        public float GetCurrentIntensity()
        {
            float age = Time.time - timestamp;
            return Mathf.Max(0f, intensity - age * 0.125f); // Decay over 8 seconds
        }
    }
    
    /// <summary>
    /// History of spawn kills at a location
    /// </summary>
    private class SpawnKillHistory
    {
        public Vector3 position;
        public int spawnKillCount;
        public float lastSpawnKillTime;
        public List<float> spawnKillTimes = new List<float>();
        
        public float GetSpawnKillRisk()
        {
            // Recent spawn kills = higher risk
            float recentKills = spawnKillTimes.Count(t => Time.time - t < 30f);
            return recentKills / 5f; // 5 recent kills = 100% risk
        }
    }
    
    public enum CombatEventType
    {
        Death,
        Damage,
        Gunfire,
        Explosion
    }
    
    void Start()
    {
        // Get Firebase component
        firebaseSafety = GetComponent<FirebaseTemporalSafety>();
    }
    
    /// <summary>
    /// Track player spawn time (for spawn kill detection)
    /// </summary>
    public void TrackPlayerSpawn(GameObject player)
    {
        playerSpawnTimes[player] = Time.time;
    }
    
    /// <summary>
    /// Register a combat event
    /// </summary>
    public void RegisterCombatEvent(Vector3 position, CombatEventType type, float intensity = 1f)
    {
        if (!enableSafety) return;
        
        combatEvents.Add(new CombatEvent
        {
            position = position,
            timestamp = Time.time,
            type = type,
            intensity = intensity
        });
        
        // Cleanup old events
        CleanupOldEvents();
    }
    
    /// <summary>
    /// Register a spawn kill (for learning)
    /// </summary>
    public void RegisterSpawnKill(Vector3 spawnPosition, Vector3 deathPosition, float timeToKill)
    {
        if (!enableSafety) return;
        
        // Round position to grid for clustering
        Vector3 gridPos = RoundToGrid(spawnPosition, 5f);
        
        if (!spawnKillLocations.ContainsKey(gridPos))
        {
            spawnKillLocations[gridPos] = new SpawnKillHistory
            {
                position = gridPos,
                spawnKillCount = 0,
                spawnKillTimes = new List<float>()
            };
        }
        
        var history = spawnKillLocations[gridPos];
        history.spawnKillCount++;
        history.lastSpawnKillTime = Time.time;
        history.spawnKillTimes.Add(Time.time);
        
        // Keep only recent history (last 50 spawn kills)
        if (history.spawnKillTimes.Count > 50)
        {
            history.spawnKillTimes.RemoveAt(0);
        }
        
        // Upload to Firebase
        if (firebaseSafety != null)
        {
            firebaseSafety.RecordSpawnKill(spawnPosition, deathPosition, timeToKill);
        }
        
        Debug.LogWarning($"⚠️ Spawn kill registered at {gridPos} (Total: {history.spawnKillCount})");
    }
    
    /// <summary>
    /// Calculate safety score for a spawn position (0-100)
    /// Higher = safer
    /// </summary>
    public float CalculateSafetyScore(Vector3 position)
    {
        if (!enableSafety) return 100f;
        
        float safetyScore = 100f;
        
        // 1. Check combat intensity in area
        float combatIntensity = GetCombatIntensity(position);
        safetyScore -= combatIntensity * 80f; // Max -80 for active combat
        
        // 2. Check spawn kill history
        float spawnKillRisk = GetSpawnKillRisk(position);
        safetyScore -= spawnKillRisk * spawnKillPenalty; // Massive penalty for spawn kill zones
        
        // 3. Check recent deaths nearby
        float recentDeathIntensity = GetRecentDeathIntensity(position);
        safetyScore -= recentDeathIntensity * 60f; // -60 for very recent deaths
        
        return Mathf.Max(0f, safetyScore);
    }
    
    /// <summary>
    /// Get combat intensity at a position (0-1)
    /// </summary>
    private float GetCombatIntensity(Vector3 position)
    {
        float totalIntensity = 0f;
        
        foreach (var evt in combatEvents)
        {
            float distance = Vector3.Distance(position, evt.position);
            
            if (distance < combatEventRadius)
            {
                float currentIntensity = evt.GetCurrentIntensity();
                float distanceFalloff = 1f - (distance / combatEventRadius);
                totalIntensity += currentIntensity * distanceFalloff;
            }
        }
        
        return Mathf.Clamp01(totalIntensity);
    }
    
    /// <summary>
    /// Get spawn kill risk at a position (0-1)
    /// </summary>
    private float GetSpawnKillRisk(Vector3 position)
    {
        Vector3 gridPos = RoundToGrid(position, 5f);
        
        // Check exact grid cell
        if (spawnKillLocations.ContainsKey(gridPos))
        {
            return spawnKillLocations[gridPos].GetSpawnKillRisk();
        }
        
        // Check nearby cells
        float maxRisk = 0f;
        foreach (var kvp in spawnKillLocations)
        {
            float distance = Vector3.Distance(position, kvp.Key);
            if (distance < 10f)
            {
                float risk = kvp.Value.GetSpawnKillRisk();
                float distanceFalloff = 1f - (distance / 10f);
                maxRisk = Mathf.Max(maxRisk, risk * distanceFalloff);
            }
        }
        
        return maxRisk;
    }
    
    /// <summary>
    /// Get recent death intensity at a position (0-1)
    /// </summary>
    private float GetRecentDeathIntensity(Vector3 position)
    {
        float intensity = 0f;
        
        var recentDeaths = combatEvents
            .Where(e => e.type == CombatEventType.Death)
            .Where(e => Time.time - e.timestamp < recentTimeWindow);
        
        foreach (var death in recentDeaths)
        {
            float distance = Vector3.Distance(position, death.position);
            
            if (distance < combatEventRadius)
            {
                float age = Time.time - death.timestamp;
                float timeFalloff = 1f - (age / recentTimeWindow);
                float distanceFalloff = 1f - (distance / combatEventRadius);
                intensity += timeFalloff * distanceFalloff;
            }
        }
        
        return Mathf.Clamp01(intensity);
    }
    
    /// <summary>
    /// Check if position is in a temporal safe zone
    /// </summary>
    public bool IsTemporallySafe(Vector3 position)
    {
        return CalculateSafetyScore(position) > 50f;
    }
    
    /// <summary>
    /// Get recommended minimum distance from combat
    /// </summary>
    public float GetRecommendedSafeDistance(Vector3 combatPosition)
    {
        // More recent combat = larger safe distance
        var recentCombat = combatEvents
            .Where(e => Vector3.Distance(e.position, combatPosition) < 5f)
            .OrderByDescending(e => e.timestamp)
            .FirstOrDefault();
        
        if (recentCombat != null)
        {
            float age = Time.time - recentCombat.timestamp;
            
            if (age < 2f)
                return 30f; // Very recent - stay far away
            else if (age < 5f)
                return 20f; // Recent - moderate distance
            else
                return 15f; // Older - standard distance
        }
        
        return 15f; // Default safe distance
    }
    
    /// <summary>
    /// Cleanup old combat events
    /// </summary>
    private void CleanupOldEvents()
    {
        float cutoffTime = Time.time - recentTimeWindow * 2f; // Keep 2x window for safety
        combatEvents.RemoveAll(e => e.timestamp < cutoffTime);
    }
    
    /// <summary>
    /// Round position to grid for clustering
    /// </summary>
    private Vector3 RoundToGrid(Vector3 position, float gridSize)
    {
        return new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize,
            Mathf.Round(position.y / gridSize) * gridSize,
            Mathf.Round(position.z / gridSize) * gridSize
        );
    }
    
    /// <summary>
    /// Get debug info for a position
    /// </summary>
    public string GetDebugInfo(Vector3 position)
    {
        float safetyScore = CalculateSafetyScore(position);
        float combatIntensity = GetCombatIntensity(position);
        float spawnKillRisk = GetSpawnKillRisk(position);
        float deathIntensity = GetRecentDeathIntensity(position);
        
        string info = $"=== Temporal Safety Analysis ===\n";
        info += $"Position: {position}\n";
        info += $"Safety Score: {safetyScore:F1}/100\n";
        info += $"Combat Intensity: {combatIntensity:P0}\n";
        info += $"Spawn Kill Risk: {spawnKillRisk:P0}\n";
        info += $"Recent Death Intensity: {deathIntensity:P0}\n";
        info += $"Status: {(IsTemporallySafe(position) ? "SAFE" : "DANGER")}\n";
        
        return info;
    }
    
    /// <summary>
    /// Visualize safety zones
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showSafetyZones || !enableSafety) return;
        
        // Draw active combat zones
        foreach (var evt in combatEvents)
        {
            float intensity = evt.GetCurrentIntensity();
            if (intensity > 0.1f)
            {
                Color color = Color.Lerp(safeZoneColor, dangerZoneColor, intensity);
                Gizmos.color = color;
                Gizmos.DrawWireSphere(evt.position, combatEventRadius * intensity);
            }
        }
        
        // Draw spawn kill zones
        foreach (var kvp in spawnKillLocations)
        {
            float risk = kvp.Value.GetSpawnKillRisk();
            if (risk > 0.1f)
            {
                Gizmos.color = new Color(1f, 0f, 0f, risk * 0.5f);
                Gizmos.DrawSphere(kvp.Key, 3f);
                
                // Draw warning indicator
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(kvp.Key, 10f);
            }
        }
    }
    
    void Update()
    {
        if (enableSafety)
        {
            CleanupOldEvents();
        }
    }
}
