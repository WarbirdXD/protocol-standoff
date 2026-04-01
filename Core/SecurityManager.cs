using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// Handles security measures including rate limiting, input validation, and anti-cheat
/// </summary>
public class SecurityManager : MonoBehaviour
{
    public static SecurityManager Instance { get; private set; }
    
    [Header("Rate Limiting Settings")]
    [SerializeField] private int maxLoginAttemptsPerMinute = 5;
    [SerializeField] private int maxRegisterAttemptsPerMinute = 3;
    [SerializeField] private int maxMatchmakingAttemptsPerMinute = 10;
    
    [Header("Input Validation")]
    [SerializeField] private int maxUsernameLength = 20;
    [SerializeField] private int minUsernameLength = 3;
    [SerializeField] private int maxEmailLength = 100;
    [SerializeField] private int minPasswordLength = 6;
    [SerializeField] private int maxPasswordLength = 128;
    
    [Header("Anti-Cheat Settings")]
    [SerializeField] private float maxAllowedSpeedMultiplier = 1.5f;
    [SerializeField] private float maxAllowedDamageMultiplier = 1.2f;
    [SerializeField] private bool enablePositionValidation = true;
    
    // Rate limiting tracking
    private Dictionary<string, List<float>> loginAttempts = new Dictionary<string, List<float>>();
    private Dictionary<string, List<float>> registerAttempts = new Dictionary<string, List<float>>();
    private Dictionary<string, List<float>> matchmakingAttempts = new Dictionary<string, List<float>>();
    
    // Suspicious activity tracking
    private Dictionary<string, int> suspiciousActivityCount = new Dictionary<string, int>();
    private const int MAX_SUSPICIOUS_ACTIVITIES = 10;
    
    // Firebase logging
    private bool firebaseLoggingEnabled = true;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    #region Rate Limiting
    
    /// <summary>
    /// Check if login attempt is allowed (rate limiting)
    /// </summary>
    public bool CanAttemptLogin(string identifier)
    {
        return CheckRateLimit(loginAttempts, identifier, maxLoginAttemptsPerMinute);
    }
    
    /// <summary>
    /// Check if register attempt is allowed (rate limiting)
    /// </summary>
    public bool CanAttemptRegister(string identifier)
    {
        return CheckRateLimit(registerAttempts, identifier, maxRegisterAttemptsPerMinute);
    }
    
    /// <summary>
    /// Check if matchmaking attempt is allowed (rate limiting)
    /// </summary>
    public bool CanAttemptMatchmaking(string identifier)
    {
        return CheckRateLimit(matchmakingAttempts, identifier, maxMatchmakingAttemptsPerMinute);
    }
    
    private bool CheckRateLimit(Dictionary<string, List<float>> attempts, string identifier, int maxAttempts)
    {
        if (string.IsNullOrEmpty(identifier))
            return false;
        
        float currentTime = Time.time;
        
        if (!attempts.ContainsKey(identifier))
        {
            attempts[identifier] = new List<float>();
        }
        
        // Remove attempts older than 1 minute
        attempts[identifier].RemoveAll(time => currentTime - time > 60f);
        
        // Check if under limit
        if (attempts[identifier].Count >= maxAttempts)
        {
            Debug.LogWarning($"Rate limit exceeded for {identifier}");
            LogToFirebase("rate_limiting", "exceeded", new Dictionary<string, object>
            {
                { "identifier", identifier },
                { "maxAttempts", maxAttempts },
                { "attemptType", GetAttemptType(attempts) }
            });
            return false;
        }
        
        // Record this attempt
        attempts[identifier].Add(currentTime);
        return true;
    }
    
    #endregion
    
    #region Input Validation
    
    /// <summary>
    /// Validate username input
    /// </summary>
    public (bool isValid, string errorMessage) ValidateUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, "Username cannot be empty");
        
        if (username.Length < minUsernameLength)
            return (false, $"Username must be at least {minUsernameLength} characters");
        
        if (username.Length > maxUsernameLength)
            return (false, $"Username cannot exceed {maxUsernameLength} characters");
        
        // Check for valid characters (alphanumeric, underscore, hyphen)
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_-]+$"))
            return (false, "Username can only contain letters, numbers, underscores, and hyphens");
        
        // Check for profanity (basic filter)
        if (ContainsProfanity(username))
            return (false, "Username contains inappropriate content");
        
        return (true, "");
    }
    
    /// <summary>
    /// Validate email input
    /// </summary>
    public (bool isValid, string errorMessage) ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return (false, "Email cannot be empty");
        
        if (email.Length > maxEmailLength)
            return (false, $"Email cannot exceed {maxEmailLength} characters");
        
        // Basic email format validation
        if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return (false, "Invalid email format");
        
        return (true, "");
    }
    
    /// <summary>
    /// Validate password input
    /// </summary>
    public (bool isValid, string errorMessage) ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (false, "Password cannot be empty");
        
        if (password.Length < minPasswordLength)
            return (false, $"Password must be at least {minPasswordLength} characters");
        
        if (password.Length > maxPasswordLength)
            return (false, $"Password cannot exceed {maxPasswordLength} characters");
        
        return (true, "");
    }
    
    /// <summary>
    /// Sanitize string input to prevent injection attacks
    /// </summary>
    public string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        
        // Remove potentially dangerous characters
        input = input.Trim();
        input = System.Text.RegularExpressions.Regex.Replace(input, @"[<>""';\\]", "");
        
        return input;
    }
    
    private bool ContainsProfanity(string text)
    {
        // Basic profanity filter - expand this list as needed
        string[] profanityList = new string[]
        {
            "admin", "moderator", "official", "support", "system"
        };
        
        string lowerText = text.ToLower();
        foreach (string word in profanityList)
        {
            if (lowerText.Contains(word))
                return true;
        }
        
        return false;
    }
    
    #endregion
    
    #region Anti-Cheat
    
    /// <summary>
    /// Validate player movement speed
    /// </summary>
    public bool ValidateMovementSpeed(Vector3 previousPosition, Vector3 currentPosition, float deltaTime, float normalSpeed)
    {
        if (!enablePositionValidation || deltaTime <= 0)
            return true;
        
        float distance = Vector3.Distance(previousPosition, currentPosition);
        float speed = distance / deltaTime;
        float maxAllowedSpeed = normalSpeed * maxAllowedSpeedMultiplier;
        
        if (speed > maxAllowedSpeed)
        {
            Debug.LogWarning($"Suspicious movement speed detected: {speed} (max: {maxAllowedSpeed})");
            LogToFirebase("anti_cheat", "speed_hack", new Dictionary<string, object>
            {
                { "speed", speed },
                { "maxAllowed", maxAllowedSpeed },
                { "distance", distance },
                { "deltaTime", deltaTime }
            });
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Validate damage amount
    /// </summary>
    public bool ValidateDamage(float damageDealt, float expectedDamage)
    {
        float maxAllowedDamage = expectedDamage * maxAllowedDamageMultiplier;
        
        if (damageDealt > maxAllowedDamage)
        {
            Debug.LogWarning($"Suspicious damage detected: {damageDealt} (max: {maxAllowedDamage})");
            LogToFirebase("anti_cheat", "damage_hack", new Dictionary<string, object>
            {
                { "damageDealt", damageDealt },
                { "expectedDamage", expectedDamage },
                { "maxAllowed", maxAllowedDamage }
            });
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Report suspicious activity
    /// </summary>
    public void ReportSuspiciousActivity(string playerId, string activityType)
    {
        if (string.IsNullOrEmpty(playerId))
            return;
        
        if (!suspiciousActivityCount.ContainsKey(playerId))
        {
            suspiciousActivityCount[playerId] = 0;
        }
        
        suspiciousActivityCount[playerId]++;
        
        Debug.LogWarning($"Suspicious activity reported for {playerId}: {activityType} (Count: {suspiciousActivityCount[playerId]})");
        
        LogToFirebase("anti_cheat", "suspicious_activity", new Dictionary<string, object>
        {
            { "playerId", playerId },
            { "activityType", activityType },
            { "count", suspiciousActivityCount[playerId] },
            { "flagged", suspiciousActivityCount[playerId] >= MAX_SUSPICIOUS_ACTIVITIES }
        });
        
        if (suspiciousActivityCount[playerId] >= MAX_SUSPICIOUS_ACTIVITIES)
        {
            Debug.LogError($"Player {playerId} has exceeded suspicious activity threshold. Consider banning.");
            LogToFirebase("anti_cheat", "player_flagged", new Dictionary<string, object>
            {
                { "playerId", playerId },
                { "totalViolations", suspiciousActivityCount[playerId] }
            });
        }
    }
    
    /// <summary>
    /// Check if player is flagged as suspicious
    /// </summary>
    public bool IsPlayerSuspicious(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return false;
        
        return suspiciousActivityCount.ContainsKey(playerId) && 
               suspiciousActivityCount[playerId] >= MAX_SUSPICIOUS_ACTIVITIES;
    }
    
    #endregion
    
    #region Server Validation
    
    /// <summary>
    /// Validate match result before updating MMR
    /// </summary>
    public bool ValidateMatchResult(int team1Score, int team2Score, float matchDuration)
    {
        // Check for impossible scores
        if (team1Score < 0 || team2Score < 0)
        {
            Debug.LogWarning("Invalid match result: negative scores");
            LogToFirebase("server_validation", "invalid_score", new Dictionary<string, object>
            {
                { "team1Score", team1Score },
                { "team2Score", team2Score },
                { "reason", "negative_scores" }
            });
            return false;
        }
        
        // Check for suspiciously high scores
        if (team1Score > 100 || team2Score > 100)
        {
            Debug.LogWarning("Invalid match result: suspiciously high scores");
            LogToFirebase("server_validation", "invalid_score", new Dictionary<string, object>
            {
                { "team1Score", team1Score },
                { "team2Score", team2Score },
                { "reason", "suspiciously_high" }
            });
            return false;
        }
        
        // Check for suspiciously short matches
        if (matchDuration < 5f)
        {
            Debug.LogWarning("Invalid match result: match too short");
            LogToFirebase("server_validation", "invalid_duration", new Dictionary<string, object>
            {
                { "matchDuration", matchDuration },
                { "reason", "too_short" }
            });
            return false;
        }
        
        // Check for suspiciously long matches
        if (matchDuration > 3600f) // 1 hour
        {
            Debug.LogWarning("Invalid match result: match too long");
            LogToFirebase("server_validation", "invalid_duration", new Dictionary<string, object>
            {
                { "matchDuration", matchDuration },
                { "reason", "too_long" }
            });
            return false;
        }
        
        return true;
    }
    
    #endregion
    
    #region Firebase Logging
    
    /// <summary>
    /// Log security event to Firebase
    /// </summary>
    private void LogToFirebase(string category, string eventType, Dictionary<string, object> data)
    {
#if FIREBASE_INSTALLED
        if (!firebaseLoggingEnabled || FirebaseManager.Instance == null || !FirebaseManager.Instance.isInitialized)
            return;
        
        try
        {
            // Add timestamp and session info
            data["timestamp"] = Firebase.Database.ServerValue.Timestamp;
            data["category"] = category;
            data["eventType"] = eventType;
            
            // Add player info if available
            if (FirebaseManager.Instance.isSignedIn)
            {
                data["playerId"] = FirebaseManager.Instance.playerId;
                data["playerName"] = FirebaseManager.Instance.playerName;
            }
            
            // Create unique log ID
            string logId = System.Guid.NewGuid().ToString();
            
            // Store in Firebase under security_logs/{category}/{logId}
            Firebase.Database.FirebaseDatabase.DefaultInstance
                .GetReference($"security_logs/{category}/{logId}")
                .SetValueAsync(data)
                .ContinueWith(task => {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"Failed to log security event to Firebase: {task.Exception}");
                    }
                });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error logging to Firebase: {e.Message}");
        }
#endif
    }
    
    /// <summary>
    /// Log input validation failure
    /// </summary>
    public void LogValidationFailure(string inputType, string reason, string value = "")
    {
        LogToFirebase("input_validation", "failed", new Dictionary<string, object>
        {
            { "inputType", inputType },
            { "reason", reason },
            { "valueLength", value.Length }
        });
    }
    
    /// <summary>
    /// Get attempt type name for logging
    /// </summary>
    private string GetAttemptType(Dictionary<string, List<float>> attempts)
    {
        if (attempts == loginAttempts) return "login";
        if (attempts == registerAttempts) return "register";
        if (attempts == matchmakingAttempts) return "matchmaking";
        return "unknown";
    }
    
    /// <summary>
    /// Enable or disable Firebase logging
    /// </summary>
    public void SetFirebaseLogging(bool enabled)
    {
        firebaseLoggingEnabled = enabled;
        Debug.Log($"Firebase security logging {(enabled ? "enabled" : "disabled")}");
    }
    
    /// <summary>
    /// Get security statistics from Firebase (for admin dashboard)
    /// </summary>
    public void GetSecurityStats(System.Action<Dictionary<string, int>> callback)
    {
#if FIREBASE_INSTALLED
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.isInitialized)
        {
            callback?.Invoke(null);
            return;
        }
        
        var stats = new Dictionary<string, int>();
        
        Firebase.Database.FirebaseDatabase.DefaultInstance
            .GetReference("security_logs")
            .GetValueAsync()
            .ContinueWith(task => {
                if (task.IsCompleted && !task.IsFaulted && task.Result != null)
                {
                    foreach (var category in task.Result.Children)
                    {
                        stats[category.Key] = (int)category.ChildrenCount;
                    }
                    callback?.Invoke(stats);
                }
                else
                {
                    callback?.Invoke(null);
                }
            });
#else
        callback?.Invoke(null);
#endif
    }
    
    #endregion
}
