using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Spherical arena FPS controller with surface-walking gravity
/// Features: Magnetic boots, charged air dash, dynamic gravity orientation
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    private NetworkObject networkObject;
    [Header("References")]
    public Transform cameraTransform;
    public Transform bodyTransform;
    public Transform gunTransform;
    
    [Header("Movement Speeds")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;    // Increased for more noticeable difference
    public float sprintAcceleration = 25f;  // Faster acceleration when sprinting
    
    [Header("Movement Feel")]
    public float acceleration = 20f;  // Increased for snappier movement
    public float friction = 15f;      // Increased for less sliding
    public float airControl = 0.4f;
    public float jumpHeight = 2.5f;   // Height in meters (more intuitive than force)
    
    [Header("Look Settings")]
    public float mouseSensitivity = 100f;
    public float controllerSensitivity = 150f;
    public float maxLookAngle = 89f;
    public bool invertYAxis = false;
    
    [Header("Surface Gravity")]
    public LayerMask surfaceLayer;
    public float groundCheckDistance = 0.3f;   // Short range for jump detection
    public float surfaceSnapDistance = 3f;     // Long range for wall transitions
    public float minGroundClearance = 0.1f;
    public float gravityStrength = 35f;        // Stronger gravity for faster fall
    public float terminalVelocity = 50f;       // Max fall speed
    public float rotationSpeed = 8f;           // Slightly slower for smoother transitions
    
    [Header("Magnetic Boots Toggle")]
    [Tooltip("Key to toggle magnetic boots on/off")]
    public KeyCode magneticBootsKey = KeyCode.B;
    
    [Tooltip("Are magnetic boots currently enabled?")]
    public bool magneticBootsEnabled = true;
    
    [Header("Charged Air Dash")]
    public float dashForce = 15f;
    public float chargeTime = 1.2f;
    public float dashWindow = 0.7f;
    
    [Header("Recoil System")]
    public float recoilInfluence = 1.5f;
    public float recoilMovementMultiplier = 1f;
    
    // Components
    private CharacterController controller;
    private Camera playerCamera;
    private PlayerAudioManager audioManager;
    private float baseFOV = 60f;
    
    // Movement
    private Vector3 moveVelocity;
    private float verticalVelocity;
    private bool isGrounded;
    private float movementMultiplier = 1f;
    
    // Footstep timing
    private float lastFootstepTime;
    private float footstepInterval = 0.5f; // Time between footsteps when walking
    private float sprintFootstepInterval = 0.35f; // Faster footsteps when sprinting
    
    // Surface gravity
    private Vector3 gravityDirection = Vector3.down;
    private Quaternion targetRotation;
    private RaycastHit currentSurface;
    private bool isOnSurface;
    private bool hasJumped;  // Track if player intentionally jumped
    private Vector3 lockedGravityDirection;  // Locked gravity when boots disabled
    private bool hasLockedGravity = false;
    
    // Air dash
    private float chargeTimer;
    private bool isCharged;
    private bool hasDash;
    private float dashWindowTimer;
    private bool isSprinting;
    
    // Jump system
    private float jumpCooldown = 0f;
    private float coyoteTimeCounter = 0f;
    private float coyoteTime = 0.15f;  // Grace period after leaving ground
    private float jumpBufferCounter = 0f;
    private float jumpBufferTime = 0.2f;  // Input buffering before landing
    
    // Look
    private float cameraPitch;
    private float targetCameraPitch;
    
    public bool IsCharged => isCharged;
    public float ChargeProgress => Mathf.Clamp01(chargeTimer / chargeTime);
    public bool IsGrounded => isGrounded;
    public bool IsMoving() => moveVelocity.magnitude > 0.1f;
    public bool IsSprinting() => isSprinting;
    public bool IsJumping() => !isGrounded;
    public bool MagneticBootsEnabled => magneticBootsEnabled;
    
    // Control freeze for countdown
    private bool controlsFrozen = false;
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        networkObject = GetComponent<NetworkObject>();
    }
    
    private void Start()
    {
        // Only initialize for local player (owner)
        if (networkObject != null && !networkObject.IsOwner)
        {
            Debug.Log($"FPSController.Start() - Skipping initialization for non-owner player (OwnerClientId: {networkObject.OwnerClientId})");
            return; // Don't initialize for remote players
        }
        
        Debug.Log($"FPSController.Start() - Initializing for owner player (IsOwner: {networkObject?.IsOwner}, OwnerClientId: {networkObject?.OwnerClientId})");
        
        audioManager = GetComponent<PlayerAudioManager>();
        
        if (cameraTransform != null)
        {
            playerCamera = cameraTransform.GetComponent<Camera>();
            if (playerCamera != null)
            {
                baseFOV = playerCamera.fieldOfView;
            }
        }
        
        // Reset camera pitch to look forward
        cameraPitch = 0f;
        targetCameraPitch = 0f;
        
        // Initialize controls (cursor lock and event subscriptions)
        // This is needed because OnEnable() won't run if the component starts enabled
        InitializeControls();
    }
    
    private void OnEnable()
    {
        // Only subscribe to events for local player (owner)
        if (networkObject != null && !networkObject.IsOwner)
        {
            return;
        }
        
        InitializeControls();
    }
    
    private void InitializeControls()
    {
        // Lock cursor for local player
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Debug.Log($"FPSController.InitializeControls() - Cursor locked for client {networkObject?.OwnerClientId}");
        
        // Subscribe to match events for control freeze
        MatchManager matchManager = FindFirstObjectByType<MatchManager>();
        if (matchManager != null)
        {
            // Remove listeners first to avoid duplicates
            matchManager.OnCountdown.RemoveListener(OnCountdownTick);
            matchManager.OnMatchStart.RemoveListener(OnMatchStart);
            
            // Add listeners
            matchManager.OnCountdown.AddListener(OnCountdownTick);
            matchManager.OnMatchStart.AddListener(OnMatchStart);
            Debug.Log($"FPSController.InitializeControls() - Subscribed to match events for client {networkObject?.OwnerClientId}");
            
            // Only freeze controls if countdown is CURRENTLY active (not just enabled)
            // Check if match is active - if so, controls should be unfrozen even if countdown feature is enabled
            if (matchManager.CountdownActive)
            {
                controlsFrozen = true;
                Debug.Log($"FPSController.InitializeControls() - Controls frozen during active countdown for client {networkObject?.OwnerClientId}");
            }
            else if (matchManager.MatchActive)
            {
                // Match is already running, controls should be enabled
                controlsFrozen = false;
                Debug.Log($"FPSController.InitializeControls() - Controls enabled (match active) for client {networkObject?.OwnerClientId}");
            }
            else
            {
                // Match hasn't started yet, controls should be enabled
                controlsFrozen = false;
                Debug.Log($"FPSController.InitializeControls() - Controls enabled (waiting for match) for client {networkObject?.OwnerClientId}");
            }
        }
    }
    
    private void OnDisable()
    {
        // Unsubscribe from events when disabled
        MatchManager matchManager = FindFirstObjectByType<MatchManager>();
        if (matchManager != null)
        {
            matchManager.OnCountdown.RemoveListener(OnCountdownTick);
            matchManager.OnMatchStart.RemoveListener(OnMatchStart);
        }
    }
    
    private void Update()
    {
        // Update jump cooldown
        if (jumpCooldown > 0f)
        {
            jumpCooldown -= Time.deltaTime;
        }
        
        // Toggle magnetic boots (only if controls not frozen)
        if (!controlsFrozen && Input.GetKeyDown(magneticBootsKey))
        {
            magneticBootsEnabled = !magneticBootsEnabled;
            
            if (!magneticBootsEnabled && isOnSurface)
            {
                // Lock current gravity direction when disabling boots
                lockedGravityDirection = gravityDirection;
                hasLockedGravity = true;
                Debug.Log($"Magnetic boots OFF - Locked to current surface");
            }
            else if (magneticBootsEnabled)
            {
                // Re-enable surface detection
                hasLockedGravity = false;
                Debug.Log($"Magnetic boots ON");
            }
        }
        
        UpdateSurfaceGravity();
        
        // Only process input if this is the local player (owner)
        if (networkObject != null && !networkObject.IsOwner)
        {
            return; // Don't process input for remote players
        }
        
        // Debug: Log once per second to verify Update is running and controlsFrozen state
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"[Client {networkObject?.OwnerClientId}] FPSController.Update() - controlsFrozen: {controlsFrozen}, IsOwner: {networkObject?.IsOwner}");
        }
        
        // Only allow movement and actions if controls aren't frozen
        if (!controlsFrozen)
        {
            HandleMovement();
            HandleAirDash();
            HandleLook();
        }
    }
    
    private void UpdateSurfaceGravity()
    {
        // If boots disabled and we have locked gravity, use that
        if (!magneticBootsEnabled && hasLockedGravity)
        {
            // Keep using locked gravity direction
            gravityDirection = lockedGravityDirection;
            
            // Still check if we're grounded on the locked surface
            RaycastHit lockedGroundHit;
            isGrounded = Physics.Raycast(transform.position, -transform.up, out lockedGroundHit, groundCheckDistance, surfaceLayer);
            
            // Maintain body orientation to locked surface (but don't rotate the whole transform)
            // The body should stay aligned, but camera can still look around freely
            if (bodyTransform != null)
            {
                Quaternion lockedRotation = Quaternion.FromToRotation(bodyTransform.up, -lockedGravityDirection);
                bodyTransform.rotation = Quaternion.Slerp(bodyTransform.rotation, lockedRotation * bodyTransform.rotation, rotationSpeed * Time.deltaTime);
            }
            
            return;
        }
        
        // SIMPLE GROUND CHECK for jumping (short range, straight down)
        RaycastHit groundHit;
        bool isGroundedNow = Physics.Raycast(transform.position, -transform.up, out groundHit, groundCheckDistance, surfaceLayer);
        
        // SURFACE SNAPPING for wall transitions (long range, multi-directional)
        // Only active when magnetic boots are enabled
        RaycastHit hit;
        bool foundSurface = false;
        float closestDistance = float.MaxValue;
        RaycastHit closestHit = new RaycastHit();
        
        // Only do multi-directional surface detection if magnetic boots enabled
        if (magneticBootsEnabled)
        {
            // Check down first (current orientation)
            if (Physics.Raycast(transform.position, -transform.up, out hit, surfaceSnapDistance, surfaceLayer))
            {
                float dist = hit.distance;
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestHit = hit;
                    foundSurface = true;
                }
            }
            
            // Check all directions for nearby surfaces (comprehensive edge detection)
            Vector3[] directions = {
                transform.forward,                              // Forward (walking onto walls)
                (transform.forward - transform.up).normalized,  // Forward-down (edge transition)
                (transform.right - transform.up).normalized,    // Right-down (edge transition)
                (-transform.right - transform.up).normalized,   // Left-down (edge transition)
                -transform.right,                               // Left
                transform.right,                                // Right
                (transform.forward + transform.right).normalized, // Diagonal forward-right
                (transform.forward - transform.right).normalized  // Diagonal forward-left
            };
            
            foreach (Vector3 dir in directions)
            {
                if (Physics.Raycast(transform.position, dir.normalized, out hit, surfaceSnapDistance, surfaceLayer))
                {
                    float dist = hit.distance;
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestHit = hit;
                        foundSurface = true;
                    }
                }
            }
        }
        
        // Set grounded state from simple ground check
        isGrounded = isGroundedNow;
        
        if (foundSurface)
        {
            // Use the closest surface found
            hit = closestHit;
            
            // Reset jump flag when landing (falling and grounded)
            if (hasJumped && verticalVelocity <= 0 && isGrounded)
            {
                hasJumped = false;
            }
            
            // Attach to surface for rotation/snapping (not related to jumping)
            if (!hasJumped && jumpCooldown <= 0f)
            {
                isOnSurface = true;
                currentSurface = hit;
                
                // Calculate new gravity direction (perpendicular to surface)
                gravityDirection = -hit.normal;
                
                // Calculate target rotation to align with surface
                targetRotation = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;
                
                // Smoothly rotate to match surface
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                
                // Magnetic boots: snap to surface to prevent falling off edges
                transform.position = hit.point + hit.normal * minGroundClearance;
                
                // Charge dash while on surface
                if (isOnSurface && !hasDash)
                {
                    chargeTimer += Time.deltaTime;
                    if (chargeTimer >= chargeTime)
                    {
                        isCharged = true;
                    }
                }
            }
        }
        else
        {
            // No surface found - definitely not on surface
            isOnSurface = false;
            
            // In air - handle dash
            if (!isGrounded)
            {
                // Grant dash if charged
                if (isCharged && !hasDash)
                {
                    hasDash = true;
                    dashWindowTimer = dashWindow;
                }
                
                // Reset charge
                chargeTimer = 0f;
                isCharged = false;
                
                // Count down dash window
                if (hasDash)
                {
                    dashWindowTimer -= Time.deltaTime;
                    if (dashWindowTimer <= 0f)
                    {
                        hasDash = false;
                    }
                }
                
                // Default gravity when not on surface
                gravityDirection = Vector3.down;
            }
        }
    }
    
    private void HandleMovement()
    {
        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Sprint - works in all directions when holding shift and moving
        bool isPressingMovement = Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f;
        isSprinting = Input.GetKey(KeyCode.LeftShift) && isPressingMovement && isGrounded;
        
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        targetSpeed *= movementMultiplier;
        
        // Use faster acceleration when sprinting for more responsive feel
        float currentAcceleration = isSprinting ? sprintAcceleration : acceleration;
        
        // Calculate move direction relative to camera
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        // Don't normalize to preserve input magnitude for micro-movements
        Vector3 moveDirection = forward * vertical + right * horizontal;
        float inputMagnitude = Mathf.Clamp01(moveDirection.magnitude);
        
        if (inputMagnitude > 0.01f)
        {
            moveDirection = moveDirection.normalized;
        }
        
        // Apply acceleration/friction with smooth interpolation
        if (isGrounded)
        {
            if (inputMagnitude > 0.01f)
            {
                // Smooth acceleration to target speed
                Vector3 targetVelocity = moveDirection * targetSpeed * inputMagnitude;
                moveVelocity = Vector3.Lerp(moveVelocity, targetVelocity, currentAcceleration * Time.deltaTime);
            }
            else
            {
                // Apply friction for smooth deceleration
                moveVelocity = Vector3.Lerp(moveVelocity, Vector3.zero, friction * Time.deltaTime);
                
                // Stop completely when very slow
                if (moveVelocity.magnitude < 0.1f)
                {
                    moveVelocity = Vector3.zero;
                }
            }
        }
        else
        {
            // Air control with lerp for smoother air movement
            moveVelocity = Vector3.Lerp(moveVelocity, moveDirection * targetSpeed, airControl * Time.deltaTime);
        }
        
        // Coyote Time - grace period after leaving ground
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
        
        // Jump Buffer - remember jump input slightly before landing
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
        
        // Jump - using Unity's standard physics formula: v = sqrt(2 * g * h)
        // Can jump if: (grounded OR coyote time) AND (jump pressed OR buffered) AND not on cooldown
        bool canJump = (coyoteTimeCounter > 0f || isGrounded) && jumpBufferCounter > 0f && jumpCooldown <= 0f;
        
        if (canJump)
        {
            // Calculate jump velocity from desired height using physics formula
            // v = sqrt(2 * gravity * jumpHeight)
            verticalVelocity = Mathf.Sqrt(2f * gravityStrength * jumpHeight);
            hasJumped = true;
            isOnSurface = false;
            jumpCooldown = 0.15f;
            
            // Reset counters so we don't double jump
            coyoteTimeCounter = 0f;
            jumpBufferCounter = 0f;
        }
        
        // Variable jump height - release jump early for shorter jump
        if (Input.GetButtonUp("Jump") && verticalVelocity > 0f)
        {
            verticalVelocity *= 0.5f;  // Cut velocity in half for responsive short jumps
        }
        
        // Apply gravity relative to surface
        if (isGrounded && verticalVelocity <= 0)
        {
            // Stick to surface - small downward force
            verticalVelocity = -2f;
        }
        else
        {
            // Apply gravity (always pulls toward surface)
            // Gravity pulls DOWN (negative direction in local up space)
            verticalVelocity -= gravityStrength * Time.deltaTime;
            
            // Terminal velocity cap (falling speed limit)
            if (verticalVelocity < -terminalVelocity)
            {
                verticalVelocity = -terminalVelocity;
            }
        }
        
        // Move - combine horizontal movement with vertical (relative to surface)
        Vector3 move = moveVelocity * Time.deltaTime;
        move += transform.up * verticalVelocity * Time.deltaTime;
        controller.Move(move);
        
        // Play footstep sounds when moving on ground
        if (isGrounded && moveVelocity.magnitude > 0.1f && audioManager != null)
        {
            float currentInterval = isSprinting ? sprintFootstepInterval : footstepInterval;
            if (Time.time - lastFootstepTime >= currentInterval)
            {
                audioManager.PlayFootstep();
                lastFootstepTime = Time.time;
            }
        }
    }
    
    private void HandleAirDash()
    {
        // Use dash with Space in air
        if (hasDash && Input.GetButtonDown("Jump") && !isGrounded)
        {
            // Dash in camera forward direction
            Vector3 dashDirection = cameraTransform.forward;
            moveVelocity += dashDirection * dashForce;
            
            hasDash = false;
            Debug.Log("Air Dash!");
        }
    }
    
    private void HandleLook()
    {
        // Mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        
        // Controller input
        float controllerX = Input.GetAxis("Right Stick Horizontal") * controllerSensitivity * Time.deltaTime;
        float controllerY = Input.GetAxis("Right Stick Vertical") * controllerSensitivity * Time.deltaTime;
        
        float lookX = mouseX + controllerX;
        float lookY = mouseY + controllerY;
        
        if (invertYAxis) lookY = -lookY;
        
        // Vertical look
        targetCameraPitch -= lookY;
        targetCameraPitch = Mathf.Clamp(targetCameraPitch, -maxLookAngle, maxLookAngle);
        cameraPitch = targetCameraPitch;
        
        // Apply rotation to camera and gun
        Quaternion pitchRotation = Quaternion.Euler(cameraPitch, 0, 0);
        
        if (cameraTransform != null)
        {
            cameraTransform.localRotation = pitchRotation;
        }
        
        if (gunTransform != null)
        {
            gunTransform.localRotation = pitchRotation;
        }
        
        // Horizontal look
        transform.Rotate(Vector3.up * lookX);
    }
    
    // Called by WeaponController
    public void ApplyRecoilMovement(float recoilStrength, float weaponMultiplier)
    {
        if (!isGrounded)
        {
            // Apply recoil as upward velocity in air
            float recoilForce = recoilStrength * recoilInfluence * weaponMultiplier * recoilMovementMultiplier;
            verticalVelocity += recoilForce * 0.1f;
        }
    }
    
    public void SetMovementMultiplier(float multiplier)
    {
        movementMultiplier = multiplier;
    }
    
    public void SetRecoilMovementMultiplier(float multiplier)
    {
        recoilMovementMultiplier = multiplier;
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
        Debug.Log("Player controls enabled!");
    }
}
