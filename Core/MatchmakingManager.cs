using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;

#if FIREBASE_INSTALLED
using Firebase;
using Firebase.Database;
#endif

/// <summary>
/// Handles matchmaking queue and player matching
/// Supports 1v1 and 2v2 modes with ranking-based matchmaking
/// </summary>
public class MatchmakingManager : MonoBehaviour
{
    public static MatchmakingManager Instance { get; private set; }
    
    [Header("Matchmaking Settings")]
    public MatchMode currentMode = MatchMode.OneVsOne;
    public int rankingRange = 200; // Max ranking difference for matchmaking
    public float searchTimeout = 60f; // Seconds before expanding search
    public float expandRangeInterval = 10f; // Expand range every X seconds
    
    [Header("Queue Status")]
    public bool isInQueue = false;
    public float queueTime = 0f;
    public int currentRankingRange;
    
    [Header("Events")]
    public UnityEvent OnQueueJoined;
    public UnityEvent OnQueueLeft;
    public UnityEvent<MatchData> OnMatchFound;
    public UnityEvent OnMatchmakingFailed;
    
    private float queueStartTime;
    private float lastRangeExpansion;
    
    // Firebase references (will be set when Firebase SDK is added)
    // private DatabaseReference queueRef;
    
    public enum MatchMode
    {
        OneVsOne,
        TwoVsTwo
    }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnDestroy()
    {
        // Clean up Firebase data when destroyed
        if (Instance == this)
        {
            LeaveQueue();
        }
    }
    
    private void OnApplicationQuit()
    {
        // Clean up Firebase data when quitting
        LeaveQueue();
    }
    
    private void Update()
    {
        if (isInQueue)
        {
            queueTime = Time.time - queueStartTime;
            
            // Expand search range over time
            if (Time.time - lastRangeExpansion >= expandRangeInterval)
            {
                ExpandSearchRange();
                lastRangeExpansion = Time.time;
            }
            
            // Timeout
            if (queueTime >= searchTimeout)
            {
                Debug.LogWarning("Matchmaking timeout!");
                LeaveQueue();
                OnMatchmakingFailed?.Invoke();
            }
        }
    }
    
    /// <summary>
    /// Join matchmaking queue
    /// </summary>
    public void JoinQueue(MatchMode mode)
    {
        if (!FirebaseManager.Instance.isSignedIn)
        {
            Debug.LogError("Must be signed in to join queue!");
            return;
        }
        
        if (isInQueue)
        {
            Debug.LogWarning("Already in queue!");
            return;
        }
        
        // Security: Rate limiting
        if (SecurityManager.Instance != null)
        {
            string playerId = FirebaseManager.Instance.playerId;
            if (!SecurityManager.Instance.CanAttemptMatchmaking(playerId))
            {
                Debug.LogWarning("Too many matchmaking attempts. Please wait a moment.");
                return;
            }
        }
        
        currentMode = mode;
        isInQueue = true;
        queueStartTime = Time.time;
        lastRangeExpansion = Time.time;
        currentRankingRange = rankingRange;
        
        OnQueueJoined?.Invoke();
        
        // Set current mode in RankingSystem and get MMR for this mode
        if (RankingSystem.Instance != null)
        {
            RankingSystem.Instance.SetCurrentMode(mode);
        }
        
        int playerMMR = RankingSystem.Instance != null ? RankingSystem.Instance.GetMMRForMode(mode) : 600;
        string rankName = RankingSystem.Instance != null ? RankingSystem.Instance.GetRankDisplayName() : "Chrome I";
        
        Debug.Log($"Joined {mode} queue | MMR: {playerMMR} ({rankName})");
        
        // Add player to Firebase queue
        AddToFirebaseQueue();
    }
    
    /// <summary>
    /// Leave matchmaking queue
    /// </summary>
    public void LeaveQueue()
    {
        if (!isInQueue) return;
        
        isInQueue = false;
        queueTime = 0f;
        
        OnQueueLeft?.Invoke();
        
        // Remove from Firebase queue
        RemoveFromFirebaseQueue();
        
        Debug.Log("Left matchmaking queue");
    }
    
    /// <summary>
    /// Add player to Firebase matchmaking queue
    /// </summary>
    private void AddToFirebaseQueue()
    {
        if (!FirebaseManager.Instance.isSignedIn)
        {
            Debug.LogError("Cannot add to queue - not signed in!");
            return;
        }
        
        // Get player MMR
        int playerMMR = RankingSystem.Instance != null ? RankingSystem.Instance.currentMMR : 600;
        
#if FIREBASE_INSTALLED
        Dictionary<string, object> queueData = new Dictionary<string, object> {
            { "playerId", FirebaseManager.Instance.playerId },
            { "playerName", FirebaseManager.Instance.playerName },
            { "mmr", playerMMR },
            { "mode", currentMode.ToString() },
            { "timestamp", ServerValue.Timestamp },
            { "searchRange", currentRankingRange }
        };
        
        string queuePath = $"matchmaking/{currentMode}/{FirebaseManager.Instance.playerId}";
        var queueRef = FirebaseDatabase.DefaultInstance.GetReference(queuePath);
        
        // Set up OnDisconnect to auto-remove if connection lost
        queueRef.OnDisconnect().RemoveValue();
        
        queueRef.SetValueAsync(queueData)
            .ContinueWith(task => {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log("Added to Firebase queue successfully");
                    ListenForMatch();
                }
                else
                {
                    Debug.LogError($"Failed to add to queue: {task.Exception}");
                }
            });
#else
        Debug.LogWarning("Firebase not installed - matchmaking disabled. Install Firebase SDK and add FIREBASE_INSTALLED to Scripting Define Symbols.");
#endif
    }
    
    /// <summary>
    /// Remove player from Firebase queue
    /// </summary>
    private void RemoveFromFirebaseQueue()
    {
        if (!FirebaseManager.Instance.isSignedIn) return;
        
#if FIREBASE_INSTALLED
        string queuePath = $"matchmaking/{currentMode}/{FirebaseManager.Instance.playerId}";
        
        // Set up OnDisconnect to auto-remove if connection lost
        var queueRef = FirebaseDatabase.DefaultInstance.GetReference(queuePath);
        queueRef.OnDisconnect().RemoveValue();
        
        // Remove immediately
        queueRef.RemoveValueAsync().ContinueWith(task => {
            if (task.IsCompleted && !task.IsFaulted)
            {
                Debug.Log("Removed from Firebase queue");
            }
            else
            {
                Debug.LogError($"Failed to remove from queue: {task.Exception}");
            }
        });
#endif
    }
    
    /// <summary>
    /// Listen for match in Firebase
    /// </summary>
    private void ListenForMatch()
    {
#if FIREBASE_INSTALLED
        string matchPath = $"matches/{FirebaseManager.Instance.playerId}";
        FirebaseDatabase.DefaultInstance
            .GetReference(matchPath)
            .ValueChanged += OnMatchDataChanged;
        
        // Also try to create a match immediately (client-side matchmaking)
        TryCreateMatch();
#endif
    }
    
#if FIREBASE_INSTALLED
    /// <summary>
    /// Called when match data changes in Firebase
    /// </summary>
    private void OnMatchDataChanged(object sender, ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError($"Firebase error: {args.DatabaseError.Message}");
            return;
        }
        
        if (!args.Snapshot.Exists) return;
        
        // Parse match data from Firebase
        var data = args.Snapshot.Value as Dictionary<string, object>;
        if (data == null) return;
        
        // Create MatchData from Firebase data
        MatchData matchData = ParseMatchDataFromFirebase(data);
        
        if (matchData != null)
        {
            isInQueue = false;
            OnMatchFound?.Invoke(matchData);
            Debug.Log($"Match found! {matchData.GetMatchSummary()}");
            
            // Stop listening
            FirebaseDatabase.DefaultInstance
                .GetReference($"matches/{FirebaseManager.Instance.playerId}")
                .ValueChanged -= OnMatchDataChanged;
        }
    }
#endif
    
#if FIREBASE_INSTALLED
    /// <summary>
    /// Parse MatchData from Firebase dictionary
    /// </summary>
    private MatchData ParseMatchDataFromFirebase(Dictionary<string, object> data)
    {
        try
        {
            MatchData matchData = new MatchData
            {
                matchId = data["matchId"].ToString(),
                mode = (MatchMode)System.Enum.Parse(typeof(MatchMode), data["mode"].ToString())
            };
            
            // Parse team 1 players
            var team1Data = data["team1Players"] as List<object>;
            if (team1Data != null)
            {
                foreach (var playerObj in team1Data)
                {
                    var playerDict = playerObj as Dictionary<string, object>;
                    if (playerDict != null)
                    {
                        matchData.team1Players.Add(new PlayerData
                        {
                            playerId = playerDict["playerId"].ToString(),
                            playerName = playerDict["playerName"].ToString(),
                            ranking = int.Parse(playerDict["ranking"].ToString())
                        });
                    }
                }
            }
            
            // Parse team 2 players
            var team2Data = data["team2Players"] as List<object>;
            if (team2Data != null)
            {
                foreach (var playerObj in team2Data)
                {
                    var playerDict = playerObj as Dictionary<string, object>;
                    if (playerDict != null)
                    {
                        matchData.team2Players.Add(new PlayerData
                        {
                            playerId = playerDict["playerId"].ToString(),
                            playerName = playerDict["playerName"].ToString(),
                            ranking = int.Parse(playerDict["ranking"].ToString())
                        });
                    }
                }
            }
            
            return matchData;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to parse match data: {e.Message}");
            return null;
        }
    }
#endif
    
    /// <summary>
    /// Expand search range to find more players
    /// </summary>
    private void ExpandSearchRange()
    {
        currentRankingRange += 100;
        Debug.Log($"Expanded search range to ±{currentRankingRange} ranking");
        
#if FIREBASE_INSTALLED
        // Try to create match again with expanded range
        TryCreateMatch();
#endif
    }
    
#if FIREBASE_INSTALLED
    /// <summary>
    /// Client-side matchmaking - try to create a match from queue
    /// </summary>
    private void TryCreateMatch()
    {
        string queuePath = $"matchmaking/{currentMode}";
        FirebaseDatabase.DefaultInstance
            .GetReference(queuePath)
            .GetValueAsync()
            .ContinueWith(task => {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"Failed to check queue: {task.Exception}");
                    return;
                }
                
                if (!task.IsCompleted) return;
                
                var snapshot = task.Result;
                if (!snapshot.Exists) return;
                
                // Parse all players in queue
                List<QueuedPlayer> queuedPlayers = new List<QueuedPlayer>();
                foreach (var child in snapshot.Children)
                {
                    var data = child.Value as Dictionary<string, object>;
                    if (data != null)
                    {
                        queuedPlayers.Add(new QueuedPlayer
                        {
                            playerId = child.Key,
                            playerName = data["playerName"].ToString(),
                            mmr = int.Parse(data["mmr"].ToString()),
                            timestamp = data.ContainsKey("timestamp") ? long.Parse(data["timestamp"].ToString()) : 0
                        });
                    }
                }
                
                // Check if we have enough players
                int requiredPlayers = currentMode == MatchMode.OneVsOne ? 2 : 4;
                if (queuedPlayers.Count < requiredPlayers)
                {
                    Debug.Log($"Not enough players in queue ({queuedPlayers.Count}/{requiredPlayers})");
                    return;
                }
                
                // Sort by timestamp (first come, first served)
                queuedPlayers.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
                
                // Take first N players
                var matchedPlayers = queuedPlayers.GetRange(0, requiredPlayers);
                
                // Check if we're one of the matched players
                bool weAreMatched = matchedPlayers.Exists(p => p.playerId == FirebaseManager.Instance.playerId);
                if (!weAreMatched)
                {
                    Debug.Log("We're not in the first group of players");
                    return;
                }
                
                // Only the first player creates the match (to avoid duplicates)
                if (matchedPlayers[0].playerId != FirebaseManager.Instance.playerId)
                {
                    Debug.Log("Waiting for first player to create match...");
                    return;
                }
                
                Debug.Log("We're the first player - creating match!");
                CreateMatchForPlayers(matchedPlayers);
            });
    }
    
    /// <summary>
    /// Create match data for all matched players
    /// </summary>
    private void CreateMatchForPlayers(List<QueuedPlayer> players)
    {
        string matchId = FirebaseDatabase.DefaultInstance.GetReference("matches").Push().Key;
        
        // Split into balanced teams based on MMR
        int teamSize = players.Count / 2;
        List<QueuedPlayer> team1;
        List<QueuedPlayer> team2;
        
        if (currentMode == MatchMode.TwoVsTwo)
        {
            // For 2v2, balance teams by MMR
            var balancedTeams = BalanceTeamsByMMR(players);
            team1 = balancedTeams.Item1;
            team2 = balancedTeams.Item2;
            
            int team1AvgMMR = (team1[0].mmr + team1[1].mmr) / 2;
            int team2AvgMMR = (team2[0].mmr + team2[1].mmr) / 2;
            Debug.Log($"Balanced teams - Team 1 avg: {team1AvgMMR}, Team 2 avg: {team2AvgMMR}");
        }
        else
        {
            // For 1v1, just split normally
            team1 = players.GetRange(0, teamSize);
            team2 = players.GetRange(teamSize, teamSize);
        }
        
        // Create match data
        Dictionary<string, object> matchData = new Dictionary<string, object>
        {
            { "matchId", matchId },
            { "mode", currentMode.ToString() },
            { "timestamp", ServerValue.Timestamp }
        };
        
        // Add team 1 players
        List<object> team1List = new List<object>();
        foreach (var player in team1)
        {
            team1List.Add(new Dictionary<string, object>
            {
                { "playerId", player.playerId },
                { "playerName", player.playerName },
                { "ranking", player.mmr }
            });
        }
        matchData["team1Players"] = team1List;
        
        // Add team 2 players
        List<object> team2List = new List<object>();
        foreach (var player in team2)
        {
            team2List.Add(new Dictionary<string, object>
            {
                { "playerId", player.playerId },
                { "playerName", player.playerName },
                { "ranking", player.mmr }
            });
        }
        matchData["team2Players"] = team2List;
        
        // Write match data for all players and remove them from queue
        Dictionary<string, object> updates = new Dictionary<string, object>();
        foreach (var player in players)
        {
            updates[$"matches/{player.playerId}"] = matchData;
            updates[$"matchmaking/{currentMode}/{player.playerId}"] = null;
        }
        
        // Apply all updates atomically
        FirebaseDatabase.DefaultInstance
            .GetReference("/")
            .UpdateChildrenAsync(updates)
            .ContinueWith(task => {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"Match {matchId} created successfully!");
                }
                else
                {
                    Debug.LogError($"Failed to create match: {task.Exception}");
                }
            });
    }
    
    /// <summary>
    /// Balance 4 players into 2 teams with closest average MMR
    /// </summary>
    private (List<QueuedPlayer>, List<QueuedPlayer>) BalanceTeamsByMMR(List<QueuedPlayer> players)
    {
        // For 4 players, try all possible team combinations and find the most balanced
        // Possible combinations: (0,1 vs 2,3), (0,2 vs 1,3), (0,3 vs 1,2)
        
        int bestDifference = int.MaxValue;
        List<QueuedPlayer> bestTeam1 = null;
        List<QueuedPlayer> bestTeam2 = null;
        
        // Combination 1: Players 0,1 vs 2,3
        int avg1 = (players[0].mmr + players[1].mmr) / 2;
        int avg2 = (players[2].mmr + players[3].mmr) / 2;
        int diff1 = Mathf.Abs(avg1 - avg2);
        
        if (diff1 < bestDifference)
        {
            bestDifference = diff1;
            bestTeam1 = new List<QueuedPlayer> { players[0], players[1] };
            bestTeam2 = new List<QueuedPlayer> { players[2], players[3] };
        }
        
        // Combination 2: Players 0,2 vs 1,3
        avg1 = (players[0].mmr + players[2].mmr) / 2;
        avg2 = (players[1].mmr + players[3].mmr) / 2;
        int diff2 = Mathf.Abs(avg1 - avg2);
        
        if (diff2 < bestDifference)
        {
            bestDifference = diff2;
            bestTeam1 = new List<QueuedPlayer> { players[0], players[2] };
            bestTeam2 = new List<QueuedPlayer> { players[1], players[3] };
        }
        
        // Combination 3: Players 0,3 vs 1,2
        avg1 = (players[0].mmr + players[3].mmr) / 2;
        avg2 = (players[1].mmr + players[2].mmr) / 2;
        int diff3 = Mathf.Abs(avg1 - avg2);
        
        if (diff3 < bestDifference)
        {
            bestDifference = diff3;
            bestTeam1 = new List<QueuedPlayer> { players[0], players[3] };
            bestTeam2 = new List<QueuedPlayer> { players[1], players[2] };
        }
        
        Debug.Log($"Best team balance found with MMR difference: {bestDifference}");
        return (bestTeam1, bestTeam2);
    }
    
    /// <summary>
    /// Helper class for queued player data
    /// </summary>
    private class QueuedPlayer
    {
        public string playerId;
        public string playerName;
        public int mmr;
        public long timestamp;
    }
#endif
    
    
    /// <summary>
    /// Get estimated queue time based on mode and ranking
    /// </summary>
    public string GetEstimatedQueueTime()
    {
        // This would be calculated based on historical data
        // For now, return estimate based on mode
        return currentMode == MatchMode.OneVsOne ? "~30s" : "~60s";
    }
}

/// <summary>
/// Data structure for a matched game
/// </summary>
[System.Serializable]
public class MatchData
{
    public string matchId;
    public MatchmakingManager.MatchMode mode;
    public List<PlayerData> team1Players = new List<PlayerData>();
    public List<PlayerData> team2Players = new List<PlayerData>();
    
    public string GetMatchSummary()
    {
        string team1Names = string.Join(", ", team1Players.ConvertAll(p => p.playerName));
        string team2Names = string.Join(", ", team2Players.ConvertAll(p => p.playerName));
        return $"{mode}: {team1Names} vs {team2Names}";
    }
    
    public int GetAverageRanking(int team)
    {
        List<PlayerData> players = team == 1 ? team1Players : team2Players;
        int total = 0;
        foreach (var player in players)
        {
            total += player.ranking;
        }
        return players.Count > 0 ? total / players.Count : 0;
    }
}

/// <summary>
/// Data structure for player info
/// </summary>
[System.Serializable]
public class PlayerData
{
    public string playerId;
    public string playerName;
    public int ranking;
}
