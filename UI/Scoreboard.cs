using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

/// <summary>
/// In-game scoreboard showing all players, their stats, and team scores
/// Press Tab to show/hide
/// </summary>
public class Scoreboard : MonoBehaviour
{
    [Header("References")]
    public GameObject scoreboardPanel;
    public Transform team1Container;
    public Transform team2Container;
    public GameObject playerEntryPrefab;
    
    [Header("Team Headers")]
    public TextMeshProUGUI team1NameText;
    public TextMeshProUGUI team1ScoreText;
    public TextMeshProUGUI team2NameText;
    public TextMeshProUGUI team2ScoreText;
    
    [Header("Match Info")]
    public TextMeshProUGUI matchTimerText;
    
    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.Tab;
    public bool showOnDeath = true;
    public float updateInterval = 0.5f;
    
    private Dictionary<string, GameObject> playerEntries = new Dictionary<string, GameObject>();
    private float lastUpdateTime;
    private bool isVisible = false;
    private MatchManager matchManager;
    
    private void Start()
    {
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(false);
        }
        
        // Cache MatchManager reference
        matchManager = FindAnyObjectByType<MatchManager>();
    }
    
    private void Update()
    {
        // Toggle scoreboard with Tab key
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleScoreboard(true);
        }
        else if (Input.GetKeyUp(toggleKey))
        {
            ToggleScoreboard(false);
        }
        
        // Update scoreboard data periodically when visible
        if (isVisible && Time.time - lastUpdateTime > updateInterval)
        {
            UpdateScoreboard();
            lastUpdateTime = Time.time;
        }
    }
    
    public void ToggleScoreboard(bool show)
    {
        isVisible = show;
        
        if (scoreboardPanel != null)
        {
            scoreboardPanel.SetActive(show);
        }
        
        if (show)
        {
            UpdateScoreboard();
        }
        
        // Unlock cursor when scoreboard is visible
        Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = show;
    }
    
    private void UpdateScoreboard()
    {
        // This would normally get data from a NetworkManager or MatchManager
        // For now, we'll create a sample implementation
        
        // Update team scores
        UpdateTeamScores();
        
        // Update match timer
        UpdateMatchTimer();
        
        // Update player entries
        UpdatePlayerEntries();
    }
    
    private void UpdateTeamScores()
    {
        // Use cached MatchManager reference
        if (matchManager != null)
        {
            if (team1ScoreText != null)
            {
                team1ScoreText.text = matchManager.Team1Score.ToString();
            }
            
            if (team2ScoreText != null)
            {
                team2ScoreText.text = matchManager.Team2Score.ToString();
            }
        }
    }
    
    private void UpdateMatchTimer()
    {
        // Use cached MatchManager reference
        if (matchManager != null && matchTimerText != null)
        {
            float timeRemaining = matchManager.MatchTimeRemaining;
            int minutes = Mathf.FloorToInt(timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(timeRemaining % 60f);
            matchTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    
    private void UpdatePlayerEntries()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
        {
            return;
        }
        
        // Find all networked players
        var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        
        foreach (var networkPlayer in allPlayers)
        {
            if (networkPlayer == null || !networkPlayer.IsSpawned)
            {
                continue;
            }
            
            // Use GetInstanceID to match MatchManager's player ID system
            string playerId = networkPlayer.gameObject.GetInstanceID().ToString();
            
            // Add player entry if not exists
            if (!playerEntries.ContainsKey(playerId))
            {
                // Get actual player name from NetworkPlayer
                string playerName = networkPlayer.GetPlayerName();
                
                // Fallback to ClientId if name is empty
                if (string.IsNullOrEmpty(playerName))
                {
                    playerName = $"Player {networkPlayer.OwnerClientId}";
                }
                
                int team = networkPlayer.GetTeam();
                AddPlayer(playerId, playerName, team);
            }
            
            // Update stats only if entry exists
            if (playerEntries.ContainsKey(playerId))
            {
                var playerHealth = networkPlayer.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    UpdatePlayerStats(playerId, playerHealth.Kills, playerHealth.Deaths, 0);
                }
            }
        }
    }
    
    public void AddPlayer(string playerId, string playerName, int team)
    {
        if (playerEntries.ContainsKey(playerId))
        {
            return;
        }
        
        if (playerEntryPrefab == null)
        {
            Debug.LogError("Scoreboard: playerEntryPrefab is null!");
            return;
        }
        
        Transform container = team == 1 ? team1Container : team2Container;
        
        if (container == null)
        {
            Debug.LogError($"Scoreboard: Container for team {team} is null!");
            return;
        }
        
        GameObject entryObj = Instantiate(playerEntryPrefab, container);
        
        // Ensure proper RectTransform setup
        RectTransform rectTransform = entryObj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.localPosition = Vector3.zero;
        }
        
        // Find and set player name text
        TextMeshProUGUI nameText = entryObj.transform.Find("PlayerNameText")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null)
        {
            nameText.text = playerName;
        }
        
        playerEntries[playerId] = entryObj;
        Debug.Log($"Scoreboard: Successfully added player {playerName} (ID: {playerId}) to team {team}");
    }
    
    public void RemovePlayer(string playerId)
    {
        if (playerEntries.TryGetValue(playerId, out GameObject entryObj))
        {
            Destroy(entryObj);
            playerEntries.Remove(playerId);
        }
    }
    
    private void UpdateEntryStats(GameObject entryObj, int kills, int deaths)
    {
        TextMeshProUGUI killsText = entryObj.transform.Find("KillsText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI deathsText = entryObj.transform.Find("DeathsText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI kdRatioText = entryObj.transform.Find("KDRatioText")?.GetComponent<TextMeshProUGUI>();
        
        if (killsText != null)
            killsText.text = kills.ToString();
            
        if (deathsText != null)
            deathsText.text = deaths.ToString();
            
        if (kdRatioText != null)
        {
            float kd = deaths > 0 ? (float)kills / deaths : kills;
            kdRatioText.text = kd.ToString("F2");
        }
    }
    
    public void UpdatePlayerStats(string playerId, int kills, int deaths, int ping)
    {
        if (playerEntries.TryGetValue(playerId, out GameObject entryObj))
        {
            UpdateEntryStats(entryObj, kills, deaths);
            
            // Update ping if available
            TextMeshProUGUI pingText = entryObj.transform.Find("PingText")?.GetComponent<TextMeshProUGUI>();
            if (pingText != null)
            {
                pingText.text = ping.ToString();
            }
        }
    }
    
    public void ShowOnPlayerDeath()
    {
        if (showOnDeath)
        {
            ToggleScoreboard(true);
            Invoke(nameof(HideScoreboard), 3f); // Auto-hide after 3 seconds
        }
    }
    
    private void HideScoreboard()
    {
        ToggleScoreboard(false);
    }
}
