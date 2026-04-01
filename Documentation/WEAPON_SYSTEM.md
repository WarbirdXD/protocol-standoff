# Weapon System Documentation

**Responsive, skill-based shooting mechanics with calibration**

---

## Overview

The Weapon System provides responsive shooting mechanics with recoil, spread, ammo management, and real-time calibration. It uses client-side hit detection with server validation, supports automatic fire, and integrates with the calibration system for player-customizable weapon characteristics.

---

## Architecture

```
Player Input
    ↓
WeaponController.Update()
    ↓
Fire Input Detected?
├─ Yes → Fire()
│         ↓
│    Check Ammo
│         ↓
│    Client Raycast (instant feedback)
│         ↓
│    Hit Player?
│    ├─ Yes → TakeDamageServerRpc()
│    │         ↓
│    │    Server Validates
│    │         ↓
│    │    Apply Damage
│    │         ↓
│    │    NetworkVariable Sync
│    │
│    └─ No → Hit Environment
│              ↓
│         Visual Effects Only
│
└─ No → Check Reload
        ↓
    Reload Input?
    ├─ Yes → StartReload()
    │         ↓
    │    Wait Reload Time
    │         ↓
    │    CompleteReload()
    │
    └─ No → Continue
```

---

## Core Mechanics

### Fire Rate

**Configuration:**
```csharp
[Header("Fire Rate")]
public float baseFireRate = 0.1f;  // Base time between shots (seconds)
private float currentFireRate;     // Modified by calibration
private float nextFireTime;        // When can fire next
```

**Calibration Impact:**
```csharp
// Fire rate: 900 RPM (tempo) → 450 RPM (control)
float fireRate = Mathf.Lerp(900f, 450f, tempoControlSlider);
currentFireRate = 60f / fireRate; // Convert RPM to seconds

// Examples:
// Tempo (0.0): 900 RPM = 0.067s between shots
// Balanced (0.5): 675 RPM = 0.089s between shots
// Control (1.0): 450 RPM = 0.133s between shots
```

**Fire Rate Check:**
```csharp
void Update()
{
    if (isFiring && Time.time >= nextFireTime)
    {
        if (currentAmmo > 0)
        {
            Fire();
            nextFireTime = Time.time + currentFireRate;
        }
    }
}
```

### Damage System

**Configuration:**
```csharp
[Header("Damage")]
public float baseDamage = 20f;           // Base damage per bullet
private float damage;                     // Modified by calibration
public float headshotMultiplier = 2.5f;   // Headshot damage multiplier
public float range = 100f;                // Max effective range
```

**Calibration Impact:**
```csharp
// Damage: 15 (tempo) → 30 (control)
damage = Mathf.Lerp(15f, 30f, tempoControlSlider);

// Examples:
// Tempo (0.0): 15 damage per bullet
// Balanced (0.5): 22.5 damage per bullet
// Control (1.0): 30 damage per bullet
```

**Damage Application:**
```csharp
void Fire()
{
    // Client-side raycast for instant feedback
    if (Physics.Raycast(firePoint.position, firePoint.forward, out RaycastHit hit, range, hitLayers))
    {
        var targetHealth = hit.collider.GetComponent<PlayerHealth>();
        if (targetHealth != null)
        {
            // Check if headshot
            bool isHeadshot = hit.collider.CompareTag("Head");
            
            // Tell server about hit (server validates and applies)
            targetHealth.TakeDamageServerRpc(damage, isHeadshot, NetworkManager.Singleton.LocalClientId);
            
            // Visual feedback (client-side)
            ShowHitMarker(isHeadshot);
        }
    }
}
```

---

## Ammo System

### Ammo Configuration

```csharp
[Header("Ammo")]
public int magazineSize = 30;      // Bullets per magazine
public int reserveAmmo = 120;      // Total reserve ammo
private int currentAmmo;           // Current bullets in magazine
private int currentReserve;        // Current reserve ammo
```

### Ammo Consumption

```csharp
void Fire()
{
    // Check ammo
    if (currentAmmo <= 0)
    {
        // Play empty click sound
        if (audioManager != null)
        {
            audioManager.PlayEmptyClick();
        }
        return;
    }
    
    // Consume ammo
    currentAmmo--;
    
    // Auto-reload when empty
    if (currentAmmo == 0 && currentReserve > 0)
    {
        StartReload();
    }
}
```

### Reload System

**Reload Times:**
```csharp
[Header("Reload")]
public float reloadTime = 2.5f;      // Normal reload duration
public float emptyReloadTime = 3.0f; // Empty reload duration (longer)
private bool isReloading = false;
private float reloadStartTime;
private bool wasEmptyReload;
```

**Reload Process:**
```csharp
public void StartReload()
{
    // Can't reload if already reloading
    if (isReloading)
        return;
    
    // Can't reload if no reserve ammo
    if (currentReserve <= 0)
        return;
    
    // Can't reload if magazine is full
    if (currentAmmo >= magazineSize)
        return;
    
    isReloading = true;
    reloadStartTime = Time.time;
    wasEmptyReload = (currentAmmo == 0);
    
    // Play reload sound
    if (audioManager != null)
    {
        audioManager.PlayReloadSound();
    }
    
    // Play reload animation
    if (animator != null)
    {
        animator.SetTrigger("Reload");
    }
}

void Update()
{
    if (isReloading)
    {
        float reloadDuration = wasEmptyReload ? emptyReloadTime : reloadTime;
        
        if (Time.time >= reloadStartTime + reloadDuration)
        {
            CompleteReload();
        }
    }
}

void CompleteReload()
{
    isReloading = false;
    
    // Calculate how many bullets to add
    int bulletsNeeded = magazineSize - currentAmmo;
    int bulletsToAdd = Mathf.Min(bulletsNeeded, currentReserve);
    
    // Transfer ammo from reserve to magazine
    currentAmmo += bulletsToAdd;
    currentReserve -= bulletsToAdd;
    
    // Sync reload completion across network
    if (NetworkObject != null && NetworkObject.IsSpawned)
    {
        SyncReloadClientRpc();
    }
}
```

### Kill Reward

**Ammo Refill on Kill:**
```csharp
public void RefillAmmo()
{
    currentAmmo = magazineSize;
    currentReserve = reserveAmmo;
    isReloading = false;
    
    Debug.Log("Kill reward: Ammo refilled!");
}
```

**Triggered by:**
```csharp
// In PlayerHealth.cs when player gets a kill
[ClientRpc]
private void RefillAmmoClientRpc()
{
    if (!IsOwner) return;
    
    var weaponController = GetComponent<WeaponController>();
    if (weaponController != null)
    {
        weaponController.RefillAmmo();
    }
}
```

---

## Recoil System

### Recoil Configuration

```csharp
[Header("Recoil")]
public float baseRecoil = 1f;           // Base recoil amount
private float currentRecoil;             // Modified by calibration
public float recoilRecoverySpeed = 5f;  // How fast recoil recovers
public float maxRecoil = 10f;            // Maximum accumulated recoil
```

**Calibration Impact:**
```csharp
// Recoil: High (tempo) → Low (control)
float recoilMultiplier = Mathf.Lerp(2.0f, 0.5f, tempoControlSlider);
currentRecoil = baseRecoil * recoilMultiplier;

// Examples:
// Tempo (0.0): 2.0x recoil (very high)
// Balanced (0.5): 1.25x recoil (moderate)
// Control (1.0): 0.5x recoil (very low)
```

### Recoil Application

```csharp
private float accumulatedRecoil = 0f;

void Fire()
{
    // Apply recoil to camera
    float verticalRecoil = currentRecoil * Random.Range(0.8f, 1.2f);
    float horizontalRecoil = currentRecoil * Random.Range(-0.3f, 0.3f);
    
    // Add to accumulated recoil
    accumulatedRecoil += verticalRecoil;
    accumulatedRecoil = Mathf.Min(accumulatedRecoil, maxRecoil);
    
    // Apply to camera rotation
    playerCamera.transform.Rotate(-verticalRecoil, horizontalRecoil, 0f);
}

void Update()
{
    // Recover recoil over time
    if (accumulatedRecoil > 0f)
    {
        float recovery = recoilRecoverySpeed * Time.deltaTime;
        accumulatedRecoil -= recovery;
        accumulatedRecoil = Mathf.Max(accumulatedRecoil, 0f);
    }
}
```

---

## Spread System

### Spread Configuration

```csharp
[Header("Spread")]
public float baseSpread = 0.5f;              // Base bullet spread
private float currentSpread;                  // Modified by calibration
public float movementSpreadMultiplier = 2f;   // Spread increase while moving
public float shootingSpreadIncrease = 0.1f;   // Spread increase per shot
public float spreadRecoverySpeed = 2f;        // How fast spread recovers
public float maxSpread = 5f;                  // Maximum spread
```

**Calibration Impact:**
```csharp
// Spread: Wide (tempo) → Tight (control)
float spreadMultiplier = Mathf.Lerp(2.0f, 0.5f, tempoControlSlider);
currentSpread = baseSpread * spreadMultiplier;

// Movement penalty: Low (aggressive) → High (defensive)
movementSpreadMultiplier = Mathf.Lerp(1.2f, 3.0f, movementStyleSlider);
```

### Spread Calculation

```csharp
private float accumulatedSpread = 0f;

float GetTotalSpread()
{
    float spread = currentSpread + accumulatedSpread;
    
    // Apply movement penalty
    if (characterController.velocity.magnitude > 0.1f)
    {
        spread *= movementSpreadMultiplier;
    }
    
    // Apply ADS reduction
    if (isAiming)
    {
        spread *= 0.5f; // 50% reduction when aiming
    }
    
    return Mathf.Min(spread, maxSpread);
}

void Fire()
{
    // Get total spread
    float totalSpread = GetTotalSpread();
    
    // Apply random spread to shot direction
    Vector3 spreadDirection = firePoint.forward;
    spreadDirection += firePoint.right * Random.Range(-totalSpread, totalSpread);
    spreadDirection += firePoint.up * Random.Range(-totalSpread, totalSpread);
    spreadDirection.Normalize();
    
    // Raycast with spread
    Physics.Raycast(firePoint.position, spreadDirection, out RaycastHit hit, range, hitLayers);
    
    // Increase accumulated spread
    accumulatedSpread += shootingSpreadIncrease;
}

void Update()
{
    // Recover spread over time
    if (accumulatedSpread > 0f)
    {
        accumulatedSpread -= spreadRecoverySpeed * Time.deltaTime;
        accumulatedSpread = Mathf.Max(accumulatedSpread, 0f);
    }
}
```

---

## Aiming Down Sights (ADS)

### ADS Configuration

```csharp
[Header("Aiming")]
public float aimFOV = 40f;           // FOV when aiming (zoom)
public float normalFOV = 60f;        // Normal FOV
public float aimSpeed = 10f;         // ADS transition speed
private bool isAiming = false;
```

**Calibration Impact:**
```csharp
// ADS speed: Fast (aggressive) → Slow (defensive)
aimSpeed = Mathf.Lerp(15f, 5f, movementStyleSlider);
```

### ADS Implementation

```csharp
void Update()
{
    // Check aim input
    bool aimInput = Input.GetButton("Aim"); // Right mouse button
    
    if (aimInput && !isReloading)
    {
        isAiming = true;
    }
    else
    {
        isAiming = false;
    }
    
    // Smooth FOV transition
    float targetFOV = isAiming ? aimFOV : normalFOV;
    playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, aimSpeed * Time.deltaTime);
}
```

### ADS Benefits

1. **Reduced Spread:** 50% tighter bullet spread
2. **Better Accuracy:** Easier to hit targets at range
3. **Zoom:** Easier to see distant targets

### ADS Drawbacks

1. **Slower Movement:** Reduced movement speed
2. **Slower ADS Speed (Defensive):** Takes longer to aim
3. **Reduced FOV:** Less peripheral vision

---

## Hit Detection

### Client-Side Raycast

```csharp
void Fire()
{
    // Client performs raycast immediately (responsive)
    Vector3 direction = GetSpreadDirection();
    
    if (Physics.Raycast(firePoint.position, direction, out RaycastHit hit, range, hitLayers))
    {
        // Hit something
        if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("Head"))
        {
            // Hit player - tell server
            var targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            if (targetHealth != null)
            {
                bool isHeadshot = hit.collider.CompareTag("Head");
                targetHealth.TakeDamageServerRpc(damage, isHeadshot, NetworkManager.Singleton.LocalClientId);
            }
        }
        
        // Visual effects (immediate feedback)
        ShowImpactEffect(hit.point, hit.normal);
    }
}
```

### Server Validation

```csharp
// In PlayerHealth.cs
[ServerRpc(RequireOwnership = false)]
public void TakeDamageServerRpc(float damage, bool isHeadshot, ulong attackerClientId)
{
    // Server validates everything
    if (isDead.Value) return;
    
    // Validate damage amount
    if (damage < 0 || damage > 1000)
    {
        Debug.LogWarning($"Invalid damage: {damage}");
        return;
    }
    
    // Server calculates actual damage
    float actualDamage = damage;
    if (isHeadshot)
    {
        actualDamage *= headshotMultiplier;
    }
    
    // Server applies damage
    currentHealth.Value -= actualDamage;
    
    if (currentHealth.Value <= 0)
    {
        Die(isHeadshot, attackerClientId);
    }
}
```

---

## Visual Effects

### Muzzle Flash

```csharp
[Header("Visual Effects")]
public ParticleSystem muzzleFlash;
public Light muzzleLight;
public float muzzleLightDuration = 0.05f;

void Fire()
{
    // Play muzzle flash
    if (muzzleFlash != null)
    {
        muzzleFlash.Play();
    }
    
    // Flash muzzle light
    if (muzzleLight != null)
    {
        StartCoroutine(FlashMuzzleLight());
    }
}

IEnumerator FlashMuzzleLight()
{
    muzzleLight.enabled = true;
    yield return new WaitForSeconds(muzzleLightDuration);
    muzzleLight.enabled = false;
}
```

### Bullet Tracers

```csharp
public LineRenderer tracerPrefab;
public float tracerSpeed = 300f;
public float tracerDuration = 0.1f;

void Fire()
{
    if (tracerPrefab != null)
    {
        StartCoroutine(ShowTracer(firePoint.position, hit.point));
    }
}

IEnumerator ShowTracer(Vector3 start, Vector3 end)
{
    LineRenderer tracer = Instantiate(tracerPrefab);
    tracer.SetPosition(0, start);
    tracer.SetPosition(1, start);
    
    float elapsed = 0f;
    
    while (elapsed < tracerDuration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / tracerDuration;
        
        tracer.SetPosition(1, Vector3.Lerp(start, end, t));
        
        yield return null;
    }
    
    Destroy(tracer.gameObject);
}
```

### Impact Effects

```csharp
public GameObject impactEffectPrefab;

void ShowImpactEffect(Vector3 position, Vector3 normal)
{
    if (impactEffectPrefab != null)
    {
        GameObject effect = Instantiate(impactEffectPrefab, position, Quaternion.LookRotation(normal));
        Destroy(effect, 2f);
    }
}
```

### Hit Markers

```csharp
public Image hitMarkerUI;
public float hitMarkerDuration = 0.1f;

void ShowHitMarker(bool isHeadshot)
{
    if (hitMarkerUI != null)
    {
        hitMarkerUI.color = isHeadshot ? Color.red : Color.white;
        StartCoroutine(FlashHitMarker());
    }
}

IEnumerator FlashHitMarker()
{
    hitMarkerUI.enabled = true;
    yield return new WaitForSeconds(hitMarkerDuration);
    hitMarkerUI.enabled = false;
}
```

---

## Audio System

### Fire Sounds

```csharp
[Header("Audio")]
public AudioClip fireSound;
public AudioClip reloadSound;
public AudioClip emptyClickSound;

void Fire()
{
    if (audioManager != null && fireSound != null)
    {
        audioManager.PlayOneShot(fireSound);
    }
}
```

### Reload Sounds

```csharp
void StartReload()
{
    if (audioManager != null && reloadSound != null)
    {
        audioManager.PlayOneShot(reloadSound);
    }
}
```

---

## Calibration Integration

### Stat Recalculation

```csharp
public void RecalculateWeaponStats()
{
    // Fire rate
    float fireRate = Mathf.Lerp(900f, 450f, tempoControlSlider);
    currentFireRate = 60f / fireRate;
    
    // Damage
    damage = Mathf.Lerp(15f, 30f, tempoControlSlider);
    
    // Recoil
    float recoilMultiplier = Mathf.Lerp(2.0f, 0.5f, tempoControlSlider);
    currentRecoil = baseRecoil * recoilMultiplier;
    
    // Spread
    float spreadMultiplier = Mathf.Lerp(2.0f, 0.5f, tempoControlSlider);
    currentSpread = baseSpread * spreadMultiplier;
    
    // Movement penalty
    movementSpreadMultiplier = Mathf.Lerp(1.2f, 3.0f, movementStyleSlider);
    
    // ADS speed
    aimSpeed = Mathf.Lerp(15f, 5f, movementStyleSlider);
}
```

---

## Integration with Other Systems

### With Calibration System
```
Calibration changes → RecalculateWeaponStats() → Instant effect
```

### With Movement System
```
Moving → Apply movement spread penalty
Standing still → Best accuracy
```

### With Network System
```
Client raycast → ServerRpc → Server validates → Apply damage
```

### With UI System
```
Ammo changes → Update ammo display
Reloading → Show reload indicator
```

---

## Best Practices

1. **Client-side hit detection** for responsiveness
2. **Server validation** for security
3. **Use spread instead of random miss** for skill-based accuracy
4. **Smooth recoil recovery** for better feel
5. **Visual feedback** for all actions
6. **Audio feedback** for all actions
7. **Cache component references** in Start
8. **Use object pooling** for effects
9. **Test different calibrations** for balance
10. **Profile performance** of visual effects

---

## Debugging

### Debug Display

```csharp
void OnGUI()
{
    if (showDebugInfo)
    {
        GUILayout.Label($"Ammo: {currentAmmo}/{currentReserve}");
        GUILayout.Label($"Fire Rate: {60f / currentFireRate:F0} RPM");
        GUILayout.Label($"Damage: {damage:F0}");
        GUILayout.Label($"Recoil: {currentRecoil:F2}");
        GUILayout.Label($"Spread: {GetTotalSpread():F2}");
        GUILayout.Label($"Reloading: {isReloading}");
        GUILayout.Label($"Aiming: {isAiming}");
    }
}
```

### Debug Visualization

```csharp
void OnDrawGizmos()
{
    if (!Application.isPlaying || firePoint == null)
        return;
    
    // Draw fire direction
    Gizmos.color = Color.red;
    Gizmos.DrawLine(firePoint.position, firePoint.position + firePoint.forward * range);
    
    // Draw spread cone
    Gizmos.color = Color.yellow;
    float spread = GetTotalSpread();
    Vector3 spreadUp = firePoint.position + (firePoint.forward + firePoint.up * spread) * 10f;
    Vector3 spreadDown = firePoint.position + (firePoint.forward - firePoint.up * spread) * 10f;
    Vector3 spreadLeft = firePoint.position + (firePoint.forward - firePoint.right * spread) * 10f;
    Vector3 spreadRight = firePoint.position + (firePoint.forward + firePoint.right * spread) * 10f;
    
    Gizmos.DrawLine(firePoint.position, spreadUp);
    Gizmos.DrawLine(firePoint.position, spreadDown);
    Gizmos.DrawLine(firePoint.position, spreadLeft);
    Gizmos.DrawLine(firePoint.position, spreadRight);
}
```

---

*This documentation explains the weapon system architecture and functionality. For implementation details, see `WeaponController.cs`.*
