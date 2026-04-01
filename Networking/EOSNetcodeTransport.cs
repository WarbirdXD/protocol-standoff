using System;
using UnityEngine;
using Unity.Netcode;

#if EOS_INSTALLED
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
#endif

/// <summary>
/// Custom transport that uses EOS P2P for Unity Netcode
/// Bridges Unity Netcode with Epic Online Services P2P networking
/// </summary>
public class EOSNetcodeTransport : NetworkTransport
{
#if EOS_INSTALLED
    private const int MAX_PACKET_SIZE = 1200;
    private P2PInterface p2pInterface;
    private ProductUserId remoteUserId;
    private bool isServer = false;
    private bool pendingConnect = false;
    
    public override ulong ServerClientId => 0;

    public override void DisconnectLocalClient()
    {
        if (p2pInterface != null && remoteUserId != null)
        {
            var closeOptions = new CloseConnectionOptions()
            {
                LocalUserId = SimpleEOSManager.Instance.localUserId,
                RemoteUserId = remoteUserId,
                SocketId = new SocketId() { SocketName = "NETCODE" }
            };
            
            p2pInterface.CloseConnection(ref closeOptions);
        }
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        DisconnectLocalClient();
    }

    public override ulong GetCurrentRtt(ulong clientId)
    {
        return 0; // EOS doesn't expose RTT directly
    }

    public override void Initialize(NetworkManager networkManager = null)
    {
        if (SimpleEOSManager.Instance == null || !SimpleEOSManager.Instance.isAuthenticated)
        {
            Debug.LogError("EOSNetcodeTransport: EOS not authenticated!");
            return;
        }
        
        p2pInterface = SimpleEOSManager.Instance.GetPlatformInterface().GetP2PInterface();
        
        // Add notification for incoming connections
        var addNotifyOptions = new AddNotifyPeerConnectionRequestOptions()
        {
            LocalUserId = SimpleEOSManager.Instance.localUserId,
            SocketId = new SocketId() { SocketName = "NETCODE" }
        };
        
        p2pInterface.AddNotifyPeerConnectionRequest(ref addNotifyOptions, null, OnIncomingConnectionRequest);
        
        Debug.Log("EOSNetcodeTransport initialized");
    }

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        clientId = 0;
        payload = new ArraySegment<byte>();
        receiveTime = Time.realtimeSinceStartup;
        
        if (p2pInterface == null)
            return NetworkEvent.Nothing;
        
        // Check if we have a pending connection event to report
        if (pendingConnect)
        {
            pendingConnect = false;
            clientId = isServer ? 1ul : 0ul;
            Debug.Log($"PollEvent: Reporting Connect event for clientId {clientId}");
            return NetworkEvent.Connect;
        }
        
        // Try to receive a packet
        var receiveOptions = new ReceivePacketOptions()
        {
            LocalUserId = SimpleEOSManager.Instance.localUserId,
            MaxDataSizeBytes = MAX_PACKET_SIZE,
            RequestedChannel = null
        };
        
        byte[] buffer = new byte[MAX_PACKET_SIZE];
        ProductUserId outPeerId = null;
        SocketId outSocketId = new SocketId();
        byte outChannel = 0;
        uint bytesWritten = 0;
        
        var result = p2pInterface.ReceivePacket(ref receiveOptions, ref outPeerId, ref outSocketId, out outChannel, buffer, out bytesWritten);
        
        if (result == Result.Success && bytesWritten > 0)
        {
            payload = new ArraySegment<byte>(buffer, 0, (int)bytesWritten);
            clientId = isServer ? 1ul : 0ul;
            return NetworkEvent.Data;
        }
        
        return NetworkEvent.Nothing;
    }

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
    {
        if (p2pInterface == null || remoteUserId == null)
        {
            Debug.LogWarning("Cannot send: P2P not ready");
            return;
        }
        
        var sendOptions = new SendPacketOptions()
        {
            LocalUserId = SimpleEOSManager.Instance.localUserId,
            RemoteUserId = remoteUserId,
            SocketId = new SocketId() { SocketName = "NETCODE" },
            Channel = 0,
            AllowDelayedDelivery = true,
            Data = new ArraySegment<byte>(payload.Array, payload.Offset, payload.Count),
            Reliability = networkDelivery == NetworkDelivery.Reliable ? PacketReliability.ReliableOrdered : PacketReliability.UnreliableUnordered
        };
        
        var result = p2pInterface.SendPacket(ref sendOptions);
        
        if (result != Result.Success)
        {
            Debug.LogWarning($"Failed to send packet: {result}");
        }
    }

    public override void Shutdown()
    {
        if (p2pInterface != null && remoteUserId != null)
        {
            DisconnectLocalClient();
        }
        
        p2pInterface = null;
        remoteUserId = null;
        pendingConnect = false;
    }

    public override bool StartClient()
    {
        isServer = false;
        Debug.Log("EOSNetcodeTransport: Starting as client");
        
        // Client will connect when they receive the host's ProductUserId
        // This is handled by the matchmaking system
        
        return true;
    }

    public override bool StartServer()
    {
        isServer = true;
        Debug.Log("EOSNetcodeTransport: Starting as server (host)");
        return true;
    }
    
    /// <summary>
    /// Connect to a remote peer by their ProductUserId
    /// Called by NetworkGameManager when client needs to connect to host
    /// </summary>
    public void ConnectToHost(string hostUserIdString)
    {
        if (string.IsNullOrEmpty(hostUserIdString))
        {
            Debug.LogError("Host User ID is null or empty!");
            return;
        }
        
        // Ensure transport is initialized
        if (p2pInterface == null)
        {
            Debug.Log("P2P interface not initialized, initializing now...");
            Initialize();
        }
        
        // Parse the ProductUserId from string
        remoteUserId = ProductUserId.FromString(hostUserIdString);
        if (remoteUserId == null)
        {
            Debug.LogError($"Failed to parse ProductUserId from: {hostUserIdString}");
            return;
        }
        
        Debug.Log($"Connecting to host: {hostUserIdString}");
        
        // Accept the connection
        var acceptOptions = new AcceptConnectionOptions()
        {
            LocalUserId = SimpleEOSManager.Instance.localUserId,
            RemoteUserId = remoteUserId,
            SocketId = new SocketId() { SocketName = "NETCODE" }
        };
        
        var acceptResult = p2pInterface.AcceptConnection(ref acceptOptions);
        
        if (acceptResult != Result.Success)
        {
            Debug.LogError($"Failed to accept connection: {acceptResult}");
        }
        else
        {
            Debug.Log("Connection accepted successfully");
            // Signal that we have a pending connection event to report
            pendingConnect = true;
        }
    }
    
    private void OnIncomingConnectionRequest(ref OnIncomingConnectionRequestInfo data)
    {
        Debug.Log($"Incoming connection request from: {data.RemoteUserId}");
        
        if (isServer)
        {
            // Server accepts the connection
            remoteUserId = data.RemoteUserId;
            
            var acceptOptions = new AcceptConnectionOptions()
            {
                LocalUserId = data.LocalUserId,
                RemoteUserId = data.RemoteUserId,
                SocketId = data.SocketId
            };
            
            var result = p2pInterface.AcceptConnection(ref acceptOptions);
            
            if (result == Result.Success)
            {
                Debug.Log("Server accepted incoming connection");
                // Signal that we have a pending connection event to report to Unity Netcode
                pendingConnect = true;
            }
            else
            {
                Debug.LogError($"Server failed to accept connection: {result}");
            }
        }
    }
#else
    // Stub implementation when EOS is not installed
    public override ulong ServerClientId => 0;
    public override void DisconnectLocalClient() { }
    public override void DisconnectRemoteClient(ulong clientId) { }
    public override ulong GetCurrentRtt(ulong clientId) => 0;
    public override void Initialize(NetworkManager networkManager = null) 
    {
        Debug.LogError("EOS SDK not installed! Cannot use EOSNetcodeTransport.");
    }
    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        clientId = 0;
        payload = new ArraySegment<byte>();
        receiveTime = 0;
        return NetworkEvent.Nothing;
    }
    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery) { }
    public override void Shutdown() { }
    public override bool StartClient() => false;
    public override bool StartServer() => false;
#endif
}
