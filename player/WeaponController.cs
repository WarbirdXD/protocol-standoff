using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class WeaponController : MonoBehaviour
{
    private NetworkObject networkObject;
    [Header("References")]
    public Transform cameraTransform;
    public Transform muzzlePoint;          // Where bullets/tracers spawn from
    public LayerMask hitLayers;
    
    [Header("Base Weapon Stats")]
    public float baseDamage = 22f;          // Base damage (modified by slider) - Longer TTK
    public float baseFireRate = 0.13f;      // 462 RPM (tactical pace)
    public float baseRecoil = 1.8f;         // Moderate kick per shot
    public float baseSpread = 0.006f;       // Tight accuracy (R6 style)
    public float maxRange = 150f;           // Standard engagement range
    
    [Header("Ammo System")]
    public int magazineSize = 30;           // Bullets per magazine
    public int reserveAmmo = 90;            // Extra ammo (3 mags)
    public float reloadTime = 2.2f;         // Time to reload (seconds)
    public float emptyReloadTime = 2.5f;    // Slower when mag is empty
    public bool autoReload = true;          // Auto reload when empty
    
    [Header("Reload Animation Timing")]
    public float magOutTime = 0.5f;         // When mag is removed
    public float magInTime = 1.5f;          // When new mag is inserted
    public float chamberTime = 1.9f;        // When bolt is pulled (empty reload only)
    
    [Header("Bloom System (Spread Growth)")]
    public float spreadIncreasePerShot = 0.004f;  // How much spread grows per shot (reduced)
    public float maxBloom = 0.12f;                // Maximum spread cap (higher for spray)
    public float bloomRecoveryRate = 0.025f;      // Spread recovery per second (faster)
    
    [Header("Recoil System (Camera Kick)")]
    public float verticalRecoilPerShot = 0.4f;    // Camera kick up per shot
    public float horizontalRecoilRange = 0.2f;    // Random horizontal kick
    public float recoilRecoverySpeed = 3.5f;      // How fast camera returns (lower = smoother)
    public float recoilSmoothTime = 0.15f;        // Smoothing time for recoil recovery
    public float recoilKickSmoothness = 0.08f;    // Smoothness of initial kick (lower = snappier)
    [Range(0f, 1f)] public float recoilDamping = 0.7f;  // Damping factor (higher = more damped)
    
    [Header("Tuning Ranges")]
    [Range(0f, 1f)] public float tempoControlSlider = 0.5f; // 0 = Tempo, 1 = Control
    [Range(0f, 1f)] public float movementStyleSlider = 0.5f; // 0 = Agile, 1 = Heavy
    
    [Header("Tempo/Control Tuning (Slider: 0=Tempo, 1=Control)")]
    // TEMPO (0) = SMG style - higher fire rate, manageable recoil, lower damage (R6 SMG)
    public float tempoFireRateMultiplier = 0.8f;     // 0.104s = 577 RPM (fast but fair)
    public float tempoRecoilMultiplier = 2.2f;       // 3.96 recoil (manageable with skill)
    public float tempoSpreadMultiplier = 2.5f;       // 0.015 spread (wider spray)
    public float tempoBloomMultiplier = 2.2f;        // Bloom grows faster for spray
    public float tempoDamageMultiplier = 0.82f;      // 18 damage (9 shots to kill = 0.93s TTK)
    
    // CONTROL (1) = DMR style - slow, precise, high damage per shot (R6 DMR)
    public float controlFireRateMultiplier = 2.0f;   // 0.26s = 231 RPM (very deliberate)
    public float controlRecoilMultiplier = 0.6f;     // 1.08 recoil (very manageable)
    public float controlSpreadMultiplier = 0.4f;     // 0.0024 spread (precise)
    public float controlBloomMultiplier = 0.5f;      // Bloom grows slowly
    public float controlDamageMultiplier = 1.5f;     // 33 damage (5 shots to kill = 1.3s TTK)
    
    [Header("Movement Style Effects (Slider: 0=Agile, 1=Heavy)")]
    // AGILE (0) = Fast movement, poor accuracy while moving (flanker/rusher)
    public float agileMoveSpeedMultiplier = 1.25f;      // 125% speed (very fast)
    public float agileMovementAccuracyPenalty = 2.0f;   // 2x worse spread when moving
    public float agileStrafePenalty = 1.3f;             // Extra penalty for strafing
    
    // HEAVY (1) = Slow movement, excellent accuracy while moving (anchor/holder)
    public float heavyMoveSpeedMultiplier = 0.75f;      // 75% speed (slow but stable)
    public float heavyMovementAccuracyBonus = 0.5f;     // 50% better spread when moving
    public float heavyStrafePenalty = 0.7f;             // Less penalty for strafing
    
    [Header("Zoom/ADS System")]
    public float zoomFOV = 40f;                // FOV when zoomed (lower = more zoom)
    public float normalFOV = 60f;              // Normal FOV
    public float zoomSpeed = 10f;              // How fast to zoom in/out
    public float zoomSpreadReduction = 0.5f;   // 50% less spread when zoomed
    public float controlThresholdForZoom = 0.25f; // Must be 75%+ Control to zoom (slider <= 0.25)
    
    [Header("Visual Feedback")]
    public GameObject muzzleFlashPrefab;
    public GameObject bulletImpactPrefab;
    public DynamicCrosshair dynamicCrosshair;  // Reference to crosshair UI
    public HitMarker hitMarker;                // Hit marker UI
    public bool showBulletTracers = true;      // Show visible bullet trails (CS:GO style)
    public Color tracerColor = Color.yellow;
    public float tracerSpeed = 300f;
    
    [Header("Weapon Feel")]
    public CameraShake cameraShake;            // Camera shake on fire
    public ShellEjection shellEjection;        // Shell casing ejection
    public float fireShakeIntensity = 0.05f;   // Shake amount per shot
    
    private float nextFireTime = 0f;
    private float currentBloom = 0f;              // Current spread bloom
    private float currentRecoilAmount = 0f;       // Legacy (for crosshair)
    private Vector2 currentCameraRecoil = Vector2.zero;  // Actual camera recoil
    private Vector2 targetCameraRecoil = Vector2.zero;    // Target recoil (for smooth kick)
    private Vector2 recoilVelocity;
    private Vector2 recoilKickVelocity;                   // Velocity for kick smoothing
    private bool isZooming = false;               // Currently zooming
    private float currentFOV;                     // Current camera FOV
    private Camera playerCamera;                  // Reference to camera
    
    // Ammo system
    private int currentAmmo;                      // Current bullets in magazine
    private int currentReserve;                   // Reserve ammo
    private bool isReloading = false;             // Currently reloading
    private float reloadStartTime;                // When reload started
    private bool wasEmptyReload;                  // Track if reload was from empty
    
    // Calculated stats based on tuning
    private float currentFireRate;
    private float currentRecoil;
    private float currentSpread;
    private float currentDamage;
    private float currentMoveSpeedMultiplier;
    private float currentMovementAccuracyMultiplier;
    
    private FPSController fpsController;
    private NetworkAnimationSync animationSync;
    private PlayerAudioManager audioManager;
    
    // Control freeze for countdown
    private bool controlsFrozen = false;
    
    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        animationSync = GetComponent<NetworkAnimationSync>();
    }
    
    private void Start()
    {
        // Only initialize for local player (owner)
        if (networkObject != null && !networkObject.IsOwner)
        {
            Debug.Log($"WeaponController.Start() - Skipping initialization for non-owner player (OwnerClientId: {networkObject.OwnerClientId})");
            return; // Don't initialize for remote players
        }
        
        Debug.Log($"WeaponController.Start() - Initializing for owner player (IsOwner: {networkObject?.IsOwner}, OwnerClientId: {networkObject?.OwnerClientId})");
        
        // Find the player's camera (not Camera.main which might be wrong)
        if (cameraTransform == null)
        {
            // Try to find camera as child of player
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cameraTransform = cam.transform;
                playerCamera = cam;
            }
            else if (Camera.main != null)
            {
                // Fallback to main camera
                cameraTransform = Camera.main.transform;
                playerCamera = Camera.main;
            }
        }
        else
        {
            // cameraTransform was assigned in Inspector
            playerCamera = cameraTransform.GetComponent<Camera>();
        }
        
        // Store original FOV for zoom
        if (playerCamera != null)
        {
            normalFOV = playerCamera.fieldOfView;
            currentFOV = normalFOV;
            Debug.Log($"WeaponController: Using camera '{playerCamera.gameObject.name}' at position {cameraTransform.position}");
        }
        else
        {
            Debug.LogError("WeaponController: No camera found! Raycasts will fail.");
        }
        
        fpsController = GetComponent<FPSController>();
        audioManager = GetComponent<PlayerAudioManager>();
        
        // Initialize ammo
        currentAmmo = magazineSize;
        currentReserve = reserveAmmo;
        
        RecalculateWeaponStats();
        
        // Subscribe to match events for control freeze
        MatchManager matchManager = FindFirstObjectByType<MatchManager>();
        if (matchManager != null)
        {
            matchManager.OnCountdown.AddListener(OnCountdownTick);
            matchManager.OnMatchStart.AddListener(OnMatchStart);
            
            // Freeze controls if countdown is active
            if (matchManager.useCountdown)
            {
                controlsFrozen = true;
            }
        }
    }
    
    private void Update()
    {
        // Only process input if this is the local player (owner)
        if (networkObject != null && !networkObject.IsOwner)
        {
            return; // Don't process input for remote players
        }
        
        // Only handle input if controls aren't frozen
        if (!controlsFrozen)
        {
            // Handle reload input
            HandleReload();
            
            // Handle zoom input (only if Control build)
            HandleZoom();
        }
        
        // Check if currently reloading
        if (isReloading)
        {
            UpdateReload();
            return; // Can't shoot while reloading
        }
        
        // Only allow shooting if controls aren't frozen
        if (!controlsFrozen)
        {
            // Check mouse button (old Input Manager)
            bool mouseClick = Input.GetButton("Fire1");
            
            // Check controller trigger (new Input System)
            bool triggerPressed = false;
            if (Gamepad.current != null)
            {
                triggerPressed = Gamepad.current.rightTrigger.isPressed;
            }
            
            bool isFiring = mouseClick || triggerPressed;
            
            if (isFiring && Time.time >= nextFireTime)
            {
                // Check ammo
                if (currentAmmo > 0)
                {
                    Fire();
                }
                else
                {
                    // Play dry fire sound
                    if (audioManager != null)
                    {
                        audioManager.PlayDryFireSound();
                    }
                    
                    if (autoReload && currentReserve > 0)
                    {
                        StartReload();
                    }
                }
            }
        }
        
        // Bloom recovery (spread decreases when not firing)
        if (currentBloom > 0f)
        {
            currentBloom = Mathf.Max(0f, currentBloom - bloomRecoveryRate * Time.deltaTime);
        }
        
        // Multi-layer smoothing like AAA shooters
        // Layer 1: Smooth kick application (SmoothDamp for natural acceleration)
        currentCameraRecoil = Vector2.SmoothDamp(currentCameraRecoil, targetCameraRecoil, ref recoilKickVelocity, recoilKickSmoothness);
        
        // Layer 2: Damping (reduce velocity over time for smoother feel)
        recoilKickVelocity *= recoilDamping;
        
        // Layer 3: Camera recoil recovery (smooth return to center)
        targetCameraRecoil = Vector2.SmoothDamp(targetCameraRecoil, Vector2.zero, ref recoilVelocity, recoilSmoothTime, recoilRecoverySpeed);
        
        // Legacy recoil for crosshair (matches bloom)
        currentRecoilAmount = currentBloom * 100f;
        
        // Update crosshair to match EXACT weapon spread
        if (dynamicCrosshair != null && fpsController != null)
        {
            // Use the same spread calculation as firing for perfect accuracy
            float actualSpread = CalculateTotalSpread();
            
            bool isMoving = fpsController.IsMoving();
            bool isSprinting = false; // Removed old movement states
            bool isJumping = !fpsController.IsGrounded;
            
            // Pass actual spread so crosshair matches bullet spread exactly
            dynamicCrosshair.UpdateCrosshair(actualSpread, currentRecoilAmount, isMoving, isSprinting, isJumping, false, false);
        }
    }
    
    public void RecalculateWeaponStats()
    {
        // Tempo/Control slider: 0 = Tempo (fast/loose), 1 = Control (slow/tight)
        // SWAP THE LERP ORDER - Unity's slider is inverted!
        currentFireRate = baseFireRate * 
            Mathf.Lerp(controlFireRateMultiplier, tempoFireRateMultiplier, tempoControlSlider);
        
        currentRecoil = baseRecoil * 
            Mathf.Lerp(controlRecoilMultiplier, tempoRecoilMultiplier, tempoControlSlider);
        
        currentSpread = baseSpread * 
            Mathf.Lerp(controlSpreadMultiplier, tempoSpreadMultiplier, tempoControlSlider);
        
        // Damage scaling: Tempo = low damage/high fire, Control = high damage/low fire
        currentDamage = baseDamage * 
            Mathf.Lerp(controlDamageMultiplier, tempoDamageMultiplier, tempoControlSlider);
        
        // Bloom rate also affected by Tempo/Control
        float currentBloomMultiplier = 
            Mathf.Lerp(controlBloomMultiplier, tempoBloomMultiplier, tempoControlSlider);
        spreadIncreasePerShot = 0.004f * currentBloomMultiplier;
        
        // Movement style slider: 0 = Agile (fast/inaccurate), 1 = Heavy (slow/accurate)
        // SWAP THE LERP ORDER - Unity's slider is inverted!
        currentMoveSpeedMultiplier = 
            Mathf.Lerp(heavyMoveSpeedMultiplier, agileMoveSpeedMultiplier, movementStyleSlider);
        
        currentMovementAccuracyMultiplier = 
            Mathf.Lerp(heavyMovementAccuracyBonus, agileMovementAccuracyPenalty, movementStyleSlider);
        
        // Apply movement speed to FPS controller
        if (fpsController != null)
        {
            fpsController.SetMovementMultiplier(currentMoveSpeedMultiplier);
            
            // Apply player build recoil movement multiplier
            // movementStyleSlider: 0 = Heavy (stable), 1 = Agile (sensitive)
            float playerRecoilMultiplier = Mathf.Lerp(0.6f, 1.5f, movementStyleSlider);
            fpsController.SetRecoilMovementMultiplier(playerRecoilMultiplier);
        }
    }
    
    private void Fire()
    {
        // Play shoot sound
        if (audioManager != null)
        {
            audioManager.PlayShootSound();
        }
        
        // Consume ammo
        currentAmmo--;
        
        nextFireTime = Time.time + currentFireRate;
        
        // Calculate total spread based on ALL factors (like COD)
        float totalSpread = CalculateTotalSpread();
        
        // Apply bloom
        float bloomIncrease = spreadIncreasePerShot;
        currentBloom = Mathf.Min(currentBloom + bloomIncrease, maxBloom);
        
        // Apply camera recoil (visual kick)
        ApplyCameraRecoil();
        
        // Apply recoil movement (pushback in air/slide)
        if (fpsController != null)
        {
            // Calculate recoil strength from vertical + horizontal recoil
            float recoilStrength = verticalRecoilPerShot + (horizontalRecoilRange * 0.5f);
            
            // Apply weapon build multiplier (Tempo = high, Control = low)
            // tempoControlSlider: 0 = Tempo (high recoil), 1 = Control (low recoil)
            float weaponBuildMultiplier = Mathf.Lerp(1.8f, 0.6f, tempoControlSlider);
            
            fpsController.ApplyRecoilMovement(recoilStrength, weaponBuildMultiplier);
        }
        
        // Camera shake on fire
        if (cameraShake != null)
        {
            cameraShake.Shake(fireShakeIntensity * currentRecoil, 0.1f);
        }
        
        // Eject shell casing
        if (shellEjection != null)
        {
            shellEjection.EjectShell();
        }
        
        // Raycast with spread (COD-style cone of fire)
        Vector3 shootDirection = cameraTransform.forward;
        
        // Add random spread within cone
        shootDirection += cameraTransform.right * Random.Range(-totalSpread, totalSpread);
        shootDirection += cameraTransform.up * Random.Range(-totalSpread, totalSpread);
        shootDirection.Normalize();
        
        Ray ray = new Ray(cameraTransform.position, shootDirection);
        Vector3 bulletEndPoint;
        bool didHit = false;
        
        if (Physics.Raycast(ray, out RaycastHit hit, maxRange, hitLayers))
        {
            bulletEndPoint = hit.point;
            didHit = true;
            
            // Check for headshot (assuming head collider has tag "Head")
            bool isHeadshot = hit.collider.CompareTag("Head");
            
            // Deal damage to PlayerHealth (multiplayer)
            PlayerHealth targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();
            if (targetHealth != null)
            {
                // Pass attacker's client ID for kill tracking
                ulong attackerClientId = networkObject != null ? networkObject.OwnerClientId : 0;
                targetHealth.TakeDamage(currentDamage, isHeadshot, attackerClientId);
                
                // Show hit marker (only for enemies)
                if (hitMarker != null)
                {
                    hitMarker.ShowHit(isHeadshot);
                }
            }
            
            // Deal damage to PracticeTarget (testing)
            PracticeTarget practiceTarget = hit.collider.GetComponent<PracticeTarget>();
            if (practiceTarget != null)
            {
                float damage = currentDamage;
                if (isHeadshot)
                {
                    damage *= 2.5f; // Headshot multiplier
                }
                practiceTarget.TakeDamage(damage, isHeadshot);
                
                // Show hit marker for practice targets
                if (hitMarker != null)
                {
                    hitMarker.ShowHit(isHeadshot);
                }
            }
            
            // Visual feedback
            if (bulletImpactPrefab != null)
            {
                GameObject impact = Instantiate(bulletImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 2f);
            }
            
            // Crosshair hit feedback - DISABLED (using HitMarker only)
            // if (hitEnemy && dynamicCrosshair != null)
            // {
            //     dynamicCrosshair.ShowHitFeedback();
            // }
            
            Debug.DrawLine(ray.origin, hit.point, Color.red, 0.1f);
        }
        else
        {
            // Bullet didn't hit anything, end at max range
            bulletEndPoint = ray.origin + shootDirection * maxRange;
            Debug.DrawRay(ray.origin, shootDirection * maxRange, Color.yellow, 0.1f);
        }
        
        // Spawn bullet tracer (CS:GO style visual)
        if (showBulletTracers)
        {
            // Use muzzle point if assigned, otherwise fallback to camera
            Vector3 tracerStart = muzzlePoint != null ? muzzlePoint.position : cameraTransform.position;
            
            // Debug: Check where tracer is spawning from
            if (muzzlePoint == null)
            {
                Debug.LogWarning("WeaponController: Muzzle Point not assigned! Tracers spawning from camera. Please assign MuzzlePoint in Inspector.");
            }
            
            SpawnBulletTracer(tracerStart, bulletEndPoint);
        }
        
        // Sync shooting effects across network
        if (animationSync != null)
        {
            animationSync.OnShoot(shootDirection, bulletEndPoint, didHit);
        }
        else
        {
            // Fallback: Play local effects only if no network sync
            // Muzzle flash
            if (muzzleFlashPrefab != null)
            {
                Vector3 flashPos = muzzlePoint != null ? muzzlePoint.position : cameraTransform.position + cameraTransform.forward * 0.5f;
                Quaternion flashRot = muzzlePoint != null ? muzzlePoint.rotation : cameraTransform.rotation;
                GameObject flash = Instantiate(muzzleFlashPrefab, flashPos, flashRot);
                Destroy(flash, 0.1f);
            }
        }
    }
    
    /// <summary>
    /// Apply camera recoil (visual kick like COD/Battlefield/R6/CSGO)
    /// Multi-layer smoothing for AAA-quality feel
    /// </summary>
    private void ApplyCameraRecoil()
    {
        // Vertical recoil (always kicks up) - reduced per shot for smoother feel
        float verticalKick = verticalRecoilPerShot * currentRecoil;
        
        // Horizontal recoil (random left/right) - tighter pattern
        float horizontalKick = Random.Range(-horizontalRecoilRange, horizontalRecoilRange) * currentRecoil;
        
        // Recoil pattern: slight pull to the right over time (like COD/CSGO)
        float recoilPattern = Mathf.Sin(Time.time * 2f) * 0.05f * currentRecoil;
        horizontalKick += recoilPattern;
        
        // Add to target recoil (smoothly interpolated in Update with multi-layer damping)
        Vector2 recoilToAdd = new Vector2(verticalKick, horizontalKick);
        targetCameraRecoil += recoilToAdd;
        
        // Camera recoil is now handled directly in this script via cameraTransform rotation
    }
    
    /// <summary>
    /// Spawn a visual bullet tracer (CS:GO style)
    /// </summary>
    private void SpawnBulletTracer(Vector3 start, Vector3 end)
    {
        GameObject tracerObj = new GameObject("BulletTracer");
        BulletTracer tracer = tracerObj.AddComponent<BulletTracer>();
        tracer.tracerSpeed = tracerSpeed;
        tracer.tracerColor = tracerColor;
        tracer.Initialize(start, end);
    }
    
    /// <summary>
    /// Calculate total spread based on ALL factors (COD-style accuracy system)
    /// </summary>
    private float CalculateTotalSpread()
    {
        if (fpsController == null) return currentSpread;
        
        // Start with base spread + bloom (spread growth from firing)
        float spread = currentSpread + currentBloom;
        
        // No stance modifiers - pure skill-based accuracy
        
        // Movement modifiers (stacks with stance)
        if (fpsController.IsJumping())
        {
            spread *= 3.0f; // 200% worse accuracy when jumping (COD style)
        }
        else if (fpsController.IsSprinting())
        {
            spread *= 2.0f; // 100% worse when sprinting
        }
        else if (fpsController.IsMoving())
        {
            // Apply movement accuracy multiplier from Heavy/Agile slider
            spread *= currentMovementAccuracyMultiplier;
        }
        
        // Zoom bonus (only for Control builds)
        if (isZooming)
        {
            spread *= zoomSpreadReduction; // 50% tighter when zoomed
        }
        
        return spread;
    }
    
    /// <summary>
    /// Handle zoom/ADS input (only available for 75%+ Control builds)
    /// </summary>
    private void HandleZoom()
    {
        if (playerCamera == null) return;
        
        // Check if player has enough Control to zoom (slider <= 0.25 = 75%+ Control)
        bool canZoom = tempoControlSlider <= controlThresholdForZoom;
        
        if (canZoom)
        {
            // Right mouse button or Left Trigger for zoom
            bool mouseZoom = Input.GetButton("Fire2"); // Right mouse button
            bool controllerZoom = false;
            
            if (Gamepad.current != null)
            {
                // Left Trigger (LT) - use ReadValue for analog trigger
                controllerZoom = Gamepad.current.leftTrigger.ReadValue() > 0.5f;
            }
            
            isZooming = mouseZoom || controllerZoom;
        }
        else
        {
            isZooming = false;
        }
        
        // Smoothly interpolate FOV
        float targetFOV = isZooming ? zoomFOV : normalFOV;
        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * zoomSpeed);
        playerCamera.fieldOfView = currentFOV;
    }
    
    public float GetCurrentFireRate() => currentFireRate;
    public float GetCurrentRecoil() => currentRecoil;
    public float GetCurrentSpread() => currentSpread;
    public bool IsZooming() => isZooming;
    public int GetCurrentAmmo() => currentAmmo;
    public int GetReserveAmmo() => currentReserve;
    public bool IsReloading() => isReloading;
    
    /// <summary>
    /// Handle reload input
    /// </summary>
    private void HandleReload()
    {
        // R key or X button (Xbox)
        bool reloadInput = Input.GetKeyDown(KeyCode.R);
        
        if (Gamepad.current != null)
        {
            reloadInput |= Gamepad.current.buttonWest.wasPressedThisFrame; // X on Xbox, Square on PS
        }
        
        if (reloadInput && !isReloading)
        {
            // Only reload if we have reserve ammo and mag isn't full
            if (currentReserve > 0 && currentAmmo < magazineSize)
            {
                StartReload();
            }
        }
    }
    
    /// <summary>
    /// Start the reload process
    /// </summary>
    private void StartReload()
    {
        if (isReloading) return;
        
        isReloading = true;
        reloadStartTime = Time.time;
        wasEmptyReload = (currentAmmo == 0);
        
        // Play reload sound
        if (audioManager != null)
        {
            audioManager.PlayReloadSound(wasEmptyReload);
        }
        
        // Cancel zoom during reload
        isZooming = false;
        
        // Sync reload animation across network
        if (animationSync != null)
        {
            animationSync.OnStartReload(wasEmptyReload);
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"<color=yellow>Reloading... ({currentAmmo}/{magazineSize}) Reserve: {currentReserve}</color>");
        }
    }
    
    /// <summary>
    /// Update reload progress
    /// </summary>
    private void UpdateReload()
    {
        float reloadDuration = wasEmptyReload ? emptyReloadTime : reloadTime;
        float reloadProgress = (Time.time - reloadStartTime) / reloadDuration;
        
        // Check if reload is complete
        if (reloadProgress >= 1f)
        {
            CompleteReload();
        }
        
        // TODO: Play reload sounds at specific times
        // if (reloadProgress >= magOutTime / reloadDuration && !magOutPlayed)
        // {
        //     PlayMagOutSound();
        //     magOutPlayed = true;
        // }
    }
    
    /// <summary>
    /// Complete the reload and refill ammo
    /// </summary>
    private void CompleteReload()
    {
        isReloading = false;
        
        // Calculate how many bullets to add
        int bulletsNeeded = magazineSize - currentAmmo;
        int bulletsToAdd = Mathf.Min(bulletsNeeded, currentReserve);
        
        // Transfer ammo from reserve to magazine
        currentAmmo += bulletsToAdd;
        currentReserve -= bulletsToAdd;
        
        // Sync reload completion across network
        if (animationSync != null)
        {
            animationSync.OnReloadComplete();
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"<color=green>Reload complete! ({currentAmmo}/{magazineSize}) Reserve: {currentReserve}</color>");
        }
    }
    
    /// <summary>
    /// Cancel reload (for future reload canceling feature)
    /// </summary>
    public void CancelReload()
    {
        if (isReloading)
        {
            isReloading = false;
            Debug.Log("Reload canceled!");
        }
    }
    
    /// <summary>
    /// Refill ammo (for pickups/respawn)
    /// </summary>
    public void RefillAmmo()
    {
        currentAmmo = magazineSize;
        currentReserve = reserveAmmo;
        isReloading = false;
    }
    
    /// <summary>
    /// Called during countdown - keeps controls frozen
    /// </summary>
    private void OnCountdownTick(int number)
    {
        // Keep controls frozen during countdown
        controlsFrozen = true;
    }
    
    /// <summary>
    /// Called when match starts - unfreezes controls
    /// </summary>
    private void OnMatchStart()
    {
        // Unfreeze controls when match starts
        controlsFrozen = false;
    }
    
    private bool showDebugLogs = false; // Set to true for debugging
}
