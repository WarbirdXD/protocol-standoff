using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Simple lobby UI with 1v1 and 2v2 matchmaking buttons
/// Teams are automatically assigned by matchmaking system
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("Mode Selection")]
    public Button oneVsOneButton;
    public Button twoVsTwoButton;
    public Button backButton;
    public Button accountSettingsButton;
    
    [Header("Player Info")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerRankText;
    public TextMeshProUGUI playerMMRText;
    public TextMeshProUGUI winLossText;
    
    [Header("Queue Status")]
    public TextMeshProUGUI titleText;
    
    [Header("Online Status")]
    public TextMeshProUGUI onlinePlayersText;
    
    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu";
    public string accountSceneName = "AccountScene";
    
    private MatchmakingManager.MatchMode selectedMode = MatchmakingManager.MatchMode.OneVsOne;
    private bool isSearching = false;
    private MatchmakingManager.MatchMode searchingMode;
    private string oneVsOneOriginalText = "1v1";
    private string twoVsTwoOriginalText = "2v2";
    
    private void Start()
    {
        // Setup button listeners
        if (oneVsOneButton != null)
        {
            // Store original button text
            TextMeshProUGUI buttonText = oneVsOneButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                oneVsOneOriginalText = buttonText.text;
            }
            
            oneVsOneButton.onClick.AddListener(() => JoinMatchmaking(MatchmakingManager.MatchMode.OneVsOne));
            
            // Add hover listeners to show mode-specific stats
            var trigger = oneVsOneButton.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
            entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { OnModeHover(MatchmakingManager.MatchMode.OneVsOne); });
            trigger.triggers.Add(entry);
        }
        
        if (twoVsTwoButton != null)
        {
            // Store original button text
            TextMeshProUGUI buttonText = twoVsTwoButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                twoVsTwoOriginalText = buttonText.text;
            }
            
            twoVsTwoButton.onClick.AddListener(() => JoinMatchmaking(MatchmakingManager.MatchMode.TwoVsTwo));
            
            // Add hover listeners to show mode-specific stats
            var trigger = twoVsTwoButton.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var entry = new UnityEngine.EventSystems.EventTrigger.Entry();
            entry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { OnModeHover(MatchmakingManager.MatchMode.TwoVsTwo); });
            trigger.triggers.Add(entry);
        }
        
        if (backButton != null)
            backButton.onClick.AddListener(BackToMainMenu);
        
        if (accountSettingsButton != null)
            accountSettingsButton.onClick.AddListener(OpenAccountSettings);
        
        // Update player info display
        UpdatePlayerInfo();
        
        // Setup online players tracking
        SetupOnlinePlayersDisplay();
        
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Subscribe to matchmaking events
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.OnQueueJoined.AddListener(OnQueueJoined);
            MatchmakingManager.Instance.OnQueueLeft.AddListener(OnQueueLeft);
            MatchmakingManager.Instance.OnMatchFound.AddListener(OnMatchFound);
        }
    }
    
    private void Update()
    {
        // Update queue timer if searching
        if (isSearching && titleText != null && MatchmakingManager.Instance != null)
        {
            int seconds = Mathf.FloorToInt(MatchmakingManager.Instance.queueTime);
            string modeText = searchingMode == MatchmakingManager.MatchMode.OneVsOne ? "1v1" : "2v2";
            titleText.text = $"Searching for {modeText} match... {seconds}s";
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from online players events
        if (OnlinePlayersTracker.Instance != null)
        {
            OnlinePlayersTracker.Instance.OnPlayerCountChanged -= UpdateOnlinePlayersDisplay;
        }
        
        // Unsubscribe from matchmaking events
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.OnQueueJoined.RemoveListener(OnQueueJoined);
            MatchmakingManager.Instance.OnQueueLeft.RemoveListener(OnQueueLeft);
            MatchmakingManager.Instance.OnMatchFound.RemoveListener(OnMatchFound);
        }
    }
    
    /// <summary>
    /// Join matchmaking queue for selected mode
    /// Teams are automatically assigned by matchmaking system
    /// </summary>
    private void JoinMatchmaking(MatchmakingManager.MatchMode mode)
    {
        if (MatchmakingManager.Instance == null)
        {
            Debug.LogError("MatchmakingManager not found!");
            return;
        }
        
        // If already searching, cancel the search
        if (isSearching)
        {
            CancelSearch();
            return;
        }
        
        // Join queue - matchmaking will automatically assign teams
        searchingMode = mode;
        MatchmakingManager.Instance.JoinQueue(mode);
        
        Debug.Log($"Joining {mode} matchmaking queue...");
    }
    
    /// <summary>
    /// Cancel matchmaking search
    /// </summary>
    private void CancelSearch()
    {
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.LeaveQueue();
        }
    }
    
    /// <summary>
    /// Called when queue is joined
    /// </summary>
    private void OnQueueJoined()
    {
        isSearching = true;
        
        // Update button states
        UpdateButtonStates();
        
        // Update title text
        if (titleText != null)
        {
            string modeText = searchingMode == MatchmakingManager.MatchMode.OneVsOne ? "1v1" : "2v2";
            titleText.text = $"Searching for {modeText} match...";
        }
    }
    
    /// <summary>
    /// Called when queue is left
    /// </summary>
    private void OnQueueLeft()
    {
        isSearching = false;
        
        // Update button states
        UpdateButtonStates();
        
        // Reset title text
        if (titleText != null)
        {
            titleText.text = "Select Game Mode";
        }
    }
    
    /// <summary>
    /// Called when match is found
    /// </summary>
    private void OnMatchFound(MatchData matchData)
    {
        isSearching = false;
        
        // Update title text
        if (titleText != null)
        {
            titleText.text = "Match Found! Loading...";
        }
    }
    
    /// <summary>
    /// Update button states based on search status
    /// </summary>
    private void UpdateButtonStates()
    {
        if (oneVsOneButton != null)
        {
            TextMeshProUGUI buttonText = oneVsOneButton.GetComponentInChildren<TextMeshProUGUI>();
            
            if (isSearching && searchingMode == MatchmakingManager.MatchMode.OneVsOne)
            {
                // This button is searching - show "Searching..." and make it a cancel button
                if (buttonText != null)
                {
                    buttonText.text = "Cancel";
                }
                oneVsOneButton.interactable = true;
            }
            else if (isSearching)
            {
                // Other mode is searching - disable this button
                if (buttonText != null)
                {
                    buttonText.text = oneVsOneOriginalText;
                }
                oneVsOneButton.interactable = false;
            }
            else
            {
                // Not searching - restore original state
                if (buttonText != null)
                {
                    buttonText.text = oneVsOneOriginalText;
                }
                oneVsOneButton.interactable = true;
            }
        }
        
        if (twoVsTwoButton != null)
        {
            TextMeshProUGUI buttonText = twoVsTwoButton.GetComponentInChildren<TextMeshProUGUI>();
            
            if (isSearching && searchingMode == MatchmakingManager.MatchMode.TwoVsTwo)
            {
                // This button is searching - show "Searching..." and make it a cancel button
                if (buttonText != null)
                {
                    buttonText.text = "Cancel";
                }
                twoVsTwoButton.interactable = true;
            }
            else if (isSearching)
            {
                // Other mode is searching - disable this button
                if (buttonText != null)
                {
                    buttonText.text = twoVsTwoOriginalText;
                }
                twoVsTwoButton.interactable = false;
            }
            else
            {
                // Not searching - restore original state
                if (buttonText != null)
                {
                    buttonText.text = twoVsTwoOriginalText;
                }
                twoVsTwoButton.interactable = true;
            }
        }
    }
    
    /// <summary>
    /// Called when hovering over a mode button
    /// </summary>
    private void OnModeHover(MatchmakingManager.MatchMode mode)
    {
        selectedMode = mode;
        UpdatePlayerInfo();
    }
    
    /// <summary>
    /// Update player info display for the selected mode
    /// </summary>
    private void UpdatePlayerInfo()
    {
        if (RankingSystem.Instance == null) return;
        
        // Set the current mode in RankingSystem to update display stats
        RankingSystem.Instance.SetCurrentMode(selectedMode);
        
        // Display player name
        if (playerNameText != null && FirebaseManager.Instance != null)
        {
            playerNameText.text = FirebaseManager.Instance.playerName;
        }
        
        // Display mode-specific rank
        if (playerRankText != null)
        {
            string modeText = selectedMode == MatchmakingManager.MatchMode.OneVsOne ? "1v1" : "2v2";
            playerRankText.text = $"{modeText} Rank: {RankingSystem.Instance.GetRankDisplayName()}";
        }
        
        // Display mode-specific MMR
        if (playerMMRText != null)
        {
            int mmr = RankingSystem.Instance.GetMMRForMode(selectedMode);
            playerMMRText.text = $"MMR: {mmr}";
        }
        
        // Display mode-specific W/L stats
        if (winLossText != null)
        {
            int wins = RankingSystem.Instance.wins;
            int losses = RankingSystem.Instance.losses;
            int matches = RankingSystem.Instance.matchesPlayed;
            float winRate = matches > 0 ? (float)wins / matches * 100f : 0f;
            winLossText.text = $"W/L: {wins}/{losses} ({winRate:F1}%)";
        }
    }
    
    /// <summary>
    /// Setup online players display
    /// </summary>
    private void SetupOnlinePlayersDisplay()
    {
        // Ensure OnlinePlayersTracker exists
        if (OnlinePlayersTracker.Instance == null)
        {
            GameObject tracker = new GameObject("OnlinePlayersTracker");
            tracker.AddComponent<OnlinePlayersTracker>();
        }
        
        // Subscribe to player count changes
        if (OnlinePlayersTracker.Instance != null)
        {
            OnlinePlayersTracker.Instance.OnPlayerCountChanged += UpdateOnlinePlayersDisplay;
            
            // Update immediately with current count
            UpdateOnlinePlayersDisplay(OnlinePlayersTracker.Instance.onlinePlayersCount);
        }
    }
    
    /// <summary>
    /// Update online players display
    /// </summary>
    private void UpdateOnlinePlayersDisplay(int count)
    {
        if (onlinePlayersText != null)
        {
            onlinePlayersText.text = $"Players Online: {count}";
        }
    }
    
    /// <summary>
    /// Open account settings
    /// </summary>
    private void OpenAccountSettings()
    {
        SceneManager.LoadScene(accountSceneName);
    }
    
    /// <summary>
    /// Return to main menu
    /// </summary>
    private void BackToMainMenu()
    {
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
