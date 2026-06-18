using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum GameMode
{
    OneVsOne,
    TwoVsTwo
}

/// <summary>
/// Dynamic spawn system that generates spawn positions procedurally
/// No static spawn points - impossible to learn or predict
/// Works with spherical arenas and any surface orientation
/// Perfect for competitive 1v1/2v2 gameplay
/// </summary>
public class DynamicSpawnSystem : MonoBehaviour
{
    [Header("Game Mode")]
    public GameMode gameMode = GameMode.OneVsOne;
    
    [Header("Map Boundaries")]
    [Tooltip("Center point of the building/map")]
    public Vector3 mapCenter = Vector3.zero;
    
    [Tooltip("Use box bounds instead of circular bounds (better for buildings)")]
    public bool useBoxBounds = true;
    
    [Tooltip("Box size for building bounds (X=width, Y=height, Z=depth)")]
    public Vector3 boxSize = new Vector3(50f, 30f, 50f);
    
    [Tooltip("Radius for circular map bounds (legacy, use when useBoxBounds=false)")]
    public float mapRadius = 25f;
    
    [Tooltip("Max raycast distance for finding surfaces")]
    public float maxRaycastDistance = 50f;
    
    public LayerMask groundLayer;
    public LayerMask obstacleLayer;
    
    [Header("Spawn Generation")]
    [Tooltip("How many potential spawns to generate and evaluate")]
    public int candidateSpawnCount = 100;
    
    [Tooltip("Number of vertical samples per horizontal position (for multi-level buildings)")]
    public int verticalSamples = 5;
    
    [Tooltip("Minimum clearance from surface for spawn")]
    public float minSurfaceClearance = 0.5f;
    
    [Tooltip("Allow spawning on walls and ceilings")]
    public bool allowAllSurfaces = true;
    
    [Tooltip("Player capsule height for spawn clearance check")]
    public float playerHeight = 2f;
    
    [Tooltip("Player capsule radius for spawn clearance check")]
    public float playerRadius = 0.5f;
    
    [Header("1v1/2v2 Balance Settings")]
    [Tooltip("Minimum distance from enemies")]
    public float minEnemyDistance = 25f;
    
    [Tooltip("Maximum distance from enemies (prevents edge spawns)")]
    public float maxEnemyDistance = 60f;
    
    [Tooltip("Prevent spawning behind enemies")]
    public bool preventBackSpawns = true;
    
    [Tooltip("Ensure balanced map control")]
    public bool balanceMapControl = true;
    
    [Header("2v2 Team Spawn Settings")]
    [Tooltip("Distance between teammates on spawn")]
    public float teammateSpawnDistance = 8f;    // Close but not stacked
    
    [Tooltip("Max angle between teammates (prevents line formation)")]
    public float teammateSpawnAngle = 45f;      // Spread out slightly
    
    [Header("Safety Checks")]
    [Tooltip("No line of sight to enemies")]
    public bool requireCover = true;
    
    [Tooltip("Check enemy field of view")]
    public bool checkEnemyFOV = true;
    
    [Tooltip("Enemy FOV angle (degrees)")]
    public float enemyFOVAngle = 90f;
    
    [Tooltip("Enemy FOV distance")]
    public float enemyFOVDistance = 40f;
    
    [Tooltip("Predict enemy movement after kill")]
    public bool predictEnemyMovement = true;
    
    [Tooltip("Movement prediction time (seconds)")]
    public float movementPredictionTime = 2f;
    
    [Tooltip("Minimum distance from last death")]
    public float minDeathDistance = 20f;
    
    [Tooltip("Avoid recent combat zones")]
    public float combatZoneRadius = 15f;
    public float combatZoneDecayTime = 8f;
    
    [Header("Firebase Cloud Learning")]
    [Tooltip("Use cloud data to improve spawn quality")]
    public bool useCloudData = true;
    
    [Tooltip("Weight for cloud quality data (0-1)")]
    [Range(0f, 1f)]
    public float cloudDataWeight = 0.7f;
    
    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool showDebugLogs = true;
    
    [Header("Debug - Floor Visualization")]
    [Tooltip("Show floor level indicators in gizmos")]
    public bool showFloorLevels = true;
    
    [Tooltip("Height between floors (meters)")]
    public float floorHeight = 3f;
    
    [Tooltip("Color for floor level indicators")]
    public Color floorLevelColor = new Color(0f, 1f, 1f, 0.2f);
    
    [Tooltip("Show floor numbers in scene view")]
    public bool showFloorNumbers = true;
    
    // Runtime data
    private Dictionary<Vector3, float> recentDeaths = new Dictionary<Vector3, float>();
    private Dictionary<Vector3, float> combatZones = new Dictionary<Vector3, float>();
    private List<SpawnPoint> recentSpawns = new List<SpawnPoint>();
    private SpawnPoint lastSpawn;
    
    // Firebase integration components
    private SpawnQualityAnalyzer qualityAnalyzer;
    private TemporalSafetyTracker safetyTracker;
    private FirebaseSpawnAnalytics firebaseAnalytics;
    private FirebaseTemporalSafety firebaseSafety;
    
    // Advanced enemy prediction
    private AdvancedEnemyPredictor enemyPredictor;
    
    // Spawn point data structure
    private struct SpawnPoint
    {
        public Vector3 position;
        public Vector3 normal;
        public Quaternion rotation;
        
        public SpawnPoint(Vector3 pos, Vector3 norm)
        {
            position = pos;
            normal = norm;
            // Calculate rotation to align player with surface
            rotation = Quaternion.FromToRotation(Vector3.up, norm) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }
    
    void Start()
    {
        // Initialize Firebase components
        InitializeFirebaseComponents();
    }
    
    /// <summary>
    /// Initialize Firebase analytics components
    /// </summary>
    private void InitializeFirebaseComponents()
    {
        qualityAnalyzer = GetComponent<SpawnQualityAnalyzer>();
        safetyTracker = GetComponent<TemporalSafetyTracker>();
        firebaseAnalytics = GetComponent<FirebaseSpawnAnalytics>();
        firebaseSafety = GetComponent<FirebaseTemporalSafety>();
        
        // Auto-add components if missing
        if (qualityAnalyzer == null)
        {
            qualityAnalyzer = gameObject.AddComponent<SpawnQualityAnalyzer>();
        }
        
        if (safetyTracker == null)
        {
            safetyTracker = gameObject.AddComponent<TemporalSafetyTracker>();
        }
        
        if (useCloudData)
        {
            if (firebaseAnalytics == null)
            {
                firebaseAnalytics = gameObject.AddComponent<FirebaseSpawnAnalytics>();
                firebaseAnalytics.mapId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            }
            
            if (firebaseSafety == null)
            {
                firebaseSafety = gameObject.AddComponent<FirebaseTemporalSafety>();
                firebaseSafety.mapId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            }
            
            Debug.Log("✅ Firebase cloud learning enabled for spawn system");
        }
        
        // Initialize advanced enemy predictor
        if (predictEnemyMovement)
        {
            if (enemyPredictor == null)
            {
                enemyPredictor = gameObject.AddComponent<AdvancedEnemyPredictor>();
                enemyPredictor.predictionTime = movementPredictionTime;
                enemyPredictor.coverLayer = obstacleLayer;
            }
            
            Debug.Log("✅ Advanced enemy prediction enabled");
        }
    }
    
    /// <summary>
    /// Find the best dynamic spawn position for a player
    /// Returns position, rotation based on surface normal
    /// </summary>
    public (Vector3 position, Quaternion rotation)? GetBestSpawnPosition(GameObject player, Vector3? deathLocation = null, GameObject teammate = null, Dictionary<GameObject, (Vector3 pos, Vector3 fwd)> enemySnapshot = null)
    {
        CleanupOldData();
        
        if (deathLocation.HasValue)
        {
            RegisterDeath(deathLocation.Value);
        }
        
        // Find all players
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        List<GameObject> enemies = allPlayers.Where(p => p != player && p != teammate && p != null).ToList();
        
        if (enemies.Count == 0)
        {
            Debug.LogWarning("DynamicSpawnSystem: No enemies found, using random spawn");
            return GenerateRandomValidSpawn();
        }
        
        // Generate candidate spawn positions
        List<SpawnPoint> candidates = GenerateCandidateSpawns();
        
        if (candidates.Count == 0)
        {
            Debug.LogError("DynamicSpawnSystem: No valid spawn candidates found!");
            return null;
        }
        
        // Score all candidates
        SpawnPoint? bestSpawn = null;
        float bestScore = float.MinValue;
        
        foreach (SpawnPoint candidate in candidates)
        {
            float score = ScoreSpawnPosition(candidate.position, enemies, deathLocation, teammate, enemySnapshot);
            
            if (showDebugLogs)
            {
                Debug.Log($"Candidate {candidate.position}: Score = {score:F2}");
            }
            
            if (score > bestScore)
            {
                bestScore = score;
                bestSpawn = candidate;
            }
        }
        
        if (bestSpawn.HasValue)
        {
            lastSpawn = bestSpawn.Value;
            recentSpawns.Add(bestSpawn.Value);
            
            // Keep only last 5 spawns
            if (recentSpawns.Count > 5)
            {
                recentSpawns.RemoveAt(0);
            }
            
            if (showDebugLogs)
            {
                Debug.Log($"<color=green>Selected spawn at {bestSpawn.Value.position} with score {bestScore:F2}</color>");
            }
            
            return (bestSpawn.Value.position, bestSpawn.Value.rotation);
        }
        
        return null;
    }
    
    /// <summary>
    /// Generate random candidate spawn positions across the circular map
    /// Now supports walls and ceilings!
    /// </summary>
    private List<SpawnPoint> GenerateCandidateSpawns()
    {
        List<SpawnPoint> candidates = new List<SpawnPoint>();
        int attempts = 0;
        int maxAttempts = candidateSpawnCount * 3; // Try 3x to get enough valid spawns
        
        while (candidates.Count < candidateSpawnCount && attempts < maxAttempts)
        {
            attempts++;
            
            // Generate random position within map bounds
            Vector3 randomPos;
            
            if (useBoxBounds)
            {
                // Box bounds - better for buildings with multiple levels
                randomPos = new Vector3(
                    mapCenter.x + Random.Range(-boxSize.x * 0.5f, boxSize.x * 0.5f),
                    mapCenter.y + Random.Range(-boxSize.y * 0.5f, boxSize.y * 0.5f),
                    mapCenter.z + Random.Range(-boxSize.z * 0.5f, boxSize.z * 0.5f)
                );
            }
            else
            {
                // Circular bounds - legacy for arena-style maps
                float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float randomDistance = Random.Range(0f, mapRadius);
                
                randomPos = new Vector3(
                    mapCenter.x + Mathf.Cos(randomAngle) * randomDistance,
                    mapCenter.y + boxSize.y * 0.5f,
                    mapCenter.z + Mathf.Sin(randomAngle) * randomDistance
                );
            }
            
            if (allowAllSurfaces)
            {
                // Cast rays in all directions to find ANY surface (floor, wall, ceiling)
                Vector3[] directions = {
                    Vector3.down,      // Floor
                    Vector3.up,        // Ceiling
                    Vector3.forward,   // Walls
                    Vector3.back,
                    Vector3.left,
                    Vector3.right,
                    new Vector3(1, -1, 0).normalized,  // Diagonal down
                };
                
                foreach (Vector3 dir in directions)
                {
                    // Check both ground and obstacle layers for spawn surfaces
                    if (Physics.Raycast(randomPos, dir, out RaycastHit hit, maxRaycastDistance, groundLayer | obstacleLayer))
                    {
                        Vector3 spawnPos = hit.point + hit.normal * minSurfaceClearance;
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"Raycast hit surface at {hit.point}, checking spawn at {spawnPos}, surface: {hit.collider.name}, layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
                        }
                        
                        // Validate spawn position
                        if (IsValidSpawnPosition(spawnPos, hit.normal))
                        {
                            candidates.Add(new SpawnPoint(spawnPos, hit.normal));
                            if (showDebugLogs)
                            {
                                Debug.Log($"<color=cyan>Added candidate spawn at {spawnPos}</color>");
                            }
                            break; // Found valid spawn, move to next attempt
                        }
                    }
                }
            }
            else
            {
                // Original behavior - only floors
                if (Physics.Raycast(randomPos, Vector3.down, out RaycastHit hit, maxRaycastDistance, groundLayer | obstacleLayer))
                {
                    Vector3 spawnPos = hit.point + Vector3.up * minSurfaceClearance;
                    
                    // Validate spawn position
                    if (IsValidSpawnPosition(spawnPos, hit.normal))
                    {
                        candidates.Add(new SpawnPoint(spawnPos, hit.normal));
                    }
                }
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"Generated {candidates.Count} valid spawn candidates from {attempts} attempts");
        }
        
        return candidates;
    }
    
    /// <summary>
    /// Check if a position is valid for spawning
    /// </summary>
    private bool IsValidSpawnPosition(Vector3 position, Vector3 surfaceNormal)
    {
        
        // Check for obstacles using player-sized capsule (more accurate for tight spaces)
        if (playerHeight > 0 && playerRadius > 0)
        {
            Vector3 capsuleBottom = position + surfaceNormal * (playerRadius + 0.1f);
            Vector3 capsuleTop = capsuleBottom + surfaceNormal * (playerHeight - playerRadius * 2f);
            
            Collider[] obstacles = Physics.OverlapCapsule(capsuleBottom, capsuleTop, playerRadius, obstacleLayer);
            if (obstacles.Length > 0)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"Spawn position {position} rejected: {obstacles.Length} obstacles in player capsule - {string.Join(", ", System.Array.ConvertAll(obstacles, o => o.name))}");
                }
                return false;
            }
            
            // Additional check: ensure there's enough headroom above spawn
            if (Physics.Raycast(position, surfaceNormal, playerHeight + 0.5f, obstacleLayer))
            {
                if (showDebugLogs)
                {
                    Debug.Log($"Spawn position {position} rejected: insufficient headroom");
                }
                return false;
            }
        }
        
        // Check if position is inside map bounds
        if (useBoxBounds)
        {
            // Box bounds check
            Vector3 halfSize = boxSize * 0.5f;
            if (Mathf.Abs(position.x - mapCenter.x) > halfSize.x ||
                Mathf.Abs(position.y - mapCenter.y) > halfSize.y ||
                Mathf.Abs(position.z - mapCenter.z) > halfSize.z)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"Spawn position {position} rejected: outside box bounds");
                }
                return false;
            }
        }
        else
        {
            // Circular bounds check (legacy)
            float distanceFromCenter = Vector3.Distance(new Vector3(position.x, mapCenter.y, position.z), 
                                                         new Vector3(mapCenter.x, mapCenter.y, mapCenter.z));
            if (distanceFromCenter > mapRadius)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"Spawn position {position} rejected: outside map bounds (distance {distanceFromCenter:F1} > radius {mapRadius})");
                }
                return false;
            }
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"<color=green>Spawn position {position} VALID</color>");
        }
        
        return true;
    }
    
    /// <summary>
    /// Score a spawn position based on game state and teammate position (for 2v2)
    /// NOW WITH FIREBASE CLOUD LEARNING!
    /// </summary>
    private float ScoreSpawnPosition(Vector3 spawnPos, List<GameObject> enemies, Vector3? deathLocation, GameObject teammate = null, Dictionary<GameObject, (Vector3 pos, Vector3 fwd)> enemySnapshot = null)
    {
        float score = 100f;
        
        // FIREBASE INTEGRATION: Use cloud data for improved scoring
        if (useCloudData)
        {
            // 1. Cloud quality score (from all players' experience)
            if (firebaseAnalytics != null)
            {
                float cloudQuality = firebaseAnalytics.GetCloudQualityScore(spawnPos);
                float qualityBonus = (cloudQuality - 50f) * 2f * cloudDataWeight; // -100 to +100 range
                score += qualityBonus;
                
                if (showDebugLogs)
                {
                    Debug.Log($"Cloud quality at {spawnPos}: {cloudQuality:F1}/100 → bonus: {qualityBonus:F1}");
                }
                
                // Extra penalty for known spawn kill zones
                if (firebaseAnalytics.IsCloudSpawnKillZone(spawnPos))
                {
                    score -= 150f * cloudDataWeight;
                    if (showDebugLogs)
                    {
                        Debug.LogWarning($"⚠️ Cloud data: Spawn kill zone detected at {spawnPos}");
                    }
                }
            }
            
            // 2. Cloud safety/risk score (temporal danger zones)
            if (firebaseSafety != null)
            {
                float cloudRisk = firebaseSafety.GetCloudRiskScore(spawnPos);
                float riskPenalty = cloudRisk * 1.5f * cloudDataWeight; // 0-150 penalty
                score -= riskPenalty;
                
                if (showDebugLogs && cloudRisk > 30f)
                {
                    Debug.Log($"Cloud risk at {spawnPos}: {cloudRisk:F1}/100 → penalty: {riskPenalty:F1}");
                }
            }
        }
        
        // Get predicted enemy positions (using advanced predictor)
        Dictionary<GameObject, Vector3> predictedPositions = new Dictionary<GameObject, Vector3>();
        Dictionary<GameObject, float> predictionConfidence = new Dictionary<GameObject, float>();
        
        if (predictEnemyMovement)
        {
            foreach (GameObject enemy in enemies)
            {
                if (enemy == null) continue;
                
                // Use advanced predictor if available
                if (enemyPredictor != null)
                {
                    predictedPositions[enemy] = enemyPredictor.PredictEnemyPosition(enemy, deathLocation, mapCenter, boxSize);
                    predictionConfidence[enemy] = enemyPredictor.GetPredictionConfidence(enemy);
                    
                    if (showDebugLogs)
                    {
                        Debug.Log($"Advanced prediction for {enemy.name}: {predictedPositions[enemy]} (confidence: {predictionConfidence[enemy]:P0})");
                    }
                }
                else
                {
                    // Fallback to basic prediction
                    predictedPositions[enemy] = PredictEnemyPosition(enemy, deathLocation);
                    predictionConfidence[enemy] = 0.5f;
                }
            }
        }
        
        // 2v2 SPECIFIC: Teammate proximity scoring
        if (gameMode == GameMode.TwoVsTwo && teammate != null && !teammate.GetComponent<PlayerHealth>().IsDead)
        {
            float teammateDistance = Vector3.Distance(spawnPos, teammate.transform.position);
            
            // Reward spawning near teammate (but not too close)
            if (teammateDistance < teammateSpawnDistance * 0.5f)
            {
                // Too close - penalty
                score -= 50f;
            }
            else if (teammateDistance >= teammateSpawnDistance * 0.8f && teammateDistance <= teammateSpawnDistance * 2f)
            {
                // Perfect distance - big reward
                score += 80f;
            }
            else if (teammateDistance > teammateSpawnDistance * 3f)
            {
                // Too far - penalty (prevents splitting team)
                score -= (teammateDistance - teammateSpawnDistance * 3f) * 5f;
            }
            
            // Check angle between teammates (prevent line formation)
            Vector3 toTeammate = (teammate.transform.position - spawnPos).normalized;
            Vector3 toMapCenter = (mapCenter - spawnPos).normalized;
            float angleToTeammate = Vector3.Angle(toTeammate, toMapCenter);
            
            // Reward spawns that create a spread formation (not in a line)
            if (angleToTeammate > teammateSpawnAngle && angleToTeammate < 180f - teammateSpawnAngle)
            {
                score += 40f; // Good tactical positioning
            }
        }
        
        // 1. Distance from enemies (CRITICAL for 1v1/2v2)
        // Check both current and predicted positions
        float closestEnemyDist = float.MaxValue;
        GameObject closestEnemy = null;
        
        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;
            
            // Check current (or death-time snapshot) position
            Vector3 enemyPos = enemySnapshot != null && enemySnapshot.ContainsKey(enemy)
                ? enemySnapshot[enemy].pos : enemy.transform.position;
            float currentDist = Vector3.Distance(spawnPos, enemyPos);
            
            // Check predicted position if available
            float dist = currentDist;
            if (predictedPositions.ContainsKey(enemy))
            {
                float predictedDist = Vector3.Distance(spawnPos, predictedPositions[enemy]);
                // Use the closer of the two (more conservative)
                dist = Mathf.Min(currentDist, predictedDist);
            }
            
            if (dist < closestEnemyDist)
            {
                closestEnemyDist = dist;
                closestEnemy = enemy;
            }
        }
        
        // Enforce minimum distance
        if (closestEnemyDist < minEnemyDistance)
        {
            score -= (minEnemyDistance - closestEnemyDist) * 20f;
        }
        else if (closestEnemyDist > maxEnemyDistance)
        {
            // Penalize spawns too far away (edge camping)
            score -= (closestEnemyDist - maxEnemyDistance) * 5f;
        }
        else
        {
            // Reward optimal distance
            score += 50f;
        }
        
        // 2. Line of sight check (no spawn kills)
        if (requireCover && closestEnemy != null)
        {
            // Check LOS to snapshot (or current) position
            Vector3 closestEnemyPos = enemySnapshot != null && enemySnapshot.ContainsKey(closestEnemy)
                ? enemySnapshot[closestEnemy].pos : closestEnemy.transform.position;
            Vector3 toEnemy = closestEnemyPos - spawnPos;
            bool hasLOS = !Physics.Raycast(spawnPos + Vector3.up * 1.6f, toEnemy.normalized, toEnemy.magnitude, obstacleLayer);
            
            // Also check LOS to predicted position
            if (predictedPositions.ContainsKey(closestEnemy))
            {
                Vector3 toPredicted = predictedPositions[closestEnemy] - spawnPos;
                bool hasPredictedLOS = !Physics.Raycast(spawnPos + Vector3.up * 1.6f, toPredicted.normalized, toPredicted.magnitude, obstacleLayer);
                
                // If either has LOS, it's bad
                hasLOS = hasLOS || hasPredictedLOS;
            }
            
            if (!hasLOS)
            {
                // Has cover - good!
                score += 40f;
            }
            else
            {
                // Direct line of sight - bad!
                score -= 100f;
            }
        }
        
        // 2.5 FOV check - prevent spawning in enemy's field of view
        if (checkEnemyFOV)
        {
            foreach (GameObject enemy in enemies)
            {
                if (enemy == null) continue;

                // Use death-time snapshot when available (prevents look-direction exploit)
                Vector3 fovPos = enemySnapshot != null && enemySnapshot.ContainsKey(enemy)
                    ? enemySnapshot[enemy].pos : enemy.transform.position;
                Vector3 fovFwd = enemySnapshot != null && enemySnapshot.ContainsKey(enemy)
                    ? enemySnapshot[enemy].fwd
                    : (enemy.GetComponent<FPSController>() is FPSController f ? f.LookForward : enemy.transform.forward);

                if (IsInEnemyFOVAtPosition(spawnPos, fovPos, fovFwd))
                {
                    score -= 150f; // Massive penalty for spawning in view
                }
                
                // Check predicted position if available.
                // Use travel direction as the predicted forward - a moving player looks where they're going.
                if (predictedPositions.ContainsKey(enemy))
                {
                    Vector3 travelDir = predictedPositions[enemy] - fovPos;
                    Vector3 predictedFwd = travelDir.sqrMagnitude > 0.01f ? travelDir.normalized : fovFwd;
                    float confidence = predictionConfidence.ContainsKey(enemy) ? predictionConfidence[enemy] : 0.5f;

                    if (IsInEnemyFOVAtPosition(spawnPos, predictedPositions[enemy], predictedFwd))
                    {
                        float penalty = 100f * confidence;
                        score -= penalty;
                        
                        if (showDebugLogs)
                        {
                            Debug.Log($"Predicted FOV penalty: {penalty:F1} (confidence: {confidence:P0})");
                        }
                    }

                    // Also check the reverse look direction at half penalty.
                    // After a kill, players frequently turn around to check behind them,
                    // so a spawn that would be in their back-turn sightline is also risky.
                    if (IsInEnemyFOVAtPosition(spawnPos, predictedPositions[enemy], -predictedFwd))
                    {
                        float penalty = 50f * confidence;
                        score -= penalty;

                        if (showDebugLogs)
                        {
                            Debug.Log($"Predicted reverse-FOV penalty: {penalty:F1} (confidence: {confidence:P0})");
                        }
                    }
                }
            }
        }
        
        // 3. Prevent back spawns (no spawning behind enemy)
        if (preventBackSpawns && closestEnemy != null)
        {
            Vector3 snappedPos = enemySnapshot != null && enemySnapshot.ContainsKey(closestEnemy)
                ? enemySnapshot[closestEnemy].pos : closestEnemy.transform.position;
            Vector3 snappedFwd = enemySnapshot != null && enemySnapshot.ContainsKey(closestEnemy)
                ? enemySnapshot[closestEnemy].fwd
                : (closestEnemy.GetComponent<FPSController>() is FPSController fc ? fc.LookForward : closestEnemy.transform.forward);
            Vector3 toSpawn = (spawnPos - snappedPos).normalized;
            float dotProduct = Vector3.Dot(snappedFwd, toSpawn);
            
            if (dotProduct < -0.2f) // Behind enemy
            {
                score -= 120f; // Massive penalty
            }
            else if (dotProduct > 0.5f) // In front of enemy
            {
                score += 30f; // Reward fair spawns
            }
        }
        
        // 4. Distance from death location
        if (deathLocation.HasValue)
        {
            float deathDist = Vector3.Distance(spawnPos, deathLocation.Value);
            
            if (deathDist < minDeathDistance)
            {
                score -= (minDeathDistance - deathDist) * 15f;
            }
            else
            {
                score += Mathf.Min(deathDist, 40f) * 2f;
            }
        }
        
        // 5. Combat zone avoidance
        foreach (var zone in combatZones)
        {
            float dist = Vector3.Distance(spawnPos, zone.Key);
            if (dist < combatZoneRadius)
            {
                float intensity = 1f - (dist / combatZoneRadius);
                score -= intensity * 80f;
            }
        }
        
        // 6. Recent spawn avoidance (prevent patterns)
        foreach (SpawnPoint recentSpawn in recentSpawns)
        {
            float dist = Vector3.Distance(spawnPos, recentSpawn.position);
            if (dist < 10f)
            {
                score -= (10f - dist) * 30f; // Heavy penalty for nearby recent spawns
            }
        }
        
        // 7. Map control balance
        if (balanceMapControl && closestEnemy != null)
        {
            Vector3 balancePos = enemySnapshot != null && enemySnapshot.ContainsKey(closestEnemy)
                ? enemySnapshot[closestEnemy].pos : closestEnemy.transform.position;
            float enemyDistToCenter = Vector3.Distance(balancePos, mapCenter);
            float spawnDistToCenter = Vector3.Distance(spawnPos, mapCenter);
            
            // Reward balanced positioning
            float balanceDiff = Mathf.Abs(enemyDistToCenter - spawnDistToCenter);
            score += (20f - Mathf.Min(balanceDiff, 20f)) * 3f;
        }
        
        // 8. Height advantage consideration (slight penalty for high ground camping)
        float heightDiff = spawnPos.y - mapCenter.y;
        if (Mathf.Abs(heightDiff) > 5f)
        {
            score -= Mathf.Abs(heightDiff) * 2f;
        }
        
        return score;
    }
    
    /// <summary>
    /// Predict where an enemy will be after getting a kill (BASIC FALLBACK)
    /// NOTE: Advanced prediction is now handled by AdvancedEnemyPredictor component
    /// This is kept as a fallback for when advanced predictor is not available
    /// </summary>
    private Vector3 PredictEnemyPosition(GameObject enemy, Vector3? deathLocation)
    {
        if (!deathLocation.HasValue)
            return enemy.transform.position;
        
        Vector3 currentPos = enemy.transform.position;
        
        // Get enemy movement component
        var fpsController = enemy.GetComponent<FPSController>();
        if (fpsController == null)
            return currentPos;
        
        // Assume enemy will move toward the kill location to confirm/loot
        Vector3 toKill = (deathLocation.Value - currentPos).normalized;
        
        // Predict movement based on typical player speed (~5-7 m/s)
        float predictedDistance = 6f * movementPredictionTime;
        
        Vector3 predictedPos = currentPos + toKill * predictedDistance;
        
        // Clamp to map bounds
        if (useBoxBounds)
        {
            Vector3 halfSize = boxSize * 0.5f;
            predictedPos.x = Mathf.Clamp(predictedPos.x, mapCenter.x - halfSize.x, mapCenter.x + halfSize.x);
            predictedPos.y = Mathf.Clamp(predictedPos.y, mapCenter.y - halfSize.y, mapCenter.y + halfSize.y);
            predictedPos.z = Mathf.Clamp(predictedPos.z, mapCenter.z - halfSize.z, mapCenter.z + halfSize.z);
        }
        
        return predictedPos;
    }
    
    /// <summary>
    /// Check if a spawn position is within an enemy's field of view
    /// </summary>
    private bool IsInEnemyFOV(Vector3 spawnPos, GameObject enemy)
    {
        var fps = enemy.GetComponent<FPSController>();
        Vector3 fwd = fps != null ? fps.LookForward : enemy.transform.forward;
        return IsInEnemyFOVAtPosition(spawnPos, enemy.transform.position, fwd);
    }
    
    /// <summary>
    /// Check if a spawn position is within an enemy's FOV at a specific position
    /// </summary>
    private bool IsInEnemyFOVAtPosition(Vector3 spawnPos, Vector3 enemyPosition, Vector3 enemyForward)
    {
        Vector3 toSpawn = spawnPos - enemyPosition;
        float distance = toSpawn.magnitude;
        
        // Outside FOV distance
        if (distance > enemyFOVDistance)
            return false;
        
        float angle = Vector3.Angle(enemyForward, toSpawn.normalized);
        
        // Within FOV cone
        if (angle <= enemyFOVAngle * 0.5f)
        {
            // Check if there's line of sight
            if (!Physics.Raycast(enemyPosition + Vector3.up * 1.6f, toSpawn.normalized, distance, obstacleLayer))
            {
                return true; // In FOV and has LOS
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Generate a random valid spawn (fallback)
    /// </summary>
    private (Vector3 position, Quaternion rotation)? GenerateRandomValidSpawn()
    {
        for (int i = 0; i < 100; i++)
        {
            // Generate random position within circular map bounds
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomDistance = Random.Range(0f, mapRadius);
            
            Vector3 randomPos = new Vector3(
                mapCenter.x + Mathf.Cos(randomAngle) * randomDistance,
                mapCenter.y + boxSize.y * 0.5f,
                mapCenter.z + Mathf.Sin(randomAngle) * randomDistance
            );
            
            if (allowAllSurfaces)
            {
                // Cast rays in all directions to find ANY surface
                Vector3[] directions = {
                    Vector3.down,      // Floor
                    Vector3.up,        // Ceiling
                    Vector3.forward,   // Walls
                    Vector3.back,
                    Vector3.left,
                    Vector3.right,
                    new Vector3(1, -1, 0).normalized,
                    new Vector3(-1, -1, 0).normalized,
                    new Vector3(0, -1, 1).normalized,
                    new Vector3(0, -1, -1).normalized
                };
                
                foreach (Vector3 dir in directions)
                {
                    if (Physics.Raycast(randomPos, dir, out RaycastHit hit, maxRaycastDistance, groundLayer | obstacleLayer))
                    {
                        Vector3 spawnPos = hit.point + hit.normal * minSurfaceClearance;
                        if (IsValidSpawnPosition(spawnPos, hit.normal))
                        {
                            SpawnPoint spawn = new SpawnPoint(spawnPos, hit.normal);
                            return (spawn.position, spawn.rotation);
                        }
                    }
                }
            }
            else
            {
                // Only floors
                if (Physics.Raycast(randomPos, Vector3.down, out RaycastHit hit, maxRaycastDistance, groundLayer | obstacleLayer))
                {
                    Vector3 spawnPos = hit.point + hit.normal * minSurfaceClearance;
                    if (IsValidSpawnPosition(spawnPos, hit.normal))
                    {
                        SpawnPoint spawn = new SpawnPoint(spawnPos, hit.normal);
                        return (spawn.position, spawn.rotation);
                    }
                }
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Register a death location
    /// NOW WITH FIREBASE TRACKING!
    /// </summary>
    public void RegisterDeath(Vector3 location)
    {
        recentDeaths[location] = Time.time;
        combatZones[location] = Time.time;
        
        // Track with temporal safety
        if (safetyTracker != null)
        {
            safetyTracker.RegisterCombatEvent(location, TemporalSafetyTracker.CombatEventType.Death, 1f);
        }
    }
    
    /// <summary>
    /// Register a kill location
    /// NOW WITH FIREBASE TRACKING!
    /// </summary>
    public void RegisterKill(Vector3 location)
    {
        combatZones[location] = Time.time;
        
        // Track with temporal safety
        if (safetyTracker != null)
        {
            safetyTracker.RegisterCombatEvent(location, TemporalSafetyTracker.CombatEventType.Gunfire, 0.8f);
        }
    }
    
    /// <summary>
    /// Clean up old data
    /// </summary>
    private void CleanupOldData()
    {
        float currentTime = Time.time;
        
        var oldDeaths = recentDeaths.Where(kvp => currentTime - kvp.Value > 15f).Select(kvp => kvp.Key).ToList();
        foreach (var key in oldDeaths)
        {
            recentDeaths.Remove(key);
        }
        
        var oldCombat = combatZones.Where(kvp => currentTime - kvp.Value > combatZoneDecayTime).Select(kvp => kvp.Key).ToList();
        foreach (var key in oldCombat)
        {
            combatZones.Remove(key);
        }
    }
    
    /// <summary>
    /// Spawn a player at the best dynamic position
    /// Now supports spawning on walls and ceilings!
    /// NOW WITH FIREBASE TRACKING!
    /// </summary>
    public void SpawnPlayer(GameObject player, Vector3? deathLocation = null)
    {
        var spawnData = GetBestSpawnPosition(player, deathLocation);
        
        // Note: qualityAnalyzer.RegisterSpawn is handled inside PlayerHealth.Respawn() below,
        // so we do NOT call it here to avoid double-registration.
        
        // Track spawn for temporal safety (Respawn() does not cover this)
        if (spawnData.HasValue && safetyTracker != null)
        {
            safetyTracker.TrackPlayerSpawn(player);
        }
        
        if (spawnData.HasValue)
        {
            // Teleport player
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
                player.transform.position = spawnData.Value.position;
                player.transform.rotation = spawnData.Value.rotation;
                controller.enabled = true;
            }
            else
            {
                player.transform.position = spawnData.Value.position;
                player.transform.rotation = spawnData.Value.rotation;
            }
            
            // Reset player health
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Respawn();
            }
            
            Debug.Log($"<color=cyan>Spawned player at dynamic position {spawnData.Value.position}</color>");
        }
        else
        {
            Debug.LogError("DynamicSpawnSystem: Failed to find valid spawn position!");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;
        
        // Draw map bounds
        Gizmos.color = Color.cyan;
        
        if (useBoxBounds)
        {
            // Draw box bounds for building
            Gizmos.DrawWireCube(mapCenter, boxSize);
            
            // Draw floor levels
            if (showFloorLevels && floorHeight > 0)
            {
                Gizmos.color = floorLevelColor;
                int numFloors = Mathf.CeilToInt(boxSize.y / floorHeight);
                float startY = mapCenter.y - boxSize.y * 0.5f;
                
                for (int i = 0; i <= numFloors; i++)
                {
                    float y = startY + i * floorHeight;
                    Vector3 floorCenter = new Vector3(mapCenter.x, y, mapCenter.z);
                    
                    // Draw floor plane
                    Gizmos.DrawWireCube(floorCenter, new Vector3(boxSize.x, 0.1f, boxSize.z));
                    
                    // Draw floor number label
                    #if UNITY_EDITOR
                    if (showFloorNumbers)
                    {
                        Vector3 labelPos = new Vector3(mapCenter.x - boxSize.x * 0.5f - 2f, y, mapCenter.z);
                        UnityEditor.Handles.Label(labelPos, $"Floor {i}", new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = floorLevelColor },
                            fontSize = 12,
                            fontStyle = FontStyle.Bold
                        });
                    }
                    #endif
                }
            }
        }
        else
        {
            // Draw spherical map bounds (legacy)
            DrawCircle(mapCenter, mapRadius, 64, Vector3.up);
            DrawCircle(mapCenter, mapRadius, 64, Vector3.right);
            DrawCircle(mapCenter, mapRadius, 64, Vector3.forward);
        }
        
        // Draw combat zones
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        foreach (var zone in combatZones)
        {
            Gizmos.DrawWireSphere(zone.Key, combatZoneRadius);
        }
        
        // Draw recent deaths
        Gizmos.color = Color.red;
        foreach (var death in recentDeaths)
        {
            Gizmos.DrawSphere(death.Key, 0.5f);
        }
        
        // Draw recent spawns
        Gizmos.color = Color.green;
        foreach (var spawn in recentSpawns)
        {
            Gizmos.DrawWireSphere(spawn.position, 1f);
            // Draw surface normal for each spawn
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            Gizmos.DrawLine(spawn.position, spawn.position + spawn.normal * 1.5f);
            Gizmos.color = Color.green;
        }
        
        // Draw last spawn
        if (lastSpawn.position != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lastSpawn.position, 0.8f);
            
            // Draw surface normal
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(lastSpawn.position, lastSpawn.position + lastSpawn.normal * 2f);
            
            // Draw spawn orientation (forward direction)
            Gizmos.color = Color.magenta;
            Vector3 forward = lastSpawn.rotation * Vector3.forward;
            Gizmos.DrawLine(lastSpawn.position, lastSpawn.position + forward * 1.5f);
        }
    }
    
    /// <summary>
    /// Helper to draw a circle in the scene view on any plane
    /// </summary>
    private void DrawCircle(Vector3 center, float radius, int segments, Vector3 normal)
    {
        // Create perpendicular vectors to the normal
        Vector3 forward = Vector3.Slerp(normal, -normal, 0.5f);
        if (Vector3.Dot(forward, normal) > 0.9f)
        {
            forward = Vector3.Cross(normal, Vector3.right);
        }
        else
        {
            forward = Vector3.Cross(normal, Vector3.up);
        }
        
        Vector3 right = Vector3.Cross(normal, forward).normalized;
        forward = Vector3.Cross(right, normal).normalized;
        
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + right * radius;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
