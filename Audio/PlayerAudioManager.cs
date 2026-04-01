using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Manages all audio for player actions (footsteps, shooting, reloading, etc.)
/// Handles 3D spatial audio and network synchronization
/// </summary>
public class PlayerAudioManager : NetworkBehaviour
{
    [Header("Audio Sources")]
    [Tooltip("Audio source for footsteps")]
    public AudioSource footstepSource;
    
    [Tooltip("Audio source for weapon sounds")]
    public AudioSource weaponSource;
    
    [Tooltip("Audio source for voice/character sounds")]
    public AudioSource voiceSource;
    
    [Header("Footstep Sounds")]
    public AudioClip[] footstepSounds;
    public float footstepVolume = 1.0f;
    public float footstepPitchVariation = 0.1f;
    
    [Header("Weapon Sounds")]
    public AudioClip shootSound;
    public AudioClip reloadSound;
    public AudioClip reloadEmptySound;
    public AudioClip magOutSound;
    public AudioClip magInSound;
    public AudioClip boltSound;
    public AudioClip dryFireSound;
    public float weaponVolume = 1.0f;
    
    [Header("Character Sounds")]
    public AudioClip[] damageSounds;
    public AudioClip[] deathSounds;
    public AudioClip jumpSound;
    public AudioClip landSound;
    public float voiceVolume = 1.0f;
    
    [Header("3D Audio Settings")]
    [Tooltip("Distance where sound is at full volume")]
    public float minDistance = 5f;
    [Tooltip("Distance where sound fades to zero")]
    public float maxDistance = 100f;
    public float dopplerLevel = 0.5f;
    [Tooltip("Use logarithmic rolloff for more realistic distance attenuation")]
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
    
    private float lastFootstepTime;
    private int lastFootstepIndex = -1;
    
    private void Start()
    {
        SetupAudioSources();
    }
    
    private void SetupAudioSources()
    {
        // Setup footstep source
        if (footstepSource != null)
        {
            footstepSource.spatialBlend = 1f; // Full 3D
            footstepSource.rolloffMode = rolloffMode;
            footstepSource.minDistance = minDistance;
            footstepSource.maxDistance = maxDistance;
            footstepSource.dopplerLevel = dopplerLevel;
            footstepSource.volume = footstepVolume;
        }
        
        // Setup weapon source
        if (weaponSource != null)
        {
            weaponSource.spatialBlend = 1f; // Full 3D
            weaponSource.rolloffMode = rolloffMode;
            weaponSource.minDistance = minDistance;
            weaponSource.maxDistance = maxDistance * 1.5f; // Gunshots travel further
            weaponSource.dopplerLevel = dopplerLevel;
            weaponSource.volume = weaponVolume;
        }
        
        // Setup voice source
        if (voiceSource != null)
        {
            voiceSource.spatialBlend = 1f; // Full 3D
            voiceSource.rolloffMode = rolloffMode;
            voiceSource.minDistance = minDistance;
            voiceSource.maxDistance = maxDistance * 0.8f; // Voice doesn't travel as far
            voiceSource.dopplerLevel = dopplerLevel;
            voiceSource.volume = voiceVolume;
        }
    }
    
    #region Footstep Sounds
    
    /// <summary>
    /// Play footstep sound (called by FPSController)
    /// </summary>
    public void PlayFootstep()
    {
        if (footstepSounds == null || footstepSounds.Length == 0 || footstepSource == null)
            return;
        
        // Avoid playing same footstep twice in a row
        int index = Random.Range(0, footstepSounds.Length);
        if (index == lastFootstepIndex && footstepSounds.Length > 1)
        {
            index = (index + 1) % footstepSounds.Length;
        }
        lastFootstepIndex = index;
        
        AudioClip clip = footstepSounds[index];
        if (clip != null)
        {
            // Add pitch variation for realism
            footstepSource.pitch = 1f + Random.Range(-footstepPitchVariation, footstepPitchVariation);
            footstepSource.PlayOneShot(clip);
            
            // Sync footstep sound to other clients
            if (IsServer)
            {
                PlayFootstepClientRpc(index);
            }
            else if (IsOwner)
            {
                PlayFootstepServerRpc(index);
            }
        }
    }
    
    [ServerRpc]
    private void PlayFootstepServerRpc(int clipIndex)
    {
        PlayFootstepClientRpc(clipIndex);
    }
    
    [ClientRpc]
    private void PlayFootstepClientRpc(int clipIndex)
    {
        // Don't play on owner - they already heard it locally
        if (IsOwner) return;
        
        if (footstepSounds != null && clipIndex >= 0 && clipIndex < footstepSounds.Length && footstepSource != null)
        {
            AudioClip clip = footstepSounds[clipIndex];
            if (clip != null)
            {
                footstepSource.pitch = 1f + Random.Range(-footstepPitchVariation, footstepPitchVariation);
                footstepSource.PlayOneShot(clip);
            }
        }
    }
    
    #endregion
    
    #region Weapon Sounds
    
    /// <summary>
    /// Play shooting sound
    /// </summary>
    public void PlayShootSound()
    {
        if (shootSound != null && weaponSource != null)
        {
            weaponSource.pitch = 1f + Random.Range(-0.05f, 0.05f);
            weaponSource.PlayOneShot(shootSound);
            
            // Sync to other clients
            if (IsServer)
            {
                PlayShootSoundClientRpc();
            }
            else if (IsOwner)
            {
                PlayShootSoundServerRpc();
            }
        }
    }
    
    [ServerRpc]
    private void PlayShootSoundServerRpc()
    {
        PlayShootSoundClientRpc();
    }
    
    [ClientRpc]
    private void PlayShootSoundClientRpc()
    {
        if (IsOwner) return;
        
        if (shootSound != null && weaponSource != null)
        {
            weaponSource.pitch = 1f + Random.Range(-0.05f, 0.05f);
            weaponSource.PlayOneShot(shootSound);
        }
    }
    
    /// <summary>
    /// Play reload sound
    /// </summary>
    public void PlayReloadSound(bool isEmpty)
    {
        AudioClip clip = isEmpty ? reloadEmptySound : reloadSound;
        if (clip != null && weaponSource != null)
        {
            weaponSource.PlayOneShot(clip);
            
            // Sync to other clients
            if (IsServer)
            {
                PlayReloadSoundClientRpc(isEmpty);
            }
            else if (IsOwner)
            {
                PlayReloadSoundServerRpc(isEmpty);
            }
        }
    }
    
    [ServerRpc]
    private void PlayReloadSoundServerRpc(bool isEmpty)
    {
        PlayReloadSoundClientRpc(isEmpty);
    }
    
    [ClientRpc]
    private void PlayReloadSoundClientRpc(bool isEmpty)
    {
        if (IsOwner) return;
        
        AudioClip clip = isEmpty ? reloadEmptySound : reloadSound;
        if (clip != null && weaponSource != null)
        {
            weaponSource.PlayOneShot(clip);
        }
    }
    
    /// <summary>
    /// Play magazine out sound
    /// </summary>
    public void PlayMagOutSound()
    {
        if (magOutSound != null && weaponSource != null)
        {
            weaponSource.PlayOneShot(magOutSound);
        }
    }
    
    /// <summary>
    /// Play magazine in sound
    /// </summary>
    public void PlayMagInSound()
    {
        if (magInSound != null && weaponSource != null)
        {
            weaponSource.PlayOneShot(magInSound);
        }
    }
    
    /// <summary>
    /// Play bolt/chamber sound
    /// </summary>
    public void PlayBoltSound()
    {
        if (boltSound != null && weaponSource != null)
        {
            weaponSource.PlayOneShot(boltSound);
        }
    }
    
    /// <summary>
    /// Play dry fire sound (empty gun)
    /// </summary>
    public void PlayDryFireSound()
    {
        if (dryFireSound != null && weaponSource != null)
        {
            weaponSource.PlayOneShot(dryFireSound);
        }
    }
    
    #endregion
    
    #region Character Sounds
    
    /// <summary>
    /// Play damage sound
    /// </summary>
    public void PlayDamageSound()
    {
        if (damageSounds != null && damageSounds.Length > 0 && voiceSource != null)
        {
            AudioClip clip = damageSounds[Random.Range(0, damageSounds.Length)];
            if (clip != null)
            {
                voiceSource.PlayOneShot(clip);
                
                // Sync to other clients
                if (IsServer)
                {
                    PlayDamageSoundClientRpc();
                }
                else if (IsOwner)
                {
                    PlayDamageSoundServerRpc();
                }
            }
        }
    }
    
    [ServerRpc]
    private void PlayDamageSoundServerRpc()
    {
        PlayDamageSoundClientRpc();
    }
    
    [ClientRpc]
    private void PlayDamageSoundClientRpc()
    {
        if (IsOwner) return;
        
        if (damageSounds != null && damageSounds.Length > 0 && voiceSource != null)
        {
            AudioClip clip = damageSounds[Random.Range(0, damageSounds.Length)];
            if (clip != null)
            {
                voiceSource.PlayOneShot(clip);
            }
        }
    }
    
    /// <summary>
    /// Play death sound
    /// </summary>
    public void PlayDeathSound()
    {
        if (deathSounds != null && deathSounds.Length > 0 && voiceSource != null)
        {
            AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
            if (clip != null)
            {
                voiceSource.PlayOneShot(clip);
                
                // Sync to other clients
                if (IsServer)
                {
                    PlayDeathSoundClientRpc();
                }
                else if (IsOwner)
                {
                    PlayDeathSoundServerRpc();
                }
            }
        }
    }
    
    [ServerRpc]
    private void PlayDeathSoundServerRpc()
    {
        PlayDeathSoundClientRpc();
    }
    
    [ClientRpc]
    private void PlayDeathSoundClientRpc()
    {
        if (IsOwner) return;
        
        if (deathSounds != null && deathSounds.Length > 0 && voiceSource != null)
        {
            AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
            if (clip != null)
            {
                voiceSource.PlayOneShot(clip);
            }
        }
    }
    
    /// <summary>
    /// Play jump sound
    /// </summary>
    public void PlayJumpSound()
    {
        if (jumpSound != null && voiceSource != null)
        {
            voiceSource.PlayOneShot(jumpSound);
        }
    }
    
    /// <summary>
    /// Play landing sound
    /// </summary>
    public void PlayLandSound()
    {
        if (landSound != null && voiceSource != null)
        {
            voiceSource.PlayOneShot(landSound);
        }
    }
    
    #endregion
}
