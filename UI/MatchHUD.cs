using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MatchHUD : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI team1ScoreText;
    public TextMeshProUGUI team2ScoreText;
    
    [Header("Match End Panel")]
    public GameObject matchEndPanel;
    public TextMeshProUGUI winnerText;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI mmrChangeText;
    public Button returnToLobbyButton;
    
    [Header("Settings")]
    public float autoReturnDelay = 10f; // Auto-return after 10 seconds
    public string lobbySceneName = "Lobby";
    
    private MatchManager matchManager;
    
    private void Start()
    {
        if (matchEndPanel != null)
        {
            matchEndPanel.SetActive(false);
        }
        
        // Setup return to lobby button
        if (returnToLobbyButton != null)
        {
            returnToLobbyButton.onClick.AddListener(ReturnToLobby);
        }
        
        // Find MatchManager dynamically (it's a singleton in the scene)
        matchManager = FindFirstObjectByType<MatchManager>();
        
        // Subscribe to match events
        if (matchManager != null)
        {
            matchManager.OnTimeChanged.AddListener(UpdateTimer);
            matchManager.OnScoreChanged.AddListener(UpdateScore);
            matchManager.OnMatchEnd.AddListener(ShowMatchEnd);
            Debug.Log("MatchHUD: Subscribed to MatchManager events");
            
            // Initialize with current values
            UpdateTimer(matchManager.MatchTimeRemaining);
            UpdateScore(matchManager.Team1Score, matchManager.Team2Score);
        }
        else
        {
            Debug.LogError("MatchHUD: MatchManager not found in scene!");
        }
    }
    
    private void UpdateTimer(float timeRemaining)
    {
        if (timerText == null) return;
        
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        timerText.text = $"{minutes:00}:{seconds:00}";
        
        // Change color when time is running out
        if (timeRemaining < 30f)
        {
            timerText.color = Color.Lerp(Color.red, Color.white, timeRemaining / 30f);
        }
        else
        {
            timerText.color = Color.white;
        }
    }
    
    private void UpdateScore(int team1Score, int team2Score)
    {
        if (team1ScoreText != null)
        {
            team1ScoreText.text = team1Score.ToString();
        }
        
        if (team2ScoreText != null)
        {
            team2ScoreText.text = team2Score.ToString();
        }
    }
    
    private void ShowMatchEnd(int winningTeam)
    {
        if (matchEndPanel != null)
        {
            matchEndPanel.SetActive(true);
        }
        
        if (matchManager != null)
        {
            // Update winner text
            if (winnerText != null)
            {
                if (winningTeam == 0)
                {
                    winnerText.text = "TIE GAME!";
                    winnerText.color = Color.yellow;
                }
                else
                {
                    winnerText.text = $"TEAM {winningTeam} WINS!";
                    winnerText.color = winningTeam == 1 ? Color.cyan : Color.magenta;
                }
            }
            
            // Update final score
            if (finalScoreText != null)
            {
                finalScoreText.text = $"Final Score: {matchManager.Team1Score} - {matchManager.Team2Score}";
            }
            
            // Show MMR change
            if (mmrChangeText != null && RankingSystem.Instance != null)
            {
                int currentMMR = RankingSystem.Instance.currentMMR;
                string rankName = RankingSystem.Instance.GetRankDisplayName();
                mmrChangeText.text = $"MMR: {currentMMR} ({rankName})";
            }
        }
        
        // Unlock cursor for UI interaction
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Start auto-return timer
        Invoke(nameof(ReturnToLobby), autoReturnDelay);
    }
    
    /// <summary>
    /// Return to lobby scene
    /// </summary>
    public void ReturnToLobby()
    {
        // Cancel auto-return if manually triggered
        CancelInvoke(nameof(ReturnToLobby));
        
        Debug.Log("Returning to lobby...");
        
        // Properly disconnect from network before loading lobby
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Debug.Log("Shutting down network connection...");
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }
        
        // Load lobby scene
        SceneManager.LoadScene(lobbySceneName);
    }
}
