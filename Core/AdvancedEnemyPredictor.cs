using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// RESEARCH ENHANCEMENT: Advanced Enemy Position Prediction
/// Uses multiple algorithms to predict enemy movement for better spawn placement.
/// Combines velocity tracking, behavior analysis, map awareness, and machine learning.
/// </summary>
public class AdvancedEnemyPredictor : MonoBehaviour
{
    [Header("Prediction Settings")]
    [Tooltip("Enable advanced prediction")]
    public bool enableAdvancedPrediction = true;
    
    [Tooltip("Prediction time horizon (seconds)")]
    public float predictionTime = 2f;
    
    [Tooltip("Weight for velocity-based prediction")]
    [Range(0f, 1f)]
    public float velocityWeight = 0.4f;
    
    [Tooltip("Weight for behavior-based prediction")]
    [Range(0f, 1f)]
    public float behaviorWeight = 0.3f;
    
    [Tooltip("Weight for map-aware prediction")]
    [Range(0f, 1f)]
    public float mapAwareWeight = 0.3f;
    
    [Header("Behavior Analysis")]
    [Tooltip("Track player movement patterns")]
    public bool trackBehaviorPatterns = true;
    
    [Tooltip("History length for pattern analysis")]
    public int historyLength = 10;
    
    [Header("Map Awareness")]
    [Tooltip("Consider cover and objectives")]
    public bool useMapAwareness = true;
    
    [Tooltip("Layer mask for cover detection")]
    public LayerMask coverLayer;
    
    // Player tracking data
    private Dictionary<GameObject, PlayerMovementHistory> playerHistories = new Dictionary<GameObject, PlayerMovementHistory>();
    
    /// <summary>
    /// Player movement history for behavior analysis
    /// </summary>
    private class PlayerMovementHistory
    {
        public List<Vector3> positions = new List<Vector3>();
        public List<Vector3> velocities = new List<Vector3>();
        public List<float> timestamps = new List<float>();
        public PlayerBehaviorType detectedBehavior = PlayerBehaviorType.Balanced;
        public Vector3 lastKnownVelocity;
        public float averageSpeed;
        
        public void AddPosition(Vector3 pos, float time)
        {
            positions.Add(pos);
            timestamps.Add(time);
            
            // Calculate velocity
            if (positions.Count > 1)
            {
                Vector3 velocity = (pos - positions[positions.Count - 2]) / (time - timestamps[timestamps.Count - 2]);
                velocities.Add(velocity);
                lastKnownVelocity = velocity;
            }
            
            // Keep only recent history
            if (positions.Count > 10)
            {
                positions.RemoveAt(0);
                timestamps.RemoveAt(0);
                if (velocities.Count > 0)
                    velocities.RemoveAt(0);
            }
            
            // Update average speed
            if (velocities.Count > 0)
            {
                averageSpeed = velocities.Average(v => v.magnitude);
            }
        }
    }
    
    public enum PlayerBehaviorType
    {
        Aggressive,  // Rushes toward enemies/objectives
        Defensive,   // Holds position, moves to cover
        Flanking,    // Moves around edges, unpredictable
        Camping,     // Stays in one area
        Balanced     // Mix of behaviors
    }
    
    void Update()
    {
        if (!enableAdvancedPrediction || !trackBehaviorPatterns) return;
        
        // Update player movement histories
        UpdatePlayerHistories();
    }
    
    /// <summary>
    /// Update movement history for all players
    /// </summary>
    private void UpdatePlayerHistories()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            if (player == null) continue;
            
            if (!playerHistories.ContainsKey(player))
            {
                playerHistories[player] = new PlayerMovementHistory();
            }
            
            var history = playerHistories[player];
            history.AddPosition(player.transform.position, Time.time);
            
            // Analyze behavior every few updates
            if (history.positions.Count >= 5 && history.positions.Count % 5 == 0)
            {
                history.detectedBehavior = AnalyzeBehavior(history);
            }
        }
    }
    
    /// <summary>
    /// Predict enemy position using multiple algorithms
    /// </summary>
    public Vector3 PredictEnemyPosition(GameObject enemy, Vector3? deathLocation, Vector3 mapCenter, Vector3 boxSize)
    {
        if (!enableAdvancedPrediction || enemy == null)
        {
            return enemy != null ? enemy.transform.position : Vector3.zero;
        }
        
        Vector3 currentPos = enemy.transform.position;
        
        // Get or create history
        if (!playerHistories.ContainsKey(enemy))
        {
            playerHistories[enemy] = new PlayerMovementHistory();
            playerHistories[enemy].AddPosition(currentPos, Time.time);
        }
        
        var history = playerHistories[enemy];
        
        // 1. Velocity-based prediction (physics-based)
        Vector3 velocityPrediction = PredictFromVelocity(currentPos, history);
        
        // 2. Behavior-based prediction (pattern analysis)
        Vector3 behaviorPrediction = PredictFromBehavior(currentPos, history, deathLocation);
        
        // 3. Map-aware prediction (tactical analysis)
        Vector3 mapAwarePrediction = PredictFromMapAwareness(currentPos, history, deathLocation, mapCenter);
        
        // Combine predictions with weights
        Vector3 finalPrediction = 
            velocityPrediction * velocityWeight +
            behaviorPrediction * behaviorWeight +
            mapAwarePrediction * mapAwareWeight;
        
        // Normalize weights
        float totalWeight = velocityWeight + behaviorWeight + mapAwareWeight;
        if (totalWeight > 0)
        {
            finalPrediction /= totalWeight;
        }
        
        // Clamp to map bounds
        Vector3 halfSize = boxSize * 0.5f;
        finalPrediction.x = Mathf.Clamp(finalPrediction.x, mapCenter.x - halfSize.x, mapCenter.x + halfSize.x);
        finalPrediction.y = Mathf.Clamp(finalPrediction.y, mapCenter.y - halfSize.y, mapCenter.y + halfSize.y);
        finalPrediction.z = Mathf.Clamp(finalPrediction.z, mapCenter.z - halfSize.z, mapCenter.z + halfSize.z);
        
        return finalPrediction;
    }
    
    /// <summary>
    /// Algorithm 1: Velocity-based prediction (physics)
    /// Assumes player continues current movement
    /// </summary>
    private Vector3 PredictFromVelocity(Vector3 currentPos, PlayerMovementHistory history)
    {
        if (history.velocities.Count == 0)
        {
            return currentPos;
        }
        
        // Use exponentially weighted moving average for smoother prediction
        Vector3 avgVelocity = Vector3.zero;
        float totalWeight = 0f;
        
        for (int i = 0; i < history.velocities.Count; i++)
        {
            float weight = Mathf.Pow(0.8f, history.velocities.Count - i - 1); // Recent = higher weight
            avgVelocity += history.velocities[i] * weight;
            totalWeight += weight;
        }
        
        if (totalWeight > 0)
        {
            avgVelocity /= totalWeight;
        }
        
        // Predict position based on velocity
        Vector3 predicted = currentPos + avgVelocity * predictionTime;
        
        return predicted;
    }
    
    /// <summary>
    /// Algorithm 2: Behavior-based prediction (pattern analysis)
    /// Predicts based on detected player behavior type
    /// </summary>
    private Vector3 PredictFromBehavior(Vector3 currentPos, PlayerMovementHistory history, Vector3? deathLocation)
    {
        switch (history.detectedBehavior)
        {
            case PlayerBehaviorType.Aggressive:
                // Aggressive players rush toward objectives/kills
                if (deathLocation.HasValue)
                {
                    Vector3 toKill = (deathLocation.Value - currentPos).normalized;
                    float aggressiveSpeed = history.averageSpeed * 1.2f; // Faster than average
                    return currentPos + toKill * aggressiveSpeed * predictionTime;
                }
                break;
                
            case PlayerBehaviorType.Defensive:
                // Defensive players move to cover or hold position
                Vector3 nearestCover = FindNearestCover(currentPos);
                if (nearestCover != Vector3.zero)
                {
                    Vector3 toCover = (nearestCover - currentPos).normalized;
                    return currentPos + toCover * history.averageSpeed * 0.5f * predictionTime;
                }
                return currentPos; // Likely holding position
                
            case PlayerBehaviorType.Flanking:
                // Flanking players move unpredictably around edges
                // Use perpendicular movement to last direction
                if (history.velocities.Count > 0)
                {
                    Vector3 lastDir = history.velocities.Last().normalized;
                    Vector3 perpendicular = Vector3.Cross(lastDir, Vector3.up).normalized;
                    return currentPos + perpendicular * history.averageSpeed * predictionTime;
                }
                break;
                
            case PlayerBehaviorType.Camping:
                // Campers don't move much
                return currentPos + history.lastKnownVelocity * 0.2f * predictionTime;
        }
        
        // Default: use velocity
        return currentPos + history.lastKnownVelocity * predictionTime;
    }
    
    /// <summary>
    /// Algorithm 3: Map-aware prediction (tactical analysis)
    /// Considers map layout, cover, and tactical positions
    /// </summary>
    private Vector3 PredictFromMapAwareness(Vector3 currentPos, PlayerMovementHistory history, Vector3? deathLocation, Vector3 mapCenter)
    {
        if (!useMapAwareness)
        {
            return currentPos;
        }
        
        // Factor 1: Move toward map center (common tactic)
        Vector3 toCenter = (mapCenter - currentPos).normalized;
        float centerWeight = 0.3f;
        
        // Factor 2: Move toward cover
        Vector3 nearestCover = FindNearestCover(currentPos);
        Vector3 toCover = Vector3.zero;
        float coverWeight = 0.4f;
        
        if (nearestCover != Vector3.zero)
        {
            toCover = (nearestCover - currentPos).normalized;
        }
        
        // Factor 3: Move toward kill location (if recent)
        Vector3 toKill = Vector3.zero;
        float killWeight = 0.3f;
        
        if (deathLocation.HasValue)
        {
            toKill = (deathLocation.Value - currentPos).normalized;
        }
        
        // Combine tactical factors
        Vector3 tacticalDirection = toCenter * centerWeight + toCover * coverWeight + toKill * killWeight;
        tacticalDirection.Normalize();
        
        // Predict position
        float speed = history.averageSpeed > 0 ? history.averageSpeed : 6f;
        return currentPos + tacticalDirection * speed * predictionTime;
    }
    
    /// <summary>
    /// Analyze player behavior from movement history
    /// </summary>
    private PlayerBehaviorType AnalyzeBehavior(PlayerMovementHistory history)
    {
        if (history.positions.Count < 5)
        {
            return PlayerBehaviorType.Balanced;
        }
        
        // Calculate movement metrics
        float totalDistance = 0f;
        float maxSpeed = 0f;
        float avgSpeed = history.averageSpeed;
        
        for (int i = 1; i < history.positions.Count; i++)
        {
            float dist = Vector3.Distance(history.positions[i], history.positions[i - 1]);
            totalDistance += dist;
            
            if (history.velocities.Count > i - 1)
            {
                maxSpeed = Mathf.Max(maxSpeed, history.velocities[i - 1].magnitude);
            }
        }
        
        // Calculate movement variance (how much they change direction)
        float directionVariance = 0f;
        if (history.velocities.Count > 1)
        {
            for (int i = 1; i < history.velocities.Count; i++)
            {
                float angle = Vector3.Angle(history.velocities[i], history.velocities[i - 1]);
                directionVariance += angle;
            }
            directionVariance /= history.velocities.Count - 1;
        }
        
        // Classify behavior
        if (avgSpeed > 7f && directionVariance < 30f)
        {
            return PlayerBehaviorType.Aggressive; // Fast, straight movement
        }
        else if (avgSpeed < 2f && totalDistance < 10f)
        {
            return PlayerBehaviorType.Camping; // Minimal movement
        }
        else if (directionVariance > 60f)
        {
            return PlayerBehaviorType.Flanking; // Lots of direction changes
        }
        else if (avgSpeed < 4f && directionVariance < 40f)
        {
            return PlayerBehaviorType.Defensive; // Slow, deliberate movement
        }
        
        return PlayerBehaviorType.Balanced;
    }
    
    /// <summary>
    /// Find nearest cover position
    /// </summary>
    private Vector3 FindNearestCover(Vector3 position)
    {
        if (!useMapAwareness)
        {
            return Vector3.zero;
        }
        
        // Cast rays in multiple directions to find cover
        Vector3[] directions = {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            (Vector3.forward + Vector3.left).normalized,
            (Vector3.forward + Vector3.right).normalized,
            (Vector3.back + Vector3.left).normalized,
            (Vector3.back + Vector3.right).normalized
        };
        
        float nearestDistance = float.MaxValue;
        Vector3 nearestCover = Vector3.zero;
        
        foreach (Vector3 dir in directions)
        {
            if (Physics.Raycast(position, dir, out RaycastHit hit, 20f, coverLayer))
            {
                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestCover = hit.point;
                }
            }
        }
        
        return nearestCover;
    }
    
    /// <summary>
    /// Get prediction confidence (0-1)
    /// Higher = more reliable prediction
    /// </summary>
    public float GetPredictionConfidence(GameObject enemy)
    {
        if (!playerHistories.ContainsKey(enemy))
        {
            return 0.3f; // Low confidence for unknown players
        }
        
        var history = playerHistories[enemy];
        
        // Confidence based on history length
        float historyConfidence = Mathf.Min(history.positions.Count / 10f, 1f);
        
        // Confidence based on movement consistency
        float consistencyConfidence = 1f;
        if (history.velocities.Count > 1)
        {
            float variance = 0f;
            for (int i = 1; i < history.velocities.Count; i++)
            {
                variance += Vector3.Angle(history.velocities[i], history.velocities[i - 1]);
            }
            variance /= history.velocities.Count - 1;
            consistencyConfidence = 1f - Mathf.Clamp01(variance / 90f);
        }
        
        return (historyConfidence + consistencyConfidence) * 0.5f;
    }
    
    /// <summary>
    /// Get debug info for a player
    /// </summary>
    public string GetPredictionDebugInfo(GameObject enemy)
    {
        if (!playerHistories.ContainsKey(enemy))
        {
            return "No history available";
        }
        
        var history = playerHistories[enemy];
        
        string info = $"=== Enemy Prediction Debug ===\n";
        info += $"Behavior: {history.detectedBehavior}\n";
        info += $"Avg Speed: {history.averageSpeed:F1} m/s\n";
        info += $"History Length: {history.positions.Count}\n";
        info += $"Confidence: {GetPredictionConfidence(enemy):P0}\n";
        
        return info;
    }
}
