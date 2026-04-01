# Ranking System Documentation

**MMR-based skill tracking with rank tiers**

---

## Overview

The Ranking System tracks player skill through MMR (Matchmaking Rating) and assigns visual rank tiers. It uses the Elo rating system adapted for FPS gameplay, with separate ratings for 1v1 and 2v2 modes. The system calculates MMR changes after each match based on win/loss and opponent skill, then persists data to Firebase.

---

## Architecture

```
Match Ends
    ↓
Server sends match result via ClientRpc
    ↓
RankingSystem.ApplyMatchResult()
    ↓
Calculate MMR change using Elo formula
    ↓
Update mode-specific MMR and stats
    ↓
Determine new rank tier
    ↓
Trigger rank change events
    ↓
SavePlayerStats()
    ↓
┌─────────────────────────────┐
│  Save to Firebase           │
│  Save to PlayerPrefs        │
└─────────────────────────────┘
    ↓
Update UI displays
```

---

## MMR System

### What is MMR?

**MMR (Matchmaking Rating)** is a numerical representation of player skill:
- Higher MMR = Higher skill
- Range: 0 to 3000 (soft cap)
- Starting MMR: 600 (Bronze tier)
- Separate MMR for each mode (1v1, 2v2)

### Elo Rating Formula

Based on the Elo rating system used in chess:

```
MMR Change = K × (Actual - Expected)

Where:
K = 32 (sensitivity factor)
Actual = 1 (win) or 0 (loss)
Expected = 1 / (1 + 10^((OpponentMMR - MyMMR) / 400))
```

### Expected Score Calculation

```csharp
private float CalculateExpectedScore(int myMMR, int opponentMMR)
{
    float exponent = (opponentMMR - myMMR) / 400f;
    return 1f / (1f + Mathf.Pow(10f, exponent));
}
```

**What it means:**
- Expected score is probability of winning (0.0 to 1.0)
- Equal MMR → 0.5 expected (50% chance)
- Higher MMR → >0.5 expected (favored to win)
- Lower MMR → <0.5 expected (underdog)

### MMR Change Calculation

```csharp
public int CalculateMMRChange(bool won, int opponentMMR, int myMMR)
{
    float expectedScore = CalculateExpectedScore(myMMR, opponentMMR);
    float actualScore = won ? 1f : 0f;
    
    int mmrChange = Mathf.RoundToInt(kFactor * (actualScore - expectedScore));
    
    return mmrChange;
}
```

**K-Factor (32):**
- Higher K = More volatile ratings (faster changes)
- Lower K = More stable ratings (slower changes)
- 32 is standard for competitive games

---

## MMR Change Examples

### Equal Skill (600 vs 600)

**Expected Score:** 0.5 (50% chance to win)

**Win:**
```
MMR Change = 32 × (1.0 - 0.5) = +16
New MMR: 616
```

**Loss:**
```
MMR Change = 32 × (0.0 - 0.5) = -16
New MMR: 584
```

### Higher Skill Wins (700 vs 500)

**Expected Score:** 0.76 (76% chance to win)

**Win (expected):**
```
MMR Change = 32 × (1.0 - 0.76) = +8
New MMR: 708
```

**Loss (upset):**
```
MMR Change = 32 × (0.0 - 0.76) = -24
New MMR: 676
```

### Lower Skill Wins (500 vs 700)

**Expected Score:** 0.24 (24% chance to win)

**Win (upset):**
```
MMR Change = 32 × (1.0 - 0.24) = +24
New MMR: 524
```

**Loss (expected):**
```
MMR Change = 32 × (0.0 - 0.24) = -8
New MMR: 492
```

### Extreme Difference (1000 vs 400)

**Expected Score:** 0.97 (97% chance to win)

**Win (very expected):**
```
MMR Change = 32 × (1.0 - 0.97) = +1
New MMR: 1001
```

**Loss (major upset):**
```
MMR Change = 32 × (0.0 - 0.97) = -31
New MMR: 969
```

---

## Rank Tiers

### Tier Structure

| Rank | MMR Range | Color | Description |
|------|-----------|-------|-------------|
| **Chrome** | 0-399 | Gray (#808080) | Beginner |
| **Bronze** | 400-599 | Bronze (#CD7F32) | Learning |
| **Silver** | 600-799 | Silver (#C0C0C0) | Competent |
| **Gold** | 800-999 | Gold (#FFD700) | Skilled |
| **Platinum** | 1000-1199 | Cyan (#00FFFF) | Advanced |
| **Diamond** | 1200-1499 | Blue (#0080FF) | Expert |
| **Radiant** | 1500+ | Purple (#8B00FF) | Elite |

### Rank Calculation

```csharp
public RankTier GetRankFromMMR(int mmr)
{
    if (mmr < 400) return RankTier.Chrome;
    if (mmr < 600) return RankTier.Bronze;
    if (mmr < 800) return RankTier.Silver;
    if (mmr < 1000) return RankTier.Gold;
    if (mmr < 1200) return RankTier.Platinum;
    if (mmr < 1500) return RankTier.Diamond;
    return RankTier.Radiant;
}
```

### Division System

Each rank has 3 divisions (I, II, III):

```csharp
public int GetDivision(int mmr, RankTier rank)
{
    int rankMinMMR = GetRankMinMMR(rank);
    int rankMaxMMR = GetRankMaxMMR(rank);
    int rankRange = rankMaxMMR - rankMinMMR;
    
    int divisionSize = rankRange / 3;
    int mmrInRank = mmr - rankMinMMR;
    
    int division = (mmrInRank / divisionSize) + 1;
    return Mathf.Clamp(division, 1, 3);
}
```

**Example (Silver: 600-799):**
- Division III: 600-666 MMR
- Division II: 667-733 MMR
- Division I: 734-799 MMR

---

## Stats Tracking

### Per-Mode Statistics

```csharp
[System.Serializable]
public class ModeStats
{
    public int mmr;
    public int wins;
    public int losses;
    public int gamesPlayed;
    public float winRate;
    public int highestMMR;
    public int currentStreak;
    public int longestWinStreak;
}
```

### Tracked Stats

**1v1 Mode:**
- MMR (current rating)
- Wins (total victories)
- Losses (total defeats)
- Games Played (total matches)
- Win Rate (wins / gamesPlayed × 100)
- Highest MMR (peak rating)
- Current Streak (consecutive wins/losses)
- Longest Win Streak (best streak)

**2v2 Mode:**
- Same stats as 1v1, tracked separately

### Stat Updates

```csharp
public void ApplyMatchResult(bool won, int opponentMMR, MatchMode mode)
{
    // Calculate MMR change
    int mmrChange = CalculateMMRChange(won, opponentMMR, currentMMR);
    
    // Update MMR
    currentMMR += mmrChange;
    currentMMR = Mathf.Clamp(currentMMR, minMMR, maxMMR);
    
    // Update mode-specific stats
    if (mode == MatchMode.OneVsOne)
    {
        oneVsOneMmr = currentMMR;
        oneVsOneMatches++;
        
        if (won)
        {
            oneVsOneWins++;
            currentStreak = Mathf.Max(0, currentStreak) + 1;
        }
        else
        {
            oneVsOneLosses++;
            currentStreak = Mathf.Min(0, currentStreak) - 1;
        }
        
        // Update longest win streak
        if (currentStreak > longestWinStreak)
            longestWinStreak = currentStreak;
        
        // Update win rate
        oneVsOneStats.winRate = (float)oneVsOneWins / oneVsOneMatches * 100f;
        
        // Update highest MMR
        if (currentMMR > oneVsOneStats.highestMMR)
            oneVsOneStats.highestMMR = currentMMR;
    }
    
    // Save to Firebase and PlayerPrefs
    SavePlayerStats();
}
```

---

## Match Result Flow

### Server-Side (Match End)

```
Match ends on server
    ↓
MatchManager.EndMatch()
    ↓
Validate match results (SecurityManager)
    ↓
Determine winner (team with higher score)
    ↓
Get team average MMRs
    ↓
NotifyMatchEndClientRpc(winner, team1MMR, team2MMR, mode)
    ↓
Sent to all clients
```

### Client-Side (MMR Calculation)

```
ClientRpc received
    ↓
Determine if local player won
    ↓
Get opponent's average MMR
    ↓
RankingSystem.ApplyMatchResult(won, opponentMMR, mode)
    ↓
Calculate MMR change
    ↓
Update local stats
    ↓
Check for rank change
    ↓
Trigger UI events
    ↓
Save to Firebase and PlayerPrefs
```

### Why Client-Side Calculation?

**Problem:** Server doesn't know which client is which player.

**Solution:** Each client calculates their own MMR change:
```csharp
[ClientRpc]
private void NotifyMatchEndClientRpc(int winner, int team1AvgMMR, int team2AvgMMR, int matchModeInt)
{
    // Each client determines their own result
    int myTeam = NetworkGameManager.Instance.GetLocalPlayerTeam();
    bool iWon = (myTeam == winner);
    
    // Get opponent's MMR
    int opponentMMR = (myTeam == 1) ? team2AvgMMR : team1AvgMMR;
    
    // Calculate own MMR change
    RankingSystem.Instance.ApplyMatchResult(iWon, opponentMMR, mode);
}
```

---

## Data Persistence

### Dual Storage Strategy

**1. Firebase (Primary):**
- Cloud storage
- Accessible from any device
- Persistent across sessions
- Requires internet connection

**2. PlayerPrefs (Backup):**
- Local storage
- Offline access
- Fallback if Firebase fails
- Device-specific

### Save Process

```csharp
public void SavePlayerStats()
{
    // Save to PlayerPrefs (local backup)
    PlayerPrefs.SetInt("OneVsOneMmr", oneVsOneMmr);
    PlayerPrefs.SetInt("OneVsOneWins", oneVsOneWins);
    PlayerPrefs.SetInt("OneVsOneLosses", oneVsOneLosses);
    PlayerPrefs.SetInt("TwoVsTwoMmr", twoVsTwoMmr);
    // ... more stats
    PlayerPrefs.Save();
    
    // Save to Firebase (cloud)
    SaveToFirebase();
}
```

### Firebase Save Structure

```csharp
private void SaveToFirebase()
{
    var updates = new Dictionary<string, object>
    {
        // Root-level fields (backward compatibility)
        { $"players/{playerId}/oneVsOneMmr", oneVsOneMmr },
        { $"players/{playerId}/oneVsOneWins", oneVsOneWins },
        { $"players/{playerId}/oneVsOneLosses", oneVsOneLosses },
        
        // Nested stats structure (new format)
        { $"players/{playerId}/stats/ranked_1v1", oneVsOneStats },
        { $"players/{playerId}/stats/ranked_2v2", twoVsTwoStats },
        
        // Timestamp
        { $"players/{playerId}/lastPlayed", ServerValue.Timestamp }
    };
    
    FirebaseDatabase.DefaultInstance.RootReference.UpdateChildrenAsync(updates);
}
```

### Load Process

```csharp
public void LoadPlayerStats()
{
    // Try Firebase first
    FirebaseManager.Instance.LoadPlayerData();
    
    // If Firebase fails, load from PlayerPrefs
    if (!dataLoaded)
    {
        LoadFromPlayerPrefs();
    }
}
```

---

## Rank Change Events

### Event System

```csharp
public UnityEvent<int> OnMMRChanged;
public UnityEvent<RankTier, int> OnRankChanged;
public UnityEvent<bool> OnRankUp;
```

### Rank Change Detection

```csharp
public void ApplyMatchResult(bool won, int opponentMMR, MatchMode mode)
{
    // Store old rank
    RankTier oldRank = currentRank;
    int oldDivision = division;
    
    // Apply MMR change
    // ...
    
    // Update rank
    currentRank = GetRankFromMMR(currentMMR);
    division = GetDivision(currentMMR, currentRank);
    
    // Trigger MMR changed event
    OnMMRChanged?.Invoke(currentMMR);
    
    // Check if rank changed
    if (currentRank != oldRank || division != oldDivision)
    {
        OnRankChanged?.Invoke(currentRank, division);
        
        // Determine if rank up or down
        bool rankedUp = IsHigherRank(currentRank, division, oldRank, oldDivision);
        OnRankUp?.Invoke(rankedUp);
    }
}
```

### UI Integration

```csharp
void Start()
{
    RankingSystem.Instance.OnMMRChanged.AddListener(UpdateMMRDisplay);
    RankingSystem.Instance.OnRankChanged.AddListener(UpdateRankDisplay);
    RankingSystem.Instance.OnRankUp.AddListener(ShowRankChangeAnimation);
}

void UpdateMMRDisplay(int newMMR)
{
    mmrText.text = $"MMR: {newMMR}";
}

void UpdateRankDisplay(RankTier rank, int division)
{
    rankText.text = $"{rank} {division}";
    rankImage.color = GetRankColor(rank);
}

void ShowRankChangeAnimation(bool rankedUp)
{
    if (rankedUp)
    {
        // Show rank up animation
        PlaySound("RankUp");
        ShowParticles(rankUpEffect);
    }
    else
    {
        // Show rank down animation
        PlaySound("RankDown");
    }
}
```

---

## Mode Switching

### Current Mode

```csharp
private MatchMode currentMode = MatchMode.OneVsOne;

public void SetCurrentMode(MatchMode mode)
{
    currentMode = mode;
    
    // Update displayed stats
    if (mode == MatchMode.OneVsOne)
    {
        currentMMR = oneVsOneMmr;
        currentRank = GetRankFromMMR(oneVsOneMmr);
        division = GetDivision(oneVsOneMmr, currentRank);
    }
    else
    {
        currentMMR = twoVsTwoMmr;
        currentRank = GetRankFromMMR(twoVsTwoMmr);
        division = GetDivision(twoVsTwoMmr, currentRank);
    }
    
    // Update UI
    OnMMRChanged?.Invoke(currentMMR);
    OnRankChanged?.Invoke(currentRank, division);
}
```

### Getting MMR for Mode

```csharp
public int GetMMRForMode(MatchMode mode)
{
    return mode == MatchMode.OneVsOne ? oneVsOneMmr : twoVsTwoMmr;
}
```

---

## MMR Boundaries

### Minimum and Maximum

```csharp
private int minMMR = 0;
private int maxMMR = 3000;

// Clamp MMR after changes
currentMMR = Mathf.Clamp(currentMMR, minMMR, maxMMR);
```

### Why 3000 Cap?

1. **Prevents inflation:** Keeps ratings meaningful
2. **Matchmaking:** Easier to find opponents
3. **Psychological:** Achievable goal for top players
4. **Balance:** Prevents extreme rating differences

### Soft Cap Behavior

```csharp
// At 3000 MMR, gains are minimal
// Expected score against 2600 opponent: 0.91
// Win: +3 MMR (32 × (1.0 - 0.91))
// Loss: -29 MMR (32 × (0.0 - 0.91))
```

---

## Integration with Other Systems

### With Matchmaking System
```
Get MMR for mode → Used in queue entry
Match found → Opponent MMR stored
Match ends → Calculate MMR change
```

### With Firebase System
```
Load stats on login → Firebase query
Save stats after match → Firebase update
Backup to PlayerPrefs → Local storage
```

### With UI System
```
MMR changed → Update displays
Rank changed → Update rank badge
Rank up → Show animation
```

### With Match System
```
Match ends → Server sends results
Client calculates → MMR change
Save to Firebase → Persist data
```

---

## Best Practices

1. **Always clamp MMR** to min/max range
2. **Save after every match** to prevent data loss
3. **Use separate MMR** for different modes
4. **Validate opponent MMR** before calculation
5. **Handle Firebase errors** gracefully
6. **Provide visual feedback** on rank changes
7. **Track highest MMR** for player motivation
8. **Calculate client-side** for accurate results
9. **Use atomic updates** when saving to Firebase
10. **Test MMR formula** with edge cases

---

## Debugging

### Enable Debug Logs
```csharp
public bool showDebugLogs = true;

public void ApplyMatchResult(bool won, int opponentMMR, MatchMode mode)
{
    if (showDebugLogs)
    {
        Debug.Log($"Match Result: {(won ? "WIN" : "LOSS")}");
        Debug.Log($"My MMR: {currentMMR}, Opponent MMR: {opponentMMR}");
        Debug.Log($"Expected Score: {CalculateExpectedScore(currentMMR, opponentMMR):F2}");
        Debug.Log($"MMR Change: {mmrChange:+0;-0}");
        Debug.Log($"New MMR: {currentMMR}");
        Debug.Log($"New Rank: {currentRank} {division}");
    }
}
```

### MMR Calculator Tool
```csharp
[ContextMenu("Test MMR Calculation")]
void TestMMRCalculation()
{
    int myMMR = 600;
    int opponentMMR = 700;
    
    Debug.Log("=== MMR Calculation Test ===");
    Debug.Log($"My MMR: {myMMR}");
    Debug.Log($"Opponent MMR: {opponentMMR}");
    
    float expected = CalculateExpectedScore(myMMR, opponentMMR);
    Debug.Log($"Expected Score: {expected:F2}");
    
    int winChange = CalculateMMRChange(true, opponentMMR, myMMR);
    int lossChange = CalculateMMRChange(false, opponentMMR, myMMR);
    
    Debug.Log($"If Win: {winChange:+0;-0} → {myMMR + winChange}");
    Debug.Log($"If Loss: {lossChange:+0;-0} → {myMMR + lossChange}");
}
```

---

*This documentation explains the ranking system architecture and functionality. For implementation details, see `RankingSystem.cs`.*
