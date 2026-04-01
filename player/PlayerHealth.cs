using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 150f;          // R6 Siege style - longer TTK, more tactical
    public float headshotMultiplier = 2.5f; // Headshots very rewarding (instant kill potential)
    
    [Header("Health Regeneration")]
    public bool enableRegeneration = true;       // Enable health regen
    public float regenDelay = 5f;                // Delay after taking damage before regen starts
    public float regenRate = 10f;                // HP per second
    public float regenStartThreshold = 0.99f;    // Start regen below this % of max health
    
    [Header("Respawn Settings")]
    public float respawnDelay = 3f;              // Time before respawn
    public bool autoRespawn = true;              // Auto respawn or wait for input
    
    [Header("Corpse Settings")]
    public GameObject corpsePrefab;              // Optional ragdoll/corpse prefab to spawn
    public float corpseLifetime = 10f;           // How long corpse stays before cleanup
    
    [Header("Events")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent<bool> OnDeath; // bool = wasHeadshot
    public UnityEvent OnRespawn;
    
    private NetworkVariable<float> currentHealth = new NetworkVariable<float>(150f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<bool> isDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private Vector3 deathLocation;
    private DynamicSpawnSystem dynamicSpawnSystem;
    private float timeSinceLastDamage = 0f;
    
    // Stats tracking (networked)
    private NetworkVariable<int> kills = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> deaths = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private NetworkAnimationSync animationSync;
    private PlayerAudioManager audioManager;
    
    public float CurrentHealth => currentHealth.Value;
    public bool IsDead => isDead.Value;
    public int Kills => kills.Value;
    public int Deaths => deaths.Value;
    
    public void AddKill()
    {
        // Only server can modify kills
        if (!IsServer) return;
        kills.Value++;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Get animation sync component
        animationSync = GetComponent<NetworkAnimationSync>();
        
        // Subscribe to network variable changes
        currentHealth.OnValueChanged += OnHealthChangedNetwork;
        isDead.OnValueChanged += OnIsDeadChanged;
        
        // Initialize health on server
        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }
        
        // Invoke initial health for UI
        OnHealthChanged?.Invoke(currentHealth.Value);
        
        // Find dynamic spawn system (server only)
        if (IsServer)
        {
            dynamicSpawnSystem = FindFirstObjectByType<DynamicSpawnSystem>();
            animationSync = GetComponent<NetworkAnimationSync>();
            audioManager = GetComponent<PlayerAudioManager>();
            if (dynamicSpawnSystem == null)
            {
                Debug.LogWarning("PlayerHealth: No DynamicSpawnSystem found in scene!");
            }
        }
    }
    
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        currentHealth.OnValueChanged -= OnHealthChangedNetwork;
        isDead.OnValueChanged -= OnIsDeadChanged;
    }
    
    private void OnHealthChangedNetwork(float oldValue, float newValue)
    {
        OnHealthChanged?.Invoke(newValue);
    }
    
    private void OnIsDeadChanged(bool oldValue, bool newValue)
    {
        if (newValue && !oldValue)
        {
            // Player just died - disable controls locally
            DisablePlayerControls();
        }
    }
    
    private void Update()
    {
        // Only server handles regeneration
        if (!IsServer || isDead.Value || !enableRegeneration) return;
        
        // Increment time since last damage
        timeSinceLastDamage += Time.deltaTime;
        
        // Check if we should regenerate
        if (timeSinceLastDamage >= regenDelay && currentHealth.Value < maxHealth * regenStartThreshold)
        {
            // Regenerate health
            currentHealth.Value += regenRate * Time.deltaTime;
            currentHealth.Value = Mathf.Min(currentHealth.Value, maxHealth);
        }
    }
    
    public void TakeDamage(float damage, bool isHeadshot = false, ulong attackerClientId = 0)
    {
        if (isDead.Value) return;
        
        // Only server processes damage
        if (!IsServer)
        {
            TakeDamageServerRpc(damage, isHeadshot, attackerClientId);
            return;
        }
        
        float finalDamage = damage;
        if (isHeadshot)
        {
            finalDamage *= headshotMultiplier;
        }
        
        currentHealth.Value -= finalDamage;
        currentHealth.Value = Mathf.Max(0f, currentHealth.Value);
        
        // Reset regeneration timer
        timeSinceLastDamage = 0f;
        
        // Trigger damage animation on all clients
        if (animationSync != null)
        {
            Vector3 damageDirection = Vector3.zero; // Could calculate from attacker position
            NotifyDamageClientRpc(damageDirection);
        }
        
        if (currentHealth.Value <= 0f)
        {
            Die(isHeadshot, attackerClientId);
        }
    }
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TakeDamageServerRpc(float damage, bool isHeadshot, ulong attackerClientId)
    {
        TakeDamage(damage, isHeadshot, attackerClientId);
    }
    
    private void Die(bool wasHeadshot, ulong attackerClientId)
    {
        if (isDead.Value) return;
        
        isDead.Value = true;
        deaths.Value++; // Increment death count
        deathLocation = transform.position;
        
        // Instantly disable colliders so bullets pass through
        DisableColliders();
        
        // Award kill to attacker and refill their ammo
        if (attackerClientId != 0 && attackerClientId != OwnerClientId)
        {
            var attacker = NetworkManager.Singleton.ConnectedClients[attackerClientId].PlayerObject;
            if (attacker != null)
            {
                var attackerHealth = attacker.GetComponent<PlayerHealth>();
                if (attackerHealth != null)
                {
                    attackerHealth.AddKill();
                }
                
                // Refill attacker's ammo as reward for kill
                var attackerWeapon = attacker.GetComponent<WeaponController>();
                if (attackerWeapon != null)
                {
                    // Call ClientRpc to refill ammo on the attacker's client
                    RefillKillerAmmoClientRpc(attackerClientId);
                }
            }
        }
        
        // Spawn corpse if prefab is assigned (use deathLocation for accuracy)
        if (corpsePrefab != null)
        {
            SpawnCorpseClientRpc(deathLocation, transform.rotation);
        }
        
        // Notify all clients of death
        NotifyDeathClientRpc(wasHeadshot);
        
        // Register death with dynamic spawn system
        if (dynamicSpawnSystem != null)
        {
            dynamicSpawnSystem.RegisterDeath(deathLocation);
        }
        
        // Auto respawn after delay (server only)
        if (autoRespawn)
        {
            Invoke(nameof(HandleRespawn), respawnDelay);
        }
    }
    
    [ClientRpc]
    private void NotifyDeathClientRpc(bool wasHeadshot)
    {
        OnDeath?.Invoke(wasHeadshot);
        
        // Trigger death animation
        if (animationSync != null)
        {
            animationSync.OnDeath();
        }
        
        // Play death sound
        if (audioManager != null)
        {
            audioManager.PlayDeathSound();
        }
        
        // Disable colliders on all clients
        DisableColliders();
        
        // Hide player model immediately
        HidePlayerModel();
        
        DisablePlayerControls();
    }
    
    /// <summary>
    /// Refill ammo on the client who got the kill
    /// </summary>
    [ClientRpc]
    private void RefillKillerAmmoClientRpc(ulong killerClientId)
    {
        // Only execute on the killer's client
        if (NetworkManager.Singleton.LocalClientId != killerClientId) return;
        
        // Find the killer's player object
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(killerClientId, out var clientInfo))
        {
            var killerPlayer = clientInfo.PlayerObject;
            if (killerPlayer != null)
            {
                var weaponController = killerPlayer.GetComponent<WeaponController>();
                if (weaponController != null)
                {
                    weaponController.RefillAmmo();
                    Debug.Log($"[CLIENT {killerClientId}] Kill reward: Ammo refilled!");
                }
            }
        }
    }
    
    [ClientRpc]
    private void SpawnCorpseClientRpc(Vector3 position, Quaternion rotation)
    {
        if (corpsePrefab == null) return;
        
        // Spawn corpse at death location
        GameObject corpse = Instantiate(corpsePrefab);
        
        // Force exact position and rotation (ignore prefab offsets)
        corpse.transform.position = position;
        corpse.transform.rotation = rotation;
        
        // Copy velocity if there's a rigidbody
        var playerRb = GetComponent<Rigidbody>();
        var corpseRb = corpse.GetComponent<Rigidbody>();
        if (playerRb != null && corpseRb != null)
        {
            corpseRb.linearVelocity = playerRb.linearVelocity;
        }
        
        // Destroy corpse after lifetime
        Destroy(corpse, corpseLifetime);
    }
    
    private void DisableColliders()
    {
        // Disable all colliders so bullets pass through dead player
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }
    }
    
    private void EnableColliders()
    {
        // Re-enable colliders on respawn
        var colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = true;
        }
    }
    
    private void HidePlayerModel()
    {
        // Hide all renderers so player model is invisible
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
        }
    }
    
    private void ShowPlayerModel()
    {
        // Show all renderers on respawn
        var renderers = GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = true;
        }
    }
    
    [ClientRpc]
    private void NotifyDamageClientRpc(Vector3 damageDirection)
    {
        // Trigger damage animation on all clients
        if (animationSync != null)
        {
            animationSync.OnTakeDamage(damageDirection);
        }
        
        // Play damage sound
        if (audioManager != null)
        {
            audioManager.PlayDamageSound();
        }
    }
    
    private void DisablePlayerControls()
    {
        // Only disable controls for the owner
        if (!IsOwner) return;
        
        var controller = GetComponent<FPSController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        
        var weapon = GetComponent<WeaponController>();
        if (weapon != null)
        {
            weapon.enabled = false;
        }
    }
    
    private void HandleRespawn()
    {
        if (dynamicSpawnSystem != null)
        {
            dynamicSpawnSystem.SpawnPlayer(gameObject, deathLocation);
        }
        else
        {
            // Fallback: respawn at current position
            Respawn();
        }
    }
    
    public void Respawn(Vector3? spawnPosition = null, Quaternion? spawnRotation = null)
    {
        // Only server can respawn
        if (!IsServer) return;
        
        // Disable CharacterController before teleporting to prevent physics issues
        var controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }
        
        Vector3 finalPosition = transform.position;
        Quaternion finalRotation = transform.rotation;
        
        if (spawnPosition.HasValue)
        {
            transform.position = spawnPosition.Value;
            finalPosition = spawnPosition.Value;
        }
        
        if (spawnRotation.HasValue)
        {
            transform.rotation = spawnRotation.Value;
            finalRotation = spawnRotation.Value;
        }
        
        // Re-enable CharacterController after teleporting
        if (controller != null)
        {
            controller.enabled = true;
        }
        
        currentHealth.Value = maxHealth;
        isDead.Value = false;
        
        // Notify clients of respawn with position and rotation
        NotifyRespawnClientRpc(finalPosition, finalRotation);
        
        Debug.Log($"<color=green>Player respawned at {transform.position}</color>");
    }
    
    [ClientRpc]
    private void NotifyRespawnClientRpc(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Teleport to respawn position (disable CharacterController first)
        var charController = GetComponent<CharacterController>();
        if (charController != null)
        {
            charController.enabled = false;
        }
        
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        
        if (charController != null)
        {
            charController.enabled = true;
        }
        
        // Re-enable colliders on respawn
        EnableColliders();
        
        // Show player model again
        ShowPlayerModel();
        
        // Refill ammo on respawn
        var weapon = GetComponent<WeaponController>();
        if (weapon != null)
        {
            weapon.RefillAmmo();
        }
        
        OnRespawn?.Invoke();
        
        // Trigger respawn animation
        if (animationSync != null)
        {
            animationSync.OnRespawn();
        }
        
        // Only enable controls for the owner
        if (!IsOwner) return;
        
        var controller = GetComponent<FPSController>();
        if (controller != null)
        {
            controller.enabled = true;
        }
        
        // Re-enable weapon (reuse weapon variable from above)
        if (weapon != null)
        {
            weapon.enabled = true;
        }
        
    }
}
