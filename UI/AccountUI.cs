using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// UI for account management (login, register, settings)
/// Simple and clean interface for player authentication
/// </summary>
public class AccountUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject registerPanel;
    public GameObject accountSettingsPanel;
    
    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu";
    
    // State flags for thread-safe callbacks
    private bool loginSuccessful = false;
    private bool loginFailed = false;
    private bool registerSuccessful = false;
    private bool registerFailed = false;
    private string loginMessage = "";
    private string registerMessage = "";
    
    [Header("Login UI")]
    public TMP_InputField loginEmail;
    public TMP_InputField loginPassword;
    public Toggle rememberMeToggle;
    public Button loginButton;
    public Button showRegisterButton;
    public TextMeshProUGUI loginStatusText;
    
    [Header("Register UI")]
    public TMP_InputField registerEmail;
    public TMP_InputField registerPassword;
    public TMP_InputField registerConfirmPassword;
    public TMP_InputField registerUsername;
    public Button registerButton;
    public Button backToLoginButton;
    public TextMeshProUGUI registerStatusText;
    
    [Header("Account Settings UI")]
    public TextMeshProUGUI accountNameText;
    public TMP_InputField newUsernameInput;
    public Button changeUsernameButton;
    public TMP_InputField currentPasswordInput;
    public TMP_InputField newPasswordInput;
    public TMP_InputField confirmNewPasswordInput;
    public Button changePasswordButton;
    public Button backToMenuButton;
    public Button logoutButton;
    public Button deleteAccountButton;
    public TextMeshProUGUI settingsStatusText;
    
    private void Start()
    {
        // Setup button listeners
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);
        
        if (showRegisterButton != null)
            showRegisterButton.onClick.AddListener(ShowRegisterPanel);
        
        if (registerButton != null)
            registerButton.onClick.AddListener(OnRegisterClicked);
        
        if (backToLoginButton != null)
            backToLoginButton.onClick.AddListener(ShowLoginPanel);
        
        if (changeUsernameButton != null)
            changeUsernameButton.onClick.AddListener(OnChangeUsernameClicked);
        
        if (changePasswordButton != null)
            changePasswordButton.onClick.AddListener(OnChangePasswordClicked);
        
        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);
        
        if (logoutButton != null)
            logoutButton.onClick.AddListener(OnLogoutClicked);
        
        if (deleteAccountButton != null)
            deleteAccountButton.onClick.AddListener(OnDeleteAccountClicked);
        
        // Check if already logged in
        if (AccountManager.Instance != null && AccountManager.Instance.isLoggedIn)
        {
            ShowAccountSettings();
        }
        else
        {
            ShowLoginPanel();
        }
        
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    private void Update()
    {
        // Handle login callbacks on main thread
        if (loginSuccessful)
        {
            loginSuccessful = false;
            Debug.Log("Update: Processing loginSuccessful");
            ShowStatus(loginStatusText, loginMessage, Color.green);
            Invoke(nameof(GoToMainMenu), 0.5f);
        }
        
        if (loginFailed)
        {
            loginFailed = false;
            ShowStatus(loginStatusText, loginMessage, Color.red);
        }
        
        // Handle register callbacks on main thread
        if (registerSuccessful)
        {
            registerSuccessful = false;
            Debug.Log("Update: Processing registerSuccessful");
            ShowStatus(registerStatusText, registerMessage, Color.green);
            Invoke(nameof(GoToMainMenu), 1f);
        }
        
        if (registerFailed)
        {
            registerFailed = false;
            ShowStatus(registerStatusText, registerMessage, Color.red);
        }
    }
    
    private void ShowLoginPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);
        if (accountSettingsPanel != null) accountSettingsPanel.SetActive(false);
        
        ClearStatusTexts();
    }
    
    private void ShowRegisterPanel()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(true);
        if (accountSettingsPanel != null) accountSettingsPanel.SetActive(false);
        
        ClearStatusTexts();
    }
    
    private void ShowAccountSettings()
    {
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registerPanel != null) registerPanel.SetActive(false);
        if (accountSettingsPanel != null) accountSettingsPanel.SetActive(true);
        
        // Update account name display
        if (accountNameText != null && AccountManager.Instance != null)
        {
            accountNameText.text = $"Account: {AccountManager.Instance.currentUsername}";
        }
        
        ClearStatusTexts();
    }
    
    /// <summary>
    /// Navigate to main menu scene
    /// </summary>
    private void GoToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    private void OnLoginClicked()
    {
        if (AccountManager.Instance == null)
        {
            ShowStatus(loginStatusText, "Account system not available", Color.red);
            return;
        }
        
        string email = loginEmail != null ? loginEmail.text : "";
        string password = loginPassword != null ? loginPassword.text : "";
        
        // Security validation
        if (SecurityManager.Instance != null)
        {
            // Rate limiting
            if (!SecurityManager.Instance.CanAttemptLogin(email))
            {
                ShowStatus(loginStatusText, "Too many login attempts. Please wait a moment.", Color.red);
                return;
            }
            
            // Input validation
            var emailValidation = SecurityManager.Instance.ValidateEmail(email);
            if (!emailValidation.isValid)
            {
                SecurityManager.Instance.LogValidationFailure("email", emailValidation.errorMessage, email);
                ShowStatus(loginStatusText, emailValidation.errorMessage, Color.red);
                return;
            }
            
            var passwordValidation = SecurityManager.Instance.ValidatePassword(password);
            if (!passwordValidation.isValid)
            {
                SecurityManager.Instance.LogValidationFailure("password", passwordValidation.errorMessage, password);
                ShowStatus(loginStatusText, passwordValidation.errorMessage, Color.red);
                return;
            }
            
            // Sanitize inputs
            email = SecurityManager.Instance.SanitizeInput(email);
        }
        
        if (rememberMeToggle != null)
        {
            AccountManager.Instance.rememberMe = rememberMeToggle.isOn;
        }
        
        ShowStatus(loginStatusText, "Logging in...", Color.yellow);
        
        AccountManager.Instance.Login(email, password, (success, message) => {
            Debug.Log($"Login callback: success={success}, message={message}");
            if (success)
            {
                loginSuccessful = true;
                loginMessage = "Login successful!";
                Debug.Log("Set loginSuccessful = true");
            }
            else
            {
                loginFailed = true;
                loginMessage = message;
            }
        });
    }
    
    private void OnRegisterClicked()
    {
        if (AccountManager.Instance == null)
        {
            ShowStatus(registerStatusText, "Account system not available", Color.red);
            return;
        }
        
        string email = registerEmail != null ? registerEmail.text : "";
        string password = registerPassword != null ? registerPassword.text : "";
        string confirmPassword = registerConfirmPassword != null ? registerConfirmPassword.text : "";
        string username = registerUsername != null ? registerUsername.text : "";
        
        // Security validation
        if (SecurityManager.Instance != null)
        {
            // Rate limiting
            if (!SecurityManager.Instance.CanAttemptRegister(email))
            {
                ShowStatus(registerStatusText, "Too many registration attempts. Please wait a moment.", Color.red);
                return;
            }
            
            // Input validation
            var emailValidation = SecurityManager.Instance.ValidateEmail(email);
            if (!emailValidation.isValid)
            {
                SecurityManager.Instance.LogValidationFailure("email", emailValidation.errorMessage, email);
                ShowStatus(registerStatusText, emailValidation.errorMessage, Color.red);
                return;
            }
            
            var usernameValidation = SecurityManager.Instance.ValidateUsername(username);
            if (!usernameValidation.isValid)
            {
                SecurityManager.Instance.LogValidationFailure("username", usernameValidation.errorMessage, username);
                ShowStatus(registerStatusText, usernameValidation.errorMessage, Color.red);
                return;
            }
            
            var passwordValidation = SecurityManager.Instance.ValidatePassword(password);
            if (!passwordValidation.isValid)
            {
                SecurityManager.Instance.LogValidationFailure("password", passwordValidation.errorMessage, password);
                ShowStatus(registerStatusText, passwordValidation.errorMessage, Color.red);
                return;
            }
            
            // Sanitize inputs
            email = SecurityManager.Instance.SanitizeInput(email);
            username = SecurityManager.Instance.SanitizeInput(username);
        }
        
        // Validate passwords match
        if (password != confirmPassword)
        {
            ShowStatus(registerStatusText, "Passwords do not match", Color.red);
            return;
        }
        
        ShowStatus(registerStatusText, "Creating account...", Color.yellow);
        
        AccountManager.Instance.RegisterAccount(email, password, username, (success, message) => {
            Debug.Log($"Register callback: success={success}, message={message}");
            if (success)
            {
                registerSuccessful = true;
                registerMessage = "Account created! Redirecting...";
                Debug.Log("Set registerSuccessful = true");
            }
            else
            {
                registerFailed = true;
                registerMessage = message;
            }
        });
    }
    
    private void OnChangeUsernameClicked()
    {
        if (AccountManager.Instance == null)
        {
            ShowStatus(settingsStatusText, "Account system not available", Color.red);
            return;
        }
        
        string newUsername = newUsernameInput != null ? newUsernameInput.text : "";
        
        ShowStatus(settingsStatusText, "Updating username...", Color.yellow);
        
        AccountManager.Instance.ChangeUsername(newUsername, (success, message) => {
            if (success)
            {
                ShowStatus(settingsStatusText, "Username updated!", Color.green);
                if (accountNameText != null)
                {
                    accountNameText.text = $"Account: {AccountManager.Instance.currentUsername}";
                }
                if (newUsernameInput != null)
                {
                    newUsernameInput.text = "";
                }
            }
            else
            {
                ShowStatus(settingsStatusText, message, Color.red);
            }
        });
    }
    
    private void OnChangePasswordClicked()
    {
        if (AccountManager.Instance == null)
        {
            ShowStatus(settingsStatusText, "Account system not available", Color.red);
            return;
        }
        
        string currentPassword = currentPasswordInput != null ? currentPasswordInput.text : "";
        string newPassword = newPasswordInput != null ? newPasswordInput.text : "";
        string confirmNewPassword = confirmNewPasswordInput != null ? confirmNewPasswordInput.text : "";
        
        // Validate passwords match
        if (newPassword != confirmNewPassword)
        {
            ShowStatus(settingsStatusText, "New passwords do not match", Color.red);
            return;
        }
        
        ShowStatus(settingsStatusText, "Changing password...", Color.yellow);
        
        AccountManager.Instance.ChangePassword(currentPassword, newPassword, (success, message) => {
            if (success)
            {
                ShowStatus(settingsStatusText, "Password changed!", Color.green);
                // Clear password fields
                if (currentPasswordInput != null) currentPasswordInput.text = "";
                if (newPasswordInput != null) newPasswordInput.text = "";
                if (confirmNewPasswordInput != null) confirmNewPasswordInput.text = "";
            }
            else
            {
                ShowStatus(settingsStatusText, message, Color.red);
            }
        });
    }
    
    private void OnBackToMenuClicked()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    private void OnLogoutClicked()
    {
        if (AccountManager.Instance != null)
        {
            AccountManager.Instance.Logout();
        }
        
        ShowLoginPanel();
    }
    
    private void OnDeleteAccountClicked()
    {
        if (AccountManager.Instance == null)
        {
            ShowStatus(settingsStatusText, "Account system not available", Color.red);
            return;
        }
        
        // Show confirmation (in a real app, use a proper dialog)
        ShowStatus(settingsStatusText, "Are you sure? Click again to confirm.", Color.yellow);
        
        // Simple double-click confirmation
        if (deleteAccountButton != null)
        {
            deleteAccountButton.onClick.RemoveAllListeners();
            deleteAccountButton.onClick.AddListener(() => {
                AccountManager.Instance.DeleteAccount((success, message) => {
                    if (success)
                    {
                        ShowStatus(settingsStatusText, "Account deleted", Color.green);
                        Invoke(nameof(ShowLoginPanel), 1f);
                    }
                    else
                    {
                        ShowStatus(settingsStatusText, message, Color.red);
                    }
                });
            });
        }
    }
    
    private void ShowStatus(TextMeshProUGUI statusText, string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }
    
    /// <summary>
    /// Public method to show error messages from external scripts
    /// </summary>
    public void ShowError(string message)
    {
        ShowStatus(loginStatusText, message, Color.red);
    }
    
    private void ClearStatusTexts()
    {
        if (loginStatusText != null) loginStatusText.text = "";
        if (registerStatusText != null) registerStatusText.text = "";
        if (settingsStatusText != null) settingsStatusText.text = "";
    }
}
