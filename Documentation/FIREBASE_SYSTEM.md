# Firebase System Documentation

**Backend services for authentication, data persistence, and real-time coordination**

---

## Overview

Firebase provides the backend infrastructure for Protocol Standoff, handling user authentication, persistent player data storage, real-time matchmaking coordination, and presence tracking. The system uses Firebase Authentication for user management and Firebase Realtime Database for all data storage and synchronization.

---

## Architecture

```
Client Application
       ↓
FirebaseManager.cs (Singleton)
       ↓
Firebase SDK
       ↓
┌──────────────────────────────────────┐
│         Firebase Services            │
├──────────────────────────────────────┤
│  Firebase Authentication             │
│    - Email/Password Auth             │
│    - Anonymous Auth                  │
│    - User Management                 │
├──────────────────────────────────────┤
│  Firebase Realtime Database          │
│    - Player Data                     │
│    - Matchmaking Queue               │
│    - Match Data                      │
│    - Presence System                 │
│    - Security Logs                   │
└──────────────────────────────────────┘
```

---

## Authentication System

### Sign In Flow

```
User enters credentials
       ↓
FirebaseManager.SignIn(email, password)
       ↓
Firebase Auth validates credentials
       ↓
Success: Returns FirebaseUser
       ↓
Extract userId and displayName
       ↓
Set isSignedIn = true
       ↓
LoadPlayerData()
       ↓
SetPlayerOnline()
       ↓
OnSignInSuccess event triggered
       ↓
UI transitions to main menu
```

### Sign Up Flow

```
User enters email, password, username
       ↓
FirebaseManager.SignUp(email, password, username)
       ↓
Firebase Auth creates new user
       ↓
Update user profile with displayName
       ↓
Initialize player data in database
       ↓
Auto sign-in
       ↓
OnSignUpSuccess event triggered
```

### Error Handling

Firebase authentication errors are mapped to user-friendly messages:

| Firebase Error | User Message |
|---------------|--------------|
| `EmailAlreadyInUse` | "This email is already registered" |
| `InvalidEmail` | "Please enter a valid email address" |
| `WeakPassword` | "Password must be at least 6 characters" |
| `WrongPassword` | "Incorrect password" |
| `UserNotFound` | "No account found with this email" |
| `NetworkRequestFailed` | "Network error. Check your connection" |

### Anonymous Authentication

Used for testing and guest access:

```csharp
auth.SignInAnonymouslyAsync()
    ↓
Generates temporary user ID
    ↓
Limited functionality (no data persistence)
    ↓
Can be upgraded to full account later
```

---

## Database Structure

### Complete Schema

```json
{
  "players": {
    "{userId}": {
      "name": "PlayerName",
      "email": "player@example.com",
      "createdAt": 1768658881764,
      "lastPlayed": 1768658881764,
      
      "oneVsOneMmr": 600,
      "oneVsOneMatches": 0,
      "oneVsOneWins": 0,
      "oneVsOneLosses": 0,
      
      "twoVsTwoMmr": 600,
      "twoVsTwoMatches": 0,
      "twoVsTwoWins": 0,
      "twoVsTwoLosses": 0,
      
      "stats": {
        "ranked_1v1": {
          "mmr": 600,
          "wins": 0,
          "losses": 0,
          "gamesPlayed": 0,
          "winRate": 0,
          "highestMMR": 600,
          "currentStreak": 0,
          "longestWinStreak": 0
        },
        "ranked_2v2": {
          "mmr": 600,
          "wins": 0,
          "losses": 0,
          "gamesPlayed": 0,
          "winRate": 0,
          "highestMMR": 600,
          "currentStreak": 0,
          "longestWinStreak": 0
        }
      },
      
      "status": {
        "online": true,
        "lastSeen": 1768658881764
      }
    }
  },
  
  "presence": {
    "{userId}": {
      "online": true,
      "sessionId": "abc-123-def-456",
      "playerName": "PlayerName",
      "lastSeen": 1768658881764
    }
  },
  
  "matchmaking": {
    "queue_1v1": {
      "{queueId}": {
        "playerId": "userId",
        "playerName": "PlayerName",
        "mmr": 600,
        "eosUserId": "eos-user-id",
        "timestamp": 1768658881764,
        "searchRange": 100
      }
    },
    "queue_2v2": {
      "{queueId}": {
        "playerId": "userId",
        "playerName": "PlayerName",
        "mmr": 600,
        "eosUserId": "eos-user-id",
        "timestamp": 1768658881764,
        "searchRange": 100
      }
    }
  },
  
  "matches": {
    "{matchId}": {
      "mode": 0,
      "status": "waiting",
      "team1": ["player1Id"],
      "team2": ["player2Id"],
      "team1Ranking": 600,
      "team2Ranking": 600,
      "hostEOSUserId": "eos-user-id",
      "createdAt": 1768658881764
    }
  },
  
  "security_logs": {
    "{logId}": {
      "clientId": "12345",
      "reason": "Rate limit exceeded: shoot",
      "timestamp": 1768658881764,
      "severity": "warning"
    }
  }
}
```

---

## Presence System

### Purpose
Tracks which players are online and prevents multiple simultaneous logins (account sharing).

### How It Works

#### 1. Setting Online Status

```
Player signs in
       ↓
Generate unique sessionId (GUID)
       ↓
Create presence data:
  - online: true
  - sessionId: unique ID
  - playerName: display name
  - lastSeen: server timestamp
       ↓
Write to /presence/{userId}
       ↓
Set OnDisconnect().RemoveValue()
       ↓
Subscribe to ValueChanged events
```

#### 2. Session Enforcement

**Problem:** Prevent account sharing and multiple logins.

**Solution:** Each login gets a unique session ID. If the session ID changes in the database, another login is detected.

```
Login A: sessionId = "abc-123"
       ↓
Write to Firebase
       ↓
Subscribe to ValueChanged
       ↓
Login B (same account): sessionId = "xyz-789"
       ↓
Firebase updates presence node
       ↓
Login A's ValueChanged triggers
       ↓
Login A detects sessionId changed
       ↓
Login A calls HandleSessionKicked()
       ↓
Login A disconnects and shows message
```

#### 3. Critical Timing Issue

**Problem:** `ValueChanged` triggers immediately after `SetValueAsync`, causing false positives.

**Solution:** Subscribe to `ValueChanged` AFTER the write completes:

```csharp
presenceRef.SetValueAsync(presenceData).ContinueWith(task => {
    if (task.IsCompleted && !task.IsFaulted)
    {
        // Only subscribe AFTER write completes
        presenceRef.ValueChanged += OnSessionChanged;
    }
});
```

#### 4. Automatic Cleanup

```
Player disconnects (network loss, crash, quit)
       ↓
Firebase detects disconnect
       ↓
OnDisconnect() triggers
       ↓
Presence node removed automatically
       ↓
Player shows as offline
```

---

## Player Data Management

### Loading Player Data

```
Sign in successful
       ↓
Get userId from FirebaseUser
       ↓
Query /players/{userId}
       ↓
DataSnapshot returned
       ↓
Parse JSON data:
  - MMR values
  - Win/loss records
  - Stats per mode
  - Last played timestamp
       ↓
Update RankingSystem with loaded data
       ↓
Update UI displays
```

### Saving Player Data

```
Match ends
       ↓
RankingSystem.ApplyMatchResult()
       ↓
Calculate new MMR
       ↓
Update local stats
       ↓
RankingSystem.SavePlayerStats()
       ↓
Create update dictionary:
  - Root-level fields (backward compatibility)
  - Nested stats structure
  - Timestamp
       ↓
FirebaseManager.SaveToFirebase()
       ↓
UpdateChildrenAsync() to Firebase
       ↓
Also save to PlayerPrefs (local backup)
```

### Data Persistence Strategy

**Dual Storage:**
1. **Firebase (Primary):** Cloud storage, accessible anywhere
2. **PlayerPrefs (Backup):** Local storage, offline access

**Update Pattern:**
```csharp
// Save to both locations
PlayerPrefs.SetInt("OneVsOneMmr", mmr);
PlayerPrefs.Save();

Dictionary<string, object> updates = new Dictionary<string, object>
{
    { $"players/{playerId}/oneVsOneMmr", mmr },
    { $"players/{playerId}/stats/ranked_1v1/mmr", mmr }
};
database.RootReference.UpdateChildrenAsync(updates);
```

---

## Matchmaking Integration

### Queue Management

#### Adding to Queue

```
Player clicks "Play"
       ↓
MatchmakingManager.JoinQueue(mode)
       ↓
Get player's MMR for mode
       ↓
Create queue entry:
  - playerId
  - playerName
  - mmr
  - eosUserId (for P2P connection)
  - timestamp
  - searchRange
       ↓
Push to /matchmaking/queue_{mode}
       ↓
Store queueId
       ↓
Set OnDisconnect().RemoveValue()
       ↓
Start search loop
```

#### Searching for Match

```
Every 2 seconds:
       ↓
Query /matchmaking/queue_{mode}
       ↓
Get all queue entries
       ↓
For each entry:
  - Calculate MMR difference
  - Check if within search range
  - Skip self
       ↓
Match found?
  Yes → Create match
  No → Expand search range, continue
```

#### Match Creation

```
Match found
       ↓
Generate matchId
       ↓
Determine host (lower playerId)
       ↓
Create match data:
  - mode
  - status: "waiting"
  - team1: [player1Id]
  - team2: [player2Id]
  - team1Ranking: player1MMR
  - team2Ranking: player2MMR
  - hostEOSUserId
       ↓
Write to /matches/{matchId}
       ↓
Remove both players from queue
       ↓
Trigger connection flow
```

### Match Cleanup

```
Match ends
       ↓
MatchManager.CleanupMatchData()
       ↓
Remove /matches/{matchId}
       ↓
Prevents stale match data
```

---

## Security Rules

### Firebase Realtime Database Rules

```json
{
  "rules": {
    "players": {
      "$uid": {
        ".read": "auth != null",
        ".write": "$uid === auth.uid",
        "stats": {
          ".validate": "newData.child('ranked_1v1/mmr').isNumber() && 
                       newData.child('ranked_1v1/mmr').val() >= 0 && 
                       newData.child('ranked_1v1/mmr').val() <= 3000"
        }
      }
    },
    "presence": {
      "$uid": {
        ".read": "auth != null",
        ".write": "$uid === auth.uid"
      }
    },
    "matchmaking": {
      "queue_1v1": {
        "$queueId": {
          ".read": "auth != null",
          ".write": "auth != null && 
                    (!data.exists() || data.child('playerId').val() === auth.uid)",
          ".validate": "newData.hasChildren(['playerId', 'playerName', 'mmr', 'eosUserId', 'timestamp'])"
        }
      },
      "queue_2v2": {
        "$queueId": {
          ".read": "auth != null",
          ".write": "auth != null && 
                    (!data.exists() || data.child('playerId').val() === auth.uid)",
          ".validate": "newData.hasChildren(['playerId', 'playerName', 'mmr', 'eosUserId', 'timestamp'])"
        }
      }
    },
    "matches": {
      "$matchId": {
        ".read": "auth != null && 
                 (data.child('team1').val().contains(auth.uid) || 
                  data.child('team2').val().contains(auth.uid))",
        ".write": "auth != null && 
                  (data.child('team1').val().contains(auth.uid) || 
                   data.child('team2').val().contains(auth.uid))"
      }
    },
    "security_logs": {
      ".read": "false",
      ".write": "auth != null"
    }
  }
}
```

### Rule Explanations

**Players Node:**
- Anyone authenticated can read any player data
- Players can only write to their own data
- MMR values validated (0-3000 range)

**Presence Node:**
- Anyone authenticated can read presence
- Players can only write their own presence

**Matchmaking Queues:**
- Anyone authenticated can read queue
- Can only create new entries or delete own entries
- Must include all required fields

**Matches Node:**
- Only participants can read match data
- Only participants can write match data

**Security Logs:**
- No one can read (admin only via console)
- Any authenticated user can write logs

---

## Error Handling

### Network Errors

```csharp
try {
    await database.Child("path").GetValueAsync();
} catch (Exception ex) {
    if (ex is AggregateException aggregateEx) {
        // Firebase operation failed
        Debug.LogError($"Firebase error: {aggregateEx.InnerException?.Message}");
    }
}
```

### Offline Handling

Firebase automatically:
- Queues writes when offline
- Syncs when connection restored
- Triggers `OnDisconnect()` handlers

### Timeout Handling

```csharp
var task = database.Child("path").GetValueAsync();
if (await Task.WhenAny(task, Task.Delay(5000)) == task) {
    // Completed within timeout
    var result = task.Result;
} else {
    // Timeout
    Debug.LogError("Firebase operation timed out");
}
```

---

## Performance Optimization

### Efficient Queries

**Bad:**
```csharp
// Downloads entire players node
database.Child("players").GetValueAsync();
```

**Good:**
```csharp
// Downloads only specific player
database.Child("players").Child(userId).GetValueAsync();
```

### Batch Updates

**Bad:**
```csharp
// Multiple separate writes
database.Child($"players/{id}/mmr").SetValueAsync(mmr);
database.Child($"players/{id}/wins").SetValueAsync(wins);
database.Child($"players/{id}/losses").SetValueAsync(losses);
```

**Good:**
```csharp
// Single atomic update
Dictionary<string, object> updates = new Dictionary<string, object>
{
    { $"players/{id}/mmr", mmr },
    { $"players/{id}/wins", wins },
    { $"players/{id}/losses", losses }
};
database.RootReference.UpdateChildrenAsync(updates);
```

### Listener Management

```csharp
// Subscribe
presenceRef.ValueChanged += OnSessionChanged;

// Always unsubscribe when done
void OnDestroy() {
    if (presenceRef != null) {
        presenceRef.ValueChanged -= OnSessionChanged;
    }
}
```

---

## Integration with Other Systems

### With Ranking System
```
Match ends → RankingSystem calculates MMR → SavePlayerStats() → Firebase
```

### With Matchmaking System
```
Join queue → Firebase queue entry → Search loop → Match found → Firebase match data
```

### With EOS System
```
EOS login → Get EOS User ID → Store in queue entry → Used for P2P connection
```

### With Security System
```
Suspicious activity detected → Log to Firebase security_logs → Admin review
```

---

## Common Operations

### Check if User is Signed In
```csharp
if (FirebaseManager.Instance.isSignedIn) {
    // User is authenticated
}
```

### Get Current Player ID
```csharp
string playerId = FirebaseManager.Instance.playerId;
```

### Get Current Player Name
```csharp
string playerName = FirebaseManager.Instance.playerName;
```

### Sign Out
```csharp
FirebaseManager.Instance.SignOut();
// Clears session, removes presence, returns to login
```

---

## Best Practices

1. **Always check authentication** before Firebase operations
2. **Use UpdateChildrenAsync** for atomic multi-field updates
3. **Set OnDisconnect handlers** for automatic cleanup
4. **Unsubscribe from listeners** to prevent memory leaks
5. **Validate data** before writing to Firebase
6. **Handle errors gracefully** with user-friendly messages
7. **Use transactions** for concurrent modifications
8. **Keep data structure flat** for better query performance
9. **Index frequently queried fields** in Firebase console
10. **Monitor usage** to stay within free tier limits

---

*This documentation explains the Firebase system architecture and functionality. For implementation details, see `FirebaseManager.cs`.*
