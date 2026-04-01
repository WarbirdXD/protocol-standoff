# Security System Documentation

**Multi-layer protection against cheating and exploits**

---

## Overview

The Security System implements defense-in-depth with multiple layers of protection against cheating, exploits, and malicious behavior. It combines server authority, rate limiting, input validation, match result validation, Firebase security rules, and anomaly detection to create a comprehensive anti-cheat solution.

---

## Security Architecture

```
┌─────────────────────────────────────────────────┐
│           Security Layer Stack                   │
├─────────────────────────────────────────────────┤
│  Layer 1: Client-Side Validation                │
│    - Basic input checks                          │
│    - UI constraints                              │
├─────────────────────────────────────────────────┤
│  Layer 2: Server Authority                       │
│    - Server decides truth                        │
│    - NetworkVariables (server-writable)          │
│    - ServerRpc validation                        │
├─────────────────────────────────────────────────┤
│  Layer 3: Rate Limiting                          │
│    - Action tracking per client                  │
│    - Configurable limits                         │
│    - Automatic blocking                          │
├─────────────────────────────────────────────────┤
│  Layer 4: Input Validation                       │
│    - Sanitize all inputs                         │
│    - Range checks                                │
│    - Injection prevention                        │
├─────────────────────────────────────────────────┤
│  Layer 5: Firebase Security Rules                │
│    - Database-level protection                   │
│    - User-specific access control                │
│    - Data validation                             │
├─────────────────────────────────────────────────┤
│  Layer 6: Anomaly Detection                      │
│    - Statistical analysis                        │
│    - Behavior tracking                           │
│    - Logging suspicious activity                 │
└─────────────────────────────────────────────────┘
```

---

## Layer 1: Client-Side Validation

### Purpose
First line of defense - catch obvious errors before they reach the server.

### Implementation

**UI Constraints:**
```csharp
// Limit input field length
emailInputField.characterLimit = 50;
usernameInputField.characterLimit = 30;

// Validate before submission
if (string.IsNullOrWhiteSpace(username))
{
    ShowError("Username cannot be empty");
    return;
}
```

**Basic Range Checks:**
```csharp
// Validate slider values
if (sensitivitySlider.value < 0.1f || sensitivitySlider.value > 10f)
{
    sensitivitySlider.value = Mathf.Clamp(sensitivitySlider.value, 0.1f, 10f);
}
```

**Why Client-Side Validation?**
- Immediate feedback to user
- Reduces server load
- Better user experience
- **NOT for security** (can be bypassed)

---

## Layer 2: Server Authority

### Principle
**Server is the source of truth. Clients suggest, server decides.**

### NetworkVariable Pattern

```csharp
public class PlayerHealth : NetworkBehaviour
{
    // Server-writable, everyone can read
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );
    
    // Clients CANNOT modify this directly
    // Only server can change health
}
```

### ServerRpc Pattern

```csharp
[ServerRpc(RequireOwnership = false)]
public void TakeDamageServerRpc(float damage, bool isHeadshot, ulong attackerClientId)
{
    // This code ONLY runs on server
    // Server validates EVERYTHING
    
    // 1. Validate player is alive
    if (isDead.Value) return;
    
    // 2. Validate damage amount
    if (damage < 0 || damage > 1000)
    {
        Debug.LogWarning($"Invalid damage: {damage} from client {attackerClientId}");
        SecurityManager.Instance.LogSuspiciousActivity(attackerClientId, "Invalid damage value");
        return;
    }
    
    // 3. Validate attacker exists
    if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(attackerClientId))
    {
        Debug.LogWarning($"Invalid attacker: {attackerClientId}");
        return;
    }
    
    // 4. Server calculates actual damage (client can't manipulate)
    float actualDamage = damage;
    if (isHeadshot)
    {
        actualDamage *= headshotMultiplier; // Server-side multiplier
    }
    
    // 5. Server applies damage
    currentHealth.Value -= actualDamage;
    
    // 6. Server checks for death
    if (currentHealth.Value <= 0)
    {
        Die(isHeadshot, attackerClientId);
    }
}
```

### Why Server Authority?

**Without Server Authority:**
```
Client: "I have 1000 health!"
Server: "OK" ❌ (Client can cheat)
```

**With Server Authority:**
```
Client: "I took 50 damage"
Server: "Let me validate... Yes, you took 50 damage. Your health is now 50." ✅
```

---

## Layer 3: Rate Limiting

### Purpose
Prevent spam, DOS attacks, and rapid-fire exploits.

### Rate Limit Configuration

```csharp
private Dictionary<string, float> rateLimits = new Dictionary<string, float>
{
    { "shoot", 20f },      // Max 20 shots per second
    { "damage", 50f },     // Max 50 damage events per second
    { "move", 100f },      // Max 100 movement updates per second
    { "respawn", 1f },     // Max 1 respawn per second
    { "chat", 5f }         // Max 5 chat messages per second
};
```

### Implementation

```csharp
public class SecurityManager : MonoBehaviour
{
    private Dictionary<ulong, Dictionary<string, ActionTracker>> playerActions = 
        new Dictionary<ulong, Dictionary<string, ActionTracker>>();
    
    private class ActionTracker
    {
        public int count;
        public float windowStart;
    }
    
    public bool CheckRateLimit(ulong clientId, string actionType)
    {
        if (!rateLimits.ContainsKey(actionType))
            return true; // No limit for this action
        
        float limit = rateLimits[actionType];
        float windowSize = 1f; // 1 second window
        
        // Initialize tracking for this player
        if (!playerActions.ContainsKey(clientId))
        {
            playerActions[clientId] = new Dictionary<string, ActionTracker>();
        }
        
        if (!playerActions[clientId].ContainsKey(actionType))
        {
            playerActions[clientId][actionType] = new ActionTracker
            {
                count = 1,
                windowStart = Time.time
            };
            return true;
        }
        
        var tracker = playerActions[clientId][actionType];
        
        // Check if window has expired
        if (Time.time - tracker.windowStart > windowSize)
        {
            // Reset window
            tracker.count = 1;
            tracker.windowStart = Time.time;
            return true;
        }
        
        // Check if within limit
        if (tracker.count < limit)
        {
            tracker.count++;
            return true;
        }
        
        // Rate limit exceeded
        Debug.LogWarning($"Rate limit exceeded for client {clientId}: {actionType} ({tracker.count}/{limit})");
        LogSuspiciousActivity(clientId, $"Rate limit exceeded: {actionType}");
        return false;
    }
}
```

### Usage in Game Code

```csharp
private void Fire()
{
    // Check rate limit before processing
    if (!SecurityManager.Instance.CheckRateLimit(NetworkManager.Singleton.LocalClientId, "shoot"))
    {
        return; // Blocked by rate limiter
    }
    
    // Process shot
    // ...
}
```

---

## Layer 4: Input Validation

### Purpose
Sanitize and validate all user inputs to prevent injection attacks and invalid data.

### String Validation

```csharp
private bool ValidateInput(string input, string fieldName, int maxLength = 50)
{
    // 1. Check for null or empty
    if (string.IsNullOrWhiteSpace(input))
    {
        Debug.LogError($"{fieldName} cannot be empty");
        return false;
    }
    
    // 2. Check length
    if (input.Length > maxLength)
    {
        Debug.LogError($"{fieldName} is too long (max {maxLength} characters)");
        return false;
    }
    
    // 3. Check for invalid characters (Firebase path characters)
    if (input.Contains("/") || input.Contains(".") || 
        input.Contains("$") || input.Contains("#") ||
        input.Contains("[") || input.Contains("]"))
    {
        Debug.LogError($"{fieldName} contains invalid characters");
        return false;
    }
    
    // 4. Check for SQL injection patterns (defense in depth)
    string lowerInput = input.ToLower();
    string[] sqlKeywords = { "select", "drop", "delete", "insert", "update", "union", "exec", "script" };
    foreach (string keyword in sqlKeywords)
    {
        if (lowerInput.Contains(keyword))
        {
            Debug.LogError($"{fieldName} contains suspicious content");
            LogSuspiciousActivity(0, $"SQL injection attempt: {input}");
            return false;
        }
    }
    
    return true;
}
```

### Numeric Validation

```csharp
private bool ValidateMMR(int mmr)
{
    if (mmr < 0 || mmr > 3000)
    {
        Debug.LogError($"Invalid MMR value: {mmr}");
        return false;
    }
    return true;
}

private bool ValidateDamage(float damage)
{
    if (damage < 0 || damage > 1000)
    {
        Debug.LogError($"Invalid damage value: {damage}");
        return false;
    }
    return true;
}
```

### Usage Example

```csharp
public void SavePlayerData(string playerName, int mmr)
{
    // Validate all inputs before saving
    if (!ValidateInput(playerName, "Player name", 30))
        return;
    
    if (!ValidateMMR(mmr))
        return;
    
    // Safe to save
    Dictionary<string, object> playerData = new Dictionary<string, object>
    {
        { "name", playerName },
        { "mmr", mmr }
    };
    
    FirebaseDatabase.DefaultInstance
        .GetReference($"players/{playerId}")
        .UpdateChildrenAsync(playerData);
}
```

---

## Layer 5: Firebase Security Rules

### Purpose
Database-level protection that cannot be bypassed by modified clients.

### Complete Rule Set

```json
{
  "rules": {
    "players": {
      "$uid": {
        ".read": "auth != null",
        ".write": "$uid === auth.uid",
        "name": {
          ".validate": "newData.isString() && newData.val().length <= 30"
        },
        "stats": {
          "ranked_1v1": {
            "mmr": {
              ".validate": "newData.isNumber() && newData.val() >= 0 && newData.val() <= 3000"
            },
            "wins": {
              ".validate": "newData.isNumber() && newData.val() >= 0"
            },
            "losses": {
              ".validate": "newData.isNumber() && newData.val() >= 0"
            }
          }
        }
      }
    },
    "presence": {
      "$uid": {
        ".read": "auth != null",
        ".write": "$uid === auth.uid",
        "sessionId": {
          ".validate": "newData.isString()"
        }
      }
    },
    "matchmaking": {
      "queue_1v1": {
        "$queueId": {
          ".read": "auth != null",
          ".write": "auth != null && (!data.exists() || data.child('playerId').val() === auth.uid)",
          ".validate": "newData.hasChildren(['playerId', 'playerName', 'mmr', 'eosUserId', 'timestamp'])",
          "mmr": {
            ".validate": "newData.isNumber() && newData.val() >= 0 && newData.val() <= 3000"
          }
        }
      }
    },
    "matches": {
      "$matchId": {
        ".read": "auth != null && (data.child('team1').val().contains(auth.uid) || data.child('team2').val().contains(auth.uid))",
        ".write": "auth != null && (data.child('team1').val().contains(auth.uid) || data.child('team2').val().contains(auth.uid))"
      }
    },
    "security_logs": {
      ".read": "false",
      ".write": "auth != null"
    }
  }
}
```

### Rule Explanations

**Players Node:**
- Anyone authenticated can read any player data (for matchmaking)
- Players can only write to their own data (`$uid === auth.uid`)
- Name must be string, max 30 characters
- MMR must be number, 0-3000 range
- Wins/losses must be non-negative numbers

**Presence Node:**
- Anyone authenticated can read presence (see who's online)
- Players can only write their own presence
- Session ID must be string

**Matchmaking Queues:**
- Anyone authenticated can read queue (find opponents)
- Can only create new entries or delete own entries
- Must include all required fields
- MMR validated at database level

**Matches Node:**
- Only participants can read match data
- Only participants can write match data
- Prevents spectating or manipulating other matches

**Security Logs:**
- No one can read (admin only via Firebase console)
- Any authenticated user can write logs
- Allows reporting suspicious activity

---

## Layer 6: Anomaly Detection

### Purpose
Detect cheating through statistical analysis of player behavior.

### Tracked Metrics

```csharp
private class PlayerStats
{
    public int totalShots;
    public int totalHits;
    public int totalHeadshots;
    public float totalDamageDealt;
    public float playTime;
    public List<float> shotIntervals;
    public List<float> movementSpeeds;
}
```

### Detection Algorithms

**1. Accuracy Detection:**
```csharp
public void AnalyzeAccuracy(ulong clientId)
{
    var stats = playerStats[clientId];
    
    if (stats.totalShots < 50)
        return; // Not enough data
    
    float accuracy = (float)stats.totalHits / stats.totalShots;
    
    if (accuracy > 0.95f) // 95% accuracy
    {
        LogSuspiciousActivity(clientId, $"Suspiciously high accuracy: {accuracy * 100:F1}%");
    }
}
```

**2. Headshot Rate Detection:**
```csharp
public void AnalyzeHeadshotRate(ulong clientId)
{
    var stats = playerStats[clientId];
    
    if (stats.totalHits < 20)
        return; // Not enough data
    
    float headshotRate = (float)stats.totalHeadshots / stats.totalHits;
    
    if (headshotRate > 0.8f) // 80% headshot rate
    {
        LogSuspiciousActivity(clientId, $"Suspiciously high headshot rate: {headshotRate * 100:F1}%");
    }
}
```

**3. Damage Per Second Detection:**
```csharp
public void AnalyzeDPS(ulong clientId)
{
    var stats = playerStats[clientId];
    
    if (stats.playTime < 10f)
        return; // Not enough data
    
    float dps = stats.totalDamageDealt / stats.playTime;
    
    // Max theoretical DPS with perfect accuracy
    float maxTheoretical DPS = 500f;
    
    if (dps > maxTheoreticalDPS)
    {
        LogSuspiciousActivity(clientId, $"Impossible DPS: {dps:F1} (max: {maxTheoreticalDPS})");
    }
}
```

**4. Fire Rate Detection:**
```csharp
public void AnalyzeFireRate(ulong clientId)
{
    var stats = playerStats[clientId];
    
    if (stats.shotIntervals.Count < 10)
        return; // Not enough data
    
    float avgInterval = stats.shotIntervals.Average();
    float minInterval = 60f / 900f; // 900 RPM max
    
    if (avgInterval < minInterval)
    {
        float actualRPM = 60f / avgInterval;
        LogSuspiciousActivity(clientId, $"Impossible fire rate: {actualRPM:F0} RPM (max: 900)");
    }
}
```

**5. Movement Speed Detection:**
```csharp
public void AnalyzeMovementSpeed(ulong clientId)
{
    var stats = playerStats[clientId];
    
    if (stats.movementSpeeds.Count < 10)
        return; // Not enough data
    
    float maxSpeed = stats.movementSpeeds.Max();
    float maxLegalSpeed = 10f; // Sprint speed + margin
    
    if (maxSpeed > maxLegalSpeed)
    {
        LogSuspiciousActivity(clientId, $"Impossible movement speed: {maxSpeed:F1} m/s (max: {maxLegalSpeed})");
    }
}
```

### Logging Suspicious Activity

```csharp
public void LogSuspiciousActivity(ulong clientId, string reason)
{
    // Log locally
    Debug.LogWarning($"[SECURITY] Suspicious activity from client {clientId}: {reason}");
    
    // Log to Firebase for admin review
    Dictionary<string, object> logData = new Dictionary<string, object>
    {
        { "clientId", clientId.ToString() },
        { "reason", reason },
        { "timestamp", ServerValue.Timestamp },
        { "severity", DetermineSeverity(reason) }
    };
    
    FirebaseDatabase.DefaultInstance
        .GetReference("security_logs")
        .Push()
        .SetValueAsync(logData);
    
    // Optionally: Auto-kick for severe violations
    if (DetermineSeverity(reason) == "critical")
    {
        KickPlayer(clientId, reason);
    }
}
```

---

## Match Result Validation

### Purpose
Prevent manipulation of match results and MMR.

### Validation Checks

```csharp
public bool ValidateMatchResult(int team1Score, int team2Score, float matchDuration)
{
    // 1. Check for negative scores
    if (team1Score < 0 || team2Score < 0)
    {
        Debug.LogError("Invalid match result: Negative scores");
        return false;
    }
    
    // 2. Check for impossibly high scores
    // Assume max 1 kill every 2 seconds
    int maxPossibleScore = Mathf.FloorToInt(matchDuration / 2f);
    
    if (team1Score > maxPossibleScore || team2Score > maxPossibleScore)
    {
        Debug.LogError($"Invalid match result: Score too high for duration ({matchDuration}s)");
        return false;
    }
    
    // 3. Check for suspiciously short matches
    if (matchDuration < 10f && (team1Score > 0 || team2Score > 0))
    {
        Debug.LogError("Invalid match result: Match too short");
        return false;
    }
    
    // 4. Check for suspiciously long matches with no score
    if (matchDuration > 300f && team1Score == 0 && team2Score == 0)
    {
        Debug.LogWarning("Suspicious match result: Long match with no score");
        return false;
    }
    
    // 5. Check for extreme score differences (possible farming)
    int scoreDiff = Mathf.Abs(team1Score - team2Score);
    if (scoreDiff > 20)
    {
        Debug.LogWarning($"Suspicious match result: Extreme score difference ({scoreDiff})");
        // Don't reject, but log for review
    }
    
    return true;
}
```

### Usage in Match End

```csharp
private void EndMatch()
{
    matchActive.Value = false;
    
    float matchDuration = Time.time - matchStartTime;
    
    // Validate match results
    if (SecurityManager.Instance != null)
    {
        if (!SecurityManager.Instance.ValidateMatchResult(team1Score.Value, team2Score.Value, matchDuration))
        {
            Debug.LogError("Invalid match result detected! Not applying MMR changes.");
            OnMatchEnd?.Invoke(0);
            CleanupMatchData();
            return; // Don't apply MMR for invalid matches
        }
    }
    
    // Continue with normal match end flow
    // ...
}
```

---

## Integration with Other Systems

### With Networking System
```
Client sends ServerRpc → Rate limit check → Input validation → Server processes
```

### With Firebase System
```
Client writes data → Firebase security rules validate → Accept or reject
```

### With Match System
```
Match ends → Validate results → Apply MMR if valid → Log if suspicious
```

### With Ranking System
```
MMR change → Validate range → Clamp to limits → Save to Firebase
```

---

## Best Practices

1. **Never trust the client** - Always validate on server
2. **Use server authority** for all critical game state
3. **Implement rate limiting** for all player actions
4. **Validate all inputs** before processing
5. **Use Firebase security rules** as last line of defense
6. **Log suspicious activity** for review
7. **Don't auto-ban** without human review (false positives)
8. **Test with modified clients** to find vulnerabilities
9. **Monitor security logs** regularly
10. **Update detection algorithms** as new cheats emerge

---

## Common Attack Vectors

### 1. Speed Hacks
**Attack:** Modify movement speed locally
**Defense:** Server validates movement distance vs time

### 2. Aimbot
**Attack:** Perfect aim assistance
**Defense:** Anomaly detection (accuracy, headshot rate)

### 3. Wallhacks
**Attack:** See through walls
**Defense:** Server doesn't send data for occluded players

### 4. Damage Manipulation
**Attack:** Modify damage values
**Defense:** Server calculates damage, client only suggests

### 5. Infinite Ammo
**Attack:** Never decrease ammo count
**Defense:** Server tracks ammo, validates reload

### 6. MMR Manipulation
**Attack:** Modify MMR in Firebase
**Defense:** Firebase security rules prevent unauthorized writes

### 7. Match Result Manipulation
**Attack:** Report fake match results
**Defense:** Server validates results before applying MMR

---

## Debugging Security

### Enable Security Logs
```csharp
public bool showSecurityLogs = true;

void LogSecurityEvent(string message)
{
    if (showSecurityLogs)
    {
        Debug.Log($"[SECURITY] {message}");
    }
}
```

### Security Dashboard
```csharp
void OnGUI()
{
    if (showSecurityLogs)
    {
        GUILayout.Label("=== Security Dashboard ===");
        GUILayout.Label($"Rate Limit Violations: {rateLimitViolations}");
        GUILayout.Label($"Invalid Inputs: {invalidInputs}");
        GUILayout.Label($"Suspicious Activities: {suspiciousActivities}");
        GUILayout.Label($"Kicked Players: {kickedPlayers}");
    }
}
```

---

*This documentation explains the security system architecture and functionality. For implementation details, see `SecurityManager.cs`.*
