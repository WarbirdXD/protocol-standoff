using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

/// <summary>
/// Handles player ranking, MMR calculation, and rank tiers
/// Rocket League-style competitive ranking system
/// </summary>
public class RankingSystem : MonoBehaviour
{
    public static RankingSystem Instance { get; private set; }
    
    [Header("Player Stats")]
    public int currentMMR = 600;  // Current mode's MMR (for display)
    public RankTier currentRank;
    public int division = 1;      // 1, 2, or 3
    
    [Header("Mode-Specific Stats")]
    public int oneVsOneMmr = 600;
    public int twoVsTwoMmr = 600;
    private MatchmakingManager.MatchMode currentMode = MatchmakingManager.MatchMode.OneVsOne;
    
    [Header("MMR Settings")]
    public int kFactor = 25;      // MMR change multiplier
    public int minMMR = 0;
    public int maxMMR = 3000;
    
    [Header("Season")]
    public int currentSeason = 1;
    public int matchesPlayed = 0;
    public int wins = 0;
    public int losses = 0;
    
    // Mode-specific stats
    public int oneVsOneMatches = 0;
    public int oneVsOneWins = 0;
    public int oneVsOneLosses = 0;
    
    public int twoVsTwoMatches = 0;
    public int twoVsTwoWins = 0;
    public int twoVsTwoLosses = 0;
    
    [Header("Rank Badge Art")]
    public Sprite scrapBadge;
    public Sprite steelBadge;
    public Sprite chromeBadge;
    public Sprite titaniumBadge;
    public Sprite carbonFiberBadge;
    public Sprite nanoweaveBadge;
    public Sprite exoticMatterBadge;
    
    [Header("Events")]
    public UnityEvent<int> OnMMRChanged;           // New MMR
    public UnityEvent<RankTier, int> OnRankChanged; // New rank, division
    public UnityEvent<bool> OnRankUp;              // true = rank up, false = rank down
    
    public enum RankTier
    {
        Scrap,
        Steel,
        Chrome,
        Titanium,
        Carbon_Fiber,
        Nanoweave,
        Exotic_Matter
    }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadPlayerStats();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Set the current game mode for MMR tracking
    /// </summary>
    public void SetCurrentMode(MatchmakingManager.MatchMode mode)
    {
        currentMode = mode;
        UpdateCurrentModeStats();
    }
    
    /// <summary>
    /// Get MMR for specific mode
    /// </summary>
    public int GetMMRForMode(MatchmakingManager.MatchMode mode)
    {
        return mode == MatchmakingManager.MatchMode.OneVsOne ? oneVsOneMmr : twoVsTwoMmr;
    }
    
    /// <summary>
    /// Update current display stats based on selected mode
    /// </summary>
    private void UpdateCurrentModeStats()
    {
        if (currentMode == MatchmakingManager.MatchMode.OneVsOne)
        {
            currentMMR = oneVsOneMmr;
            matchesPlayed = oneVsOneMatches;
            wins = oneVsOneWins;
            losses = oneVsOneLosses;
        }
        else
        {
            currentMMR = twoVsTwoMmr;
            matchesPlayed = twoVsTwoMatches;
            wins = twoVsTwoWins;
            losses = twoVsTwoLosses;
        }
        UpdateRankFromMMR();
    }
    
    /// <summary>
    /// Calculate MMR change after a match
    /// </summary>
    public int CalculateMMRChange(bool won, int opponentMMR, int playerMMR)
    {
        // Expected score formula (Elo rating)
        float expected = 1f / (1f + Mathf.Pow(10f, (opponentMMR - playerMMR) / 400f));
        
        // Actual score
        float actual = won ? 1f : 0f;
        
        // MMR change
        int change = Mathf.RoundToInt(kFactor * (actual - expected));
        
        return change;
    }
    
    /// <summary>
    /// Apply match result and update MMR/rank for specific mode
    /// </summary>
    public void ApplyMatchResult(bool won, int opponentMMR, MatchmakingManager.MatchMode mode)
    {
        // Get current MMR for this mode
        int modeMMR = GetMMRForMode(mode);
        
        // Calculate MMR change
        int mmrChange = CalculateMMRChange(won, opponentMMR, modeMMR);
        
        // Store old rank for comparison
        RankTier oldRank = currentRank;
        int oldDivision = division;
        
        // Update mode-specific MMR and stats
        if (mode == MatchmakingManager.MatchMode.OneVsOne)
        {
            oneVsOneMmr += mmrChange;
            oneVsOneMmr = Mathf.Clamp(oneVsOneMmr, minMMR, maxMMR);
            oneVsOneMatches++;
            if (won)
                oneVsOneWins++;
            else
                oneVsOneLosses++;
        }
        else
        {
            twoVsTwoMmr += mmrChange;
            twoVsTwoMmr = Mathf.Clamp(twoVsTwoMmr, minMMR, maxMMR);
            twoVsTwoMatches++;
            if (won)
                twoVsTwoWins++;
            else
                twoVsTwoLosses++;
        }
        
        // Update current display stats
        SetCurrentMode(mode);
        
        // Trigger events
        OnMMRChanged?.Invoke(currentMMR);
        
        // Check if rank changed
        if (currentRank != oldRank || division != oldDivision)
        {
            OnRankChanged?.Invoke(currentRank, division);
            
            // Determine if rank up or down
            bool rankedUp = IsHigherRank(currentRank, division, oldRank, oldDivision);
            OnRankUp?.Invoke(rankedUp);
        }
        
        // Save stats
        SavePlayerStats();
    }
    
    /// <summary>
    /// Update rank tier and division based on current MMR
    /// </summary>
    private void UpdateRankFromMMR()
    {
        if (currentMMR < 300)
        {
            currentRank = RankTier.Scrap;
            division = Mathf.Clamp(1 + currentMMR / 100, 1, 3);
        }
        else if (currentMMR < 600)
        {
            currentRank = RankTier.Steel;
            division = Mathf.Clamp(1 + (currentMMR - 300) / 100, 1, 3);
        }
        else if (currentMMR < 900)
        {
            currentRank = RankTier.Chrome;
            division = Mathf.Clamp(1 + (currentMMR - 600) / 100, 1, 3);
        }
        else if (currentMMR < 1200)
        {
            currentRank = RankTier.Titanium;
            division = Mathf.Clamp(1 + (currentMMR - 900) / 100, 1, 3);
        }
        else if (currentMMR < 1500)
        {
            currentRank = RankTier.Carbon_Fiber;
            division = Mathf.Clamp(1 + (currentMMR - 1200) / 100, 1, 3);
        }
        else if (currentMMR < 1800)
        {
            currentRank = RankTier.Nanoweave;
            division = Mathf.Clamp(1 + (currentMMR - 1500) / 100, 1, 3);
        }
        else
        {
            currentRank = RankTier.Exotic_Matter;
            division = 1; // Exotic Matter has no divisions
        }
    }
    
    /// <summary>
    /// Check if rank A is higher than rank B
    /// </summary>
    private bool IsHigherRank(RankTier rankA, int divA, RankTier rankB, int divB)
    {
        if (rankA > rankB) return true;
        if (rankA < rankB) return false;
        return divA > divB;
    }
    
    /// <summary>
    /// Get rank display string (e.g. "Chrome II")
    /// </summary>
    public string GetRankDisplayName()
    {
        string divisionRoman = division == 1 ? "I" : division == 2 ? "II" : "III";
        
        if (currentRank == RankTier.Exotic_Matter)
            return "Exotic Matter";
        
        // Format rank name (replace underscores with spaces)
        string rankName = currentRank.ToString().Replace("_", " ");
        
        return $"{rankName} {divisionRoman}";
    }
    
    /// <summary>
    /// Get rank badge sprite for current rank
    /// </summary>
    public Sprite GetRankBadge()
    {
        return GetRankBadge(currentRank);
    }
    
    /// <summary>
    /// Get rank badge sprite for specific rank tier
    /// </summary>
    public Sprite GetRankBadge(RankTier rank)
    {
        switch (rank)
        {
            case RankTier.Scrap:
                return scrapBadge;
            case RankTier.Steel:
                return steelBadge;
            case RankTier.Chrome:
                return chromeBadge;
            case RankTier.Titanium:
                return titaniumBadge;
            case RankTier.Carbon_Fiber:
                return carbonFiberBadge;
            case RankTier.Nanoweave:
                return nanoweaveBadge;
            case RankTier.Exotic_Matter:
                return exoticMatterBadge;
            default:
                return chromeBadge; // Default fallback
        }
    }
    
    /// <summary>
    /// Get win rate percentage
    /// </summary>
    public float GetWinRate()
    {
        if (matchesPlayed == 0) return 0f;
        return (float)wins / matchesPlayed * 100f;
    }
    
    /// <summary>
    /// Reset rank for new season
    /// </summary>
    public void ResetForNewSeason()
    {
        currentSeason++;
        
        // Soft reset: move MMR closer to starting value for both modes
        oneVsOneMmr = Mathf.RoundToInt(oneVsOneMmr * 0.8f + 600 * 0.2f);
        twoVsTwoMmr = Mathf.RoundToInt(twoVsTwoMmr * 0.8f + 600 * 0.2f);
        
        // Reset stats for both modes
        oneVsOneMatches = 0;
        oneVsOneWins = 0;
        oneVsOneLosses = 0;
        
        twoVsTwoMatches = 0;
        twoVsTwoWins = 0;
        twoVsTwoLosses = 0;
        
        UpdateCurrentModeStats();
        SavePlayerStats();
    }
    
    /// <summary>
    /// Save player stats to PlayerPrefs and Firebase
    /// </summary>
    public void SavePlayerStats()
    {
        // Save to PlayerPrefs (local backup)
        PlayerPrefs.SetInt("OneVsOneMmr", oneVsOneMmr);
        PlayerPrefs.SetInt("TwoVsTwoMmr", twoVsTwoMmr);
        
        PlayerPrefs.SetInt("OneVsOneMatches", oneVsOneMatches);
        PlayerPrefs.SetInt("OneVsOneWins", oneVsOneWins);
        PlayerPrefs.SetInt("OneVsOneLosses", oneVsOneLosses);
        
        PlayerPrefs.SetInt("TwoVsTwoMatches", twoVsTwoMatches);
        PlayerPrefs.SetInt("TwoVsTwoWins", twoVsTwoWins);
        PlayerPrefs.SetInt("TwoVsTwoLosses", twoVsTwoLosses);
        
        PlayerPrefs.SetInt("CurrentSeason", currentSeason);
        PlayerPrefs.Save();
        
        // Save to Firebase
        SaveToFirebase();
    }
    
    /// <summary>
    /// Save stats to Firebase
    /// </summary>
    private void SaveToFirebase()
    {
#if FIREBASE_INSTALLED
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.isSignedIn)
        {
            Debug.LogWarning("Cannot save stats to Firebase - not signed in");
            return;
        }
        
        string playerId = FirebaseManager.Instance.playerId;
        
        // Save 1v1 stats
        var oneVsOneStats = new Dictionary<string, object>
        {
            { "mmr", oneVsOneMmr },
            { "wins", oneVsOneWins },
            { "losses", oneVsOneLosses },
            { "gamesPlayed", oneVsOneMatches },
            { "winRate", oneVsOneMatches > 0 ? (float)oneVsOneWins / oneVsOneMatches * 100f : 0f },
            { "highestMMR", Mathf.Max(oneVsOneMmr, PlayerPrefs.GetInt("OneVsOneHighestMMR", oneVsOneMmr)) }
        };
        
        // Save 2v2 stats
        var twoVsTwoStats = new Dictionary<string, object>
        {
            { "mmr", twoVsTwoMmr },
            { "wins", twoVsTwoWins },
            { "losses", twoVsTwoLosses },
            { "gamesPlayed", twoVsTwoMatches },
            { "winRate", twoVsTwoMatches > 0 ? (float)twoVsTwoWins / twoVsTwoMatches * 100f : 0f },
            { "highestMMR", Mathf.Max(twoVsTwoMmr, PlayerPrefs.GetInt("TwoVsTwoHighestMMR", twoVsTwoMmr)) }
        };
        
        // Update highest MMR in PlayerPrefs
        PlayerPrefs.SetInt("OneVsOneHighestMMR", (int)oneVsOneStats["highestMMR"]);
        PlayerPrefs.SetInt("TwoVsTwoHighestMMR", (int)twoVsTwoStats["highestMMR"]);
        
        var updates = new Dictionary<string, object>
        {
            // New nested structure
            { $"players/{playerId}/stats/ranked_1v1", oneVsOneStats },
            { $"players/{playerId}/stats/ranked_2v2", twoVsTwoStats },
            
            // Also update root-level fields for backward compatibility
            { $"players/{playerId}/oneVsOneMmr", oneVsOneMmr },
            { $"players/{playerId}/oneVsOneMatches", oneVsOneMatches },
            { $"players/{playerId}/oneVsOneWins", oneVsOneWins },
            { $"players/{playerId}/oneVsOneLosses", oneVsOneLosses },
            
            { $"players/{playerId}/twoVsTwoMmr", twoVsTwoMmr },
            { $"players/{playerId}/twoVsTwoMatches", twoVsTwoMatches },
            { $"players/{playerId}/twoVsTwoWins", twoVsTwoWins },
            { $"players/{playerId}/twoVsTwoLosses", twoVsTwoLosses },
            
            { $"players/{playerId}/lastPlayed", Firebase.Database.ServerValue.Timestamp }
        };
        
        Firebase.Database.FirebaseDatabase.DefaultInstance
            .RootReference
            .UpdateChildrenAsync(updates)
            .ContinueWith(task => {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"Stats saved to Firebase - 1v1 MMR: {oneVsOneMmr}, 2v2 MMR: {twoVsTwoMmr}");
                }
                else
                {
                    Debug.LogError($"Failed to save stats to Firebase: {task.Exception}");
                }
            });
#endif
    }
    
    /// <summary>
    /// Load player stats from PlayerPrefs
    /// </summary>
    public void LoadPlayerStats()
    {
        // Load mode-specific MMR
        oneVsOneMmr = PlayerPrefs.GetInt("OneVsOneMmr", 600);
        twoVsTwoMmr = PlayerPrefs.GetInt("TwoVsTwoMmr", 600);
        
        // Load mode-specific stats
        oneVsOneMatches = PlayerPrefs.GetInt("OneVsOneMatches", 0);
        oneVsOneWins = PlayerPrefs.GetInt("OneVsOneWins", 0);
        oneVsOneLosses = PlayerPrefs.GetInt("OneVsOneLosses", 0);
        
        twoVsTwoMatches = PlayerPrefs.GetInt("TwoVsTwoMatches", 0);
        twoVsTwoWins = PlayerPrefs.GetInt("TwoVsTwoWins", 0);
        twoVsTwoLosses = PlayerPrefs.GetInt("TwoVsTwoLosses", 0);
        
        currentSeason = PlayerPrefs.GetInt("CurrentSeason", 1);
        
        // Set default mode and update display stats
        SetCurrentMode(MatchmakingManager.MatchMode.OneVsOne);
    }
}
