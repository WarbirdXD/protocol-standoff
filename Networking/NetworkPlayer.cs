using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Network component for player - syncs position, rotation, and actions
/// </summary>
public class NetworkPlayer : NetworkBehaviour
{
    [Header("Player Info")]
    private NetworkVariable<int> playerTeam = new NetworkVariable<int>(0);
    private NetworkVariable<bool> isReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private string playerId;
    private string playerName;
    
    [Header("Components")]
    private FPSController fpsController;
    private PlayerHealth playerHealth;
    
    [Header("Team Visuals")]
    [Tooltip("Renderer(s) to apply team colors to (e.g., player body mesh)")]
    public Renderer[] teamRenderers;
    
    [Tooltip("Material index to change (usually 0 for single material)")]
    public int materialIndex = 0;
    
    [Header("Team Colors")]
    public Color team1Color = new Color(0.2f, 0.5f, 1f); // Blue
    public Color team2Color = new Color(1f, 0.3f, 0.2f); // Red
    
    private void Awake()
    {
        fpsController = GetComponent<FPSController>();
        playerHealth = GetComponent<PlayerHealth>();
        
        // Disable all player controls by default - they'll be enabled in OnNetworkSpawn if this is the owner
        if (fpsController != null)
        {
            fpsController.enabled = false;
        }
        
        WeaponController weaponController = GetComponent<WeaponController>();
        if (weaponController != null)
        {
            weaponController.enabled = false;
        }
        
        // Disable PlayerInput to prevent input conflicts
        UnityEngine.InputSystem.PlayerInput playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = false;
        }
        
        // Disable camera and audio listener by default
        Camera playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }
        
        AudioListener audioListener = GetComponentInChildren<AudioListener>();
        if (audioListener != null)
        {
            audioListener.enabled = false;
        }
        
        // Disable UI canvases by default
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        foreach (var canvas in canvases)
        {
            canvas.enabled = false;
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] NetworkPlayer.OnNetworkSpawn - PlayerName: {playerName}, IsOwner: {IsOwner}, OwnerClientId: {OwnerClientId}");
        
        // Subscribe to team changes to register player once team is set
        playerTeam.OnValueChanged += OnTeamChanged;
        
        // If team is already set (non-zero), register immediately
        if (playerTeam.Value != 0)
        {
            RegisterWithMatchManager(playerTeam.Value);
            ApplyTeamVisuals(playerTeam.Value);
        }
        
        // Only enable controls for local player
        if (IsOwner)
        {
            Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] ENABLING controls for LOCAL player: {playerName}");
            
            // Enable local player controls
            if (fpsController != null)
            {
                fpsController.enabled = true;
                Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] FPSController ENABLED for {playerName}");
            }
            else
            {
                Debug.LogError($"[CLIENT {NetworkManager.Singleton.LocalClientId}] FPSController is NULL for {playerName}!");
            }
            
            // Enable PlayerInput for local player
            UnityEngine.InputSystem.PlayerInput playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = true;
                Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] PlayerInput ENABLED for {playerName}");
            }
            
            // Enable camera
            Camera playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                playerCamera.enabled = true;
            }
            
            // Enable audio listener
            AudioListener audioListener = GetComponentInChildren<AudioListener>();
            if (audioListener != null)
            {
                audioListener.enabled = true;
            }
            
            // Enable Canvas (UI)
            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            foreach (var canvas in canvases)
            {
                canvas.enabled = true;
            }
            
            Debug.Log($"Enabled controls for local player: {playerName}");
        }
        else
        {
            Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] DISABLING controls for REMOTE player: {playerName} (Owner: {OwnerClientId})");
            
            // Disable controls for remote players
            if (fpsController != null)
            {
                fpsController.enabled = false;
                Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] FPSController DISABLED for remote {playerName}");
            }
            
            // Disable PlayerInput for remote players
            UnityEngine.InputSystem.PlayerInput playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (playerInput != null)
            {
                playerInput.enabled = false;
                Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] PlayerInput DISABLED for remote {playerName}");
            }
            
            // Disable camera
            Camera playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null)
            {
                playerCamera.enabled = false;
            }
            
            // Disable audio listener
            AudioListener audioListener = GetComponentInChildren<AudioListener>();
            if (audioListener != null)
            {
                audioListener.enabled = false;
            }
            
            // Disable Canvas (UI) - prevents duplicate UI
            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            foreach (var canvas in canvases)
            {
                canvas.enabled = false;
            }
            
            // Disable WeaponController if it exists
            WeaponController weaponController = GetComponent<WeaponController>();
            if (weaponController != null)
            {
                weaponController.enabled = false;
            }
            
            Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] FINISHED disabling controls for REMOTE player: {playerName}");
        }
        
        // Mark player as ready immediately - no delay needed
        if (IsOwner)
        {
            Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] Marking player as ready...");
            MarkPlayerReady();
        }
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        // Unsubscribe from events
        playerTeam.OnValueChanged -= OnTeamChanged;
    }
    
    private void OnTeamChanged(int oldTeam, int newTeam)
    {
        Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] Team changed from {oldTeam} to {newTeam}");
        if (newTeam != 0)
        {
            RegisterWithMatchManager(newTeam);
            ApplyTeamVisuals(newTeam);
        }
    }
    
    /// <summary>
    /// Apply team-based visual changes (colors/materials)
    /// </summary>
    private void ApplyTeamVisuals(int team)
    {
        if (teamRenderers == null || teamRenderers.Length == 0)
        {
            Debug.LogWarning($"No team renderers assigned for {playerName}");
            return;
        }
        
        Color teamColor = team == 1 ? team1Color : team2Color;
        
        foreach (var renderer in teamRenderers)
        {
            if (renderer != null && renderer.materials.Length > materialIndex)
            {
                // Create a new material instance to avoid modifying the shared material
                Material[] materials = renderer.materials;
                materials[materialIndex] = new Material(materials[materialIndex]);
                materials[materialIndex].color = teamColor;
                renderer.materials = materials;
                
                Debug.Log($"Applied team {team} color to {renderer.name}");
            }
        }
    }
    
    private void RegisterWithMatchManager(int team)
    {
        if (playerHealth != null)
        {
            MatchManager matchManager = FindFirstObjectByType<MatchManager>();
            if (matchManager != null)
            {
                Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] Registering player with team {team}");
                matchManager.RegisterPlayer(playerHealth, team);
            }
        }
    }
    
    private void MarkPlayerReady()
    {
        if (!IsOwner) return;
        
        isReady.Value = true;
        Debug.Log($"[CLIENT {NetworkManager.Singleton.LocalClientId}] Player marked as ready, notifying server...");
        
        // Notify server that this player is ready
        NotifyReadyServerRpc();
    }
    
    [ServerRpc]
    private void NotifyReadyServerRpc()
    {
        Debug.Log($"[SERVER] NotifyReadyServerRpc received from client {OwnerClientId}");
        MatchManager matchManager = FindFirstObjectByType<MatchManager>();
        if (matchManager != null)
        {
            Debug.Log($"[SERVER] Found MatchManager, calling OnPlayerReady({OwnerClientId})");
            matchManager.OnPlayerReady(OwnerClientId);
        }
        else
        {
            Debug.LogError("[SERVER] MatchManager not found!");
        }
    }
    
    public bool IsPlayerReady()
    {
        return isReady.Value;
    }
    
    /// <summary>
    /// Set player data (called by server BEFORE network spawn)
    /// </summary>
    public void SetPlayerData(string id, string name, int team)
    {
        playerId = id;
        playerName = name;
        playerTeam.Value = team; // Set directly since this is called before spawn
        
        gameObject.name = $"Player_{name}_Team{team}";
        
        Debug.Log($"SetPlayerData: {name} assigned to Team {team}");
    }
    
    /// <summary>
    /// Get player's team
    /// </summary>
    public int GetTeam()
    {
        return playerTeam.Value;
    }
    
    /// <summary>
    /// Get player's name
    /// </summary>
    public string GetPlayerName()
    {
        return playerName;
    }
    
    /// <summary>
    /// Get player's ID
    /// </summary>
    public string GetPlayerId()
    {
        return playerId;
    }
}
