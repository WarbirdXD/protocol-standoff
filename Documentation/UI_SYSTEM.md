# UI System Documentation

**Clean, functional UI for menus, HUD, and match information**

---

## Overview

The UI System provides all visual interfaces for the game, from login screens to in-match HUD. It uses Unity's UI system with a focus on clarity, responsiveness, and minimal visual clutter. The system handles authentication flows, matchmaking status, in-game information display, and match results.

---

## Architecture

```
UI Hierarchy
├── Login Scene
│   ├── Login Panel
│   ├── Sign Up Panel
│   └── Error Messages
├── Lobby Scene
│   ├── Main Menu Panel
│   ├── Queue Panel
│   ├── Settings Panel
│   └── Profile Display
└── Match Scene
    ├── Match HUD
    │   ├── Timer
    │   ├── Scores
    │   ├── Ammo Counter
    │   ├── Health Bar
    │   └── Crosshair
    ├── Scoreboard (Tab)
    ├── Calibration UI (C)
    └── Match End Panel
```

---

## Login Scene

### Login Panel

**Components:**
- Email input field
- Password input field (masked)
- Login button
- "Create Account" button
- Error message text
- Loading indicator

**Flow:**
```
User enters credentials
    ↓
Click "Login" button
    ↓
Show loading indicator
    ↓
FirebaseManager.SignIn()
    ↓
Success?
├─ Yes → Load lobby scene
└─ No → Show error message
```

**Implementation:**
```csharp
public class LoginUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    public Button signUpButton;
    public TextMeshProUGUI errorText;
    public GameObject loadingIndicator;
    
    void Start()
    {
        loginButton.onClick.AddListener(OnLoginClicked);
        signUpButton.onClick.AddListener(OnSignUpClicked);
        
        // Subscribe to Firebase events
        FirebaseManager.Instance.OnSignInSuccess.AddListener(OnSignInSuccess);
        FirebaseManager.Instance.OnAuthError.AddListener(OnAuthError);
    }
    
    void OnLoginClicked()
    {
        string email = emailInput.text;
        string password = passwordInput.text;
        
        // Validate inputs
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter email and password");
            return;
        }
        
        // Show loading
        loadingIndicator.SetActive(true);
        loginButton.interactable = false;
        
        // Attempt sign in
        FirebaseManager.Instance.SignIn(email, password);
    }
    
    void OnSignInSuccess(string playerName)
    {
        // Load lobby scene
        SceneManager.LoadScene("Lobby");
    }
    
    void OnAuthError(string error)
    {
        ShowError(error);
        loadingIndicator.SetActive(false);
        loginButton.interactable = true;
    }
    
    void ShowError(string message)
    {
        errorText.text = message;
        errorText.gameObject.SetActive(true);
        
        // Auto-hide after 3 seconds
        StartCoroutine(HideErrorAfterDelay(3f));
    }
}
```

### Sign Up Panel

**Components:**
- Email input field
- Username input field
- Password input field (masked)
- Confirm password input field (masked)
- Create Account button
- Back button
- Error message text

**Validation:**
```csharp
bool ValidateSignUp()
{
    // Check email format
    if (!IsValidEmail(emailInput.text))
    {
        ShowError("Invalid email format");
        return false;
    }
    
    // Check username length
    if (usernameInput.text.Length < 3 || usernameInput.text.Length > 20)
    {
        ShowError("Username must be 3-20 characters");
        return false;
    }
    
    // Check password length
    if (passwordInput.text.Length < 6)
    {
        ShowError("Password must be at least 6 characters");
        return false;
    }
    
    // Check passwords match
    if (passwordInput.text != confirmPasswordInput.text)
    {
        ShowError("Passwords do not match");
        return false;
    }
    
    return true;
}
```

---

## Lobby Scene

### Main Menu Panel

**Components:**
- Player name display
- MMR display
- Rank badge
- Play button (1v1)
- Play button (2v2)
- Settings button
- Quit button

**Layout:**
```
┌─────────────────────────────────┐
│  Player: WADDLE                 │
│  MMR: 612  [Silver II Badge]    │
├─────────────────────────────────┤
│  [    Play 1v1    ]             │
│  [    Play 2v2    ]             │
│  [    Settings    ]             │
│  [      Quit      ]             │
└─────────────────────────────────┘
```

**Implementation:**
```csharp
public class MainMenuUI : MonoBehaviour
{
    [Header("Display Elements")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI mmrText;
    public Image rankBadge;
    
    [Header("Buttons")]
    public Button play1v1Button;
    public Button play2v2Button;
    public Button settingsButton;
    public Button quitButton;
    
    void Start()
    {
        UpdatePlayerInfo();
        
        play1v1Button.onClick.AddListener(() => JoinQueue(MatchMode.OneVsOne));
        play2v2Button.onClick.AddListener(() => JoinQueue(MatchMode.TwoVsTwo));
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);
    }
    
    void UpdatePlayerInfo()
    {
        playerNameText.text = FirebaseManager.Instance.playerName;
        
        int mmr = RankingSystem.Instance.currentMMR;
        mmrText.text = $"MMR: {mmr}";
        
        RankTier rank = RankingSystem.Instance.currentRank;
        rankBadge.sprite = GetRankSprite(rank);
        rankBadge.color = GetRankColor(rank);
    }
    
    void JoinQueue(MatchMode mode)
    {
        MatchmakingManager.Instance.JoinQueue(mode);
        queuePanel.SetActive(true);
        mainMenuPanel.SetActive(false);
    }
}
```

### Queue Panel

**Components:**
- "Searching for opponent..." text
- Queue time counter
- Search range indicator
- Estimated wait time
- Cancel button
- Animated loading indicator

**Real-Time Updates:**
```csharp
public class QueueUI : MonoBehaviour
{
    [Header("Display Elements")]
    public TextMeshProUGUI queueTimeText;
    public TextMeshProUGUI searchRangeText;
    public TextMeshProUGUI estimateText;
    public Button cancelButton;
    
    private float queueStartTime;
    
    void OnEnable()
    {
        queueStartTime = Time.time;
        cancelButton.onClick.AddListener(OnCancelClicked);
    }
    
    void Update()
    {
        if (!MatchmakingManager.Instance.isInQueue)
            return;
        
        // Update queue time
        float timeInQueue = Time.time - queueStartTime;
        queueTimeText.text = $"Searching... {timeInQueue:F0}s";
        
        // Update search range
        int searchRange = MatchmakingManager.Instance.GetCurrentSearchRange();
        searchRangeText.text = $"Search Range: ±{searchRange} MMR";
        
        // Update estimate
        string estimate = searchRange switch
        {
            <= 100 => "< 10s",
            <= 200 => "< 20s",
            <= 400 => "< 30s",
            _ => "< 60s"
        };
        estimateText.text = $"Est: {estimate}";
    }
    
    void OnCancelClicked()
    {
        MatchmakingManager.Instance.LeaveQueue();
        gameObject.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
}
```

### Settings Panel

**Components:**
- Mouse sensitivity slider
- Master volume slider
- Music volume slider
- SFX volume slider
- Graphics quality dropdown
- Resolution dropdown
- Fullscreen toggle
- Apply button
- Back button

**Settings Persistence:**
```csharp
public class SettingsUI : MonoBehaviour
{
    void SaveSettings()
    {
        // Save sensitivity
        PlayerPrefs.SetFloat("MouseSensitivity", sensitivitySlider.value);
        
        // Save volumes
        PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolumeSlider.value);
        
        // Save graphics
        PlayerPrefs.SetInt("GraphicsQuality", qualityDropdown.value);
        PlayerPrefs.SetInt("ResolutionIndex", resolutionDropdown.value);
        PlayerPrefs.SetInt("Fullscreen", fullscreenToggle.isOn ? 1 : 0);
        
        PlayerPrefs.Save();
        
        ApplySettings();
    }
    
    void LoadSettings()
    {
        sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 2f);
        masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
        qualityDropdown.value = PlayerPrefs.GetInt("GraphicsQuality", 2);
        resolutionDropdown.value = PlayerPrefs.GetInt("ResolutionIndex", 0);
        fullscreenToggle.isOn = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
    }
}
```

---

## Match HUD

### Timer Display

**Position:** Top center
**Format:** MM:SS

```csharp
public class TimerUI : MonoBehaviour
{
    public TextMeshProUGUI timerText;
    
    void Update()
    {
        if (MatchManager.Instance == null)
            return;
        
        float timeRemaining = MatchManager.Instance.GetTimeRemaining();
        
        int minutes = Mathf.FloorToInt(timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(timeRemaining % 60f);
        
        timerText.text = $"{minutes:00}:{seconds:00}";
        
        // Color warning when time low
        if (timeRemaining < 30f)
        {
            timerText.color = Color.red;
        }
        else if (timeRemaining < 60f)
        {
            timerText.color = Color.yellow;
        }
        else
        {
            timerText.color = Color.white;
        }
    }
}
```

### Score Display

**Position:** Top center (below timer)
**Format:** Team 1: X - Y :Team 2

```csharp
public class ScoreUI : MonoBehaviour
{
    public TextMeshProUGUI team1ScoreText;
    public TextMeshProUGUI team2ScoreText;
    public Image team1Background;
    public Image team2Background;
    
    void Start()
    {
        // Subscribe to score changes
        MatchManager.Instance.OnScoreChanged.AddListener(UpdateScores);
        
        // Set team colors
        team1Background.color = new Color(0.2f, 0.5f, 1f, 0.5f); // Blue
        team2Background.color = new Color(1f, 0.3f, 0.2f, 0.5f); // Red
    }
    
    void UpdateScores(int team1Score, int team2Score)
    {
        team1ScoreText.text = team1Score.ToString();
        team2ScoreText.text = team2Score.ToString();
    }
}
```

### Ammo Counter

**Position:** Bottom right
**Format:** Current / Reserve

```csharp
public class AmmoUI : MonoBehaviour
{
    public TextMeshProUGUI currentAmmoText;
    public TextMeshProUGUI reserveAmmoText;
    public Image reloadIndicator;
    
    private WeaponController weapon;
    
    void Start()
    {
        weapon = FindObjectOfType<WeaponController>();
    }
    
    void Update()
    {
        if (weapon == null)
            return;
        
        // Update ammo display
        currentAmmoText.text = weapon.GetCurrentAmmo().ToString();
        reserveAmmoText.text = weapon.GetReserveAmmo().ToString();
        
        // Color warning when low
        if (weapon.GetCurrentAmmo() == 0)
        {
            currentAmmoText.color = Color.red;
        }
        else if (weapon.GetCurrentAmmo() <= 10)
        {
            currentAmmoText.color = Color.yellow;
        }
        else
        {
            currentAmmoText.color = Color.white;
        }
        
        // Show reload indicator
        reloadIndicator.gameObject.SetActive(weapon.IsReloading());
        if (weapon.IsReloading())
        {
            float progress = weapon.GetReloadProgress();
            reloadIndicator.fillAmount = progress;
        }
    }
}
```

### Health Bar

**Position:** Bottom left
**Format:** Horizontal bar with text

```csharp
public class HealthUI : MonoBehaviour
{
    public Image healthBarFill;
    public TextMeshProUGUI healthText;
    public CanvasGroup damageFlash;
    
    private PlayerHealth playerHealth;
    
    void Start()
    {
        playerHealth = FindObjectOfType<PlayerHealth>();
        playerHealth.OnHealthChanged.AddListener(UpdateHealth);
    }
    
    void UpdateHealth(float currentHealth, float maxHealth)
    {
        float healthPercent = currentHealth / maxHealth;
        
        // Update bar
        healthBarFill.fillAmount = healthPercent;
        
        // Update text
        healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        
        // Color based on health
        if (healthPercent > 0.5f)
        {
            healthBarFill.color = Color.green;
        }
        else if (healthPercent > 0.25f)
        {
            healthBarFill.color = Color.yellow;
        }
        else
        {
            healthBarFill.color = Color.red;
        }
        
        // Flash on damage
        StartCoroutine(FlashDamage());
    }
    
    IEnumerator FlashDamage()
    {
        damageFlash.alpha = 0.5f;
        
        float duration = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            damageFlash.alpha = Mathf.Lerp(0.5f, 0f, elapsed / duration);
            yield return null;
        }
        
        damageFlash.alpha = 0f;
    }
}
```

### Crosshair

**Position:** Center screen
**Types:** Static, dynamic (expands with spread)

```csharp
public class CrosshairUI : MonoBehaviour
{
    public RectTransform crosshairTop;
    public RectTransform crosshairBottom;
    public RectTransform crosshairLeft;
    public RectTransform crosshairRight;
    
    public float baseGap = 10f;
    public float maxGap = 50f;
    
    private WeaponController weapon;
    
    void Update()
    {
        if (weapon == null)
            return;
        
        // Calculate spread-based gap
        float spread = weapon.GetCurrentSpread();
        float gap = Mathf.Lerp(baseGap, maxGap, spread / weapon.maxSpread);
        
        // Position crosshair elements
        crosshairTop.anchoredPosition = new Vector2(0, gap);
        crosshairBottom.anchoredPosition = new Vector2(0, -gap);
        crosshairLeft.anchoredPosition = new Vector2(-gap, 0);
        crosshairRight.anchoredPosition = new Vector2(gap, 0);
    }
}
```

---

## Scoreboard

### Layout

**Position:** Full screen overlay (Tab to toggle)

```
┌─────────────────────────────────────────┐
│           SCOREBOARD                    │
├─────────────────────────────────────────┤
│  Team 1 (Blue)                          │
│  ┌───────────────────────────────────┐  │
│  │ Player Name    K   D   K/D        │  │
│  │ WADDLE        12   5   2.40       │  │
│  │ Player2        8   7   1.14       │  │
│  └───────────────────────────────────┘  │
│                                         │
│  Team 2 (Red)                           │
│  ┌───────────────────────────────────┐  │
│  │ Player Name    K   D   K/D        │  │
│  │ Enemy1        10   8   1.25       │  │
│  │ Enemy2         6  10   0.60       │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

### Implementation

```csharp
public class Scoreboard : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject scoreboardPanel;
    public Transform team1Container;
    public Transform team2Container;
    public GameObject playerEntryPrefab;
    
    private Dictionary<string, GameObject> playerEntries = new Dictionary<string, GameObject>();
    
    void Update()
    {
        // Toggle with Tab key
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            scoreboardPanel.SetActive(true);
            UpdatePlayerEntries();
        }
        else if (Input.GetKeyUp(KeyCode.Tab))
        {
            scoreboardPanel.SetActive(false);
        }
    }
    
    void UpdatePlayerEntries()
    {
        var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        
        foreach (var networkPlayer in allPlayers)
        {
            string playerId = networkPlayer.gameObject.GetInstanceID().ToString();
            
            if (!playerEntries.ContainsKey(playerId))
            {
                AddPlayer(playerId, networkPlayer.GetPlayerName(), networkPlayer.GetTeam());
            }
            
            // Update stats
            var playerHealth = networkPlayer.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                UpdatePlayerStats(playerId, playerHealth.Kills, playerHealth.Deaths);
            }
        }
    }
    
    void AddPlayer(string playerId, string playerName, int team)
    {
        GameObject entry = Instantiate(playerEntryPrefab);
        entry.transform.SetParent(team == 1 ? team1Container : team2Container, false);
        
        var entryUI = entry.GetComponent<ScoreboardEntry>();
        entryUI.SetPlayerName(playerName);
        
        playerEntries[playerId] = entry;
    }
    
    void UpdatePlayerStats(string playerId, int kills, int deaths)
    {
        if (!playerEntries.ContainsKey(playerId))
            return;
        
        var entryUI = playerEntries[playerId].GetComponent<ScoreboardEntry>();
        entryUI.SetKills(kills);
        entryUI.SetDeaths(deaths);
        entryUI.SetKD(deaths > 0 ? (float)kills / deaths : kills);
    }
}
```

---

## Match End Panel

### Layout

```
┌─────────────────────────────────┐
│      TEAM 1 WINS!               │
├─────────────────────────────────┤
│  Final Score: 10 - 7            │
│                                 │
│  Your Performance:              │
│  MMR Change: +16                │
│  New MMR: 628                   │
│  New Rank: Silver II            │
│                                 │
│  [  Return to Lobby  ]          │
└─────────────────────────────────┘
```

### Implementation

```csharp
public class MatchEndUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI winnerText;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI mmrChangeText;
    public TextMeshProUGUI newMMRText;
    public TextMeshProUGUI newRankText;
    public Button returnButton;
    
    public void ShowMatchEnd(int winner, int team1Score, int team2Score, int mmrChange, int newMMR, RankTier newRank)
    {
        gameObject.SetActive(true);
        
        // Winner announcement
        if (winner == 0)
        {
            winnerText.text = "TIE!";
            winnerText.color = Color.yellow;
        }
        else
        {
            int myTeam = NetworkGameManager.Instance.GetLocalPlayerTeam();
            bool iWon = (myTeam == winner);
            
            winnerText.text = iWon ? "VICTORY!" : "DEFEAT";
            winnerText.color = iWon ? Color.green : Color.red;
        }
        
        // Final score
        finalScoreText.text = $"Final Score: {team1Score} - {team2Score}";
        
        // MMR change
        mmrChangeText.text = $"MMR Change: {mmrChange:+0;-0}";
        mmrChangeText.color = mmrChange >= 0 ? Color.green : Color.red;
        
        // New stats
        newMMRText.text = $"New MMR: {newMMR}";
        newRankText.text = $"New Rank: {newRank}";
        
        returnButton.onClick.AddListener(ReturnToLobby);
    }
    
    void ReturnToLobby()
    {
        // Disconnect from match
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // Load lobby scene
        SceneManager.LoadScene("Lobby");
    }
}
```

---

## UI Styling

### Color Palette

```csharp
public static class UIColors
{
    // Team colors
    public static Color Team1 = new Color(0.2f, 0.5f, 1f);      // Blue
    public static Color Team2 = new Color(1f, 0.3f, 0.2f);      // Red
    
    // Status colors
    public static Color Success = new Color(0.2f, 0.8f, 0.2f);  // Green
    public static Color Warning = new Color(1f, 0.8f, 0f);      // Yellow
    public static Color Error = new Color(0.9f, 0.2f, 0.2f);    // Red
    
    // UI colors
    public static Color Background = new Color(0.17f, 0.24f, 0.31f); // Dark blue-gray
    public static Color Panel = new Color(0.2f, 0.27f, 0.35f);       // Lighter panel
    public static Color Text = new Color(0.93f, 0.94f, 0.95f);       // Off-white
    public static Color TextSecondary = new Color(0.7f, 0.7f, 0.7f); // Gray
}
```

### Font Sizes

```
Title: 48pt
Heading: 36pt
Subheading: 24pt
Body: 18pt
Small: 14pt
```

### Button Styling

```csharp
// Normal state
button.colors = new ColorBlock
{
    normalColor = new Color(0.2f, 0.5f, 1f),
    highlightedColor = new Color(0.3f, 0.6f, 1f),
    pressedColor = new Color(0.1f, 0.4f, 0.9f),
    selectedColor = new Color(0.2f, 0.5f, 1f),
    disabledColor = new Color(0.5f, 0.5f, 0.5f),
    colorMultiplier = 1f,
    fadeDuration = 0.1f
};
```

---

## Best Practices

1. **Use TextMeshPro** instead of legacy Text components
2. **Anchor UI elements** properly for different resolutions
3. **Use Canvas Groups** for fading panels
4. **Subscribe to events** instead of polling
5. **Cache UI references** in Start/Awake
6. **Use object pooling** for frequently created UI elements
7. **Provide visual feedback** for all interactions
8. **Test on different resolutions** and aspect ratios
9. **Use consistent spacing** and alignment
10. **Implement accessibility features** (colorblind modes, text scaling)

---

*This documentation explains the UI system architecture and functionality. For implementation details, see UI scripts in `Assets/scripts/UI/`.*
