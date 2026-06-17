using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Simple main menu UI with play, account, and quit options
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Main Menu Buttons")]
    public Button playButton;
    public Button accountButton;
    public Button controlsButton;
    public Button quitButton;
    
    [Header("Controls Panel")]
    public ControlsUI controlsUI;
    
    [Header("Scene Names")]
    public string lobbySceneName = "Lobby";
    public string accountSceneName = "AccountScene";
    
    private void Start()
    {
        // Disable all buttons until Firebase is ready
        SetButtonsInteractable(false);
        
        // Setup button listeners
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
        
        if (accountButton != null)
            accountButton.onClick.AddListener(OnAccountClicked);
        
        if (controlsButton != null)
            controlsButton.onClick.AddListener(OnControlsClicked);
        
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
        
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Wait for Firebase to initialize before checking login
        StartCoroutine(WaitForFirebaseAndCheckLogin());
    }
    
    private System.Collections.IEnumerator WaitForFirebaseAndCheckLogin()
    {
        // Wait for Firebase to initialize
        while (FirebaseManager.Instance == null || !FirebaseManager.Instance.isInitialized)
        {
            yield return null;
        }
        
        // Give Firebase a moment to restore session
        yield return new WaitForSeconds(0.5f);
        
        // Enable buttons now that Firebase is ready
        SetButtonsInteractable(true);
        
        // Now check login status
        CheckLoginStatus();
    }
    
    /// <summary>
    /// Enable or disable all menu buttons
    /// </summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (playButton != null)
            playButton.interactable = interactable;
        
        if (accountButton != null)
            accountButton.interactable = interactable;
        
        if (controlsButton != null)
            controlsButton.interactable = interactable;
        
        if (quitButton != null)
            quitButton.interactable = interactable;
        
        Debug.Log($"Main menu buttons {(interactable ? "enabled" : "disabled")}");
    }
    
    /// <summary>
    /// Check if player is logged in, redirect to account scene if not
    /// </summary>
    private void CheckLoginStatus()
    {
        if (AccountManager.Instance == null || !AccountManager.Instance.isLoggedIn)
        {
            Debug.Log("Player not logged in - redirecting to account scene");
            SceneManager.LoadScene(accountSceneName);
        }
    }
    
    /// <summary>
    /// Load lobby scene (only if logged in)
    /// </summary>
    public void OnPlayClicked()
    {
        // Double-check login status
        if (AccountManager.Instance == null || !AccountManager.Instance.isLoggedIn)
        {
            Debug.LogWarning("Cannot access lobby - not logged in");
            SceneManager.LoadScene(accountSceneName);
            return;
        }
        
        SceneManager.LoadScene(lobbySceneName);
    }
    
    /// <summary>
    /// Load account scene
    /// </summary>
    public void OnAccountClicked()
    {
        SceneManager.LoadScene(accountSceneName);
    }
    
    /// <summary>
    /// Show controls panel
    /// </summary>
    public void OnControlsClicked()
    {
        if (controlsUI != null)
            controlsUI.Show();
    }
    
    /// <summary>
    /// Quit game
    /// </summary>
    public void OnQuitClicked()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
