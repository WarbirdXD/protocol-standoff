using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Syncs player animations and visual effects across the network
/// Handles shooting, reloading, damage reactions, etc.
/// </summary>
public class NetworkAnimationSync : NetworkBehaviour
{
    [Header("Visual Effects")]
    public GameObject muzzleFlashPrefab;
    public Transform muzzlePoint;
    public GameObject bulletImpactPrefab;
    public bool showBulletTracers = true;
    public Color tracerColor = Color.yellow;
    public float tracerSpeed = 300f;
    
    [Header("Shell Ejection")]
    public ShellEjection shellEjection;
    
    [Header("Animator")]
    public Animator animator;
    
    // Animation parameter hashes (for performance)
    private static readonly int IsShooting = Animator.StringToHash("IsShooting");
    private static readonly int IsReloading = Animator.StringToHash("IsReloading");
    private static readonly int TakeDamage = Animator.StringToHash("TakeDamage");
    private static readonly int IsDead = Animator.StringToHash("IsDead");
    
    private void Awake()
    {
        // Try to find animator if not assigned
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }
    
    /// <summary>
    /// Called by WeaponController when local player shoots
    /// </summary>
    public void OnShoot(Vector3 shootDirection, Vector3 hitPoint, bool didHit)
    {
        if (!IsOwner) return;
        
        // Request server to broadcast shoot effect to all clients
        ShootServerRpc(shootDirection, hitPoint, didHit);
    }
    
    [ServerRpc]
    private void ShootServerRpc(Vector3 shootDirection, Vector3 hitPoint, bool didHit)
    {
        // Broadcast to all clients (including shooter for consistency)
        ShootClientRpc(shootDirection, hitPoint, didHit);
    }
    
    [ClientRpc]
    private void ShootClientRpc(Vector3 shootDirection, Vector3 hitPoint, bool didHit)
    {
        // Play visual effects on all clients
        PlayShootEffects(shootDirection, hitPoint, didHit);
    }
    
    /// <summary>
    /// Play shooting visual effects (muzzle flash, tracer, impact)
    /// </summary>
    private void PlayShootEffects(Vector3 shootDirection, Vector3 hitPoint, bool didHit)
    {
        // Muzzle flash
        if (muzzleFlashPrefab != null && muzzlePoint != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation);
            Destroy(flash, 0.1f);
        }
        
        // Shell ejection (only on local player to avoid spam)
        if (IsOwner && shellEjection != null)
        {
            shellEjection.EjectShell();
        }
        
        // Bullet tracer
        if (showBulletTracers && muzzlePoint != null)
        {
            SpawnBulletTracer(muzzlePoint.position, hitPoint);
        }
        
        // Bullet impact
        if (didHit && bulletImpactPrefab != null)
        {
            // Calculate impact normal (pointing away from hit point)
            Vector3 impactNormal = (hitPoint - muzzlePoint.position).normalized;
            GameObject impact = Instantiate(bulletImpactPrefab, hitPoint, Quaternion.LookRotation(impactNormal));
            Destroy(impact, 2f);
        }
        
        // Trigger shooting animation
        if (animator != null)
        {
            animator.SetTrigger(IsShooting);
        }
    }
    
    /// <summary>
    /// Called by WeaponController when local player starts reloading
    /// </summary>
    public void OnStartReload(bool isEmptyReload)
    {
        if (!IsOwner) return;
        
        StartReloadServerRpc(isEmptyReload);
    }
    
    [ServerRpc]
    private void StartReloadServerRpc(bool isEmptyReload)
    {
        StartReloadClientRpc(isEmptyReload);
    }
    
    [ClientRpc]
    private void StartReloadClientRpc(bool isEmptyReload)
    {
        // Play reload animation
        if (animator != null)
        {
            animator.SetBool(IsReloading, true);
        }
    }
    
    /// <summary>
    /// Called when reload completes
    /// </summary>
    public void OnReloadComplete()
    {
        if (!IsOwner) return;
        
        ReloadCompleteServerRpc();
    }
    
    [ServerRpc]
    private void ReloadCompleteServerRpc()
    {
        ReloadCompleteClientRpc();
    }
    
    [ClientRpc]
    private void ReloadCompleteClientRpc()
    {
        // Stop reload animation
        if (animator != null)
        {
            animator.SetBool(IsReloading, false);
        }
    }
    
    /// <summary>
    /// Called by PlayerHealth when player takes damage
    /// </summary>
    public void OnTakeDamage(Vector3 damageDirection)
    {
        if (!IsOwner) return;
        
        TakeDamageServerRpc(damageDirection);
    }
    
    [ServerRpc]
    private void TakeDamageServerRpc(Vector3 damageDirection)
    {
        TakeDamageClientRpc(damageDirection);
    }
    
    [ClientRpc]
    private void TakeDamageClientRpc(Vector3 damageDirection)
    {
        // Play damage reaction animation
        if (animator != null)
        {
            animator.SetTrigger(TakeDamage);
        }
        
        // TODO: Add damage effects (blood splatter, screen shake for local player, etc.)
    }
    
    /// <summary>
    /// Called by PlayerHealth when player dies
    /// </summary>
    public void OnDeath()
    {
        if (!IsOwner) return;
        
        DeathServerRpc();
    }
    
    [ServerRpc]
    private void DeathServerRpc()
    {
        DeathClientRpc();
    }
    
    [ClientRpc]
    private void DeathClientRpc()
    {
        // Play death animation
        if (animator != null)
        {
            animator.SetBool(IsDead, true);
        }
        
        // TODO: Add death effects (ragdoll, etc.)
    }
    
    /// <summary>
    /// Called when player respawns
    /// </summary>
    public void OnRespawn()
    {
        if (!IsOwner) return;
        
        RespawnServerRpc();
    }
    
    [ServerRpc]
    private void RespawnServerRpc()
    {
        RespawnClientRpc();
    }
    
    [ClientRpc]
    private void RespawnClientRpc()
    {
        // Reset death animation
        if (animator != null)
        {
            animator.SetBool(IsDead, false);
        }
    }
    
    /// <summary>
    /// Spawn a visual bullet tracer
    /// </summary>
    private void SpawnBulletTracer(Vector3 start, Vector3 end)
    {
        GameObject tracerObj = new GameObject("BulletTracer");
        BulletTracer tracer = tracerObj.AddComponent<BulletTracer>();
        tracer.tracerSpeed = tracerSpeed;
        tracer.tracerColor = tracerColor;
        tracer.Initialize(start, end);
    }
}
