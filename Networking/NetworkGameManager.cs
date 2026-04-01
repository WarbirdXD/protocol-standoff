using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Manages network connection and player spawning for matches
/// Integrates with Firebase matchmaking system
/// </summary>
public class NetworkGameManager : MonoBehaviour
{
    public static NetworkGameManager Instance { get; private set; }
    
    [Header("Network Settings")]
    public GameObject playerPrefab;
    public DynamicSpawnSystem dynamicSpawnSystem;
    
    [Header("Match Info")]
    private MatchData currentMatchData;
    private int localPlayerTeam = 0;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // CRITICAL: Disable auto player spawning IMMEDIATELY in Awake - before NetworkManager starts
        // This must happen before any NetworkManager.StartHost/StartClient calls
        if (NetworkManager.Singleton != null)
        {
            var config = NetworkManager.Singleton.NetworkConfig;
            
            if (config.PlayerPrefab != null)
            {
                Debug.LogWarning($"NetworkManager had PlayerPrefab assigned: {config.PlayerPrefab.name}. Removing it to prevent auto-spawn.");
            }
            
            config.PlayerPrefab = null;
            config.EnableSceneManagement = true; // Enable to sync scene objects like MatchManager
            
            // Set the EOSNetcodeTransport as the network transport
            var eosTransport = NetworkManager.Singleton.GetComponent<EOSNetcodeTransport>();
            if (eosTransport != null)
            {
                config.NetworkTransport = eosTransport;
                Debug.Log("NetworkGameManager: EOSNetcodeTransport set as network transport");
            }
            else
            {
                Debug.LogError("EOSNetcodeTransport component not found on NetworkManager!");
            }
            
            Debug.Log("NetworkGameManager: Auto-spawn disabled in Awake - PlayerPrefab set to null, SceneManagement enabled");
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton is null in Awake! Make sure NetworkManager exists in the scene.");
        }
    }
    
    private void Start()
    {
        // Subscribe to network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            
            Debug.Log("NetworkGameManager: Manual spawn enabled");
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from network events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        }
        
        // Clean up match data when leaving
        CleanupOnExit();
    }
    
    private void OnApplicationQuit()
    {
        CleanupOnExit();
    }
    
    /// <summary>
    /// Clean up Firebase match data when player leaves
    /// </summary>
    private void CleanupOnExit()
    {
#if FIREBASE_INSTALLED
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.isSignedIn && currentMatchData != null)
        {
            // Delete the entire match entry
            Firebase.Database.FirebaseDatabase.DefaultInstance
                .GetReference($"matches/{currentMatchData.matchId}")
                .RemoveValueAsync();
            Debug.Log($"Cleaned up match {currentMatchData.matchId} on exit");
        }
#endif
    }
    
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} disconnected");
        
        // If we're the server and a client disconnects, end the match
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            var matchManager = FindFirstObjectByType<MatchManager>();
            if (matchManager != null && matchManager.IsMatchActive)
            {
                Debug.Log("Player disconnected during active match - ending match as forfeit");
                
                // Determine which team the disconnected player was on
                int disconnectedPlayerTeam = GetDisconnectedPlayerTeam(clientId);
                
                // Award win to the other team (forfeit)
                if (disconnectedPlayerTeam != 0)
                {
                    int winningTeam = disconnectedPlayerTeam == 1 ? 2 : 1;
                    Debug.Log($"Team {disconnectedPlayerTeam} forfeited. Team {winningTeam} wins by forfeit.");
                    matchManager.EndMatchWithForfeit(winningTeam);
                }
                else
                {
                    // If we can't determine team, just end the match normally
                    Debug.LogWarning("Could not determine disconnected player's team. Ending match normally.");
                    matchManager.EndMatch();
                }
            }
        }
    }
    
    /// <summary>
    /// Get the team of a disconnected player
    /// </summary>
    private int GetDisconnectedPlayerTeam(ulong clientId)
    {
        // Find all NetworkPlayer objects
        var networkPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
        
        foreach (var networkPlayer in networkPlayers)
        {
            if (networkPlayer.OwnerClientId == clientId)
            {
                return networkPlayer.GetTeam();
            }
        }
        
        return 0; // Unknown team
    }
    
    /// <summary>
    /// Start hosting a game (first player in match)
    /// </summary>
    public void StartHost()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null!");
            return;
        }
        
        // Ensure auto-spawn is disabled before starting
        if (NetworkManager.Singleton.NetworkConfig.PlayerPrefab != null)
        {
            Debug.LogWarning("PlayerPrefab was not null! Disabling auto-spawn...");
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
        }
        
        // Shutdown if already running (happens when testing in same instance)
        if (NetworkManager.Singleton.IsListening)
        {
            Debug.Log("NetworkManager already running - shutting down first");
            NetworkManager.Singleton.Shutdown();
            // Wait for shutdown to complete
            StartCoroutine(StartHostAfterShutdown());
            return;
        }
        
        Debug.Log($"Starting host with NetworkConfig: PlayerPrefab={NetworkManager.Singleton.NetworkConfig.PlayerPrefab}, EnableSceneManagement={NetworkManager.Singleton.NetworkConfig.EnableSceneManagement}");
        NetworkManager.Singleton.StartHost();
        Debug.Log("Started as Host");
    }
    
    private System.Collections.IEnumerator StartHostAfterShutdown()
    {
        // Wait for NetworkManager to fully shutdown
        while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }
        
        yield return new WaitForSeconds(0.5f); // Extra safety delay
        
        if (NetworkManager.Singleton != null)
        {
            // Ensure auto-spawn is still disabled after shutdown
            NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
            NetworkManager.Singleton.NetworkConfig.EnableSceneManagement = true;
            
            Debug.Log($"Starting host after shutdown with NetworkConfig: PlayerPrefab={NetworkManager.Singleton.NetworkConfig.PlayerPrefab}");
            NetworkManager.Singleton.StartHost();
            Debug.Log("Started as Host (after shutdown)");
        }
    }
    
    /// <summary>
    /// Join a game as client (second player in match)
    /// </summary>
    public void StartClient(string ipAddress, ushort port)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null!");
            return;
        }
        
        // Shutdown if already running
        if (NetworkManager.Singleton.IsListening)
        {
            Debug.Log("NetworkManager already running - shutting down first");
            NetworkManager.Singleton.Shutdown();
            // Wait for shutdown to complete
            StartCoroutine(StartClientAfterShutdown(ipAddress, port));
            return;
        }
        
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ipAddress, port);
        
        NetworkManager.Singleton.StartClient();
        Debug.Log($"Connecting to {ipAddress}:{port}");
    }
    
    private System.Collections.IEnumerator StartClientAfterShutdown(string ipAddress, ushort port)
    {
        // Wait for NetworkManager to fully shutdown
        while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }
        
        yield return new WaitForSeconds(0.5f); // Extra safety delay
        
        if (NetworkManager.Singleton != null)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(ipAddress, port);
            
            NetworkManager.Singleton.StartClient();
            Debug.Log($"Connecting to {ipAddress}:{port} (after shutdown)");
        }
    }
    
    /// <summary>
    /// Join a game as client using EOS P2P (no IP needed)
    /// </summary>
    public void StartClientEOS(string hostEOSUserId)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("NetworkManager.Singleton is null!");
            return;
        }
        
        // Shutdown if already running
        if (NetworkManager.Singleton.IsListening)
        {
            Debug.Log("NetworkManager already running - shutting down first");
            NetworkManager.Singleton.Shutdown();
            StartCoroutine(StartClientEOSAfterShutdown(hostEOSUserId));
            return;
        }
        
        // Configure EOS transport with host's ProductUserId
        var eosTransport = NetworkManager.Singleton.GetComponent<EOSNetcodeTransport>();
        if (eosTransport != null)
        {
            eosTransport.ConnectToHost(hostEOSUserId);
        }
        else
        {
            Debug.LogError("EOSNetcodeTransport not found on NetworkManager!");
            return;
        }
        
        Debug.Log($"Starting client with EOS, connecting to host: {hostEOSUserId}");
        NetworkManager.Singleton.StartClient();
        Debug.Log($"Connecting via EOS to {hostEOSUserId}");
    }
    
    private System.Collections.IEnumerator StartClientEOSAfterShutdown(string hostEOSUserId)
    {
        // Wait for NetworkManager to fully shutdown
        while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }
        
        yield return new WaitForSeconds(0.5f); // Extra safety delay
        
        if (NetworkManager.Singleton != null)
        {
            // Configure EOS transport with host's ProductUserId
            var eosTransport = NetworkManager.Singleton.GetComponent<EOSNetcodeTransport>();
            if (eosTransport != null)
            {
                eosTransport.ConnectToHost(hostEOSUserId);
            }
            
            Debug.Log($"Starting client with EOS after shutdown, connecting to host: {hostEOSUserId}");
            NetworkManager.Singleton.StartClient();
            Debug.Log($"Connecting via EOS to {hostEOSUserId} (after shutdown)");
        }
    }
    
    /// <summary>
    /// Set match data from matchmaking
    /// </summary>
    public void SetMatchData(MatchData matchData)
    {
        currentMatchData = matchData;
        
        // Determine which team local player is on
        string localPlayerId = FirebaseManager.Instance.playerId;
        
        Debug.Log($"=== TEAM ASSIGNMENT DEBUG ===");
        Debug.Log($"Local Player ID: {localPlayerId}");
        Debug.Log($"Team 1 Players: {matchData.team1Players.Count}");
        foreach (var player in matchData.team1Players)
        {
            Debug.Log($"  - {player.playerName} ({player.playerId})");
            if (player.playerId == localPlayerId)
            {
                localPlayerTeam = 1;
                Debug.Log($"  -> MATCH! Assigned to Team 1");
            }
        }
        
        Debug.Log($"Team 2 Players: {matchData.team2Players.Count}");
        foreach (var player in matchData.team2Players)
        {
            Debug.Log($"  - {player.playerName} ({player.playerId})");
            if (player.playerId == localPlayerId)
            {
                localPlayerTeam = 2;
                Debug.Log($"  -> MATCH! Assigned to Team 2");
            }
        }
        
        Debug.Log($"Final Team Assignment: Team {localPlayerTeam}");
        Debug.Log($"=============================");
        
        // Validate team assignment
        if (localPlayerTeam == 0)
        {
            Debug.LogError($"Failed to assign team for player {localPlayerId}! Check if player ID matches in match data.");
            Debug.LogError($"This usually means the player ID in Firebase doesn't match the local player ID.");
            localPlayerTeam = 1; // Fallback
        }
    }
    
    /// <summary>
    /// Called when server starts
    /// </summary>
    private void OnServerStarted()
    {
        Debug.Log("Server started - MatchManager will be automatically synchronized as scene object");
    }
    
    /// <summary>
    /// Called when a client connects
    /// </summary>
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        
        if (NetworkManager.Singleton.IsServer)
        {
            // Check if all players have connected
            int expectedPlayers = 0;
            if (currentMatchData != null)
            {
                expectedPlayers = currentMatchData.team1Players.Count + currentMatchData.team2Players.Count;
            }
            else
            {
                Debug.LogWarning("No match data available yet!");
                return;
            }
            
            int connectedPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            
            Debug.Log($"Connected: {connectedPlayers}/{expectedPlayers}");
            
            // Spawn players when all have connected
            if (connectedPlayers >= expectedPlayers)
            {
                Debug.Log("All players connected - spawning manually...");
                // Small delay to ensure everything is ready
                Invoke(nameof(SpawnAllPlayers), 0.5f);
            }
        }
    }
    
    /// <summary>
    /// Spawn all players in the match
    /// </summary>
    private void SpawnAllPlayers()
    {
        if (currentMatchData == null)
        {
            Debug.LogError("No match data available!");
            return;
        }
        
        // Find DynamicSpawnSystem right before spawning
        dynamicSpawnSystem = FindFirstObjectByType<DynamicSpawnSystem>();
        
        // Check if MatchManager exists
        var matchManager = FindFirstObjectByType<MatchManager>();
        Debug.Log($"MatchManager found: {(matchManager != null ? matchManager.gameObject.name : "NULL")}");
        
        if (playerPrefab == null)
        {
            Debug.LogError("PlayerPrefab is NULL in SpawnAllPlayers! Check NetworkGameManager Inspector.");
            return;
        }
        
        if (dynamicSpawnSystem == null)
        {
            Debug.LogError("DynamicSpawnSystem is NULL! MatchManager or DynamicSpawnSystem component missing.");
            
            // Try to get it from MatchManager if it exists
            if (matchManager != null)
            {
                dynamicSpawnSystem = matchManager.GetComponent<DynamicSpawnSystem>();
                if (dynamicSpawnSystem == null)
                {
                    Debug.LogError("MatchManager exists but has no DynamicSpawnSystem component!");
                }
            }
            
            if (dynamicSpawnSystem == null)
            {
                Debug.LogError("Cannot spawn without DynamicSpawnSystem. Aborting spawn.");
                return;
            }
        }
        
        Debug.Log($"Spawning all players... PlayerPrefab={playerPrefab.name}, DynamicSpawnSystem={dynamicSpawnSystem.gameObject.name}");
        
        // Get local player's Firebase ID
        string localPlayerId = FirebaseManager.Instance.playerId;
        
        // Tell MatchManager to wait for all players
        int connectedPlayerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
        Debug.Log($"[NetworkGameManager] Connected player count: {connectedPlayerCount}");
        if (matchManager != null)
        {
            Debug.Log($"[NetworkGameManager] Calling SetExpectedPlayerCount({connectedPlayerCount})");
            matchManager.SetExpectedPlayerCount(connectedPlayerCount);
        }
        else
        {
            Debug.LogError("[NetworkGameManager] MatchManager is NULL, cannot set expected player count!");
        }
        
        // Spawn a player for each connected client
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // Find which player data corresponds to this client
            PlayerData playerData = GetPlayerDataForClient(clientId, localPlayerId);
            if (playerData != null)
            {
                int team = GetTeamForPlayer(playerData.playerId);
                Debug.Log($"Spawning client {clientId} as {playerData.playerName} (Firebase ID: {playerData.playerId}) on Team {team}");
                SpawnPlayerForClient(clientId, playerData, team);
            }
            else
            {
                Debug.LogError($"No player data found for client {clientId}");
            }
        }
    }
    
    /// <summary>
    /// Get player data for a specific client ID
    /// Simple mapping: Team 1 players get even client IDs (0, 2), Team 2 players get odd client IDs (1, 3)
    /// </summary>
    private PlayerData GetPlayerDataForClient(ulong clientId, string localPlayerId)
    {
        if (currentMatchData == null) return null;
        
        int clientIndex = (int)clientId;
        
        // For 1v1: Client 0 = Team 1, Client 1 = Team 2
        // For 2v2: Client 0 = Team 1[0], Client 1 = Team 2[0], Client 2 = Team 1[1], Client 3 = Team 2[1]
        
        if (clientIndex % 2 == 0)
        {
            // Even client IDs (0, 2, 4...) get Team 1 players
            int teamIndex = clientIndex / 2;
            if (teamIndex < currentMatchData.team1Players.Count)
            {
                var player = currentMatchData.team1Players[teamIndex];
                Debug.Log($"Client {clientId} → Team 1[{teamIndex}]: {player.playerName} (Firebase ID: {player.playerId})");
                return player;
            }
        }
        else
        {
            // Odd client IDs (1, 3, 5...) get Team 2 players
            int teamIndex = clientIndex / 2;
            if (teamIndex < currentMatchData.team2Players.Count)
            {
                var player = currentMatchData.team2Players[teamIndex];
                Debug.Log($"Client {clientId} → Team 2[{teamIndex}]: {player.playerName} (Firebase ID: {player.playerId})");
                return player;
            }
        }
        
        Debug.LogError($"Failed to find player data for client {clientId}");
        return null;
    }
    
    /// <summary>
    /// Get team number for a player by their ID
    /// </summary>
    private int GetTeamForPlayer(string playerId)
    {
        if (currentMatchData == null) return 0;
        
        foreach (var player in currentMatchData.team1Players)
        {
            if (player.playerId == playerId) return 1;
        }
        
        foreach (var player in currentMatchData.team2Players)
        {
            if (player.playerId == playerId) return 2;
        }
        
        return 0;
    }
    
    /// <summary>
    /// Spawn a single player for a specific client using DynamicSpawnSystem
    /// </summary>
    private void SpawnPlayerForClient(ulong clientId, PlayerData playerData, int team)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab not assigned!");
            return;
        }
        
        if (dynamicSpawnSystem == null)
        {
            Debug.LogError("DynamicSpawnSystem not assigned!");
            return;
        }
        
        // Get teammate for 2v2 spawning (if applicable)
        GameObject teammate = GetTeammateForSpawn(team, null);
        
        // Use dynamic spawn system to find best spawn position BEFORE instantiating
        var spawnData = dynamicSpawnSystem.GetBestSpawnPosition(null, null, teammate);
        
        Vector3 spawnPos = spawnData.HasValue ? spawnData.Value.position : Vector3.zero;
        Quaternion spawnRot = spawnData.HasValue ? spawnData.Value.rotation : Quaternion.identity;
        
        // Instantiate player at spawn position
        GameObject playerObj = Instantiate(playerPrefab, spawnPos, spawnRot);
        
        // Get network object and spawn for specific client FIRST
        NetworkObject networkObject = playerObj.GetComponent<NetworkObject>();
        if (networkObject != null)
        {
            networkObject.SpawnAsPlayerObject(clientId);
            Debug.Log($"Spawned {playerData.playerName} (Client {clientId}) on Team {team} at {spawnPos}");
            
            // Set player data AFTER spawning on network to avoid NetworkVariable warning
            var networkPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (networkPlayer != null)
            {
                networkPlayer.SetPlayerData(playerData.playerId, playerData.playerName, team);
            }
        }
        else
        {
            Debug.LogError($"No NetworkObject on player prefab!");
            Destroy(playerObj);
        }
    }
    
    /// <summary>
    /// Get teammate GameObject for spawn positioning (2v2 mode)
    /// </summary>
    private GameObject GetTeammateForSpawn(int team, GameObject excludePlayer)
    {
        // Find all network objects
        var networkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        
        foreach (var netObj in networkObjects)
        {
            if (netObj.gameObject == excludePlayer) continue;
            
            var networkPlayer = netObj.GetComponent<NetworkPlayer>();
            if (networkPlayer != null && networkPlayer.GetTeam() == team)
            {
                return netObj.gameObject;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Get local player's team
    /// </summary>
    public int GetLocalPlayerTeam()
    {
        return localPlayerTeam;
    }
}
