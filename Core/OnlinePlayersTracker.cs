using UnityEngine;
using System;
using System.Collections.Generic;

#if FIREBASE_INSTALLED
using Firebase.Database;
#endif

/// <summary>
/// Tracks online players count using Firebase presence system
/// Updates in real-time when players join/leave
/// </summary>
public class OnlinePlayersTracker : MonoBehaviour
{
    public static OnlinePlayersTracker Instance { get; private set; }
    
    [Header("Online Status")]
    public int onlinePlayersCount = 0;
    public bool isTracking = false;
    
    [Header("Events")]
    public Action<int> OnPlayerCountChanged;
    
#if FIREBASE_INSTALLED
    private DatabaseReference presenceRef;
    private DatabaseReference playerPresenceRef;
#endif
    
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
            return;
        }
    }
    
    private void Start()
    {
        StartTracking();
    }
    
    /// <summary>
    /// Start tracking online players
    /// </summary>
    public void StartTracking()
    {
        if (isTracking) return;
        
#if FIREBASE_INSTALLED
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.isInitialized)
        {
            Debug.LogWarning("Firebase not initialized - cannot track online players");
            onlinePlayersCount = 0;
            OnPlayerCountChanged?.Invoke(onlinePlayersCount);
            return;
        }
        
        presenceRef = FirebaseDatabase.DefaultInstance.RootReference.Child("presence");
        
        // Set up presence for this player
        SetupPlayerPresence();
        
        // Listen for changes in online players count
        presenceRef.ChildAdded += OnPlayerJoined;
        presenceRef.ChildRemoved += OnPlayerLeft;
        
        // Get initial count
        presenceRef.GetValueAsync().ContinueWith(task => {
            if (task.IsCompleted && !task.IsFaulted)
            {
                DataSnapshot snapshot = task.Result;
                onlinePlayersCount = (int)snapshot.ChildrenCount;
                OnPlayerCountChanged?.Invoke(onlinePlayersCount);
                Debug.Log($"Online players: {onlinePlayersCount}");
            }
        });
        
        isTracking = true;
#else
        Debug.LogWarning("Firebase not installed - cannot track online players. Add FIREBASE_INSTALLED to Scripting Define Symbols.");
        onlinePlayersCount = 0;
        OnPlayerCountChanged?.Invoke(onlinePlayersCount);
#endif
    }
    
#if FIREBASE_INSTALLED
    /// <summary>
    /// Setup presence system for this player
    /// Automatically removes player when they disconnect
    /// </summary>
    private void SetupPlayerPresence()
    {
        if (FirebaseManager.Instance == null || string.IsNullOrEmpty(FirebaseManager.Instance.playerId))
            return;
        
        string playerId = FirebaseManager.Instance.playerId;
        playerPresenceRef = presenceRef.Child(playerId);
        
        // Set player as online
        Dictionary<string, object> presenceData = new Dictionary<string, object>
        {
            { "online", true },
            { "lastSeen", ServerValue.Timestamp },
            { "playerName", FirebaseManager.Instance.playerName }
        };
        
        playerPresenceRef.SetValueAsync(presenceData);
        
        // Set up automatic removal on disconnect
        playerPresenceRef.OnDisconnect().RemoveValue();
    }
    
    /// <summary>
    /// Called when a player joins
    /// </summary>
    private void OnPlayerJoined(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError($"Player joined error: {args.DatabaseError.Message}");
            return;
        }
        
        onlinePlayersCount++;
        OnPlayerCountChanged?.Invoke(onlinePlayersCount);
        Debug.Log($"Player joined. Online: {onlinePlayersCount}");
    }
    
    /// <summary>
    /// Called when a player leaves
    /// </summary>
    private void OnPlayerLeft(object sender, ChildChangedEventArgs args)
    {
        if (args.DatabaseError != null)
        {
            Debug.LogError($"Player left error: {args.DatabaseError.Message}");
            return;
        }
        
        onlinePlayersCount = Mathf.Max(0, onlinePlayersCount - 1);
        OnPlayerCountChanged?.Invoke(onlinePlayersCount);
        Debug.Log($"Player left. Online: {onlinePlayersCount}");
    }
#endif
    
    /// <summary>
    /// Stop tracking online players
    /// </summary>
    public void StopTracking()
    {
        if (!isTracking) return;
        
#if FIREBASE_INSTALLED
        if (presenceRef != null)
        {
            presenceRef.ChildAdded -= OnPlayerJoined;
            presenceRef.ChildRemoved -= OnPlayerLeft;
        }
        
        // Remove this player's presence
        if (playerPresenceRef != null)
        {
            playerPresenceRef.RemoveValueAsync();
        }
#endif
        
        isTracking = false;
    }
    
    private void OnDestroy()
    {
        StopTracking();
    }
    
    private void OnApplicationQuit()
    {
        StopTracking();
    }
}
