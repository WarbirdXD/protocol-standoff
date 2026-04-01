using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Firebase integration for temporal safety tracking - cloud-based spawn kill prevention.
/// Learns from all players to identify and avoid spawn kill zones globally.
/// RESEARCH ENHANCEMENT: Collective learning improves safety for entire player base.
/// </summary>
public class FirebaseTemporalSafety : MonoBehaviour
{
    [Header("Firebase Settings")]
    [Tooltip("Enable cloud safety tracking")]
    public bool enableCloudTracking = true;
    
    [Tooltip("Upload interval (seconds)")]
    public float uploadInterval = 20f;
    
    [Tooltip("Download interval (seconds)")]
    public float downloadInterval = 45f;
    
    [Header("Map Identification")]
    [Tooltip("Current map/scene name")]
    public string mapId = "default_map";
    
    private DatabaseReference databaseRef;
    private bool firebaseInitialized = false;
    
    // Local cache
    private List<SpawnKillEvent> pendingSpawnKills = new List<SpawnKillEvent>();
    private Dictionary<string, CloudSpawnKillZone> cloudSpawnKillZones = new Dictionary<string, CloudSpawnKillZone>();
    
    private float lastUploadTime;
    private float lastDownloadTime;
    
    /// <summary>
    /// Spawn kill event to upload
    /// </summary>
    [Serializable]
    public class SpawnKillEvent
    {
        public string mapId;
        public float spawnX, spawnY, spawnZ;
        public float deathX, deathY, deathZ;
        public float timeToKill; // How fast they died
        public long timestamp;
        
        public SpawnKillEvent(string map, Vector3 spawn, Vector3 death, float ttk)
        {
            mapId = map;
            spawnX = spawn.x;
            spawnY = spawn.y;
            spawnZ = spawn.z;
            deathX = death.x;
            deathY = death.y;
            deathZ = death.z;
            timeToKill = ttk;
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
    
    /// <summary>
    /// Cloud data for a spawn kill zone
    /// </summary>
    [Serializable]
    public class CloudSpawnKillZone
    {
        public string zoneKey;
        public int totalSpawnKills;
        public float avgTimeToKill;
        public float riskScore; // 0-100, higher = more dangerous
        public long lastSpawnKill;
        public long firstSpawnKill;
        
        public CloudSpawnKillZone()
        {
            totalSpawnKills = 0;
            avgTimeToKill = 0f;
            riskScore = 0f;
            lastSpawnKill = 0;
            firstSpawnKill = 0;
        }
    }
    
    void Start()
    {
        InitializeFirebase();
    }
    
    void Update()
    {
        if (!firebaseInitialized || !enableCloudTracking) return;
        
        // Periodic upload
        if (Time.time - lastUploadTime > uploadInterval && pendingSpawnKills.Count > 0)
        {
            UploadSpawnKillData();
            lastUploadTime = Time.time;
        }
        
        // Periodic download
        if (Time.time - lastDownloadTime > downloadInterval)
        {
            DownloadCloudData();
            lastDownloadTime = Time.time;
        }
    }
    
    /// <summary>
    /// Initialize Firebase
    /// </summary>
    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                databaseRef = FirebaseDatabase.DefaultInstance.RootReference;
                firebaseInitialized = true;
                
                Debug.Log("✅ Firebase Temporal Safety initialized");
                
                // Download existing spawn kill zones
                DownloadCloudData();
            }
            else
            {
                Debug.LogError($"❌ Firebase initialization failed: {task.Result}");
            }
        });
    }
    
    /// <summary>
    /// Record a spawn kill event (called by TemporalSafetyTracker)
    /// </summary>
    public void RecordSpawnKill(Vector3 spawnPosition, Vector3 deathPosition, float timeToKill)
    {
        if (!enableCloudTracking) return;
        
        var spawnKill = new SpawnKillEvent(
            mapId,
            spawnPosition,
            deathPosition,
            timeToKill
        );
        
        pendingSpawnKills.Add(spawnKill);
        
        Debug.Log($"📊 Recorded spawn kill: {timeToKill:F1}s survival");
        
        // Upload immediately if critical (very fast spawn kill)
        if (timeToKill < 1f && pendingSpawnKills.Count >= 3)
        {
            UploadSpawnKillData();
        }
    }
    
    /// <summary>
    /// Upload spawn kill data to Firebase
    /// </summary>
    private async void UploadSpawnKillData()
    {
        if (!firebaseInitialized || pendingSpawnKills.Count == 0) return;
        
        Debug.Log($"📤 Uploading {pendingSpawnKills.Count} spawn kill events to Firebase...");
        
        try
        {
            // Group by zone
            var groupedByZone = new Dictionary<string, List<SpawnKillEvent>>();
            
            foreach (var evt in pendingSpawnKills)
            {
                Vector3 spawnPos = new Vector3(evt.spawnX, evt.spawnY, evt.spawnZ);
                string zoneKey = GetZoneKey(spawnPos);
                
                if (!groupedByZone.ContainsKey(zoneKey))
                {
                    groupedByZone[zoneKey] = new List<SpawnKillEvent>();
                }
                
                groupedByZone[zoneKey].Add(evt);
            }
            
            // Upload each zone
            foreach (var kvp in groupedByZone)
            {
                await UploadZoneData(kvp.Key, kvp.Value);
            }
            
            pendingSpawnKills.Clear();
            Debug.Log("✅ Spawn kill data uploaded");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Upload failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Upload data for a specific zone
    /// </summary>
    private async Task UploadZoneData(string zoneKey, List<SpawnKillEvent> events)
    {
        string path = $"spawn_safety/{mapId}/kill_zones/{zoneKey}";
        
        // Get existing data
        var snapshot = await databaseRef.Child(path).GetValueAsync();
        
        CloudSpawnKillZone zoneData;
        
        if (snapshot.Exists)
        {
            string json = snapshot.GetRawJsonValue();
            zoneData = JsonUtility.FromJson<CloudSpawnKillZone>(json);
        }
        else
        {
            zoneData = new CloudSpawnKillZone
            {
                zoneKey = zoneKey,
                firstSpawnKill = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
        
        // Aggregate new events
        foreach (var evt in events)
        {
            float totalWeight = zoneData.totalSpawnKills;
            
            zoneData.avgTimeToKill = 
                (zoneData.avgTimeToKill * totalWeight + evt.timeToKill) / (totalWeight + 1);
            
            zoneData.totalSpawnKills++;
            zoneData.lastSpawnKill = evt.timestamp;
        }
        
        // Calculate risk score
        // More spawn kills = higher risk
        // Faster kills = higher risk
        // Recent kills = higher risk
        
        float frequencyScore = Mathf.Min(zoneData.totalSpawnKills / 10f, 1f) * 40f; // Max 40 points
        float speedScore = (1f - Mathf.Clamp01(zoneData.avgTimeToKill / 3f)) * 40f; // Max 40 points
        
        // Recency score (decays over time)
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long timeSinceLastKill = now - zoneData.lastSpawnKill;
        float daysSinceLastKill = timeSinceLastKill / (1000f * 60f * 60f * 24f);
        float recencyScore = Mathf.Max(0f, 20f - daysSinceLastKill * 2f); // Max 20 points, decays over 10 days
        
        zoneData.riskScore = frequencyScore + speedScore + recencyScore;
        zoneData.riskScore = Mathf.Clamp(zoneData.riskScore, 0f, 100f);
        
        // Upload
        string updatedJson = JsonUtility.ToJson(zoneData);
        await databaseRef.Child(path).SetRawJsonValueAsync(updatedJson);
        
        // Update local cache
        cloudSpawnKillZones[zoneKey] = zoneData;
    }
    
    /// <summary>
    /// Download cloud spawn kill zone data
    /// </summary>
    private async void DownloadCloudData()
    {
        if (!firebaseInitialized) return;
        
        Debug.Log("📥 Downloading spawn kill zones from Firebase...");
        
        try
        {
            string path = $"spawn_safety/{mapId}/kill_zones";
            var snapshot = await databaseRef.Child(path).GetValueAsync();
            
            if (snapshot.Exists)
            {
                cloudSpawnKillZones.Clear();
                
                foreach (var child in snapshot.Children)
                {
                    string json = child.GetRawJsonValue();
                    var zoneData = JsonUtility.FromJson<CloudSpawnKillZone>(json);
                    cloudSpawnKillZones[zoneData.zoneKey] = zoneData;
                }
                
                Debug.Log($"✅ Downloaded {cloudSpawnKillZones.Count} spawn kill zones");
            }
            else
            {
                Debug.Log("ℹ️ No spawn kill zone data available yet");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Download failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Get cloud risk score for a position (0-100)
    /// </summary>
    public float GetCloudRiskScore(Vector3 position)
    {
        string zoneKey = GetZoneKey(position);
        
        if (cloudSpawnKillZones.ContainsKey(zoneKey))
        {
            return cloudSpawnKillZones[zoneKey].riskScore;
        }
        
        // Check nearby zones
        float maxRisk = 0f;
        foreach (var kvp in cloudSpawnKillZones)
        {
            Vector3 zoneCenter = GetZoneCenter(kvp.Key);
            float distance = Vector3.Distance(position, zoneCenter);
            
            if (distance < 10f) // Within 10m
            {
                float distanceFalloff = 1f - (distance / 10f);
                float risk = kvp.Value.riskScore * distanceFalloff;
                maxRisk = Mathf.Max(maxRisk, risk);
            }
        }
        
        return maxRisk;
    }
    
    /// <summary>
    /// Check if position is in a known spawn kill zone
    /// </summary>
    public bool IsCloudSpawnKillZone(Vector3 position)
    {
        return GetCloudRiskScore(position) > 50f;
    }
    
    /// <summary>
    /// Get spawn kill zone data for a position
    /// </summary>
    public CloudSpawnKillZone GetZoneData(Vector3 position)
    {
        string zoneKey = GetZoneKey(position);
        
        if (cloudSpawnKillZones.ContainsKey(zoneKey))
        {
            return cloudSpawnKillZones[zoneKey];
        }
        
        return null;
    }
    
    /// <summary>
    /// Get total spawn kills tracked globally
    /// </summary>
    public int GetTotalCloudSpawnKills()
    {
        return cloudSpawnKillZones.Values.Sum(z => z.totalSpawnKills);
    }
    
    /// <summary>
    /// Get zone key for a position (10m grid)
    /// </summary>
    private string GetZoneKey(Vector3 position)
    {
        int gridX = Mathf.FloorToInt(position.x / 10f);
        int gridY = Mathf.FloorToInt(position.y / 10f);
        int gridZ = Mathf.FloorToInt(position.z / 10f);
        
        return $"{gridX}_{gridY}_{gridZ}";
    }
    
    /// <summary>
    /// Get center position of a zone
    /// </summary>
    private Vector3 GetZoneCenter(string zoneKey)
    {
        string[] parts = zoneKey.Split('_');
        
        if (parts.Length == 3)
        {
            float x = int.Parse(parts[0]) * 10f + 5f;
            float y = int.Parse(parts[1]) * 10f + 5f;
            float z = int.Parse(parts[2]) * 10f + 5f;
            
            return new Vector3(x, y, z);
        }
        
        return Vector3.zero;
    }
    
    /// <summary>
    /// Get cloud safety report
    /// </summary>
    public string GetCloudSafetyReport()
    {
        if (cloudSpawnKillZones.Count == 0)
        {
            return "No cloud spawn kill data available yet.";
        }
        
        int totalSpawnKills = cloudSpawnKillZones.Values.Sum(z => z.totalSpawnKills);
        float avgRisk = cloudSpawnKillZones.Values.Average(z => z.riskScore);
        
        var highRiskZones = cloudSpawnKillZones.Values
            .Where(z => z.riskScore > 50f)
            .OrderByDescending(z => z.riskScore)
            .ToList();
        
        string report = "=== CLOUD SPAWN SAFETY REPORT ===\n\n";
        report += $"Total Spawn Kills (All Players): {totalSpawnKills}\n";
        report += $"Tracked Kill Zones: {cloudSpawnKillZones.Count}\n";
        report += $"High Risk Zones: {highRiskZones.Count}\n";
        report += $"Average Risk Score: {avgRisk:F1}/100\n\n";
        
        if (highRiskZones.Count > 0)
        {
            report += "⚠️ DANGEROUS SPAWN ZONES:\n";
            foreach (var zone in highRiskZones.Take(5))
            {
                report += $"  Zone {zone.zoneKey}:\n";
                report += $"    Risk: {zone.riskScore:F1}/100\n";
                report += $"    Spawn Kills: {zone.totalSpawnKills}\n";
                report += $"    Avg Kill Time: {zone.avgTimeToKill:F1}s\n";
            }
        }
        
        return report;
    }
    
    void OnApplicationQuit()
    {
        // Upload remaining data
        if (pendingSpawnKills.Count > 0)
        {
            UploadSpawnKillData();
        }
    }
}
