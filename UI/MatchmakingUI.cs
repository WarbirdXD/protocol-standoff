using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// UI for matchmaking queue and mode selection
/// Shows queue status, estimated time, and match found screen
/// </summary>
public class MatchmakingUI : MonoBehaviour
{
    [Header("Mode Selection")]
    public GameObject modeSelectionPanel;
    public Button oneVsOneButton;
    public Button twoVsTwoButton;
    public Button backButton;
    
    [Header("Queue Panel")]
    public GameObject queuePanel;
    public TextMeshProUGUI queueStatusText;
    public TextMeshProUGUI queueTimeText;
    public TextMeshProUGUI estimatedTimeText;
    public TextMeshProUGUI rankingText;
    public Button cancelQueueButton;
    public Image queueSpinner; // Rotating loading icon
    
    [Header("Match Found Panel")]
    public GameObject matchFoundPanel;
    public TextMeshProUGUI matchModeText;
    public TextMeshProUGUI team1Text;
    public TextMeshProUGUI team2Text;
    public TextMeshProUGUI team1RankingText;
    public TextMeshProUGUI team2RankingText;
    public Button acceptButton;
    public float acceptTimeout = 10f;
    
    [Header("Player Info")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerRankingText;
    public TextMeshProUGUI playerTierText;
    public TextMeshProUGUI winLossText;
    
    [Header("Scene Names")]
    public string gameSceneName = "Game";
    public string mainMenuSceneName = "MainMenu";
    
    private MatchData currentMatch;
    private float acceptTimeRemaining;
    
    private void Start()
    {
        // Setup button listeners
        if (oneVsOneButton != null)
            oneVsOneButton.onClick.AddListener(() => JoinQueue(MatchmakingManager.MatchMode.OneVsOne));
        
        if (twoVsTwoButton != null)
            twoVsTwoButton.onClick.AddListener(() => JoinQueue(MatchmakingManager.MatchMode.TwoVsTwo));
        
        if (cancelQueueButton != null)
            cancelQueueButton.onClick.AddListener(CancelQueue);
        
        if (acceptButton != null)
            acceptButton.onClick.AddListener(AcceptMatch);
        
        if (backButton != null)
            backButton.onClick.AddListener(BackToMainMenu);
        
        // Subscribe to matchmaking events
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.OnQueueJoined.AddListener(OnQueueJoined);
            MatchmakingManager.Instance.OnQueueLeft.AddListener(OnQueueLeft);
            MatchmakingManager.Instance.OnMatchFound.AddListener(OnMatchFound);
            MatchmakingManager.Instance.OnMatchmakingFailed.AddListener(OnMatchmakingFailed);
        }
        
        // Show mode selection by default
        ShowModeSelection();
        
        // Update player info
        UpdatePlayerInfo();
        
        // Unlock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    private void Update()
    {
        // Update queue time display
        if (MatchmakingManager.Instance != null && MatchmakingManager.Instance.isInQueue)
        {
            UpdateQueueDisplay();
            
            // Rotate spinner
            if (queueSpinner != null)
            {
                queueSpinner.transform.Rotate(0, 0, -180f * Time.deltaTime);
            }
        }
        
        // Update accept timeout
        if (matchFoundPanel != null && matchFoundPanel.activeSelf)
        {
            acceptTimeRemaining -= Time.deltaTime;
            if (acceptTimeRemaining <= 0)
            {
                // Auto-decline if timeout
                DeclineMatch();
            }
        }
    }
    
    private void ShowModeSelection()
    {
        if (modeSelectionPanel != null) modeSelectionPanel.SetActive(true);
        if (queuePanel != null) queuePanel.SetActive(false);
        if (matchFoundPanel != null) matchFoundPanel.SetActive(false);
    }
    
    private void JoinQueue(MatchmakingManager.MatchMode mode)
    {
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.JoinQueue(mode);
        }
    }
    
    private void CancelQueue()
    {
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.LeaveQueue();
        }
    }
    
    private void OnQueueJoined()
    {
        // Show queue panel
        if (modeSelectionPanel != null) modeSelectionPanel.SetActive(false);
        if (queuePanel != null) queuePanel.SetActive(true);
        
        // Update queue info
        if (queueStatusText != null)
        {
            string mode = MatchmakingManager.Instance.currentMode == MatchmakingManager.MatchMode.OneVsOne ? "1v1" : "2v2";
            queueStatusText.text = $"Searching for {mode} match...";
        }
        
        if (estimatedTimeText != null)
        {
            estimatedTimeText.text = $"Estimated: {MatchmakingManager.Instance.GetEstimatedQueueTime()}";
        }
        
        if (rankingText != null)
        {
            int playerMMR = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetPlayerMMR() : (RankingSystem.Instance != null ? RankingSystem.Instance.currentMMR : 0);
            rankingText.text = $"Your Ranking: {playerMMR}";
        }
    }
    
    private void OnQueueLeft()
    {
        ShowModeSelection();
    }
    
    private void UpdateQueueDisplay()
    {
        if (queueTimeText != null)
        {
            int seconds = Mathf.FloorToInt(MatchmakingManager.Instance.queueTime);
            queueTimeText.text = $"Queue Time: {seconds}s";
        }
    }
    
    private void OnMatchFound(MatchData matchData)
    {
        currentMatch = matchData;
        acceptTimeRemaining = acceptTimeout;
        
        // Hide queue panel
        if (queuePanel != null) queuePanel.SetActive(false);
        
        // Show match found panel
        if (matchFoundPanel != null) matchFoundPanel.SetActive(true);
        
        // Display match info
        if (matchModeText != null)
        {
            string mode = matchData.mode == MatchmakingManager.MatchMode.OneVsOne ? "1v1" : "2v2";
            matchModeText.text = $"{mode} MATCH FOUND!";
        }
        
        if (team1Text != null)
        {
            team1Text.text = "YOUR TEAM:\n" + string.Join("\n", matchData.team1Players.ConvertAll(p => $"{p.playerName} ({p.ranking})"));
        }
        
        if (team2Text != null)
        {
            team2Text.text = "OPPONENT TEAM:\n" + string.Join("\n", matchData.team2Players.ConvertAll(p => $"{p.playerName} ({p.ranking})"));
        }
        
        if (team1RankingText != null)
        {
            team1RankingText.text = $"Avg: {matchData.GetAverageRanking(1)}";
        }
        
        if (team2RankingText != null)
        {
            team2RankingText.text = $"Avg: {matchData.GetAverageRanking(2)}";
        }
    }
    
    private void AcceptMatch()
    {
        if (currentMatch == null) return;
        
        Debug.Log($"Match accepted: {currentMatch.matchId}");
        
        // TODO: Send accept to Firebase
        // In real implementation, wait for all players to accept
        
        // Load game scene
        SceneManager.LoadScene(gameSceneName);
    }
    
    private void DeclineMatch()
    {
        Debug.Log("Match declined or timeout");
        currentMatch = null;
        ShowModeSelection();
        
        // TODO: Send decline to Firebase
    }
    
    private void OnMatchmakingFailed()
    {
        Debug.LogWarning("Matchmaking failed - no players found");
        ShowModeSelection();
        
        // TODO: Show error message to player
    }
    
    private void UpdatePlayerInfo()
    {
        if (FirebaseManager.Instance == null) return;
        
        if (playerNameText != null)
        {
            playerNameText.text = FirebaseManager.Instance.playerName;
        }
        
        if (playerRankingText != null)
        {
            int playerMMR = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetPlayerMMR() : (RankingSystem.Instance != null ? RankingSystem.Instance.currentMMR : 0);
            playerRankingText.text = $"Ranking: {playerMMR}";
        }
        
        if (playerTierText != null)
        {
            string rankName = FirebaseManager.Instance != null ? FirebaseManager.Instance.GetPlayerRank() : (RankingSystem.Instance != null ? RankingSystem.Instance.GetRankDisplayName() : "Unranked");
            playerTierText.text = rankName;
        }
        
        if (winLossText != null)
        {
            int wins = RankingSystem.Instance != null ? RankingSystem.Instance.wins : 0;
            int losses = RankingSystem.Instance != null ? RankingSystem.Instance.losses : 0;
            int total = wins + losses;
            float winRate = total > 0 ? (wins / (float)total) * 100f : 0f;
            winLossText.text = $"W/L: {wins}/{losses} ({winRate:F1}%)";
        }
    }
    
    private void BackToMainMenu()
    {
        // Cancel queue if in one
        if (MatchmakingManager.Instance != null && MatchmakingManager.Instance.isInQueue)
        {
            MatchmakingManager.Instance.LeaveQueue();
        }
        
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
