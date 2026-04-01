# EOS Networking System Documentation

**Epic Online Services P2P networking with automatic NAT traversal**

---

## Overview

The EOS (Epic Online Services) Networking System provides peer-to-peer multiplayer connectivity using Epic's free infrastructure. It handles NAT traversal automatically, eliminating the need for dedicated servers, port forwarding, or expensive relay services. The system integrates with Unity Netcode for GameObjects through a custom transport layer.

---

## Architecture

```
Unity Game Code
       ↓
Unity Netcode for GameObjects
       ↓
EOSTransport (Custom Transport Layer)
       ↓
SimpleEOSManager (EOS Wrapper)
       ↓
EOS P2P SDK
       ↓
┌────────────────────────────────────┐
│      EOS Infrastructure            │
├────────────────────────────────────┤
│  NAT Traversal (STUN)              │
│  Relay Servers (TURN)              │
│  Packet Routing                    │
│  Connection Management             │
└────────────────────────────────────┘
       ↓
Internet
       ↓
Other Players
```

---

## Why EOS?

### Comparison with Alternatives

| Solution | Cost | NAT Traversal | Port Forwarding | Platform Lock |
|----------|------|---------------|-----------------|---------------|
| **Unity Transport (UTP)** | Free | Relay required | Yes | No |
| **Steam P2P** | Free | Automatic | No | Steam only |
| **Photon** | $95/month | Automatic | No | No |
| **EOS P2P** | **Free** | **Automatic** | **No** | **No** |

### EOS Advantages

1. **Free Forever:** No CCU limits, no bandwidth costs
2. **Automatic NAT Traversal:** Works behind any router
3. **Cross-Platform:** Windows, Mac, Linux, consoles
4. **Reliable:** Epic's infrastructure (Fortnite uses it)
5. **No Port Forwarding:** Players don't need to configure routers
6. **Fallback Relay:** If direct connection fails, uses relay servers
7. **Low Latency:** Direct P2P when possible

---

## System Components

### 1. SimpleEOSManager

**Purpose:** Manages EOS platform initialization, authentication, and lifecycle.

**Key Responsibilities:**
- Initialize EOS Platform
- Handle device-based authentication
- Manage Product User ID
- Provide EOS User ID to other systems
- Tick EOS platform (required for callbacks)

**Initialization Flow:**
```
Game starts
       ↓
SimpleEOSManager.Awake()
       ↓
InitializeEOS()
  - Create PlatformInterface
  - Set product credentials
  - Configure platform options
       ↓
LoginAnonymous()
  - Create device ID
  - Login with device credentials
  - Store Product User ID
       ↓
isInitialized = true
isLoggedIn = true
       ↓
Ready for networking
```

### 2. EOSTransport

**Purpose:** Unity Netcode transport layer that routes network traffic through EOS P2P.

**Key Responsibilities:**
- Implement Unity Netcode transport interface
- Send/receive packets via EOS P2P
- Handle connection requests
- Manage packet reliability
- Route traffic between Unity Netcode and EOS

**Transport Interface:**
```csharp
public class EOSTransport : NetworkTransport
{
    // Unity Netcode calls these
    public override void Send(ArraySegment<byte> data, NetworkDelivery delivery);
    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload);
    public override bool StartClient();
    public override bool StartServer();
    public override void DisconnectLocalClient();
    public override void DisconnectRemoteClient(ulong clientId);
}
```

### 3. EOS P2P Interface

**Purpose:** Low-level EOS SDK interface for packet transmission.

**Key Methods:**
- `SendPacket()`: Send data to remote peer
- `ReceivePacket()`: Read incoming data
- `AddNotifyPeerConnectionRequest()`: Listen for connection requests
- `AcceptConnection()`: Accept incoming connection
- `CloseConnection()`: Terminate connection

---

## Connection Establishment

### Host-Client Model

```
Player A (Host)                    Player B (Client)
       ↓                                  ↓
Start as Host                      Find match in Firebase
       ↓                                  ↓
NetworkManager.StartHost()         Read host's EOS User ID
       ↓                                  ↓
EOSTransport.StartServer()         NetworkManager.StartClient()
       ↓                                  ↓
Listen for connections             EOSTransport.StartClient()
       ↓                                  ↓
AddNotifyPeerConnectionRequest     SendPacket("CONNECT")
       ↓                                  ↓
       ←────── Connection Request ────────
       ↓
AcceptConnection()
       ↓
       ─────── Connection Accepted ──────→
       ↓                                  ↓
P2P Connection Established
       ↓                                  ↓
Unity Netcode traffic flows
```

### Detailed Connection Flow

#### 1. Host Setup
```
Match created in Firebase
       ↓
Host determined (lower player ID)
       ↓
Host stores EOS User ID in match data
       ↓
Host calls NetworkManager.StartHost()
       ↓
EOSTransport.StartServer()
  - Get local EOS Product User ID
  - Set up connection listener
  - AddNotifyPeerConnectionRequest()
       ↓
Waiting for client connection
```

#### 2. Client Connection
```
Client reads match data from Firebase
       ↓
Extract host's EOS User ID
       ↓
Client calls NetworkManager.StartClient()
       ↓
EOSTransport.StartClient()
  - Get local EOS Product User ID
  - Parse host's EOS User ID
  - Create SendPacketOptions
       ↓
SendPacket("CONNECT") to host
       ↓
Wait for acceptance
```

#### 3. Connection Acceptance
```
Host receives connection request
       ↓
OnIncomingConnectionRequest callback
       ↓
Validate request
       ↓
AcceptConnection()
       ↓
Store remote user ID
       ↓
Connection established
       ↓
Both sides can now send/receive
```

---

## NAT Traversal

### How It Works

#### 1. Direct Connection (Best Case)
```
Client ──────────────────────→ Host
         Direct P2P
         (Lowest latency)
```

**When it works:**
- Both players have open NAT
- Routers support UPnP
- Same local network

#### 2. STUN (NAT Hole Punching)
```
Client ──→ STUN Server ←── Host
              ↓
         Discovers public IPs
              ↓
Client ←──────────────────→ Host
         Direct P2P through NAT
```

**When it works:**
- Moderate NAT types
- Symmetric NAT on one side
- Most home routers

#### 3. TURN Relay (Fallback)
```
Client ──→ Relay Server ←── Host
              ↓
         Forwards packets
              ↓
         (Higher latency)
```

**When it works:**
- Strict NAT on both sides
- Corporate firewalls
- Always works (guaranteed)

### EOS Handles This Automatically

The game code doesn't need to know which method is used. EOS:
1. Attempts direct connection first
2. Falls back to STUN if needed
3. Uses TURN relay as last resort
4. All transparent to the application

---

## Packet Handling

### Reliability Modes

#### 1. Reliable Ordered
```csharp
var sendOptions = new SendPacketOptions()
{
    Reliability = PacketReliability.ReliableOrdered,
    AllowDelayedDelivery = true
};
```

**Use for:**
- Player damage
- Death events
- Score updates
- Match state changes
- Critical game events

**Guarantees:**
- Packet arrives
- Arrives in order
- No duplicates

#### 2. Unreliable Unordered
```csharp
var sendOptions = new SendPacketOptions()
{
    Reliability = PacketReliability.UnreliableUnordered,
    AllowDelayedDelivery = false
};
```

**Use for:**
- Position updates
- Rotation updates
- Animation states
- Non-critical data

**Characteristics:**
- May be lost
- May arrive out of order
- Lower latency

### Channel System

EOS supports multiple channels for traffic separation:

```csharp
var sendOptions = new SendPacketOptions()
{
    Channel = 0,  // Game state
    // or
    Channel = 1,  // Voice chat
    // or
    Channel = 2   // Telemetry
};
```

**Benefits:**
- Separate bandwidth allocation
- Independent reliability settings
- Traffic prioritization

### Packet Size Limits

- **Maximum packet size:** 1170 bytes
- **Recommended:** Keep under 1000 bytes
- **Large data:** Fragment into multiple packets

---

## Unity Netcode Integration

### Transport Layer Implementation

EOSTransport implements Unity Netcode's transport interface:

```csharp
public override void Send(ArraySegment<byte> data, NetworkDelivery delivery)
{
    // Convert Unity delivery mode to EOS reliability
    PacketReliability reliability = delivery switch
    {
        NetworkDelivery.Reliable => PacketReliability.ReliableOrdered,
        NetworkDelivery.Unreliable => PacketReliability.UnreliableUnordered,
        _ => PacketReliability.ReliableOrdered
    };
    
    // Send via EOS P2P
    var sendOptions = new SendPacketOptions()
    {
        LocalUserId = localUserId,
        RemoteUserId = remoteUserId,
        SocketId = new SocketId() { SocketName = "GAME" },
        Channel = 0,
        Reliability = reliability,
        Data = data
    };
    
    p2pInterface.SendPacket(ref sendOptions);
}
```

### Event Polling

Unity Netcode polls for network events:

```csharp
public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload)
{
    // Check for incoming packets
    var receiveOptions = new ReceivePacketOptions()
    {
        LocalUserId = localUserId,
        MaxDataSizeBytes = 1200
    };
    
    var result = p2pInterface.ReceivePacket(ref receiveOptions, out remoteUserId, out socketId, out channel, out data);
    
    if (result == Result.Success)
    {
        clientId = ConvertToNetcodeId(remoteUserId);
        payload = data;
        return NetworkEvent.Data;
    }
    
    // Check for connection events
    if (pendingConnection)
    {
        clientId = pendingClientId;
        payload = default;
        return NetworkEvent.Connect;
    }
    
    return NetworkEvent.Nothing;
}
```

---

## Authentication System

### Device-Based Authentication

EOS uses device IDs for anonymous authentication:

```
First Launch
       ↓
CreateDeviceId()
  - Uses device model
  - Generates unique ID
  - Stores locally
       ↓
Login with device ID
       ↓
Receive Product User ID
       ↓
Store for P2P connections
```

### Product User ID

**Format:** `xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx` (32 hex characters)

**Usage:**
- Identifies player in EOS ecosystem
- Used for P2P connection establishment
- Stored in Firebase matchmaking queue
- Required for SendPacket/ReceivePacket

**Conversion:**
```csharp
// String to ProductUserId
ProductUserId.FromString(eosUserIdString, out ProductUserId userId);

// ProductUserId to String
string eosUserIdString = userId.ToString();
```

---

## Connection Management

### Connection States

```
Disconnected
       ↓
Connecting (SendPacket sent)
       ↓
Connected (AcceptConnection received)
       ↓
Active (Data flowing)
       ↓
Disconnecting (CloseConnection called)
       ↓
Disconnected
```

### Timeout Handling

```csharp
private float connectionTimeout = 10f;
private float connectionStartTime;

void StartConnection()
{
    connectionStartTime = Time.time;
    SendConnectionRequest();
}

void Update()
{
    if (isConnecting && Time.time - connectionStartTime > connectionTimeout)
    {
        // Connection timed out
        OnConnectionFailed("Connection timeout");
    }
}
```

### Disconnect Handling

```csharp
public void Disconnect()
{
    if (p2pInterface != null && remoteUserId != null)
    {
        var closeOptions = new CloseConnectionOptions()
        {
            LocalUserId = localUserId,
            RemoteUserId = remoteUserId,
            SocketId = new SocketId() { SocketName = "GAME" }
        };
        
        p2pInterface.CloseConnection(ref closeOptions);
    }
    
    // Clean up Unity Netcode
    NetworkManager.Singleton.Shutdown();
}
```

---

## Error Handling

### Common EOS Errors

| Error Code | Meaning | Solution |
|------------|---------|----------|
| `InvalidParameters` | Invalid function parameters | Check parameter values |
| `NotFound` | User/connection not found | Verify EOS User ID |
| `AlreadyPending` | Operation already in progress | Wait for completion |
| `InvalidUser` | User not authenticated | Re-authenticate |
| `ConnectionClosed` | Connection terminated | Reconnect |
| `Timeout` | Operation timed out | Retry with backoff |

### Error Recovery

```csharp
private int maxRetries = 3;
private int currentRetry = 0;

void SendPacketWithRetry(byte[] data)
{
    var result = p2pInterface.SendPacket(ref sendOptions);
    
    if (result != Result.Success)
    {
        if (currentRetry < maxRetries)
        {
            currentRetry++;
            StartCoroutine(RetryAfterDelay(data, 1f));
        }
        else
        {
            OnConnectionFailed($"Send failed: {result}");
        }
    }
    else
    {
        currentRetry = 0; // Reset on success
    }
}
```

---

## Performance Optimization

### Bandwidth Management

**Reduce packet frequency:**
```csharp
private float sendInterval = 0.05f; // 20 updates/second
private float lastSendTime;

void Update()
{
    if (Time.time - lastSendTime >= sendInterval)
    {
        SendPositionUpdate();
        lastSendTime = Time.time;
    }
}
```

**Compress data:**
```csharp
// Instead of sending full float (4 bytes)
float position = 123.456f;

// Send as short (2 bytes) with precision loss
short compressedPos = (short)(position * 100);

// Decompress on receive
float decompressedPos = compressedPos / 100f;
```

### Latency Optimization

**Client-side prediction:**
```csharp
// Client predicts movement immediately
void Update()
{
    if (IsOwner)
    {
        // Apply input immediately (responsive)
        transform.position += input * speed * Time.deltaTime;
        
        // Send to server for validation
        SendMovementServerRpc(transform.position);
    }
}
```

**Server reconciliation:**
```csharp
[ServerRpc]
void SendMovementServerRpc(Vector3 clientPosition)
{
    // Server validates and corrects if needed
    if (Vector3.Distance(clientPosition, serverPosition) > threshold)
    {
        // Client is too far off, correct them
        CorrectPositionClientRpc(serverPosition);
    }
}
```

---

## Integration with Other Systems

### With Firebase Matchmaking
```
Match created → Host's EOS User ID stored in Firebase
             → Client reads EOS User ID
             → Client initiates EOS connection
```

### With Unity Netcode
```
Unity Netcode → EOSTransport.Send()
             → EOS P2P SendPacket()
             → Internet
             → EOS P2P ReceivePacket()
             → EOSTransport.PollEvent()
             → Unity Netcode
```

### With Match System
```
Match starts → NetworkManager.StartHost/Client()
            → EOS connection established
            → Match scene loads
            → Gameplay begins
```

---

## Best Practices

1. **Always check EOS initialization** before network operations
2. **Handle connection failures gracefully** with retry logic
3. **Use appropriate reliability** for different data types
4. **Keep packets small** (<1000 bytes recommended)
5. **Implement timeout handling** for all operations
6. **Clean up connections** on scene changes
7. **Test with different NAT types** (use NAT simulators)
8. **Monitor bandwidth usage** to avoid throttling
9. **Use channels** to separate traffic types
10. **Tick EOS platform** every frame for callbacks

---

## Debugging

### Enable EOS Logging
```csharp
var logOptions = new SetLogLevelOptions()
{
    LogCategory = LogCategory.AllCategories,
    LogLevel = LogLevel.Verbose
};
PlatformInterface.SetLogLevel(ref logOptions);
```

### Connection Diagnostics
```csharp
void LogConnectionInfo()
{
    Debug.Log($"Local User ID: {localUserId}");
    Debug.Log($"Remote User ID: {remoteUserId}");
    Debug.Log($"Connection State: {connectionState}");
    Debug.Log($"Packets Sent: {packetsSent}");
    Debug.Log($"Packets Received: {packetsReceived}");
}
```

### Network Statistics
```csharp
private int packetsSent = 0;
private int packetsReceived = 0;
private int bytesReceived = 0;

void OnGUI()
{
    GUILayout.Label($"Packets Sent: {packetsSent}");
    GUILayout.Label($"Packets Received: {packetsReceived}");
    GUILayout.Label($"Bandwidth: {bytesReceived / 1024f:F2} KB/s");
}
```

---

*This documentation explains the EOS networking system architecture and functionality. For implementation details, see `SimpleEOSManager.cs` and `EOSTransport.cs`.*
