using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Unity.Netcode;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager Instance { get; private set; }
    
    [Header("Match Settings")]
    public float matchDuration = 240f;  // 4 minutes
    public float respawnDelay = 3.5f;   // Longer respawn penalty (was 2f)
    public float countdownDuration = 3f; // Countdown before match starts
    public bool useCountdown = true;
    
    [Header("UI")]
    public Scoreboard scoreboard;
    
    [Header("Spawn System")]
    public DynamicSpawnSystem dynamicSpawnSystem;
    public bool useDynamicSpawns = true;
    
    [Header("Fallback Static Spawns (if not using dynamic)")]
    public Transform[] team1Spawns;
    public Transform[] team2Spawns;
    
    [Header("Events")]
    public UnityEvent<int, int> OnScoreChanged; // team1Score, team2Score
    public UnityEvent<float> OnTimeChanged; // remainingTime
    public UnityEvent<int> OnMatchEnd; // winningTeam (1 or 2, 0 for tie)
    public UnityEvent<int> OnCountdown; // countdown number (3, 2, 1, 0=GO)
    public UnityEvent OnMatchStart; // fired when countdown finishes
    
    [Header("Match Data")]
    private MatchData currentMatchData; // Store match data for MMR calculation
    
    private NetworkVariable<float> matchTimeRemaining = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> matchActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> countdownActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> countdownTimeRemaining = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> team1Score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> team2Score = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private Dictionary<PlayerHealth, int> playerTeams = new Dictionary<PlayerHealth, int>();
    private Dictionary<PlayerHealth, float> respawnTimers = new Dictionary<PlayerHealth, float>();
    private Dictionary<PlayerHealth, string> playerIds = new Dictionary<PlayerHealth, string>();
    private HashSet<ulong> readyPlayers = new HashSet<ulong>();
    private int expectedPlayerCount = 0;
    private bool waitingForPlayers = false;
    private float matchStartTime = 0f; // Track when match started for security validation
    
    public float MatchTimeRemaining => matchTimeRemaining.Value;
    public bool IsMatchActive => matchActive.Value;
    public bool MatchActive => matchActive.Value;
    public bool CountdownActive => countdownActive.Value;
    public int Team1Score => team1Score.Value;
    public int Team2Score => team2Score.Value;
    
    [Header("Auto Start")]
    public bool autoStartMatch = false; // Set to true only for local testing
    public float waitForPlayersTimeout = 30f; // Max time to wait for all players
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Subscribe to NetworkVariable changes on all clients
        team1Score.OnValueChanged += OnTeam1ScoreChanged;
        team2Score.OnValueChanged += OnTeam2ScoreChanged;
        matchTimeRemaining.OnValueChanged += OnMatchTimeChanged;
        
        // Initialize NetworkVariables after spawn (server only)
        if (IsServer)
        {
            matchTimeRemaining.Value = matchDuration;
            
            if (autoStartMatch)
            {
                Debug.Log("[SERVER] Auto-starting match (bypassing ready system)");
                StartMatch();
            }
        }
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Unsubscribe from NetworkVariable changes
        team1Score.OnValueChanged -= OnTeam1ScoreChanged;
        team2Score.OnValueChanged -= OnTeam2ScoreChanged;
        matchTimeRemaining.OnValueChanged -= OnMatchTimeChanged;
    }
    
    private void OnTeam1ScoreChanged(int oldValue, int newValue)
    {
        OnScoreChanged?.Invoke(team1Score.Value, team2Score.Value);
    }
    
    private void OnTeam2ScoreChanged(int oldValue, int newValue)
    {
        OnScoreChanged?.Invoke(team1Score.Value, team2Score.Value);
    }
    
    private void OnMatchTimeChanged(float oldValue, float newValue)
    {
        OnTimeChanged?.Invoke(newValue);
    }
    
    private void Start()
    {
        // Don't initialize NetworkVariables here - wait for OnNetworkSpawn
    }
    
    [ClientRpc]
    private void NotifyMatchStartClientRpc()
    {
        // Only invoke on clients (host already invoked locally)
        if (!IsServer)
        {
            OnMatchStart?.Invoke();
            Debug.Log("[CLIENT] Match started via ClientRpc!");
        }
    }
    
    [ClientRpc]
    private void NotifyCountdownStartClientRpc(float duration)
    {
        // Invoke countdown event on all clients
        OnCountdown?.Invoke(Mathf.CeilToInt(duration));
        Debug.Log($"[CLIENT] Countdown started: {duration}s");
    }
    
    [ClientRpc]
    private void NotifyCountdownTickClientRpc(int countdownNumber)
    {
        // Update countdown display on all clients
        OnCountdown?.Invoke(countdownNumber);
    }
    
    
    private void Update()
    {
        // Only server should update NetworkVariables
        if (!IsServer) return;
        
        // Handle countdown
        if (countdownActive.Value)
        {
            // Server updates countdown
            countdownTimeRemaining.Value -= Time.deltaTime;
            
            int currentNumber = Mathf.CeilToInt(countdownTimeRemaining.Value);
            int previousNumber = Mathf.CeilToInt(countdownTimeRemaining.Value + Time.deltaTime);
            
            // Broadcast countdown number changes to all clients
            if (currentNumber != previousNumber && currentNumber > 0)
            {
                OnCountdown?.Invoke(currentNumber);
                NotifyCountdownTickClientRpc(currentNumber);
            }
            
            if (countdownTimeRemaining.Value <= 0f)
            {
                countdownActive.Value = false;
                matchActive.Value = true;
                matchStartTime = Time.time; // Record match start time for security validation
                OnCountdown?.Invoke(0); // 0 = GO!
                OnMatchStart?.Invoke();
                Debug.Log("Match started!");
                
                // Notify all clients that match has started (only if spawned)
                if (IsSpawned)
                {
                    NotifyMatchStartClientRpc();
                }
            }
            return;
        }
        
        if (!matchActive.Value) return;
        
        // Server updates match timer
        matchTimeRemaining.Value -= Time.deltaTime;
        OnTimeChanged?.Invoke(matchTimeRemaining.Value);
        
        if (matchTimeRemaining.Value <= 0f)
        {
            EndMatch();
            return;
        }
        
        // Handle respawn timers
        List<PlayerHealth> toRespawn = new List<PlayerHealth>();
        foreach (var kvp in respawnTimers)
        {
            if (kvp.Value <= Time.time)
            {
                toRespawn.Add(kvp.Key);
            }
        }
        
        foreach (var player in toRespawn)
        {
            RespawnPlayer(player);
            respawnTimers.Remove(player);
        }
    }
    
    public void StartMatch()
    {
        if (!IsSpawned)
        {
            Debug.LogError("[MATCHMANAGER] StartMatch called but NetworkObject not spawned! This should not happen.");
            return;
        }
        
        Debug.Log("[MATCHMANAGER] StartMatch called - waiting for all players to be ready");
        
        // Initialize match state but don't start countdown yet
        matchTimeRemaining.Value = matchDuration;
        team1Score.Value = 0;
        team2Score.Value = 0;
        
        OnScoreChanged?.Invoke(team1Score.Value, team2Score.Value);
        OnTimeChanged?.Invoke(matchTimeRemaining.Value);
        
        // Don't start countdown/match yet - wait for all players to be ready
        // The countdown/match will start in OnPlayerReady when all players are ready
    }
    
    /// <summary>
    /// Actually start the countdown or match (called when all players are ready)
    /// </summary>
    private void BeginMatchCountdown()
    {
        if (useCountdown)
        {
            countdownActive.Value = true;
            countdownTimeRemaining.Value = countdownDuration;
            OnCountdown?.Invoke(Mathf.CeilToInt(countdownDuration));
            NotifyCountdownStartClientRpc(countdownDuration);
            Debug.Log("Match countdown started!");
        }
        else
        {
            matchActive.Value = true;
            matchStartTime = Time.time; // Record match start time for security validation
            OnMatchStart?.Invoke();
            Debug.Log("Match started!");
            
            // Notify all clients that match has started
            NotifyMatchStartClientRpc();
        }
    }
    
    /// <summary>
    /// Called by NetworkPlayer when a player is ready
    /// </summary>
    public void OnPlayerReady(ulong clientId)
    {
        // MatchManager only exists on server, no need for IsServer check
        Debug.Log($"[MATCHMANAGER] OnPlayerReady called for client {clientId}");
        
        if (readyPlayers.Contains(clientId))
        {
            Debug.LogWarning($"Player {clientId} already marked as ready!");
            return;
        }
        
        readyPlayers.Add(clientId);
        Debug.Log($"[SERVER] Player {clientId} is ready. {readyPlayers.Count}/{expectedPlayerCount} players ready.");
        
        // Check if all players are ready
        if (waitingForPlayers && readyPlayers.Count >= expectedPlayerCount)
        {
            Debug.Log("[SERVER] All players ready! Starting countdown...");
            waitingForPlayers = false;
            CancelInvoke(nameof(StartMatchTimeout)); // Cancel timeout
            BeginMatchCountdown(); // Start countdown now that all players are ready
        }
    }
    
    /// <summary>
    /// Set expected player count and start waiting for players
    /// </summary>
    public void SetExpectedPlayerCount(int count)
    {
        // MatchManager only exists on server, no need for IsServer check
        Debug.Log($"[MATCHMANAGER] SetExpectedPlayerCount called with count: {count}");
        
        expectedPlayerCount = count;
        waitingForPlayers = true;
        readyPlayers.Clear();
        
        Debug.Log($"[SERVER] Waiting for {expectedPlayerCount} players to be ready... (Timeout: {waitForPlayersTimeout}s)");
        
        // Start timeout timer
        Invoke(nameof(StartMatchTimeout), waitForPlayersTimeout);
    }
    
    private void StartMatchTimeout()
    {
        if (waitingForPlayers)
        {
            Debug.LogWarning($"Match start timeout! Only {readyPlayers.Count}/{expectedPlayerCount} players ready. Starting anyway...");
            waitingForPlayers = false;
            StartMatch();
        }
    }
    
    public void RegisterPlayer(PlayerHealth player, int team)
    {
        if (team != 1 && team != 2)
        {
            Debug.LogError($"Invalid team number: {team}. Must be 1 or 2.");
            return;
        }
        
        playerTeams[player] = team;
        
        // Subscribe to death event (only on server)
        if (IsServer)
        {
            player.OnDeath.AddListener((wasHeadshot) => OnPlayerDeath(player, wasHeadshot));
        }
        
        // Add to scoreboard
        if (scoreboard != null)
        {
            string playerId = player.gameObject.GetInstanceID().ToString();
            string playerName = player.gameObject.name;
            playerIds[player] = playerId;
            scoreboard.AddPlayer(playerId, playerName, team);
        }
        
        // Spawn player
        RespawnPlayer(player);
    }
    
    private void OnPlayerDeath(PlayerHealth deadPlayer, bool wasHeadshot)
    {
        // Only server should handle death logic
        if (!IsServer) return;
        if (!matchActive.Value) return;
        
        // Find who killed them (for now, just award point to opposite team)
        // In a real implementation, you'd track the shooter
        int deadPlayerTeam = playerTeams[deadPlayer];
        int scoringTeam = deadPlayerTeam == 1 ? 2 : 1;
        
        AddScore(scoringTeam);
        
        // Update scoreboard stats for dead player
        if (scoreboard != null && playerIds.ContainsKey(deadPlayer))
        {
            string playerId = playerIds[deadPlayer];
            scoreboard.UpdatePlayerStats(playerId, deadPlayer.Kills, deadPlayer.Deaths, 0);
        }
        
        // Update scoreboard for all players on the scoring team (they got the kill)
        if (scoreboard != null)
        {
            foreach (var kvp in playerTeams)
            {
                if (kvp.Value == scoringTeam && kvp.Key != deadPlayer)
                {
                    PlayerHealth killer = kvp.Key;
                    if (playerIds.ContainsKey(killer))
                    {
                        string killerId = playerIds[killer];
                        scoreboard.UpdatePlayerStats(killerId, killer.Kills, killer.Deaths, 0);
                    }
                }
            }
        }
        
        // Schedule respawn
        respawnTimers[deadPlayer] = Time.time + respawnDelay;
        
        Debug.Log($"Player on Team {deadPlayerTeam} died. Team {scoringTeam} scores! Headshot: {wasHeadshot}");
    }
    
    private void AddScore(int team)
    {
        // MatchManager only exists on server
        if (team == 1)
        {
            team1Score.Value++;
        }
        else if (team == 2)
        {
            team2Score.Value++;
        }
        
        // NetworkVariable change callbacks will automatically notify all clients
    }
    
    private void RespawnPlayer(PlayerHealth player)
    {
        if (!playerTeams.ContainsKey(player)) return;
        
        int team = playerTeams[player];
        
        // Use dynamic spawn system if available
        if (useDynamicSpawns && dynamicSpawnSystem != null)
        {
            // Get teammate for 2v2 spawning
            GameObject teammate = GetTeammate(player);
            
            // Get death location if available
            Vector3? deathLocation = player.transform.position;
            
            // Use dynamic spawn (now returns position AND rotation for surface orientation)
            var spawnData = dynamicSpawnSystem.GetBestSpawnPosition(player.gameObject, deathLocation, teammate);
            
            if (spawnData.HasValue)
            {
                player.Respawn(spawnData.Value.position, spawnData.Value.rotation);
                Debug.Log($"Player respawned on Team {team} using dynamic spawn (surface-oriented)");
            }
            else
            {
                Debug.LogError("Dynamic spawn failed! Falling back to static spawns.");
                RespawnPlayerStatic(player, team);
            }
        }
        else
        {
            // Use static spawns
            RespawnPlayerStatic(player, team);
        }
    }
    
    private void RespawnPlayerStatic(PlayerHealth player, int team)
    {
        Transform[] spawns = team == 1 ? team1Spawns : team2Spawns;
        
        if (spawns == null || spawns.Length == 0)
        {
            Debug.LogError($"No spawn points defined for team {team}!");
            return;
        }
        
        // Pick random spawn
        Transform spawnPoint = spawns[Random.Range(0, spawns.Length)];
        player.Respawn(spawnPoint.position, spawnPoint.rotation);
        
        Debug.Log($"Player respawned on Team {team} using static spawn");
    }
    
    private GameObject GetTeammate(PlayerHealth player)
    {
        int playerTeam = playerTeams[player];
        
        foreach (var kvp in playerTeams)
        {
            if (kvp.Key != player && kvp.Value == playerTeam && !kvp.Key.IsDead)
            {
                return kvp.Key.gameObject;
            }
        }
        
        return null;
    }
    
    public void EndMatch()
    {
        // MatchManager only exists on server
        matchActive.Value = false;
        
        // Security: Validate match results
        float matchDuration = Time.time - matchStartTime;
        if (SecurityManager.Instance != null)
        {
            if (!SecurityManager.Instance.ValidateMatchResult(team1Score.Value, team2Score.Value, matchDuration))
            {
                Debug.LogError("Invalid match result detected! Not applying MMR changes.");
                NotifyMatchEndClientRpc(0, 600, 600, 0); // No winner, default MMRs, no mode
                CleanupMatchData();
                return;
            }
        }
        
        int winner = 0;
        if (team1Score.Value > team2Score.Value)
        {
            winner = 1;
        }
        else if (team2Score.Value > team1Score.Value)
        {
            winner = 2;
        }
        
        // Get opponent MMR for MMR calculation
        int team1AvgMMR = currentMatchData != null ? currentMatchData.GetAverageRanking(1) : 600;
        int team2AvgMMR = currentMatchData != null ? currentMatchData.GetAverageRanking(2) : 600;
        MatchmakingManager.MatchMode matchMode = currentMatchData != null ? currentMatchData.mode : MatchmakingManager.MatchMode.OneVsOne;
        
        // Notify all clients about match end and let them calculate their own MMR
        NotifyMatchEndClientRpc(winner, team1AvgMMR, team2AvgMMR, (int)matchMode);
        
        string result = winner == 0 ? "It's a tie!" : $"Team {winner} wins!";
        Debug.Log($"[SERVER] Match ended! {result} (Team 1: {team1Score.Value}, Team 2: {team2Score.Value})");
        
        // Clean up Firebase match data
        CleanupMatchData();
    }
    
    /// <summary>
    /// End match with forfeit when a player disconnects
    /// </summary>
    public void EndMatchWithForfeit(int winningTeam)
    {
        // MatchManager only exists on server
        matchActive.Value = false;
        
        Debug.Log($"[SERVER] Match ended by forfeit. Team {winningTeam} wins!");
        
        // Get opponent MMR for MMR calculation
        int team1AvgMMR = currentMatchData != null ? currentMatchData.GetAverageRanking(1) : 600;
        int team2AvgMMR = currentMatchData != null ? currentMatchData.GetAverageRanking(2) : 600;
        MatchmakingManager.MatchMode matchMode = currentMatchData != null ? currentMatchData.mode : MatchmakingManager.MatchMode.OneVsOne;
        
        // Notify all clients about match end (forfeit counts as win/loss for MMR)
        NotifyMatchEndClientRpc(winningTeam, team1AvgMMR, team2AvgMMR, (int)matchMode);
        
        // Clean up Firebase match data
        CleanupMatchData();
    }
    
    /// <summary>
    /// Notify all clients that the match has ended and apply MMR changes
    /// </summary>
    [ClientRpc]
    private void NotifyMatchEndClientRpc(int winner, int team1AvgMMR, int team2AvgMMR, int matchModeInt)
    {
        Debug.Log($"[CLIENT] Match end notification received. Winner: Team {winner}");
        
        // Calculate MMR change for this client
        if (NetworkGameManager.Instance != null && RankingSystem.Instance != null)
        {
            int myTeam = NetworkGameManager.Instance.GetLocalPlayerTeam();
            bool iWon = (myTeam == winner);
            int opponentMMR = (myTeam == 1) ? team2AvgMMR : team1AvgMMR;
            MatchmakingManager.MatchMode matchMode = (MatchmakingManager.MatchMode)matchModeInt;
            
            // Apply MMR change
            RankingSystem.Instance.ApplyMatchResult(iWon, opponentMMR, matchMode);
            
            string result = iWon ? "Victory" : "Defeat";
            Debug.Log($"[CLIENT] {result}! New MMR: {RankingSystem.Instance.currentMMR} ({RankingSystem.Instance.GetRankDisplayName()})");
        }
        
        // Trigger match end UI
        OnMatchEnd?.Invoke(winner);
    }
    
    /// <summary>
    /// Clean up Firebase match data for all players
    /// </summary>
    private void CleanupMatchData()
    {
        if (currentMatchData == null) return;
        
#if FIREBASE_INSTALLED
        Debug.Log("[SERVER] Cleaning up Firebase match data...");
        
        // Delete match data for all players in the match
        var updates = new Dictionary<string, object>();
        
        // Team 1 players
        foreach (var player in currentMatchData.team1Players)
        {
            updates[$"matches/{player.playerId}"] = null;
        }
        
        // Team 2 players
        foreach (var player in currentMatchData.team2Players)
        {
            updates[$"matches/{player.playerId}"] = null;
        }
        
        Firebase.Database.FirebaseDatabase.DefaultInstance
            .RootReference
            .UpdateChildrenAsync(updates)
            .ContinueWith(task => {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log("[SERVER] Successfully cleaned up Firebase match data");
                }
                else
                {
                    Debug.LogError($"[SERVER] Failed to cleanup Firebase match data: {task.Exception}");
                }
            });
#endif
    }
    
    /// <summary>
    /// Set match data (called when match starts from matchmaking)
    /// </summary>
    public void SetMatchData(MatchData matchData)
    {
        currentMatchData = matchData;
    }
    
    public int GetPlayerTeam(PlayerHealth player)
    {
        return playerTeams.ContainsKey(player) ? playerTeams[player] : 0;
    }
}
