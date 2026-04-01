# Matchmaking System Documentation

**Skill-based matchmaking with expanding search ranges**

---

## Overview

The Matchmaking System finds suitable opponents based on skill (MMR) and creates balanced matches. It uses Firebase Realtime Database for queue coordination and implements an expanding search range algorithm to ensure players find matches even during low-population periods while prioritizing skill-balanced games.

---

## Architecture

```
Player
   ↓
Join Queue
   ↓
Firebase Queue Entry Created
   ↓
Search Loop (every 2 seconds)
   ↓
┌─────────────────────────────┐
│   MMR-Based Matching        │
│   - Calculate differences   │
│   - Check search range      │
│   - Find best match         │
└─────────────────────────────┘
   ↓
Match Found?
   ├─ Yes → Create Match in Firebase
   │         ↓
   │      EOS Connection
   │         ↓
   │      Load Match Scene
   │
   └─ No → Expand Search Range
            ↓
         Wait 2 seconds
            ↓
         Search Again
```

---

## Queue System

### Queue Entry Structure

```json
{
  "matchmaking": {
    "queue_1v1": {
      "{queueId}": {
        "playerId": "firebase-user-id",
        "playerName": "WADDLE",
        "mmr": 612,
        "eosUserId": "eos-product-user-id",
        "timestamp": 1768658881764,
        "searchRange": 100
      }
    },
    "queue_2v2": {
      "{queueId}": {
        "playerId": "firebase-user-id",
        "playerName": "WADDLE",
        "mmr": 600,
        "eosUserId": "eos-product-user-id",
        "timestamp": 1768658881764,
        "searchRange": 100
      }
    }
  }
}
```

### Joining Queue

```
Player clicks "Play" button
       ↓
MatchmakingManager.JoinQueue(mode)
       ↓
Get player's MMR for selected mode
       ↓
Get player's EOS User ID
       ↓
Create queue entry data:
  - playerId (Firebase user ID)
  - playerName (display name)
  - mmr (current rating for mode)
  - eosUserId (for P2P connection)
  - timestamp (ServerValue.Timestamp)
  - searchRange (initial: 100)
       ↓
Push to Firebase queue path
       ↓
Store queueId locally
       ↓
Set OnDisconnect().RemoveValue()
       ↓
isInQueue = true
       ↓
Start search coroutine
       ↓
Update UI (show "Searching..." panel)
```

### Leaving Queue

```
Player clicks "Cancel" or disconnects
       ↓
MatchmakingManager.LeaveQueue()
       ↓
Remove queue entry from Firebase
       ↓
Stop search coroutine
       ↓
isInQueue = false
       ↓
Update UI (hide search panel)
```

### Automatic Cleanup

```
Player disconnects (crash, network loss, quit)
       ↓
Firebase detects disconnect
       ↓
OnDisconnect() trigger fires
       ↓
Queue entry automatically removed
       ↓
Other players won't match with disconnected player
```

---

## Search Algorithm

### Search Loop

```csharp
IEnumerator SearchForMatchCoroutine()
{
    while (isInQueue)
    {
        // Search for match
        SearchForMatch();
        
        // Wait 2 seconds before next search
        yield return new WaitForSeconds(2f);
    }
}
```

### Search Process

```
Query Firebase queue
       ↓
Get all queue entries
       ↓
Filter out self (skip own entry)
       ↓
For each potential opponent:
  ├─ Calculate MMR difference
  │    |myMMR - opponentMMR|
  │
  ├─ Check if within search range
  │    difference <= currentSearchRange
  │
  ├─ Match found?
  │    ├─ Yes → Create match immediately
  │    └─ No → Continue to next entry
  │
  └─ All entries checked?
       ├─ Match found → Exit loop
       └─ No match → Continue searching
```

### MMR Difference Calculation

```csharp
int myMMR = RankingSystem.Instance.GetMMRForMode(currentMode);
int opponentMMR = int.Parse(entry.Child("mmr").Value.ToString());
int mmrDifference = Mathf.Abs(myMMR - opponentMMR);

if (mmrDifference <= currentSearchRange)
{
    // Suitable opponent found
    CreateMatch(entry);
}
```

---

## Search Range Expansion

### Purpose
Ensure players find matches even during low population while prioritizing skill-balanced games.

### Expansion Schedule

```
Time in Queue    Search Range    MMR Range Example (600 MMR player)
─────────────────────────────────────────────────────────────────
0-10 seconds     ±100 MMR       500-700 (tight matching)
10-20 seconds    ±200 MMR       400-800 (moderate matching)
20-30 seconds    ±400 MMR       200-1000 (loose matching)
30+ seconds      ±800 MMR       0-1400 (very loose matching)
```

### Implementation

```csharp
private void SearchForMatch()
{
    // Calculate time in queue
    float timeInQueue = Time.time - queueStartTime;
    
    // Determine current search range based on time
    int currentSearchRange = initialSearchRange; // 100
    
    if (timeInQueue > 30f)
        currentSearchRange = 800;
    else if (timeInQueue > 20f)
        currentSearchRange = 400;
    else if (timeInQueue > 10f)
        currentSearchRange = 200;
    
    Debug.Log($"Searching with range: ±{currentSearchRange} MMR");
    
    // Search with current range
    // ...
}
```

### Benefits

1. **Fast Matches:** High-skill players find each other quickly
2. **Fair Matches:** Prioritizes similar skill levels
3. **Guaranteed Matches:** Eventually finds anyone available
4. **Adaptive:** Responds to player population

### Example Scenarios

**High Population (many players queuing):**
- Player finds match within 5 seconds
- Search range: ±100 MMR
- Very balanced match

**Medium Population:**
- Player finds match within 15 seconds
- Search range: ±200 MMR
- Reasonably balanced match

**Low Population:**
- Player finds match within 35 seconds
- Search range: ±800 MMR
- Less balanced but still playable

---

## Match Creation

### Match Data Structure

```json
{
  "matches": {
    "{matchId}": {
      "mode": 0,
      "status": "waiting",
      "team1": ["player1Id"],
      "team2": ["player2Id"],
      "team1Ranking": 612,
      "team2Ranking": 600,
      "hostEOSUserId": "eos-user-id-123",
      "createdAt": 1768658881764
    }
  }
}
```

### Creation Process

```
Match found between Player A and Player B
       ↓
Generate unique matchId (Firebase Push ID)
       ↓
Determine host (lower playerId becomes host)
       ↓
Create match data:
  - mode: 0 (1v1) or 1 (2v2)
  - status: "waiting"
  - team1: [playerAId]
  - team2: [playerBId]
  - team1Ranking: playerA's MMR
  - team2Ranking: playerB's MMR
  - hostEOSUserId: host's EOS User ID
  - createdAt: timestamp
       ↓
Write to /matches/{matchId}
       ↓
Remove both players from queue
       ↓
Notify both players (via Firebase listener)
       ↓
Both players read match data
       ↓
Connection flow begins
```

### Host Selection

**Deterministic Selection:**
```csharp
bool isHost = string.Compare(myPlayerId, opponentPlayerId) < 0;
```

**Why deterministic?**
- Both players independently calculate same result
- No race condition
- No need for server coordination
- Consistent across all clients

**Example:**
```
Player A ID: "abc123"
Player B ID: "xyz789"

"abc123" < "xyz789" → Player A is host
```

---

## Connection Flow

### After Match Creation

```
Match created in Firebase
       ↓
Both players' listeners trigger
       ↓
Both players read match data
       ↓
┌──────────────────┬──────────────────┐
│      Host        │     Client       │
├──────────────────┼──────────────────┤
│ StartAsHost()    │ ConnectToHost()  │
│       ↓          │       ↓          │
│ NetworkManager   │ Read host's      │
│ .StartHost()     │ EOS User ID      │
│       ↓          │       ↓          │
│ EOSTransport     │ NetworkManager   │
│ .StartServer()   │ .StartClient()   │
│       ↓          │       ↓          │
│ Listen for       │ EOSTransport     │
│ connections      │ .StartClient()   │
│       ↓          │       ↓          │
│                  │ Send connection  │
│                  │ request via EOS  │
│       ↓          │       ↓          │
│ ←────────────────┼──────────────────│
│ Accept           │                  │
│ connection       │                  │
│       ↓          │       ↓          │
│ ─────────────────┼─────────────────→│
│                  │ Connection       │
│                  │ established      │
│       ↓          │       ↓          │
│ Load match scene │ Load match scene │
└──────────────────┴──────────────────┘
```

### Match Data Synchronization

```
Match scene loaded
       ↓
NetworkGameManager spawns
       ↓
Read match data from Firebase
       ↓
Store locally:
  - currentMatchData
  - localPlayerTeam
  - opponent information
       ↓
Spawn players
       ↓
Assign teams
       ↓
Match starts
```

---

## Match Modes

### 1v1 Mode

**Queue:** `matchmaking/queue_1v1`

**Match Structure:**
```json
{
  "mode": 0,
  "team1": ["player1Id"],
  "team2": ["player2Id"],
  "team1Ranking": 612,
  "team2Ranking": 600
}
```

**Characteristics:**
- Pure skill-based
- Individual performance
- Faster queue times
- Direct competition

### 2v2 Mode

**Queue:** `matchmaking/queue_2v2`

**Match Structure:**
```json
{
  "mode": 1,
  "team1": ["player1Id", "player2Id"],
  "team2": ["player3Id", "player4Id"],
  "team1Ranking": 606,
  "team2Ranking": 594
}
```

**Characteristics:**
- Team-based
- Coordination required
- Longer queue times
- Average team MMR used

**Team MMR Calculation:**
```csharp
int team1AvgMMR = (player1MMR + player2MMR) / 2;
int team2AvgMMR = (player3MMR + player4MMR) / 2;
```

---

## Queue Management

### Queue Monitoring

```csharp
void Update()
{
    if (isInQueue)
    {
        // Update queue time display
        float timeInQueue = Time.time - queueStartTime;
        queueTimeText.text = $"Searching... {timeInQueue:F0}s";
        
        // Update search range display
        int currentRange = GetCurrentSearchRange();
        searchRangeText.text = $"Search Range: ±{currentRange} MMR";
    }
}
```

### Queue Statistics

Track queue performance:
```csharp
private int totalSearches = 0;
private float totalQueueTime = 0f;
private float averageQueueTime = 0f;

void OnMatchFound()
{
    totalSearches++;
    float queueTime = Time.time - queueStartTime;
    totalQueueTime += queueTime;
    averageQueueTime = totalQueueTime / totalSearches;
    
    Debug.Log($"Match found in {queueTime:F1}s (avg: {averageQueueTime:F1}s)");
}
```

---

## Error Handling

### Common Issues

#### 1. No Opponents Available
```
Search loop runs for extended time
       ↓
No matches found
       ↓
Search range expands to maximum (±800)
       ↓
Still no match after 60 seconds
       ↓
Show message: "No opponents available. Try again later."
       ↓
Option to stay in queue or cancel
```

#### 2. Connection Failure
```
Match created
       ↓
EOS connection attempt
       ↓
Connection fails (timeout, NAT issues)
       ↓
Show error message
       ↓
Remove match from Firebase
       ↓
Re-add both players to queue
       ↓
Continue searching
```

#### 3. Opponent Disconnects
```
Match created
       ↓
Opponent disconnects before connection
       ↓
Firebase listener detects opponent left queue
       ↓
Cancel match
       ↓
Re-add player to queue
       ↓
Continue searching
```

### Timeout Handling

```csharp
private float matchCreationTimeout = 30f;
private float matchCreationTime;

void CreateMatch(DataSnapshot opponent)
{
    matchCreationTime = Time.time;
    // Create match...
}

void Update()
{
    if (waitingForConnection)
    {
        if (Time.time - matchCreationTime > matchCreationTimeout)
        {
            OnConnectionTimeout();
        }
    }
}
```

---

## Performance Optimization

### Efficient Queries

**Bad:**
```csharp
// Downloads entire queue every search
database.Child("matchmaking/queue_1v1").GetValueAsync();
```

**Good:**
```csharp
// Use query limits and ordering
database.Child("matchmaking/queue_1v1")
    .OrderByChild("mmr")
    .StartAt(myMMR - searchRange)
    .EndAt(myMMR + searchRange)
    .GetValueAsync();
```

### Caching

```csharp
private Dictionary<string, QueueEntry> cachedQueue = new Dictionary<string, QueueEntry>();
private float lastCacheUpdate;
private float cacheRefreshInterval = 2f;

void SearchForMatch()
{
    if (Time.time - lastCacheUpdate > cacheRefreshInterval)
    {
        RefreshQueueCache();
    }
    
    // Search in cached data
    foreach (var entry in cachedQueue.Values)
    {
        // Check for match...
    }
}
```

---

## Integration with Other Systems

### With Firebase System
```
Queue entry → Firebase Realtime DB
Match data → Firebase Realtime DB
Presence tracking → Auto-remove on disconnect
```

### With EOS System
```
Match created → Host's EOS User ID stored
Client reads → Initiates EOS P2P connection
Connection established → Match begins
```

### With Ranking System
```
Get player MMR → Use for matchmaking
Match ends → Update MMR
New MMR → Used for next queue entry
```

### With Network System
```
Match found → NetworkManager.StartHost/Client()
Connection established → Load match scene
Match ends → Disconnect and return to lobby
```

---

## UI Integration

### Queue Panel

**Elements:**
- "Searching for opponent..." text
- Queue time counter
- Search range indicator
- Cancel button
- Estimated wait time (optional)

**Updates:**
```csharp
void UpdateQueueUI()
{
    float timeInQueue = Time.time - queueStartTime;
    int searchRange = GetCurrentSearchRange();
    
    queueTimeText.text = $"{timeInQueue:F0}s";
    searchRangeText.text = $"±{searchRange} MMR";
    
    // Estimated wait time based on search range
    string estimate = searchRange switch
    {
        <= 100 => "< 10s",
        <= 200 => "< 20s",
        <= 400 => "< 30s",
        _ => "< 60s"
    };
    estimateText.text = $"Est: {estimate}";
}
```

### Match Found Notification

```
Match found
       ↓
Show "Match Found!" popup
       ↓
Display opponent name and MMR
       ↓
Show "Connecting..." message
       ↓
Transition to match scene
```

---

## Best Practices

1. **Always remove from queue** when leaving matchmaking
2. **Set OnDisconnect handlers** for automatic cleanup
3. **Validate match data** before connecting
4. **Handle connection failures** gracefully
5. **Provide feedback** on queue status
6. **Implement timeouts** for all operations
7. **Cache queue data** to reduce Firebase reads
8. **Use appropriate search ranges** for population
9. **Log matchmaking metrics** for balancing
10. **Test with multiple clients** simultaneously

---

## Debugging

### Enable Debug Logs
```csharp
public bool showDebugLogs = true;

void SearchForMatch()
{
    if (showDebugLogs)
    {
        Debug.Log($"Searching with range ±{currentSearchRange} MMR");
        Debug.Log($"My MMR: {myMMR}");
        Debug.Log($"Queue entries found: {queueEntries.Count}");
    }
}
```

### Matchmaking Statistics
```csharp
void OnGUI()
{
    if (showDebugLogs)
    {
        GUILayout.Label($"In Queue: {isInQueue}");
        GUILayout.Label($"Queue Time: {Time.time - queueStartTime:F1}s");
        GUILayout.Label($"Search Range: ±{GetCurrentSearchRange()}");
        GUILayout.Label($"Total Searches: {searchCount}");
        GUILayout.Label($"Avg Queue Time: {averageQueueTime:F1}s");
    }
}
```

---

*This documentation explains the matchmaking system architecture and functionality. For implementation details, see `MatchmakingManager.cs`.*
