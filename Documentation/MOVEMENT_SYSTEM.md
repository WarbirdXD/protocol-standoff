# Movement System Documentation

**Responsive, skill-based player movement with modern FPS mechanics**

---

## Overview

The Movement System provides responsive player control using Unity's CharacterController with physics-based mechanics. It implements smooth acceleration/deceleration, omni-directional sprinting, variable jump height, coyote time, jump buffering, and precise camera control for competitive FPS gameplay.

---

## Architecture

```
Input System
    ↓
FPSController.Update()
    ↓
┌────────────────────────────────────┐
│  Process Input                     │
│  - WASD movement                   │
│  - Shift sprint                    │
│  - Space jump                      │
│  - Mouse look                      │
└────────────────────────────────────┘
    ↓
HandleMovement()
    ↓
┌────────────────────────────────────┐
│  Calculate Movement                │
│  - Apply acceleration              │
│  - Apply friction                  │
│  - Handle sprinting                │
│  - Apply air control               │
└────────────────────────────────────┘
    ↓
HandleJumping()
    ↓
┌────────────────────────────────────┐
│  Jump Mechanics                    │
│  - Coyote time (0.15s)             │
│  - Jump buffering (0.2s)           │
│  - Variable jump height            │
│  - Physics-based velocity          │
└────────────────────────────────────┘
    ↓
CharacterController.Move()
    ↓
Player Moves
```

---

## Basic Movement

### Movement Speed

```csharp
[Header("Movement")]
public float walkSpeed = 5f;        // Base walking speed (m/s)
public float sprintSpeed = 8f;      // Sprint speed (m/s) - 1.6x multiplier
public float acceleration = 10f;     // Speed increase rate (m/s²)
public float friction = 8f;          // Speed decrease rate (m/s²)
public float airControl = 0.3f;      // Movement control while airborne (30%)
```

### Movement Calculation

```csharp
private void HandleMovement()
{
    // Get input direction
    Vector2 input = moveAction.ReadValue<Vector2>();
    Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;
    moveDirection.Normalize();
    
    // Determine target speed
    float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
    
    // Apply calibration mode slowdown if active
    if (calibrationMode != null && calibrationMode.IsCalibrating)
    {
        targetSpeed *= 0.25f; // 75% slower while calibrating
    }
    
    // Calculate desired velocity
    Vector3 targetVelocity = moveDirection * targetSpeed;
    
    // Apply acceleration or friction
    if (moveDirection.magnitude > 0.1f)
    {
        // Accelerating
        float accel = characterController.isGrounded ? acceleration : acceleration * airControl;
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, accel * Time.deltaTime);
    }
    else
    {
        // Decelerating (friction)
        float fric = characterController.isGrounded ? friction : friction * airControl;
        currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, fric * Time.deltaTime);
    }
    
    // Apply movement
    Vector3 move = currentVelocity * Time.deltaTime;
    characterController.Move(move);
}
```

### Smooth Acceleration

**Why smooth acceleration?**
- More realistic feel
- Better control at all speeds
- Prevents instant direction changes
- Skill-based movement (momentum management)

**Example:**
```
Standing still → Press W
Frame 1: 0.0 m/s
Frame 2: 0.1 m/s (acceleration applied)
Frame 3: 0.2 m/s
...
Frame 50: 5.0 m/s (reached walk speed)
```

---

## Sprinting System

### Omni-Directional Sprint

**Old System (Forward Only):**
```csharp
// Only sprint when moving forward
if (input.y > 0 && sprintInput)
    isSprinting = true;
```

**New System (All Directions):**
```csharp
// Sprint in any direction
if (moveDirection.magnitude > 0.1f && sprintInput)
    isSprinting = true;
else
    isSprinting = false;
```

### Sprint Acceleration

```csharp
[Header("Sprint")]
public float sprintAcceleration = 15f; // Faster acceleration when sprinting

// In HandleMovement():
float accel = isSprinting ? sprintAcceleration : acceleration;
```

**Why faster sprint acceleration?**
- Sprinting feels more responsive
- Encourages aggressive play
- Rewards good movement timing

### Sprint Mechanics

```csharp
private void HandleSprinting()
{
    // Check if sprint button held
    bool sprintInput = Input.GetKey(KeyCode.LeftShift);
    
    // Check if moving
    Vector2 input = moveAction.ReadValue<Vector2>();
    bool isMoving = input.magnitude > 0.1f;
    
    // Update sprint state
    if (sprintInput && isMoving && !isCalibrating)
    {
        isSprinting = true;
    }
    else
    {
        isSprinting = false;
    }
}
```

---

## Jumping System

### Physics-Based Jump

**Old System (Force-Based):**
```csharp
// Simple upward force
velocity.y = jumpForce; // Inconsistent height
```

**New System (Height-Based):**
```csharp
// Calculate velocity from desired height using physics
float jumpVelocity = Mathf.Sqrt(jumpHeight * 2f * Mathf.Abs(gravity));
velocity.y = jumpVelocity; // Consistent height
```

**Physics Formula:**
```
v = √(2gh)

Where:
v = initial velocity
g = gravity (20 m/s²)
h = jump height (2 m)

Example:
v = √(2 × 20 × 2) = √80 = 8.94 m/s
```

### Jump Configuration

```csharp
[Header("Jumping")]
public float jumpHeight = 2f;        // Desired jump height (meters)
public float gravity = -20f;         // Gravity acceleration (m/s²)
public float coyoteTime = 0.15f;     // Grace period after leaving ground
public float jumpBufferTime = 0.2f;  // Input buffer before landing
```

### Coyote Time

**Purpose:** Allow jumping slightly after leaving a platform.

```csharp
private float coyoteTimeCounter;

void Update()
{
    if (characterController.isGrounded)
    {
        coyoteTimeCounter = coyoteTime; // Reset counter
    }
    else
    {
        coyoteTimeCounter -= Time.deltaTime; // Count down
    }
}

bool CanJump()
{
    // Can jump if grounded OR within coyote time
    return characterController.isGrounded || coyoteTimeCounter > 0f;
}
```

**Example:**
```
Player walks off platform
Frame 1: isGrounded = false, coyoteTimeCounter = 0.15s
Frame 2: Player presses jump → Success! (within coyote time)
Frame 10: coyoteTimeCounter = 0.0s → Can't jump anymore
```

### Jump Buffering

**Purpose:** Remember jump input before landing.

```csharp
private float jumpBufferCounter;

void Update()
{
    // Check for jump input
    if (Input.GetButtonDown("Jump"))
    {
        jumpBufferCounter = jumpBufferTime; // Store input
    }
    else
    {
        jumpBufferCounter -= Time.deltaTime; // Count down
    }
    
    // Execute buffered jump when landing
    if (CanJump() && jumpBufferCounter > 0f)
    {
        Jump();
        jumpBufferCounter = 0f; // Consume buffer
    }
}
```

**Example:**
```
Player in air, about to land
Frame 1: Player presses jump → jumpBufferCounter = 0.2s
Frame 2: Still in air → jumpBufferCounter = 0.18s
Frame 3: Lands → isGrounded = true → Execute jump!
```

### Variable Jump Height

**Purpose:** Short tap = short jump, hold = full jump.

```csharp
void HandleJumping()
{
    // Apply gravity
    velocity.y += gravity * Time.deltaTime;
    
    // Variable jump height
    if (Input.GetButtonUp("Jump") && velocity.y > 0f)
    {
        // Released jump early → cut velocity
        velocity.y *= 0.5f; // Shorter jump
    }
    
    // Apply vertical movement
    characterController.Move(new Vector3(0, velocity.y, 0) * Time.deltaTime);
}
```

**Example:**
```
Full Jump (hold):
- Initial velocity: 8.94 m/s
- Peak height: 2.0 m
- Air time: 0.9s

Short Jump (tap):
- Initial velocity: 8.94 m/s
- Cut to: 4.47 m/s (released early)
- Peak height: 0.5 m
- Air time: 0.45s
```

---

## Camera Control

### Mouse Look

```csharp
[Header("Camera")]
public float mouseSensitivity = 2f;
public float verticalLookLimit = 90f;

private float xRotation = 0f;

private void HandleCameraLook()
{
    // Get mouse input
    Vector2 lookInput = lookAction.ReadValue<Vector2>();
    
    // Apply sensitivity
    float mouseX = lookInput.x * mouseSensitivity;
    float mouseY = lookInput.y * mouseSensitivity;
    
    // Rotate player horizontally
    transform.Rotate(Vector3.up * mouseX);
    
    // Rotate camera vertically
    xRotation -= mouseY;
    xRotation = Mathf.Clamp(xRotation, -verticalLookLimit, verticalLookLimit);
    playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
}
```

### Vertical Clamp

**Why clamp?**
- Prevents over-rotation (looking behind yourself)
- Standard FPS behavior
- Prevents gimbal lock issues

```
Looking up: -90° (straight up)
Looking forward: 0° (horizon)
Looking down: +90° (straight down)
```

### Sensitivity Adjustment

```csharp
public void SetSensitivity(float newSensitivity)
{
    mouseSensitivity = newSensitivity;
    PlayerPrefs.SetFloat("MouseSensitivity", newSensitivity);
    PlayerPrefs.Save();
}

void Start()
{
    // Load saved sensitivity
    if (PlayerPrefs.HasKey("MouseSensitivity"))
    {
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity");
    }
}
```

---

## Air Control

### Purpose
Allow some movement control while airborne, but less than on ground.

### Implementation

```csharp
public float airControl = 0.3f; // 30% control in air

void HandleMovement()
{
    float accel = characterController.isGrounded ? acceleration : acceleration * airControl;
    
    currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, accel * Time.deltaTime);
}
```

### Why Reduced Air Control?

1. **Realism:** Can't change direction mid-air
2. **Skill-based:** Rewards good movement planning
3. **Balance:** Prevents air strafing exploits
4. **Predictability:** Easier to track airborne players

**Example:**
```
Ground acceleration: 10 m/s²
Air acceleration: 3 m/s² (30%)

Result: Can adjust trajectory slightly, but not drastically
```

---

## CharacterController

### Configuration

```csharp
void Awake()
{
    characterController = GetComponent<CharacterController>();
    
    // Configure CharacterController
    characterController.radius = 0.5f;      // Capsule radius
    characterController.height = 2f;        // Player height
    characterController.slopeLimit = 45f;   // Max walkable slope
    characterController.stepOffset = 0.3f;  // Max step height
}
```

### Collision Detection

```csharp
void OnControllerColliderHit(ControllerColliderHit hit)
{
    // Handle collision with objects
    if (hit.gameObject.CompareTag("Obstacle"))
    {
        // Stop movement in collision direction
        Vector3 normal = hit.normal;
        currentVelocity = Vector3.ProjectOnPlane(currentVelocity, normal);
    }
}
```

### Ground Detection

```csharp
bool IsGrounded()
{
    // CharacterController.isGrounded is reliable
    return characterController.isGrounded;
}

// Alternative: Raycast check
bool IsGroundedRaycast()
{
    float rayLength = 0.1f;
    return Physics.Raycast(transform.position, Vector3.down, rayLength, groundLayer);
}
```

---

## Movement States

### State Machine

```
Idle
  ↓ (input)
Walking
  ↓ (sprint)
Sprinting
  ↓ (jump)
Jumping
  ↓ (apex)
Falling
  ↓ (land)
Walking/Idle
```

### State Detection

```csharp
public enum MovementState
{
    Idle,
    Walking,
    Sprinting,
    Jumping,
    Falling
}

private MovementState GetCurrentState()
{
    if (!characterController.isGrounded)
    {
        return velocity.y > 0 ? MovementState.Jumping : MovementState.Falling;
    }
    
    if (currentVelocity.magnitude < 0.1f)
    {
        return MovementState.Idle;
    }
    
    return isSprinting ? MovementState.Sprinting : MovementState.Walking;
}
```

---

## Input System

### New Input System

```csharp
[Header("Input Actions")]
public InputActionReference moveAction;
public InputActionReference lookAction;
public InputActionReference jumpAction;
public InputActionReference sprintAction;

void OnEnable()
{
    moveAction.action.Enable();
    lookAction.action.Enable();
    jumpAction.action.Enable();
    sprintAction.action.Enable();
}

void OnDisable()
{
    moveAction.action.Disable();
    lookAction.action.Disable();
    jumpAction.action.Disable();
    sprintAction.action.Disable();
}
```

### Input Reading

```csharp
void Update()
{
    // Movement input
    Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
    
    // Look input
    Vector2 lookInput = lookAction.action.ReadValue<Vector2>();
    
    // Jump input (button)
    bool jumpPressed = jumpAction.action.WasPressedThisFrame();
    bool jumpReleased = jumpAction.action.WasReleasedThisFrame();
    
    // Sprint input (hold)
    bool sprintHeld = sprintAction.action.IsPressed();
}
```

---

## Integration with Other Systems

### With Calibration System
```
Calibration active → Slow movement to 25%
Calibration inactive → Restore normal speed
```

### With Weapon System
```
Moving → Apply movement spread penalty
Sprinting → Can't shoot accurately
Standing still → Best accuracy
```

### With Network System
```
Local player → Full control enabled
Remote player → Controls disabled, position synced
```

### With Animation System
```
Movement state → Trigger animations
Speed → Blend animation speed
```

---

## Performance Optimization

### Efficient Updates

```csharp
void Update()
{
    // Only process for local player
    if (!IsOwner) return;
    
    // Cache frequently used values
    bool grounded = characterController.isGrounded;
    
    // Process input
    HandleMovement();
    HandleJumping();
    HandleCameraLook();
}
```

### Avoid Unnecessary Calculations

```csharp
// Bad: Calculate every frame
void Update()
{
    float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
}

// Good: Only when needed
float GetHorizontalSpeed()
{
    return new Vector3(velocity.x, 0, velocity.z).magnitude;
}
```

---

## Best Practices

1. **Use CharacterController** for player movement (not Rigidbody)
2. **Clamp vertical rotation** to prevent over-rotation
3. **Implement coyote time** for better jump feel
4. **Use jump buffering** for responsive controls
5. **Apply smooth acceleration** for realistic movement
6. **Reduce air control** for balanced gameplay
7. **Cache component references** in Awake/Start
8. **Only process input** for local player
9. **Use physics-based jump** for consistent height
10. **Test on different frame rates** to ensure consistency

---

## Common Issues

### Issue: Player slides on slopes
**Solution:** Increase CharacterController.slopeLimit or apply downward force.

### Issue: Player gets stuck on edges
**Solution:** Increase CharacterController.stepOffset or smooth collision normals.

### Issue: Jump height inconsistent
**Solution:** Use physics formula instead of fixed force.

### Issue: Movement feels sluggish
**Solution:** Increase acceleration value or reduce friction.

### Issue: Can't jump sometimes
**Solution:** Implement coyote time and jump buffering.

### Issue: Camera flips upside down
**Solution:** Clamp vertical rotation to ±90°.

---

## Debugging

### Debug Visualization

```csharp
void OnDrawGizmos()
{
    if (!Application.isPlaying) return;
    
    // Draw velocity vector
    Gizmos.color = Color.blue;
    Gizmos.DrawLine(transform.position, transform.position + currentVelocity);
    
    // Draw ground check
    Gizmos.color = characterController.isGrounded ? Color.green : Color.red;
    Gizmos.DrawWireSphere(transform.position, 0.5f);
    
    // Draw coyote time indicator
    if (coyoteTimeCounter > 0)
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up, Vector3.one * 0.2f);
    }
}
```

### Debug Display

```csharp
void OnGUI()
{
    if (showDebugInfo)
    {
        GUILayout.Label($"Speed: {currentVelocity.magnitude:F2} m/s");
        GUILayout.Label($"Grounded: {characterController.isGrounded}");
        GUILayout.Label($"Sprinting: {isSprinting}");
        GUILayout.Label($"Coyote Time: {coyoteTimeCounter:F2}s");
        GUILayout.Label($"Jump Buffer: {jumpBufferCounter:F2}s");
        GUILayout.Label($"State: {GetCurrentState()}");
    }
}
```

---

*This documentation explains the movement system architecture and functionality. For implementation details, see `FPSController.cs`.*
