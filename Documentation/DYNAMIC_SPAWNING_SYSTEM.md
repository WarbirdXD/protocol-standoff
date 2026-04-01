# Dynamic Spawning System Documentation

**Procedural spawn generation with intelligent scoring**

---

## Overview

The Dynamic Spawning System generates spawn positions in real-time based on game state, eliminating fixed spawn points that can be learned or camped. It generates 100+ candidate positions every spawn, scores each based on 7 factors (enemy distance, line of sight, teammate positioning, etc.), and selects the best spawn to ensure fair, unpredictable gameplay.

---

## Architecture

```
Player Dies
    ↓
DynamicSpawnSystem.GetSpawnPosition()
    ↓
Generate 100+ Candidate Positions
    ↓
┌──────────────────────────────────┐
│  For each candidate:             │
│  - Raycast in all directions     │
│  - Find surface (floor/wall/ceil)│
│  - Validate position             │
│    • Check obstacles             │
│    • Check headroom              │
│    • Check map bounds            │
└──────────────────────────────────┘
    ↓
Score Each Valid Candidate
    ↓
┌──────────────────────────────────┐
│  7 Scoring Factors:              │
│  1. Teammate distance (2v2)      │
│  2. Enemy distance               │
│  3. Line of sight                │
│  4. Enemy field of view          │
│  5. Death location avoidance     │
│  6. Spawn history                │
│  7. Map control balance          │
└──────────────────────────────────┘
    ↓
Select Highest Scoring Spawn
    ↓
Return Position and Rotation
    ↓
Player Spawns
```

---

## Candidate Generation

### Random Position Generation

```csharp
private List<SpawnPoint> GenerateCandidateSpawns()
{
    List<SpawnPoint> candidates = new List<SpawnPoint>();
    int attempts = 0;
    int maxAttempts = candidateSpawnCount * 3; // Try 3x to get enough
    
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
        
        // Find surface and validate
        if (FindSurfaceAndValidate(randomPos, out SpawnPoint spawnPoint))
        {
            candidates.Add(spawnPoint);
        }
    }
    
    return candidates;
}
```

### Multi-Directional Raycasting

**Purpose:** Find ANY surface - floors, walls, ceilings.

```csharp
private bool FindSurfaceAndValidate(Vector3 randomPos, out SpawnPoint spawnPoint)
{
    spawnPoint = null;
    
    if (allowAllSurfaces)
    {
        // Cast rays in ALL directions to find ANY surface
        Vector3[] directions = {
            Vector3.down,      // Floor
            Vector3.up,        // Ceiling
            Vector3.forward,   // Walls
            Vector3.back,
            Vector3.left,
            Vector3.right,
            new Vector3(1, -1, 0).normalized,  // Diagonal down
            new Vector3(-1, -1, 0).normalized
        };
        
        foreach (Vector3 dir in directions)
        {
            // Check both ground and obstacle layers for spawn surfaces
            if (Physics.Raycast(randomPos, dir, out RaycastHit hit, 
                               maxRaycastDistance, groundLayer | obstacleLayer))
            {
                Vector3 spawnPos = hit.point + hit.normal * minSurfaceClearance;
                
                // Validate spawn position
                if (IsValidSpawnPosition(spawnPos, hit.normal))
                {
                    spawnPoint = new SpawnPoint(spawnPos, hit.normal);
                    return true;
                }
            }
        }
    }
    else
    {
        // Original behavior - only floors
        if (Physics.Raycast(randomPos, Vector3.down, out RaycastHit hit, 
                           maxRaycastDistance, groundLayer | obstacleLayer))
        {
            Vector3 spawnPos = hit.point + Vector3.up * minSurfaceClearance;
            
            if (IsValidSpawnPosition(spawnPos, hit.normal))
            {
                spawnPoint = new SpawnPoint(spawnPos, hit.normal);
                return true;
            }
        }
    }
    
    return false;
}
```

**Key Insight:** By raycasting in all directions, we can spawn on ANY surface orientation - perfect for spherical arenas or complex multi-level buildings.

---

## Spawn Validation

### Position Validation

```csharp
private bool IsValidSpawnPosition(Vector3 position, Vector3 surfaceNormal)
{
    // 1. Check for obstacles using player-sized capsule
    Vector3 capsuleBottom = position + surfaceNormal * (playerRadius + 0.1f);
    Vector3 capsuleTop = capsuleBottom + surfaceNormal * (playerHeight - playerRadius * 2f);
    
    Collider[] obstacles = Physics.OverlapCapsule(
        capsuleBottom, 
        capsuleTop, 
        playerRadius, 
        obstacleLayer
    );
    
    if (obstacles.Length > 0)
    {
        return false; // Player would collide with obstacles
    }
    
    // 2. Check headroom above spawn
    if (Physics.Raycast(position, surfaceNormal, playerHeight + 0.5f, obstacleLayer))
    {
        return false; // Not enough headroom
    }
    
    // 3. Check if within map bounds
    if (useBoxBounds)
    {
        Vector3 halfSize = boxSize * 0.5f;
        if (Mathf.Abs(position.x - mapCenter.x) > halfSize.x ||
            Mathf.Abs(position.y - mapCenter.y) > halfSize.y ||
            Mathf.Abs(position.z - mapCenter.z) > halfSize.z)
        {
            return false; // Outside box bounds
        }
    }
    else
    {
        // Circular bounds check
        float distanceFromCenter = Vector3.Distance(
            new Vector3(position.x, mapCenter.y, position.z), 
            new Vector3(mapCenter.x, mapCenter.y, mapCenter.z)
        );
        
        if (distanceFromCenter > mapRadius)
        {
            return false; // Outside circular bounds
        }
    }
    
    return true;
}
```

### Why Capsule Check?

**Problem:** Box overlap checks don't work well in tight spaces.

**Solution:** Use player-sized capsule to accurately check if player fits:
```
    ●  ← Capsule top (head)
    |
    |  ← Player height
    |
    ●  ← Capsule bottom (feet)
```

---

## Scoring System

### Score Calculation

```csharp
private float ScoreSpawnPosition(Vector3 spawnPos, List<GameObject> enemies, 
                                 Vector3? deathLocation, GameObject teammate = null)
{
    float score = 100f; // Start with base score
    
    // Apply all 7 scoring factors
    score += ScoreTeammateDistance(spawnPos, teammate);
    score += ScoreEnemyDistance(spawnPos, enemies, deathLocation);
    score += ScoreLineOfSight(spawnPos, enemies);
    score += ScoreEnemyFOV(spawnPos, enemies);
    score += ScoreDeathLocationAvoidance(spawnPos, deathLocation);
    score += ScoreSpawnHistory(spawnPos);
    score += ScoreMapBalance(spawnPos, enemies);
    
    return score;
}
```

### Factor 1: Teammate Distance (2v2 Mode)

**Purpose:** Keep team together but not stacked.

```csharp
private float ScoreTeammateDistance(Vector3 spawnPos, GameObject teammate)
{
    if (gameMode != GameMode.TwoVsTwo || teammate == null || teammate.GetComponent<PlayerHealth>().IsDead)
        return 0f;
    
    float score = 0f;
    float teammateDistance = Vector3.Distance(spawnPos, teammate.transform.position);
    
    // Reward spawning near teammate (but not too close)
    if (teammateDistance < teammateSpawnDistance * 0.5f) // < 4m
    {
        score -= 50f; // Too close - penalty
    }
    else if (teammateDistance >= teammateSpawnDistance * 0.8f && 
             teammateDistance <= teammateSpawnDistance * 2f) // 6-16m
    {
        score += 80f; // Perfect distance - big reward
    }
    else if (teammateDistance > teammateSpawnDistance * 3f) // > 24m
    {
        score -= (teammateDistance - teammateSpawnDistance * 3f) * 5f; // Too far
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
    
    return score;
}
```

**Optimal Range:** 6-16 meters (close enough to support, far enough to not stack)

### Factor 2: Enemy Distance

**Purpose:** Prevent spawn kills, enforce fair distance.

```csharp
private float ScoreEnemyDistance(Vector3 spawnPos, List<GameObject> enemies, Vector3? deathLocation)
{
    float score = 0f;
    float closestEnemyDist = float.MaxValue;
    GameObject closestEnemy = null;
    
    // Get predicted enemy positions
    Dictionary<GameObject, Vector3> predictedPositions = new Dictionary<GameObject, Vector3>();
    if (predictEnemyMovement)
    {
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
            {
                predictedPositions[enemy] = PredictEnemyPosition(enemy, deathLocation);
            }
        }
    }
    
    // Find closest enemy (current or predicted position)
    foreach (GameObject enemy in enemies)
    {
        if (enemy == null) continue;
        
        float currentDist = Vector3.Distance(spawnPos, enemy.transform.position);
        
        // Check predicted position if available
        float dist = currentDist;
        if (predictedPositions.ContainsKey(enemy))
        {
            float predictedDist = Vector3.Distance(spawnPos, predictedPositions[enemy]);
            dist = Mathf.Min(currentDist, predictedDist); // Use closer distance (more conservative)
        }
        
        if (dist < closestEnemyDist)
        {
            closestEnemyDist = dist;
            closestEnemy = enemy;
        }
    }
    
    // Enforce minimum distance (prevent spawn kills)
    if (closestEnemyDist < minEnemyDistance) // 25m
    {
        score -= (minEnemyDistance - closestEnemyDist) * 20f; // Heavy penalty
    }
    else if (closestEnemyDist > maxEnemyDistance) // 60m
    {
        score -= (closestEnemyDist - maxEnemyDistance) * 5f; // Prevent edge camping
    }
    else
    {
        score += 50f; // Optimal distance - reward
    }
    
    return score;
}
```

**Optimal Range:** 25-60 meters (safe but not too far)

### Factor 3: Line of Sight

**Purpose:** Ensure spawn has cover from enemies.

```csharp
private float ScoreLineOfSight(Vector3 spawnPos, List<GameObject> enemies)
{
    if (!requireCover || enemies.Count == 0)
        return 0f;
    
    float score = 0f;
    GameObject closestEnemy = GetClosestEnemy(spawnPos, enemies);
    
    if (closestEnemy != null)
    {
        Vector3 toEnemy = closestEnemy.transform.position - spawnPos;
        bool hasLOS = !Physics.Raycast(
            spawnPos + Vector3.up * 1.6f, // Eye level
            toEnemy.normalized, 
            toEnemy.magnitude, 
            obstacleLayer
        );
        
        if (!hasLOS)
        {
            score += 40f; // Has cover - good!
        }
        else
        {
            score -= 100f; // Direct line of sight - very bad!
        }
    }
    
    return score;
}
```

**Key:** Raycast from eye level (1.6m) to enemy position. If blocked by obstacle layer, spawn has cover.

### Factor 4: Enemy Field of View

**Purpose:** Don't spawn in enemy's view cone.

```csharp
private float ScoreEnemyFOV(Vector3 spawnPos, List<GameObject> enemies)
{
    if (!checkEnemyFOV || enemies.Count == 0)
        return 0f;
    
    float score = 0f;
    GameObject closestEnemy = GetClosestEnemy(spawnPos, enemies);
    
    if (closestEnemy != null)
    {
        Vector3 enemyForward = closestEnemy.transform.forward;
        Vector3 toSpawn = (spawnPos - closestEnemy.transform.position).normalized;
        float dotProduct = Vector3.Dot(enemyForward, toSpawn);
        
        // Check if spawn is in enemy's FOV cone
        float fovCosine = Mathf.Cos(enemyFOVAngle * 0.5f * Mathf.Deg2Rad);
        float distance = Vector3.Distance(spawnPos, closestEnemy.transform.position);
        
        if (dotProduct > fovCosine && distance < enemyFOVDistance)
        {
            score -= 80f; // In enemy's FOV - bad!
        }
        else if (dotProduct < -0.5f) // Behind enemy
        {
            score -= 60f; // Spawning behind is unfair
        }
        else if (dotProduct > 0.5f) // In front of enemy
        {
            score += 30f; // Fair spawn
        }
    }
    
    return score;
}
```

**FOV Check:**
```
Enemy looking forward →
         ╱ ╲
        ╱   ╲  FOV cone (90°)
       ╱     ╲
      ╱       ╲
     ●─────────● Spawn here = bad
```

### Factor 5: Death Location Avoidance

**Purpose:** Don't spawn near where player just died.

```csharp
private float ScoreDeathLocationAvoidance(Vector3 spawnPos, Vector3? deathLocation)
{
    if (!deathLocation.HasValue)
        return 0f;
    
    float score = 0f;
    float deathDist = Vector3.Distance(spawnPos, deathLocation.Value);
    
    if (deathDist < minDeathDistance) // 20m
    {
        score -= (minDeathDistance - deathDist) * 15f; // Don't spawn near death
    }
    
    return score;
}
```

**Reason:** Enemy might still be near death location, waiting to spawn camp.

### Factor 6: Spawn History

**Purpose:** Prevent spawning in same location repeatedly.

```csharp
private float ScoreSpawnHistory(Vector3 spawnPos)
{
    float score = 0f;
    
    foreach (Vector3 recentSpawn in recentSpawns)
    {
        float dist = Vector3.Distance(spawnPos, recentSpawn);
        if (dist < 10f)
        {
            score -= 30f; // Penalize recently used spawns
        }
    }
    
    return score;
}
```

**Implementation:**
```csharp
private List<Vector3> recentSpawns = new List<Vector3>();

void OnSpawnSelected(Vector3 spawnPos)
{
    recentSpawns.Add(spawnPos);
    
    // Keep only last 5 spawns
    if (recentSpawns.Count > 5)
    {
        recentSpawns.RemoveAt(0);
    }
}
```

### Factor 7: Map Control Balance

**Purpose:** Ensure balanced map positioning.

```csharp
private float ScoreMapBalance(Vector3 spawnPos, List<GameObject> enemies)
{
    if (!balanceMapControl || enemies.Count == 0)
        return 0f;
    
    float score = 0f;
    GameObject closestEnemy = GetClosestEnemy(spawnPos, enemies);
    
    if (closestEnemy != null)
    {
        float enemyDistToCenter = Vector3.Distance(closestEnemy.transform.position, mapCenter);
        float spawnDistToCenter = Vector3.Distance(spawnPos, mapCenter);
        
        // Reward balanced positioning (similar distance to center)
        float balanceDiff = Mathf.Abs(enemyDistToCenter - spawnDistToCenter);
        score += (20f - Mathf.Min(balanceDiff, 20f)) * 3f;
    }
    
    return score;
}
```

**Goal:** If enemy is near center, spawn player near center too. If enemy is at edge, spawn at edge.

---

## Enemy Movement Prediction

### Purpose
Predict where enemies will be in 2 seconds to prevent spawn camping.

### Implementation

```csharp
private Vector3 PredictEnemyPosition(GameObject enemy, Vector3? deathLocation)
{
    if (!deathLocation.HasValue)
        return enemy.transform.position;
    
    // Assume enemy is moving toward death location (to confirm kill)
    Vector3 currentPos = enemy.transform.position;
    Vector3 toDeathLocation = (deathLocation.Value - currentPos).normalized;
    
    // Predict position 2 seconds in the future
    float predictedDistance = 5f * movementPredictionTime; // Assume 5 m/s movement
    Vector3 predictedPos = currentPos + toDeathLocation * predictedDistance;
    
    return predictedPos;
}
```

**Example:**
```
Enemy at (0, 0, 0)
Death at (10, 0, 0)
Direction: (1, 0, 0)
Predicted: (0, 0, 0) + (1, 0, 0) * 10 = (10, 0, 0)
```

---

## Scoring Summary

| Factor | Weight | Purpose |
|--------|--------|---------|
| **Teammate Distance** | ±80 | Keep team together (6-16m optimal) |
| **Teammate Angle** | +40 | Create spread formation |
| **Enemy Distance** | ±50 | Enforce 25-60m range |
| **Line of Sight** | ±100 | Prevent spawn kills |
| **Enemy FOV** | ±80 | Don't spawn in enemy's view |
| **Death Location** | ±15/m | Avoid death location |
| **Spawn History** | -30 | Prevent repetition |
| **Map Balance** | +60 | Fair map control |

**Total Range:** ~-500 to +400 points

---

## Selection Process

### Best Spawn Selection

```csharp
public (Vector3 position, Quaternion rotation)? GetSpawnPosition(
    List<GameObject> enemies, 
    Vector3? deathLocation = null, 
    GameObject teammate = null)
{
    // Generate candidates
    List<SpawnPoint> candidates = GenerateCandidateSpawns();
    
    if (candidates.Count == 0)
    {
        Debug.LogError("No valid spawn positions found!");
        return null;
    }
    
    // Score all candidates
    float bestScore = float.MinValue;
    SpawnPoint? bestSpawn = null;
    
    foreach (SpawnPoint candidate in candidates)
    {
        float score = ScoreSpawnPosition(candidate.position, enemies, deathLocation, teammate);
        
        if (score > bestScore)
        {
            bestScore = score;
            bestSpawn = candidate;
        }
    }
    
    // Add to spawn history
    if (bestSpawn.HasValue)
    {
        recentSpawns.Add(bestSpawn.Value.position);
        
        if (recentSpawns.Count > 5)
        {
            recentSpawns.RemoveAt(0);
        }
        
        Debug.Log($"Selected spawn at {bestSpawn.Value.position} with score {bestScore:F2}");
        
        return (bestSpawn.Value.position, bestSpawn.Value.rotation);
    }
    
    return null;
}
```

**Output Example:**
```
Generated 100 valid spawn candidates from 143 attempts
Evaluating spawn at (12.3, 5.2, -8.1): Score = 245.2
Evaluating spawn at (-5.1, 3.8, 15.2): Score = 189.7
Evaluating spawn at (8.7, 2.1, 4.3): Score = 312.5
...
Selected spawn at (8.7, 2.1, 4.3) with score 312.5
```

---

## Surface Orientation

### Rotation Calculation

```csharp
private class SpawnPoint
{
    public Vector3 position;
    public Quaternion rotation;
    
    public SpawnPoint(Vector3 pos, Vector3 surfaceNormal)
    {
        position = pos;
        
        // Calculate rotation from surface normal
        // Player "up" aligns with surface normal
        rotation = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
    }
}
```

**Examples:**
- Floor (normal = up): Player stands normally
- Wall (normal = right): Player stands on wall
- Ceiling (normal = down): Player stands on ceiling

---

## Configuration

### Inspector Settings

```csharp
[Header("Map Boundaries")]
public Vector3 mapCenter = Vector3.zero;
public bool useBoxBounds = true;
public Vector3 boxSize = new Vector3(50f, 30f, 50f);
public float mapRadius = 25f;

[Header("Spawn Generation")]
public int candidateSpawnCount = 100;
public int verticalSamples = 5;
public float minSurfaceClearance = 0.5f;
public bool allowAllSurfaces = true;
public float playerHeight = 2f;
public float playerRadius = 0.5f;

[Header("1v1/2v2 Balance Settings")]
public float minEnemyDistance = 25f;
public float maxEnemyDistance = 60f;
public bool preventBackSpawns = true;
public bool balanceMapControl = true;

[Header("2v2 Team Spawn Settings")]
public float teammateSpawnDistance = 8f;
public float teammateSpawnAngle = 45f;

[Header("Safety Checks")]
public bool requireCover = true;
public bool checkEnemyFOV = true;
public float enemyFOVAngle = 90f;
public float enemyFOVDistance = 40f;
public bool predictEnemyMovement = true;
public float movementPredictionTime = 2f;
public float minDeathDistance = 20f;
```

---

## Performance

### Optimization Techniques

**1. Candidate Limit:**
```csharp
// Don't generate infinite candidates
int maxAttempts = candidateSpawnCount * 3;
```

**2. Early Exit:**
```csharp
// Stop when enough candidates found
if (candidates.Count >= candidateSpawnCount)
    break;
```

**3. Layer Masks:**
```csharp
// Only raycast against relevant layers
Physics.Raycast(pos, dir, out hit, dist, groundLayer | obstacleLayer);
```

**4. Cached Calculations:**
```csharp
// Cache predicted positions
Dictionary<GameObject, Vector3> predictedPositions = new Dictionary<GameObject, Vector3>();
```

### Performance Metrics

- **Generation Time:** ~50-100ms for 100 candidates
- **Scoring Time:** ~10-20ms for 100 candidates
- **Total Time:** ~60-120ms per spawn
- **Acceptable:** <200ms (not noticeable to player)

---

## Integration with Other Systems

### With Match System
```
Player dies → MatchManager detects
           → DynamicSpawnSystem.GetSpawnPosition()
           → Player respawns at selected position
```

### With Network System
```
Server calls GetSpawnPosition()
Server spawns player at position
Position synced to all clients via NetworkTransform
```

### With Player System
```
Spawn position returned
Player transform set to position and rotation
Player oriented to surface normal
```

---

## Best Practices

1. **Tune scoring weights** based on playtesting
2. **Test with different map sizes** and adjust ranges
3. **Enable debug visualization** during development
4. **Monitor generation time** to ensure performance
5. **Validate all candidates** before scoring
6. **Use appropriate layer masks** for raycasts
7. **Consider game mode** (1v1 vs 2v2) in scoring
8. **Test edge cases** (all players in one corner)
9. **Provide fallback spawns** if no valid candidates
10. **Log spawn statistics** for balancing

---

## Debugging

### Visualization

```csharp
void OnDrawGizmos()
{
    if (!showDebugVisualization)
        return;
    
    // Draw map bounds
    Gizmos.color = Color.yellow;
    if (useBoxBounds)
    {
        Gizmos.DrawWireCube(mapCenter, boxSize);
    }
    else
    {
        Gizmos.DrawWireSphere(mapCenter, mapRadius);
    }
    
    // Draw candidate spawns
    Gizmos.color = Color.cyan;
    foreach (var candidate in lastCandidates)
    {
        Gizmos.DrawWireSphere(candidate.position, 0.5f);
    }
    
    // Draw selected spawn
    if (lastSelectedSpawn.HasValue)
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(lastSelectedSpawn.Value, 1f);
        Gizmos.DrawLine(lastSelectedSpawn.Value, lastSelectedSpawn.Value + Vector3.up * 2f);
    }
    
    // Draw recent spawns
    Gizmos.color = Color.red;
    foreach (var recentSpawn in recentSpawns)
    {
        Gizmos.DrawWireSphere(recentSpawn, 0.3f);
    }
}
```

### Debug Logs

```csharp
public bool showDebugLogs = true;

void LogSpawnInfo(Vector3 spawnPos, float score)
{
    if (showDebugLogs)
    {
        Debug.Log($"Spawn candidate at {spawnPos}: Score = {score:F2}");
    }
}
```

---

*This documentation explains the dynamic spawning system architecture and functionality. For implementation details, see `DynamicSpawnSystem.cs`.*
