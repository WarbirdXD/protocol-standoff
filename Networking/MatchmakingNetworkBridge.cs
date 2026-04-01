using UnityEngine;
using UnityEngine.SceneManagement;

#if FIREBASE_INSTALLED
using Firebase.Database;
#endif

/// <summary>
/// Bridges Firebase matchmaking with Unity Netcode
/// Handles connection setup when match is found
/// </summary>
public class MatchmakingNetworkBridge : MonoBehaviour
{
    public static MatchmakingNetworkBridge Instance { get; private set; }
    
    [Header("Settings")]
    public string gameSceneName = "GameScene";
    public ushort defaultPort = 7777;
    
    [Header("Connection Method")]
    public ConnectionMethod connectionMethod = ConnectionMethod.EOS;
    
    public enum ConnectionMethod
    {
        DirectIP,      // Players connect directly (requires port forwarding)
        RelayService,  // Use Unity Relay (easier but requires Unity Gaming Services)
        EOS            // Use Epic Online Services P2P (best for itch.io releases)
    }
    
    private MatchData pendingMatchData;
    private bool isHost = false;
    private bool matchInProgress = false;
    
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
    
    private void Start()
    {
        // Subscribe to matchmaking events
        if (MatchmakingManager.Instance != null)
        {
            MatchmakingManager.Instance.OnMatchFound.AddListener(OnMatchFound);
        }
    }
    
    private void OnDestroy()
    {
        // Clean up match data when destroyed
        if (Instance == this)
        {
            CleanupMatchData();
        }
    }
    
    private void OnApplicationQuit()
    {
        // Clean up match data when quitting
        CleanupMatchData();
    }
    
    /// <summary>
    /// Clean up match data from Firebase
    /// </summary>
    private void CleanupMatchData()
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.isSignedIn) return;
        
#if FIREBASE_INSTALLED
        string playerId = FirebaseManager.Instance.playerId;
        
        // Remove match data
        string matchPath = $"matches/{playerId}";
        FirebaseDatabase.DefaultInstance.GetReference(matchPath).RemoveValueAsync();
        
        Debug.Log("Cleaned up match data from Firebase");
#endif
        
        // Reset match flag
        matchInProgress = false;
    }
    
    /// <summary>
    /// Called when matchmaking finds a match
    /// </summary>
    private void OnMatchFound(MatchData matchData)
    {
        // Prevent processing the same match multiple times
        if (matchInProgress)
        {
            Debug.Log("Match already in progress, ignoring duplicate OnMatchFound event");
            return;
        }
        
        matchInProgress = true;
        pendingMatchData = matchData;
        
        // Determine if we're host (first player in team 1)
        string localPlayerId = FirebaseManager.Instance.playerId;
        isHost = (matchData.team1Players.Count > 0 && matchData.team1Players[0].playerId == localPlayerId);
        
        Debug.Log($"Match found! We are {(isHost ? "HOST" : "CLIENT")}");
        
        // Load game scene (Single mode unloads all other scenes)
        SceneManager.sceneLoaded += OnGameSceneLoaded;
        SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }
    
    /// <summary>
    /// Called when game scene finishes loading
    /// </summary>
    private void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != gameSceneName) return;
        
        SceneManager.sceneLoaded -= OnGameSceneLoaded;
        
        // Set match data in network manager
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.SetMatchData(pendingMatchData);
        }
        
        // Set match data in match manager
        MatchManager matchManager = FindFirstObjectByType<MatchManager>();
        if (matchManager != null)
        {
            matchManager.SetMatchData(pendingMatchData);
        }
        
        // Start network connection
        if (connectionMethod == ConnectionMethod.DirectIP)
        {
            StartDirectIPConnection();
        }
        else if (connectionMethod == ConnectionMethod.EOS)
        {
            StartEOSConnection();
        }
        else
        {
            Debug.LogWarning("Unity Relay not implemented yet. Using Direct IP.");
            StartDirectIPConnection();
        }
    }
    
    /// <summary>
    /// Start direct IP connection (simple but requires port forwarding)
    /// </summary>
    private void StartDirectIPConnection()
    {
        if (isHost)
        {
            // Host starts server
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.StartHost();
            }
            
            // Store connection info in Firebase for client to find
            StoreConnectionInfo();
        }
        else
        {
            // Client waits for host connection info
            WaitForConnectionInfo();
        }
    }
    
    /// <summary>
    /// Host stores their IP in Firebase for client to connect
    /// </summary>
    private void StoreConnectionInfo()
    {
#if FIREBASE_INSTALLED
        // For local testing, use localhost. In production, use STUN server for public IP
        string localIP = "127.0.0.1"; // Use localhost for same-machine testing
        
        var connectionData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "hostIP", localIP },
            { "port", defaultPort },
            { "timestamp", Firebase.Database.ServerValue.Timestamp }
        };
        
        Firebase.Database.FirebaseDatabase.DefaultInstance
            .GetReference($"matches/{pendingMatchData.matchId}/connection")
            .SetValueAsync(connectionData)
            .ContinueWith(task => {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"Connection info stored: {localIP}:{defaultPort}");
                }
            });
#else
        Debug.LogWarning("Firebase not installed - using localhost for testing");
#endif
    }
    
    /// <summary>
    /// Client waits for host connection info from Firebase
    /// </summary>
    private void WaitForConnectionInfo()
    {
#if FIREBASE_INSTALLED
        Debug.Log($"Client waiting for connection info at: matches/{pendingMatchData.matchId}/connection");
        
        var connectionRef = Firebase.Database.FirebaseDatabase.DefaultInstance
            .GetReference($"matches/{pendingMatchData.matchId}/connection");
        
        // First check if data already exists
        connectionRef.GetValueAsync().ContinueWith(task => {
            if (task.IsCompleted && !task.IsFaulted)
            {
                var snapshot = task.Result;
                if (snapshot.Exists)
                {
                    Debug.Log("Connection info already exists - connecting now");
                    ParseAndConnect(snapshot);
                }
                else
                {
                    Debug.Log("Connection info not found yet - listening for changes");
                    // Listen for when it gets added
                    connectionRef.ValueChanged += OnConnectionInfoReceived;
                }
            }
            else
            {
                Debug.LogError($"Failed to check for connection info: {task.Exception}");
            }
        });
#else
        // For local testing without Firebase
        Debug.LogWarning("Firebase not installed - connecting to localhost");
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.StartClient("127.0.0.1", defaultPort);
        }
#endif
    }
    
#if FIREBASE_INSTALLED
    /// <summary>
    /// Parse connection info from snapshot and connect
    /// </summary>
    private void ParseAndConnect(Firebase.Database.DataSnapshot snapshot)
    {
        var data = snapshot.Value as System.Collections.Generic.Dictionary<string, object>;
        if (data == null)
        {
            Debug.LogError("Connection data is null!");
            return;
        }
        
        string hostIP = data["hostIP"].ToString();
        ushort port = ushort.Parse(data["port"].ToString());
        
        Debug.Log($"Received connection info: {hostIP}:{port}");
        
        // Must run on main thread since this is called from Firebase background thread
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            // Connect to host
            if (NetworkGameManager.Instance != null)
            {
                Debug.Log($"Calling StartClient on main thread...");
                NetworkGameManager.Instance.StartClient(hostIP, port);
            }
            else
            {
                Debug.LogError("NetworkGameManager.Instance is null! Retrying in 0.5s...");
                // Retry after a short delay
                StartCoroutine(RetryClientConnection(hostIP, port));
            }
        });
    }
    
    private System.Collections.IEnumerator RetryClientConnection(string hostIP, ushort port)
    {
        yield return new WaitForSeconds(0.5f);
        
        if (NetworkGameManager.Instance != null)
        {
            Debug.Log($"Retry: Calling StartClient...");
            NetworkGameManager.Instance.StartClient(hostIP, port);
        }
        else
        {
            Debug.LogError("NetworkGameManager.Instance still null after retry!");
        }
    }
    
    /// <summary>
    /// Called when connection info is received from Firebase
    /// </summary>
    private void OnConnectionInfoReceived(object sender, Firebase.Database.ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError($"Firebase error: {args.DatabaseError.Message}");
            return;
        }
        
        if (!args.Snapshot.Exists) return;
        
        Debug.Log("Connection info received via ValueChanged event");
        ParseAndConnect(args.Snapshot);
        
        // Stop listening
        Firebase.Database.FirebaseDatabase.DefaultInstance
            .GetReference($"matches/{pendingMatchData.matchId}/connection")
            .ValueChanged -= OnConnectionInfoReceived;
    }
#endif
    
    /// <summary>
    /// Get local IP address (simplified - use STUN server in production)
    /// </summary>
    private string GetLocalIPAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "127.0.0.1";
    }
    
    /// <summary>
    /// Start EOS P2P connection (works over internet, no port forwarding needed)
    /// </summary>
    private void StartEOSConnection()
    {
        Debug.Log($"=== StartEOSConnection called === isHost: {isHost}");
        
        // Check if EOS is ready
        if (SimpleEOSManager.Instance == null || !SimpleEOSManager.Instance.isAuthenticated)
        {
            Debug.LogError("EOS not authenticated! Cannot start connection.");
            return;
        }
        
        if (isHost)
        {
            Debug.Log("HOST: Starting server and storing EOS connection info...");
            
            // Host starts server
            if (NetworkGameManager.Instance != null)
            {
                NetworkGameManager.Instance.StartHost();
            }
            else
            {
                Debug.LogError("HOST: NetworkGameManager.Instance is null!");
            }
            
            // Store EOS ProductUserId in Firebase for client to find
            StoreEOSConnectionInfo();
        }
        else
        {
            Debug.Log("CLIENT: Waiting for host's EOS connection info...");
            // Client waits for host's EOS ProductUserId
            WaitForEOSConnectionInfo();
        }
    }
    
    /// <summary>
    /// Host stores their EOS ProductUserId in Firebase
    /// </summary>
    private void StoreEOSConnectionInfo()
    {
#if FIREBASE_INSTALLED
        string hostUserId = SimpleEOSManager.Instance.GetLocalUserIdString();
        Debug.Log($"HOST: Storing EOS connection info - ProductUserId: {hostUserId}");
        
        var connectionData = new System.Collections.Generic.Dictionary<string, object>
        {
            { "eosUserId", hostUserId },
            { "connectionType", "EOS" },
            { "timestamp", Firebase.Database.ServerValue.Timestamp }
        };
        
        string firebasePath = $"matches/{pendingMatchData.matchId}/connection";
        Debug.Log($"HOST: Writing to Firebase path: {firebasePath}");
        
        Firebase.Database.FirebaseDatabase.DefaultInstance
            .GetReference(firebasePath)
            .SetValueAsync(connectionData)
            .ContinueWith(task => {
                if (task.IsCompleted && !task.IsFaulted)
                {
                    Debug.Log($"EOS connection info stored: {hostUserId}");
                }
                else
                {
                    Debug.LogError($"Failed to store EOS connection info: {task.Exception}");
                }
            });
#else
        Debug.LogError("Firebase required for EOS matchmaking");
#endif
    }
    
    /// <summary>
    /// Client waits for host's EOS ProductUserId from Firebase
    /// </summary>
    private void WaitForEOSConnectionInfo()
    {
#if FIREBASE_INSTALLED
        Debug.Log($"Client waiting for EOS connection info at: matches/{pendingMatchData.matchId}/connection");
        
        var connectionRef = Firebase.Database.FirebaseDatabase.DefaultInstance
            .GetReference($"matches/{pendingMatchData.matchId}/connection");
        
        // First check if data already exists
        connectionRef.GetValueAsync().ContinueWith(task => {
            if (task.IsCompleted && !task.IsFaulted)
            {
                var snapshot = task.Result;
                if (snapshot.Exists)
                {
                    Debug.Log("EOS connection info already exists - connecting now");
                    ParseAndConnectEOS(snapshot);
                }
                else
                {
                    Debug.Log("EOS connection info not found yet - listening for changes");
                    // Listen for when it gets added
                    connectionRef.ValueChanged += OnEOSConnectionInfoReceived;
                }
            }
            else
            {
                Debug.LogError($"Failed to check for EOS connection info: {task.Exception}");
            }
        });
#else
        Debug.LogError("Firebase required for EOS matchmaking");
#endif
    }
    
#if FIREBASE_INSTALLED
    /// <summary>
    /// Parse EOS connection info and connect
    /// </summary>
    private void ParseAndConnectEOS(Firebase.Database.DataSnapshot snapshot)
    {
        var data = snapshot.Value as System.Collections.Generic.Dictionary<string, object>;
        if (data == null)
        {
            Debug.LogError("EOS connection data is null!");
            return;
        }
        
        if (!data.ContainsKey("eosUserId"))
        {
            Debug.LogError("EOS connection data missing eosUserId!");
            return;
        }
        
        string hostEOSUserId = data["eosUserId"].ToString();
        Debug.Log($"Received EOS connection info: {hostEOSUserId}");
        
        // Must run on main thread
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            // Connect to host via EOS
            if (NetworkGameManager.Instance != null)
            {
                Debug.Log($"Calling StartClient with EOS...");
                NetworkGameManager.Instance.StartClientEOS(hostEOSUserId);
            }
            else
            {
                Debug.LogError("NetworkGameManager.Instance is null!");
            }
        });
    }
    
    /// <summary>
    /// Called when EOS connection info is received from Firebase
    /// </summary>
    private void OnEOSConnectionInfoReceived(object sender, Firebase.Database.ValueChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError($"Firebase error: {args.DatabaseError.Message}");
            return;
        }
        
        if (!args.Snapshot.Exists) return;
        
        Debug.Log("EOS connection info received via ValueChanged event");
        ParseAndConnectEOS(args.Snapshot);
        
        // Stop listening
        Firebase.Database.FirebaseDatabase.DefaultInstance
            .GetReference($"matches/{pendingMatchData.matchId}/connection")
            .ValueChanged -= OnEOSConnectionInfoReceived;
    }
#endif
}
