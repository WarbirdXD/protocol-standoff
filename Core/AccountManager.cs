using UnityEngine;
using System;

/// <summary>
/// Simple account system for player authentication
/// Handles registration, login, and account management
/// </summary>
public class AccountManager : MonoBehaviour
{
    public static AccountManager Instance { get; private set; }
    
    [Header("Account Status")]
    public bool isLoggedIn = false;
    public string currentUsername = "";
    public string accountId = "";
    
    [Header("Settings")]
    public bool rememberMe = true;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Ensure UnityMainThreadDispatcher exists
            if (!UnityMainThreadDispatcher.Exists())
            {
                GameObject dispatcher = new GameObject("UnityMainThreadDispatcher");
                dispatcher.AddComponent<UnityMainThreadDispatcher>();
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // Try to auto-login if remember me is enabled
        if (rememberMe)
        {
            TryAutoLogin();
        }
    }
    
    /// <summary>
    /// Register a new account with email and password
    /// </summary>
    public void RegisterAccount(string email, string password, string username, Action<bool, string> callback)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(username))
        {
            callback?.Invoke(false, "All fields are required");
            return;
        }
        
        if (password.Length < 6)
        {
            callback?.Invoke(false, "Password must be at least 6 characters");
            return;
        }
        
        if (username.Length < 3)
        {
            callback?.Invoke(false, "Username must be at least 3 characters");
            return;
        }
        
#if FIREBASE_INSTALLED
        // Register with Firebase Authentication
        FirebaseManager.Instance.RegisterWithEmail(email, password, username, (success, message) => {
            // Store values to pass to main thread
            bool regSuccess = success;
            string regMessage = message;
            string regUsername = username;
            string regEmail = email;
            bool shouldRemember = rememberMe;
            
            // Execute on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                Debug.Log($"AccountManager: RegisterWithEmail callback - success={regSuccess}, message={regMessage}");
                if (regSuccess)
                {
                    currentUsername = regUsername;
                    isLoggedIn = true;
                    
                    if (shouldRemember)
                    {
                        SaveLoginCredentials(regEmail);
                    }
                    
                    Debug.Log($"Account created successfully: {regUsername}");
                }
                Debug.Log($"AccountManager: Invoking callback to AccountUI");
                callback?.Invoke(regSuccess, regMessage);
            });
        });
#else
        // Simulate account creation without Firebase
        accountId = System.Guid.NewGuid().ToString();
        currentUsername = username;
        isLoggedIn = true;
        
        // Save locally
        PlayerPrefs.SetString("AccountEmail", email);
        PlayerPrefs.SetString("AccountUsername", username);
        PlayerPrefs.SetString("AccountId", accountId);
        PlayerPrefs.SetInt("RememberMe", rememberMe ? 1 : 0);
        PlayerPrefs.Save();
        
        Debug.Log($"Account created (local): {username}");
        callback?.Invoke(true, "Account created successfully");
#endif
    }
    
    /// <summary>
    /// Login with email and password
    /// </summary>
    public void Login(string email, string password, Action<bool, string> callback)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            callback?.Invoke(false, "Email and password are required");
            return;
        }
        
#if FIREBASE_INSTALLED
        // Login with Firebase Authentication
        FirebaseManager.Instance.LoginWithEmail(email, password, (success, message, username) => {
            // Store values to pass to main thread
            bool loginSuccess = success;
            string loginMessage = message;
            string loginUsername = username;
            string loginEmail = email;
            bool shouldRemember = rememberMe;
            
            // Execute on main thread
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                try
                {
                    Debug.Log($"AccountManager: LoginWithEmail callback - success={loginSuccess}, message={loginMessage}, username={loginUsername}");
                    if (loginSuccess)
                    {
                        currentUsername = loginUsername;
                        isLoggedIn = true;
                        
                        if (shouldRemember)
                        {
                            SaveLoginCredentials(loginEmail);
                        }
                        
                        Debug.Log($"Logged in successfully: {loginUsername}");
                    }
                    callback?.Invoke(loginSuccess, loginMessage);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"AccountManager: Exception in login callback: {ex.Message}\n{ex.StackTrace}");
                }
            });
        });
#else
        // Simulate login without Firebase
        string savedEmail = PlayerPrefs.GetString("AccountEmail", "");
        
        if (savedEmail == email)
        {
            accountId = PlayerPrefs.GetString("AccountId", System.Guid.NewGuid().ToString());
            currentUsername = PlayerPrefs.GetString("AccountUsername", "Player");
            isLoggedIn = true;
            
            if (rememberMe)
            {
                SaveLoginCredentials(email);
            }
            
            Debug.Log($"Logged in (local): {currentUsername}");
            callback?.Invoke(true, "Logged in successfully");
        }
        else
        {
            callback?.Invoke(false, "Invalid email or password");
        }
#endif
    }
    
    /// <summary>
    /// Try to auto-login with saved credentials
    /// </summary>
    private void TryAutoLogin()
    {
        if (PlayerPrefs.GetInt("RememberMe", 0) == 0)
            return;
        
        string savedEmail = PlayerPrefs.GetString("SavedEmail", "");
        
        if (!string.IsNullOrEmpty(savedEmail))
        {
#if FIREBASE_INSTALLED
            // Try to restore Firebase session
            FirebaseManager.Instance.TryRestoreSession((success, username) => {
                if (success)
                {
                    currentUsername = username;
                    isLoggedIn = true;
                    Debug.Log($"Auto-login successful: {username}");
                }
            });
#else
            // Load from local storage
            accountId = PlayerPrefs.GetString("AccountId", "");
            currentUsername = PlayerPrefs.GetString("AccountUsername", "");
            
            if (!string.IsNullOrEmpty(accountId))
            {
                isLoggedIn = true;
                Debug.Log($"Auto-login (local): {currentUsername}");
            }
#endif
        }
    }
    
    /// <summary>
    /// Called when Firebase restores a previous session
    /// </summary>
    public void OnFirebaseSessionRestored(string username)
    {
        currentUsername = username;
        isLoggedIn = true;
        Debug.Log($"AccountManager: Session restored for {username}");
    }
    
    /// <summary>
    /// Logout current account
    /// </summary>
    public void Logout()
    {
        isLoggedIn = false;
        currentUsername = "";
        accountId = "";
        
        if (!rememberMe)
        {
            PlayerPrefs.DeleteKey("SavedEmail");
            PlayerPrefs.DeleteKey("RememberMe");
        }
        
#if FIREBASE_INSTALLED
        FirebaseManager.Instance.SignOut();
#endif
        
        Debug.Log("Logged out");
    }
    
    /// <summary>
    /// Change username
    /// </summary>
    public void ChangeUsername(string newUsername, Action<bool, string> callback)
    {
        if (!isLoggedIn)
        {
            callback?.Invoke(false, "Not logged in");
            return;
        }
        
        if (string.IsNullOrEmpty(newUsername) || newUsername.Length < 3)
        {
            callback?.Invoke(false, "Username must be at least 3 characters");
            return;
        }
        
#if FIREBASE_INSTALLED
        FirebaseManager.Instance.UpdateUsername(newUsername, (success, message) => {
            if (success)
            {
                currentUsername = newUsername;
                Debug.Log($"Username changed to: {newUsername}");
            }
            callback?.Invoke(success, message);
        });
#else
        currentUsername = newUsername;
        PlayerPrefs.SetString("AccountUsername", newUsername);
        PlayerPrefs.Save();
        
        Debug.Log($"Username changed (local): {newUsername}");
        callback?.Invoke(true, "Username updated");
#endif
    }
    
    /// <summary>
    /// Change password
    /// </summary>
    public void ChangePassword(string currentPassword, string newPassword, Action<bool, string> callback)
    {
        if (!isLoggedIn)
        {
            callback?.Invoke(false, "Not logged in");
            return;
        }
        
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
        {
            callback?.Invoke(false, "New password must be at least 6 characters");
            return;
        }
        
#if FIREBASE_INSTALLED
        FirebaseManager.Instance.ChangePassword(currentPassword, newPassword, callback);
#else
        Debug.Log("Password changed (local - not secure)");
        callback?.Invoke(true, "Password updated");
#endif
    }
    
    /// <summary>
    /// Delete account
    /// </summary>
    public void DeleteAccount(Action<bool, string> callback)
    {
        if (!isLoggedIn)
        {
            callback?.Invoke(false, "Not logged in");
            return;
        }
        
#if FIREBASE_INSTALLED
        FirebaseManager.Instance.DeleteAccount((success, message) => {
            if (success)
            {
                Logout();
                PlayerPrefs.DeleteAll();
                Debug.Log("Account deleted");
            }
            callback?.Invoke(success, message);
        });
#else
        PlayerPrefs.DeleteAll();
        Logout();
        Debug.Log("Account deleted (local)");
        callback?.Invoke(true, "Account deleted");
#endif
    }
    
    /// <summary>
    /// Save login credentials for auto-login
    /// </summary>
    private void SaveLoginCredentials(string email)
    {
        PlayerPrefs.SetString("SavedEmail", email);
        PlayerPrefs.SetInt("RememberMe", 1);
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// Get display name for UI
    /// </summary>
    public string GetDisplayName()
    {
        return isLoggedIn ? currentUsername : "Guest";
    }
}
