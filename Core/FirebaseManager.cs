using UnityEngine;
using System;
using System.Collections.Generic;

#if FIREBASE_INSTALLED
using Firebase;
using Firebase.Auth;
using Firebase.Database;
#endif


/// <summary>
/// Manages Firebase connection and authentication
/// Handles player data, matchmaking, and real-time database
/// NOTE: Install Firebase SDK and add FIREBASE_INSTALLED to Scripting Define Symbols
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }
    
    [Header("Firebase Status")]
    public bool isInitialized = false;
    public bool isSignedIn = false;
    
    [Header("Player Data")]
    public string playerId;
    public string playerName;
    private string currentSessionId;
    
#if FIREBASE_INSTALLED
    // Firebase references
    private FirebaseAuth auth;
    private DatabaseReference database;
    private DatabaseReference sessionRef;
#endif
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void OnApplicationQuit()
    {
        // Ensure player is set offline when app closes
        SetPlayerOffline();
    }
    
    private void OnDestroy()
    {
        // Ensure player is set offline when object is destroyed
        if (Instance == this)
        {
            SetPlayerOffline();
        }
    }
    
    private void Start()
    {
        InitializeFirebase();
    }
    
    /// <summary>
    /// Initialize Firebase services
    /// </summary>
    public void InitializeFirebase()
    {
#if FIREBASE_INSTALLED
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                database = FirebaseDatabase.DefaultInstance.RootReference;
                isInitialized = true;
                Debug.Log("Firebase initialized successfully!");
                
                // Check if user is already signed in
                if (auth.CurrentUser != null)
                {
                    playerId = auth.CurrentUser.UserId;
                    playerName = auth.CurrentUser.DisplayName ?? "Player";
                    isSignedIn = true;
                    
                    // Notify AccountManager about the restored session
                    if (AccountManager.Instance != null)
                    {
                        AccountManager.Instance.OnFirebaseSessionRestored(playerName);
                    }
                    
                    LoadPlayerData();
                    
                    // Set player online when session is restored
                    SetPlayerOnline();
                    
                    Debug.Log($"Firebase session restored: {playerName}");
                }
            }
            else
            {
                Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
                isInitialized = false;
            }
        });
#else
        Debug.LogWarning("Firebase SDK not installed. Install Firebase and add FIREBASE_INSTALLED to Scripting Define Symbols.");
        isInitialized = true;
#endif
    }
    
    /// <summary>
    /// Sign in anonymously (for testing) or with credentials
    /// </summary>
    public void SignInAnonymously(Action<bool> callback)
    {
#if FIREBASE_INSTALLED
        if (auth == null)
        {
            Debug.LogError("Firebase Auth not initialized!");
            callback?.Invoke(false);
            return;
        }
        
        auth.SignInAnonymouslyAsync().ContinueWith(task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError($"Sign in failed: {task.Exception}");
                callback?.Invoke(false);
                return;
            }
            
            playerId = auth.CurrentUser.UserId;
            playerName = "Player_" + UnityEngine.Random.Range(1000, 9999);
            isSignedIn = true;
            
            Debug.Log($"Signed in as {playerName} (ID: {playerId})");
            callback?.Invoke(true);
        });
#else
        // Simulate sign in without Firebase
        playerId = System.Guid.NewGuid().ToString();
        playerName = "Player_" + UnityEngine.Random.Range(1000, 9999);
        isSignedIn = true;
        Debug.Log($"Signed in (simulated) as {playerName} (ID: {playerId})");
        callback?.Invoke(true);
#endif
    }
    
    /// <summary>
    /// Load player data from Firebase
    /// </summary>
    public void LoadPlayerData()
    {
        if (!isSignedIn) return;
        
#if FIREBASE_INSTALLED
        if (database == null) return;
        
        database.Child("players").Child(playerId).GetValueAsync().ContinueWith(task => {
            if (task.IsFaulted)
            {
                Debug.LogError($"Failed to load player data: {task.Exception}");
                return;
            }
            
            if (task.IsCompleted)
            {
                DataSnapshot snapshot = task.Result;
                
                if (snapshot.Exists)
                {
                    // Load player name
                    if (snapshot.Child("name").Exists)
                        playerName = snapshot.Child("name").Value.ToString();
                    
                    // Load mode-specific MMR into RankingSystem
                    if (RankingSystem.Instance != null)
                    {
                        if (snapshot.Child("oneVsOneMmr").Exists)
                            RankingSystem.Instance.oneVsOneMmr = int.Parse(snapshot.Child("oneVsOneMmr").Value.ToString());
                        
                        if (snapshot.Child("twoVsTwoMmr").Exists)
                            RankingSystem.Instance.twoVsTwoMmr = int.Parse(snapshot.Child("twoVsTwoMmr").Value.ToString());
                        
                        // Load 1v1 stats
                        if (snapshot.Child("oneVsOneMatches").Exists)
                            RankingSystem.Instance.oneVsOneMatches = int.Parse(snapshot.Child("oneVsOneMatches").Value.ToString());
                        if (snapshot.Child("oneVsOneWins").Exists)
                            RankingSystem.Instance.oneVsOneWins = int.Parse(snapshot.Child("oneVsOneWins").Value.ToString());
                        if (snapshot.Child("oneVsOneLosses").Exists)
                            RankingSystem.Instance.oneVsOneLosses = int.Parse(snapshot.Child("oneVsOneLosses").Value.ToString());
                        
                        // Load 2v2 stats
                        if (snapshot.Child("twoVsTwoMatches").Exists)
                            RankingSystem.Instance.twoVsTwoMatches = int.Parse(snapshot.Child("twoVsTwoMatches").Value.ToString());
                        if (snapshot.Child("twoVsTwoWins").Exists)
                            RankingSystem.Instance.twoVsTwoWins = int.Parse(snapshot.Child("twoVsTwoWins").Value.ToString());
                        if (snapshot.Child("twoVsTwoLosses").Exists)
                            RankingSystem.Instance.twoVsTwoLosses = int.Parse(snapshot.Child("twoVsTwoLosses").Value.ToString());
                        
                        // Update current display stats
                        RankingSystem.Instance.SetCurrentMode(MatchmakingManager.MatchMode.OneVsOne);
                    }
                    
                    Debug.Log($"Loaded player data: {playerName}");
                }
                else
                {
                    // New player - create initial data
                    SavePlayerData();
                }
            }
        });
#else
        // Load from PlayerPrefs as fallback
        if (RankingSystem.Instance != null)
        {
            RankingSystem.Instance.LoadPlayerStats();
        }
        Debug.Log($"Loaded player data (local): {playerName}");
#endif
    }
    
    /// <summary>
    /// Save player data to Firebase
    /// </summary>
    public void SavePlayerData()
    {
        if (!isSignedIn) return;
        
#if FIREBASE_INSTALLED
        if (database == null) return;
        
        // Get mode-specific data from RankingSystem
        int oneVsOneMmr = RankingSystem.Instance != null ? RankingSystem.Instance.oneVsOneMmr : 600;
        int twoVsTwoMmr = RankingSystem.Instance != null ? RankingSystem.Instance.twoVsTwoMmr : 600;
        
        int oneVsOneMatches = RankingSystem.Instance != null ? RankingSystem.Instance.oneVsOneMatches : 0;
        int oneVsOneWins = RankingSystem.Instance != null ? RankingSystem.Instance.oneVsOneWins : 0;
        int oneVsOneLosses = RankingSystem.Instance != null ? RankingSystem.Instance.oneVsOneLosses : 0;
        
        int twoVsTwoMatches = RankingSystem.Instance != null ? RankingSystem.Instance.twoVsTwoMatches : 0;
        int twoVsTwoWins = RankingSystem.Instance != null ? RankingSystem.Instance.twoVsTwoWins : 0;
        int twoVsTwoLosses = RankingSystem.Instance != null ? RankingSystem.Instance.twoVsTwoLosses : 0;
        
        Dictionary<string, object> playerData = new Dictionary<string, object> {
            { "name", playerName },
            { "oneVsOneMmr", oneVsOneMmr },
            { "twoVsTwoMmr", twoVsTwoMmr },
            { "oneVsOneMatches", oneVsOneMatches },
            { "oneVsOneWins", oneVsOneWins },
            { "oneVsOneLosses", oneVsOneLosses },
            { "twoVsTwoMatches", twoVsTwoMatches },
            { "twoVsTwoWins", twoVsTwoWins },
            { "twoVsTwoLosses", twoVsTwoLosses },
            { "lastPlayed", ServerValue.Timestamp }
        };
        
        database.Child("players").Child(playerId).UpdateChildrenAsync(playerData).ContinueWith(task => {
            if (task.IsCompleted && !task.IsFaulted)
            {
                Debug.Log("Player data saved to Firebase");
            }
            else
            {
                string error = task.Exception?.GetBaseException().Message ?? "Unknown error";
                Debug.LogWarning($"Failed to save player data: {error}");
            }
        });
#else
        // Save to PlayerPrefs as fallback
        if (RankingSystem.Instance != null)
        {
            RankingSystem.Instance.SavePlayerStats();
        }
        Debug.Log("Player data saved (local)");
#endif
    }
    
    
    /// <summary>
    /// Sign out
    /// </summary>
    public void SignOut()
    {
        SetPlayerOffline();
        
#if FIREBASE_INSTALLED
        if (auth != null)
        {
            auth.SignOut();
        }
#endif
        
        isSignedIn = false;
        playerId = null;
        playerName = null;
        Debug.Log("Signed out");
    }
    
    /// <summary>
    /// Set player status to online in Firebase
    /// </summary>
    public void SetPlayerOnline()
    {
#if FIREBASE_INSTALLED
        if (database != null && !string.IsNullOrEmpty(playerId))
        {
            // Generate unique session ID
            currentSessionId = System.Guid.NewGuid().ToString();
            
            // Update player status to online
            Dictionary<string, object> onlineData = new Dictionary<string, object>
            {
                { "online", true },
                { "lastSeen", ServerValue.Timestamp }
            };
            
            database.Child("players").Child(playerId).Child("status").UpdateChildrenAsync(onlineData);
            
            // Add to presence system with session ID (will auto-remove on disconnect)
            Dictionary<string, object> presenceData = new Dictionary<string, object>
            {
                { "online", true },
                { "lastSeen", ServerValue.Timestamp },
                { "playerName", playerName },
                { "sessionId", currentSessionId }
            };
            
            var presenceRef = database.Child("presence").Child(playerId);
            
            // Store session reference for checking
            sessionRef = presenceRef;
            
            // Set presence data and wait for it to complete before monitoring
            presenceRef.SetValueAsync(presenceData).ContinueWith(task => {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    // Now that data is written, monitor for changes (kicks)
                    presenceRef.ValueChanged += OnSessionChanged;
                    Debug.Log($"Player {playerName} set to online with session {currentSessionId}");
                }
            });
            
            presenceRef.OnDisconnect().RemoveValue();
        }
#endif
    }
    
    /// <summary>
    /// Set player status to offline in Firebase
    /// </summary>
    public void SetPlayerOffline()
    {
#if FIREBASE_INSTALLED
        if (database != null && !string.IsNullOrEmpty(playerId))
        {
            // Unsubscribe from session listener before removing presence
            // This prevents the "kicked" warning on normal logout/close
            if (sessionRef != null)
            {
                sessionRef.ValueChanged -= OnSessionChanged;
            }
            
            // Remove from presence system
            database.Child("presence").Child(playerId).RemoveValueAsync();
            
            // Update last seen timestamp
            Dictionary<string, object> offlineData = new Dictionary<string, object>
            {
                { "lastSeen", ServerValue.Timestamp },
                { "online", false }
            };
            
            database.Child("players").Child(playerId).Child("status").UpdateChildrenAsync(offlineData);
            Debug.Log($"Player {playerName} set to offline");
        }
#endif
    }
    
    /// <summary>
    /// Get current player MMR from RankingSystem
    /// </summary>
    public int GetPlayerMMR()
    {
        return RankingSystem.Instance != null ? RankingSystem.Instance.currentMMR : 600;
    }
    
    /// <summary>
    /// Get current player rank name from RankingSystem
    /// </summary>
    public string GetPlayerRank()
    {
        return RankingSystem.Instance != null ? RankingSystem.Instance.GetRankDisplayName() : "Chrome I";
    }
    
    // ==================== ACCOUNT SYSTEM METHODS ====================
    
    /// <summary>
    /// Register new account with email and password
    /// </summary>
    public void RegisterWithEmail(string email, string password, string username, Action<bool, string> callback)
    {
#if FIREBASE_INSTALLED
        if (auth == null)
        {
            callback?.Invoke(false, "Firebase not initialized");
            return;
        }
        
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                string error = task.Exception?.GetBaseException().Message ?? "Registration failed";
                
                // Parse Firebase error to user-friendly message
                string userMessage = ParseFirebaseError(error);
                Debug.LogWarning($"Registration failed: {userMessage}");
                callback?.Invoke(false, userMessage);
                return;
            }
            
            // Set username
            var user = task.Result.User;
            playerId = user.UserId;
            playerName = username;
            isSignedIn = true;
            
            // Update profile with username
            var profile = new UserProfile { DisplayName = username };
            user.UpdateUserProfileAsync(profile).ContinueWith(profileTask => {
                if (profileTask.IsCompleted && !profileTask.IsFaulted)
                {
                    // Save initial player data
                    SavePlayerData();
                    
                    // Set player online
                    SetPlayerOnline();
                    
                    callback?.Invoke(true, "Account created successfully");
                }
                else
                {
                    callback?.Invoke(false, "Failed to set username");
                }
            });
        });
#else
        callback?.Invoke(false, "Firebase not available");
#endif
    }
    
    /// <summary>
    /// Login with email and password
    /// </summary>
    public void LoginWithEmail(string email, string password, Action<bool, string, string> callback)
    {
#if FIREBASE_INSTALLED
        if (auth == null)
        {
            callback?.Invoke(false, "Firebase not initialized", "");
            return;
        }
        
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task => {
            if (task.IsCanceled || task.IsFaulted)
            {
                string error = task.Exception?.GetBaseException().Message ?? "Login failed";
                
                // Parse Firebase error to user-friendly message
                string userMessage = ParseFirebaseError(error);
                Debug.LogWarning($"Login failed: {userMessage}");
                callback?.Invoke(false, userMessage, "");
                return;
            }
            
            var user = task.Result.User;
            playerId = user.UserId;
            playerName = user.DisplayName ?? "Player";
            
            // Check if account is already logged in elsewhere
            CheckAndKickExistingSession(() => {
                isSignedIn = true;
                
                // Load player data
                LoadPlayerData();
                
                // Set player online
                SetPlayerOnline();
                
                callback?.Invoke(true, "Login successful", playerName);
            });
        });
#else
        callback?.Invoke(false, "Firebase not available", "");
#endif
    }
    
    /// <summary>
    /// Try to restore previous session
    /// </summary>
    public void TryRestoreSession(Action<bool, string> callback)
    {
#if FIREBASE_INSTALLED
        if (auth == null)
        {
            callback?.Invoke(false, "");
            return;
        }
        
        var user = auth.CurrentUser;
        if (user != null)
        {
            playerId = user.UserId;
            playerName = user.DisplayName ?? "Player";
            isSignedIn = true;
            
            LoadPlayerData();
            callback?.Invoke(true, playerName);
        }
        else
        {
            callback?.Invoke(false, "");
        }
#else
        callback?.Invoke(false, "");
#endif
    }
    
    /// <summary>
    /// Update username
    /// </summary>
    public void UpdateUsername(string newUsername, Action<bool, string> callback)
    {
#if FIREBASE_INSTALLED
        if (auth == null || auth.CurrentUser == null)
        {
            callback?.Invoke(false, "Not logged in");
            return;
        }
        
        var profile = new UserProfile { DisplayName = newUsername };
        auth.CurrentUser.UpdateUserProfileAsync(profile).ContinueWith(task => {
            if (task.IsCompleted && !task.IsFaulted)
            {
                playerName = newUsername;
                SavePlayerData();
                callback?.Invoke(true, "Username updated");
            }
            else
            {
                callback?.Invoke(false, "Failed to update username");
            }
        });
#else
        callback?.Invoke(false, "Firebase not available");
#endif
    }
    
    /// <summary>
    /// Change password
    /// </summary>
    public void ChangePassword(string currentPassword, string newPassword, Action<bool, string> callback)
    {
#if FIREBASE_INSTALLED
        if (auth == null || auth.CurrentUser == null)
        {
            callback?.Invoke(false, "Not logged in");
            return;
        }
        
        var user = auth.CurrentUser;
        
        // Re-authenticate first
        var credential = EmailAuthProvider.GetCredential(user.Email, currentPassword);
        user.ReauthenticateAsync(credential).ContinueWith(reauthTask => {
            if (reauthTask.IsCanceled || reauthTask.IsFaulted)
            {
                callback?.Invoke(false, "Current password is incorrect");
                return;
            }
            
            // Update password
            user.UpdatePasswordAsync(newPassword).ContinueWith(updateTask => {
                if (updateTask.IsCompleted && !updateTask.IsFaulted)
                {
                    callback?.Invoke(true, "Password changed");
                }
                else
                {
                    callback?.Invoke(false, "Failed to change password");
                }
            });
        });
#else
        callback?.Invoke(false, "Firebase not available");
#endif
    }
    
    /// <summary>
    /// Parse Firebase error messages to user-friendly text
    /// </summary>
    private string ParseFirebaseError(string firebaseError)
    {
        if (string.IsNullOrEmpty(firebaseError))
            return "Oops! Something went wrong. Please try again.";
        
        // Convert to lowercase for easier matching
        string errorLower = firebaseError.ToLower();
        
        // Email-related errors
        if (errorLower.Contains("email address is already in use") || errorLower.Contains("email-already-in-use"))
            return "This email is already taken! Try logging in instead, or use a different email address.";
        
        if (errorLower.Contains("invalid-email") || errorLower.Contains("badly formatted"))
            return "That doesn't look like a valid email address. Please enter a valid email (like player@example.com).";
        
        // Password-related errors
        if (errorLower.Contains("weak-password"))
            return "Your password needs to be stronger! Use at least 6 characters with letters and numbers.";
        
        if (errorLower.Contains("wrong-password"))
            return "Incorrect password. Double-check your password and try again!";
        
        if (errorLower.Contains("password") && errorLower.Contains("required"))
            return "Don't forget your password! Please enter it to continue.";
        
        // User/account errors
        if (errorLower.Contains("user-not-found") || errorLower.Contains("no user record"))
            return "We couldn't find an account with that email. Check the spelling or create a new account!";
        
        if (errorLower.Contains("user-disabled"))
            return "This account has been disabled. Please contact support if you need help.";
        
        if (errorLower.Contains("account-exists-with-different-credential"))
            return "An account with this email already exists. Try logging in with your original method.";
        
        // Rate limiting
        if (errorLower.Contains("too-many-requests") || errorLower.Contains("rate limit"))
            return "Whoa, slow down! Too many attempts. Take a short break and try again in a few minutes.";
        
        // Network errors
        if (errorLower.Contains("network") || errorLower.Contains("connection"))
            return "Can't connect to the server. Check your internet connection and try again!";
        
        if (errorLower.Contains("timeout"))
            return "Connection timed out. Make sure you're connected to the internet and try again.";
        
        // Permission/access errors
        if (errorLower.Contains("permission") || errorLower.Contains("unauthorized"))
            return "Access denied. If this keeps happening, please contact support.";
        
        // Session/token errors
        if (errorLower.Contains("token") || errorLower.Contains("session"))
            return "Your session expired. Please log in again to continue.";
        
        if (errorLower.Contains("requires-recent-login"))
            return "For security, you need to log in again to do that.";
        
        // Generic Firebase errors
        if (errorLower.Contains("internal error"))
            return "Server hiccup! Something went wrong on our end. Please try again in a moment.";
        
        if (errorLower.Contains("service unavailable"))
            return "Service is temporarily down. Give us a moment and try again!";
        
        // If no specific match, return a cleaned up version of the error
        // Remove technical prefixes and make it more user-friendly
        string cleanedError = firebaseError
            .Replace("Firebase.Auth.", "")
            .Replace("FirebaseException:", "")
            .Replace("Exception:", "")
            .Trim();
        
        // Make first letter uppercase if it isn't
        if (cleanedError.Length > 0)
        {
            cleanedError = char.ToUpper(cleanedError[0]) + cleanedError.Substring(1);
        }
        
        return $"{cleanedError}. Please try again or contact support if this keeps happening.";
    }
    
    /// <summary>
    /// Delete account
    /// </summary>
    public void DeleteAccount(Action<bool, string> callback)
    {
#if FIREBASE_INSTALLED
        if (auth == null || auth.CurrentUser == null)
        {
            callback?.Invoke(false, "Not logged in");
            return;
        }
        
        var user = auth.CurrentUser;
        
        // Delete user data from database first
        if (database != null)
        {
            database.Child("players").Child(playerId).RemoveValueAsync().ContinueWith(deleteDataTask => {
                // Then delete auth account
                user.DeleteAsync().ContinueWith(deleteAuthTask => {
                    if (deleteAuthTask.IsCompleted && !deleteAuthTask.IsFaulted)
                    {
                        callback?.Invoke(true, "Account deleted");
                    }
                    else
                    {
                        callback?.Invoke(false, "Failed to delete account");
                    }
                });
            });
        }
        else
        {
            user.DeleteAsync().ContinueWith(deleteAuthTask => {
                if (deleteAuthTask.IsCompleted && !deleteAuthTask.IsFaulted)
                {
                    callback?.Invoke(true, "Account deleted");
                }
                else
                {
                    callback?.Invoke(false, "Failed to delete account");
                }
            });
        }
#else
        callback?.Invoke(false, "Firebase not available");
#endif
    }
    
    /// <summary>
    /// Called when session data changes - detects if we've been kicked
    /// </summary>
    private void OnSessionChanged(object sender, ValueChangedEventArgs e)
    {
#if FIREBASE_INSTALLED
        if (!isSignedIn) return;
        
        var snapshot = e.Snapshot;
        
        // If session was removed or sessionId changed, we've been kicked
        if (!snapshot.Exists)
        {
            Debug.LogWarning("Session removed - account logged in elsewhere. Logging out...");
            HandleSessionKicked();
        }
        else if (snapshot.Child("sessionId").Exists)
        {
            string remoteSessionId = snapshot.Child("sessionId").Value.ToString();
            if (remoteSessionId != currentSessionId)
            {
                Debug.LogWarning("Session ID changed - account logged in elsewhere. Logging out...");
                HandleSessionKicked();
            }
        }
#endif
    }
    
    /// <summary>
    /// Handle being kicked from session
    /// </summary>
    private void HandleSessionKicked()
    {
        isSignedIn = false;
        
        // Show message to user
        var accountUI = FindFirstObjectByType<AccountUI>();
        if (accountUI != null)
        {
            accountUI.ShowError("Your account was logged in from another device. You've been signed out for security.");
        }
        
        // Sign out locally (don't update Firebase since we're already kicked)
        playerId = "";
        playerName = "";
        currentSessionId = "";
        
        // Return to login screen (only in Play Mode)
        if (Application.isPlaying)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }
    
    /// <summary>
    /// Check if account is already logged in elsewhere and kick existing session
    /// </summary>
    private void CheckAndKickExistingSession(Action onComplete)
    {
#if FIREBASE_INSTALLED
        if (database == null || string.IsNullOrEmpty(playerId))
        {
            onComplete?.Invoke();
            return;
        }
        
        var presenceRef = database.Child("presence").Child(playerId);
        
        presenceRef.GetValueAsync().ContinueWith(task => {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning("Failed to check existing session, proceeding with login");
                onComplete?.Invoke();
                return;
            }
            
            var snapshot = task.Result;
            
            if (snapshot.Exists)
            {
                // Another session exists - remove it (kick the other session)
                Debug.LogWarning($"Account already logged in elsewhere. Kicking existing session.");
                presenceRef.RemoveValueAsync().ContinueWith(removeTask => {
                    if (removeTask.IsCompleted)
                    {
                        Debug.Log("Existing session removed, proceeding with new login");
                    }
                    onComplete?.Invoke();
                });
            }
            else
            {
                // No existing session, proceed
                onComplete?.Invoke();
            }
        });
#else
        onComplete?.Invoke();
#endif
    }
}
