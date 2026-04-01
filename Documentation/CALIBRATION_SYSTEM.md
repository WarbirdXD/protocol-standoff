# Calibration System Documentation

**Real-time weapon tuning with risk/reward mechanics**

---

## Overview

The Calibration System allows players to adjust their weapon's characteristics in real-time during gameplay using a two-slider system. Players can tune fire rate, damage, recoil, spread, and movement penalties to match their playstyle, but become vulnerable while calibrating (75% slower movement). This creates strategic decisions about when and how to tune weapons.

---

## Architecture

```
Player presses Calibration Key (C)
    ↓
CalibrationMode.ToggleCalibrationMode()
    ↓
┌────────────────────────────────────┐
│  Calibration Active                │
├────────────────────────────────────┤
│  - Slow movement (25% speed)       │
│  - Unlock cursor                   │
│  - Show UI sliders                 │
│  - Accept input (Q/E, Z/C)         │
└────────────────────────────────────┘
    ↓
Player adjusts sliders
    ↓
WeaponController.RecalculateWeaponStats()
    ↓
┌────────────────────────────────────┐
│  Update Weapon Stats               │
│  - Fire rate (450-900 RPM)         │
│  - Damage (15-30)                  │
│  - Recoil multiplier (0.5x-2.0x)   │
│  - Spread multiplier (0.5x-2.0x)   │
│  - Movement penalty (1.2x-3.0x)    │
│  - ADS speed (5-15)                │
└────────────────────────────────────┘
    ↓
Instant effect on gameplay
    ↓
Player exits calibration
    ↓
Restore normal movement
    ↓
Weapon tuned to preference
```

---

## Two-Slider System

### Slider 1: Tempo vs Control (Q/E Keys)

**Axis:** Fire rate and accuracy trade-off

```
Tempo (0.0)              Control (1.0)
─────────────────────────────────────
Fast Fire                Slow Fire
Low Damage               High Damage
High Recoil              Low Recoil
Wide Spread              Tight Spread
Spray-and-Pray           Precise Shots
```

**Stat Calculations:**
```csharp
// Fire rate: 900 RPM (tempo) → 450 RPM (control)
float fireRate = Mathf.Lerp(900f, 450f, tempoControlSlider);
currentFireRate = 60f / fireRate; // Convert to seconds between shots

// Damage: 15 (tempo) → 30 (control)
damage = Mathf.Lerp(15f, 30f, tempoControlSlider);

// Recoil: High (tempo) → Low (control)
float recoilMultiplier = Mathf.Lerp(2.0f, 0.5f, tempoControlSlider);
currentRecoil = baseRecoil * recoilMultiplier;

// Spread: Wide (tempo) → Tight (control)
float spreadMultiplier = Mathf.Lerp(2.0f, 0.5f, tempoControlSlider);
currentSpread = baseSpread * spreadMultiplier;
```

**Examples:**

**Full Tempo (0.0):**
- Fire Rate: 900 RPM
- Damage: 15 per bullet
- Recoil: 2.0x (very high)
- Spread: 2.0x (very wide)
- **Playstyle:** Aggressive spray, close range

**Balanced (0.5):**
- Fire Rate: 675 RPM
- Damage: 22.5 per bullet
- Recoil: 1.25x (moderate)
- Spread: 1.25x (moderate)
- **Playstyle:** Versatile, all ranges

**Full Control (1.0):**
- Fire Rate: 450 RPM
- Damage: 30 per bullet
- Recoil: 0.5x (very low)
- Spread: 0.5x (very tight)
- **Playstyle:** Precise tapping, long range

### Slider 2: Aggressive vs Defensive Movement (Z/C Keys)

**Axis:** Movement style and accuracy trade-off

```
Aggressive (0.0)         Defensive (1.0)
─────────────────────────────────────
Fast Movement            Slow Movement
Low Accuracy Penalty     High Accuracy Penalty
Fast ADS                 Slow ADS
Run-and-Gun              Hold Angles
Mobile                   Stationary
```

**Stat Calculations:**
```csharp
// Movement penalty: Low (aggressive) → High (defensive)
movementSpreadMultiplier = Mathf.Lerp(1.2f, 3.0f, movementStyleSlider);

// ADS speed: Fast (aggressive) → Slow (defensive)
aimSpeed = Mathf.Lerp(15f, 5f, movementStyleSlider);
```

**Examples:**

**Full Aggressive (0.0):**
- Movement Penalty: 1.2x (minimal)
- ADS Speed: 15 (very fast)
- **Playstyle:** Run-and-gun, flanking, mobile

**Balanced (0.5):**
- Movement Penalty: 2.1x (moderate)
- ADS Speed: 10 (moderate)
- **Playstyle:** Flexible, can adapt

**Full Defensive (1.0):**
- Movement Penalty: 3.0x (severe)
- ADS Speed: 5 (very slow)
- **Playstyle:** Hold angles, defensive, accurate when still

---

## Calibration Mode

### Activation

```csharp
private void Update()
{
    // Toggle calibration mode (C key or Y button)
    if (Input.GetKeyDown(calibrationKey) || Input.GetButtonDown("Calibrate"))
    {
        ToggleCalibrationMode();
    }
    
    if (isCalibrating)
    {
        HandleCalibrationInput();
    }
}
```

### Toggle Implementation

```csharp
private void ToggleCalibrationMode()
{
    isCalibrating = !isCalibrating;
    
    if (fpsController != null)
    {
        if (isCalibrating)
        {
            // SLOW DOWN MOVEMENT (vulnerable while calibrating)
            normalMoveSpeed = fpsController.walkSpeed;
            normalSprintSpeed = fpsController.sprintSpeed;
            
            fpsController.walkSpeed = normalMoveSpeed * calibrationMoveSpeedMultiplier; // 25%
            fpsController.sprintSpeed = normalSprintSpeed * calibrationMoveSpeedMultiplier; // 25%
            
            // Unlock cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            Debug.Log("Calibration mode ENABLED - Movement slowed to 25%");
        }
        else
        {
            // Restore normal movement
            fpsController.walkSpeed = normalMoveSpeed;
            fpsController.sprintSpeed = normalSprintSpeed;
            
            // Re-lock cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // Recalculate weapon stats with new tuning
            if (weaponController != null)
            {
                weaponController.RecalculateWeaponStats();
            }
            
            Debug.Log("Calibration mode DISABLED - Movement restored");
        }
    }
}
```

### Risk/Reward Balance

**While Calibrating:**
- ⚠️ Movement speed: 25% of normal (very vulnerable)
- ⚠️ Cursor unlocked (can't aim properly)
- ⚠️ Visible to enemies (easy target)
- ✅ Can adjust weapon in real-time
- ✅ See immediate feedback

**After Calibrating:**
- ✅ Weapon perfectly tuned to playstyle
- ✅ Full movement speed restored
- ✅ Instant effect on gameplay
- ✅ Can adapt to opponents

---

## Input Handling

### Keyboard Input

```csharp
private void HandleCalibrationInput()
{
    if (weaponController == null) return;
    
    bool slidersChanged = false;
    
    // Tempo/Control slider (Q/E keys)
    float tempoInput = 0f;
    if (Input.GetKey(KeyCode.Q))
        tempoInput -= 1f; // More tempo (faster fire)
    if (Input.GetKey(KeyCode.E))
        tempoInput += 1f; // More control (slower, accurate)
    
    if (tempoInput != 0f)
    {
        weaponController.tempoControlSlider = Mathf.Clamp01(
            weaponController.tempoControlSlider + tempoInput * sliderChangeSpeed * Time.deltaTime
        );
        slidersChanged = true;
    }
    
    // Movement Style slider (Z/C keys)
    float movementInput = 0f;
    if (Input.GetKey(KeyCode.Z))
        movementInput -= 1f; // More aggressive
    if (Input.GetKey(KeyCode.X))
        movementInput += 1f; // More defensive
    
    if (movementInput != 0f)
    {
        weaponController.movementStyleSlider = Mathf.Clamp01(
            weaponController.movementStyleSlider + movementInput * sliderChangeSpeed * Time.deltaTime
        );
        slidersChanged = true;
    }
    
    // Auto-recalculate when sliders change (real-time feedback)
    if (slidersChanged)
    {
        weaponController.RecalculateWeaponStats();
    }
    
    // Manual recalculate button (R key or RB button)
    if (Input.GetKeyDown(KeyCode.R) || Input.GetButtonDown("Recalculate"))
    {
        weaponController.RecalculateWeaponStats();
    }
}
```

### Gamepad Support

```csharp
// Add D-pad horizontal input (new Input System)
if (Gamepad.current != null)
{
    Vector2 dpad = Gamepad.current.dpad.ReadValue();
    tempoInput += dpad.x; // Left/Right for tempo/control
    movementInput += dpad.y; // Up/Down for movement style
}
```

### Slider Change Speed

```csharp
public float sliderChangeSpeed = 0.4f; // Slower tuning (was 0.5f)

// Applied per frame:
slider += input * sliderChangeSpeed * Time.deltaTime;
```

**Why slow?**
- Prevents accidental over-adjustment
- Encourages deliberate tuning
- More vulnerable time (risk)

---

## Weapon Stat Recalculation

### Complete Recalculation

```csharp
public void RecalculateWeaponStats()
{
    // === TEMPO/CONTROL AXIS ===
    
    // Fire rate: 900 RPM (tempo) → 450 RPM (control)
    float fireRate = Mathf.Lerp(900f, 450f, tempoControlSlider);
    currentFireRate = 60f / fireRate; // Convert RPM to seconds between shots
    
    // Damage: 15 (tempo) → 30 (control)
    damage = Mathf.Lerp(15f, 30f, tempoControlSlider);
    
    // Recoil: High (tempo) → Low (control)
    float recoilMultiplier = Mathf.Lerp(2.0f, 0.5f, tempoControlSlider);
    currentRecoil = baseRecoil * recoilMultiplier;
    
    // Spread: Wide (tempo) → Tight (control)
    float spreadMultiplier = Mathf.Lerp(2.0f, 0.5f, tempoControlSlider);
    currentSpread = baseSpread * spreadMultiplier;
    
    // === MOVEMENT STYLE AXIS ===
    
    // Movement penalty: Low (aggressive) → High (defensive)
    movementSpreadMultiplier = Mathf.Lerp(1.2f, 3.0f, movementStyleSlider);
    
    // ADS speed: Fast (aggressive) → Slow (defensive)
    aimSpeed = Mathf.Lerp(15f, 5f, movementStyleSlider);
    
    // Log changes
    Debug.Log($"Weapon recalibrated: Fire Rate={fireRate:F0} RPM, Damage={damage:F0}, " +
              $"Recoil={currentRecoil:F2}, Spread={currentSpread:F2}, " +
              $"MovementPenalty={movementSpreadMultiplier:F2}x, ADS Speed={aimSpeed:F1}");
}
```

### Instant Effect

**No delay** - changes apply immediately:
```csharp
// Auto-recalculate for real-time feedback
if (slidersChanged)
{
    weaponController.RecalculateWeaponStats();
}
```

**Player feels the difference instantly:**
- Fire rate changes immediately
- Recoil pattern changes
- Spread tightens/widens
- Damage per shot changes

---

## Strategic Use

### When to Calibrate

**Good Times:**
1. **After respawn** - Safe time before engaging
2. **Behind cover** - Protected from enemies
3. **Teammate covering (2v2)** - Someone watching your back
4. **Between rounds** - No immediate threat
5. **After winning fight** - Brief safe period

**Bad Times:**
1. **During firefight** - Too vulnerable
2. **Being chased** - Need full speed
3. **Low health** - Can't afford to be slow
4. **Enemy nearby** - Will be killed easily
5. **Open area** - No cover if attacked

### What to Tune

**Losing gunfights?**
- → Increase control (more damage, less recoil)
- → Increase defensive (better accuracy when still)

**Getting flanked?**
- → Increase tempo (faster fire rate)
- → Increase aggressive (better mobility)

**Can't track targets?**
- → Adjust movement style
- → Find balance between mobility and accuracy

**Opponents playing defensive?**
- → Increase tempo (pressure with volume of fire)
- → Increase aggressive (push aggressively)

**Opponents rushing?**
- → Increase control (precise shots)
- → Increase defensive (hold angles)

---

## Example Calibrations

### Aggressive Rusher

**Goal:** Fast, mobile, close-range dominance

```
Tempo/Control: 0.2 (Fast fire, mobile)
Movement Style: 0.1 (Run-and-gun)

Result:
- Fire Rate: 800 RPM
- Damage: 18 per bullet
- Recoil: 1.7x (high)
- Spread: 1.7x (wide)
- Movement Penalty: 1.3x (minimal)
- ADS Speed: 14 (very fast)

Playstyle:
- Rush enemies
- Spray at close range
- High mobility
- Flanking routes
```

### Defensive Anchor

**Goal:** Hold angles, precise shots, long-range

```
Tempo/Control: 0.9 (Slow, accurate)
Movement Style: 0.8 (Hold angles)

Result:
- Fire Rate: 480 RPM
- Damage: 28 per bullet
- Recoil: 0.6x (very low)
- Spread: 0.6x (very tight)
- Movement Penalty: 2.8x (severe)
- ADS Speed: 6 (slow)

Playstyle:
- Hold defensive positions
- Tap fire at long range
- Laser accuracy when still
- Pre-aim corners
```

### Balanced All-Rounder

**Goal:** Versatile, adapt to any situation

```
Tempo/Control: 0.5 (Balanced)
Movement Style: 0.5 (Balanced)

Result:
- Fire Rate: 675 RPM
- Damage: 22.5 per bullet
- Recoil: 1.25x (moderate)
- Spread: 1.25x (moderate)
- Movement Penalty: 2.1x (moderate)
- ADS Speed: 10 (moderate)

Playstyle:
- Flexible approach
- Works at all ranges
- Can rush or hold
- Adapt to opponents
```

### Headshot Hunter

**Goal:** Precise headshots, high damage per shot

```
Tempo/Control: 0.8 (Slow, accurate)
Movement Style: 0.4 (Slightly defensive)

Result:
- Fire Rate: 540 RPM
- Damage: 27 per bullet
- Recoil: 0.7x (low)
- Spread: 0.7x (tight)
- Movement Penalty: 1.9x (moderate)
- ADS Speed: 11 (moderate)

Playstyle:
- Aim for headshots
- Burst fire
- Medium range
- Controlled aggression
```

---

## UI Integration

### Calibration UI Elements

**Required UI:**
1. **Tempo/Control Slider** - Visual representation of Q/E axis
2. **Movement Style Slider** - Visual representation of Z/C axis
3. **Stat Display** - Show current weapon stats
4. **Instructions** - Key bindings and tips
5. **Warning** - "Vulnerable while calibrating!"

### UI Update

```csharp
void UpdateCalibrationUI()
{
    if (isCalibrating)
    {
        // Update slider visuals
        tempoControlSliderUI.value = weaponController.tempoControlSlider;
        movementStyleSliderUI.value = weaponController.movementStyleSlider;
        
        // Update stat displays
        fireRateText.text = $"{GetCurrentFireRate():F0} RPM";
        damageText.text = $"{weaponController.damage:F0}";
        recoilText.text = $"{weaponController.currentRecoil:F2}x";
        spreadText.text = $"{weaponController.currentSpread:F2}x";
        
        // Show calibration panel
        calibrationPanel.SetActive(true);
    }
    else
    {
        // Hide calibration panel
        calibrationPanel.SetActive(false);
    }
}
```

---

## Why This System Works

### 1. Player Agency
Players aren't forced into preset weapon archetypes. They can create their own perfect weapon.

### 2. Skill Expression
Good players can optimize for their exact playstyle and adapt mid-match.

### 3. Dynamic Gameplay
Can counter opponents by changing weapon characteristics.

### 4. Risk/Reward
Vulnerable while tuning creates strategic decisions about when to calibrate.

### 5. Accessibility
Simple two-slider system is easy to understand and use.

### 6. Instant Feedback
Real-time recalculation lets players feel changes immediately.

### 7. No "Best" Configuration
Every configuration has trade-offs. No objectively superior setup.

### 8. Encourages Experimentation
Players can try different setups without penalty.

---

## Integration with Other Systems

### With Weapon System
```
Calibration changes sliders → RecalculateWeaponStats() → Weapon behavior changes
```

### With Movement System
```
Calibration active → Slow movement → Restore on exit
```

### With UI System
```
Calibration active → Show sliders → Update displays → Hide on exit
```

### With Input System
```
Q/E keys → Adjust tempo/control
Z/C keys → Adjust movement style
C key → Toggle calibration
```

---

## Best Practices

1. **Calibrate in safe locations** - Behind cover, after respawn
2. **Experiment with extremes** - Try full tempo and full control
3. **Find your preference** - Everyone has different optimal settings
4. **Adapt to opponents** - Change calibration to counter playstyles
5. **Don't over-calibrate** - Small adjustments are often enough
6. **Remember your settings** - Note what works for you
7. **Practice with settings** - Get comfortable before competitive play
8. **Use teammate cover** - In 2v2, calibrate when teammate can protect
9. **Quick adjustments** - Don't spend too long calibrating
10. **Trust your instincts** - If it feels good, it probably is

---

## Debugging

### Debug Display

```csharp
void OnGUI()
{
    if (showDebugInfo && isCalibrating)
    {
        GUILayout.Label("=== Calibration Debug ===");
        GUILayout.Label($"Tempo/Control: {weaponController.tempoControlSlider:F2}");
        GUILayout.Label($"Movement Style: {weaponController.movementStyleSlider:F2}");
        GUILayout.Label($"Fire Rate: {GetCurrentFireRate():F0} RPM");
        GUILayout.Label($"Damage: {weaponController.damage:F0}");
        GUILayout.Label($"Recoil: {weaponController.currentRecoil:F2}x");
        GUILayout.Label($"Spread: {weaponController.currentSpread:F2}x");
        GUILayout.Label($"Movement Penalty: {weaponController.movementSpreadMultiplier:F2}x");
        GUILayout.Label($"ADS Speed: {weaponController.aimSpeed:F1}");
        GUILayout.Label($"Movement Speed: {fpsController.walkSpeed:F1} m/s");
    }
}
```

### Test Calibrations

```csharp
[ContextMenu("Test Full Tempo")]
void TestFullTempo()
{
    weaponController.tempoControlSlider = 0f;
    weaponController.movementStyleSlider = 0f;
    weaponController.RecalculateWeaponStats();
}

[ContextMenu("Test Full Control")]
void TestFullControl()
{
    weaponController.tempoControlSlider = 1f;
    weaponController.movementStyleSlider = 1f;
    weaponController.RecalculateWeaponStats();
}

[ContextMenu("Test Balanced")]
void TestBalanced()
{
    weaponController.tempoControlSlider = 0.5f;
    weaponController.movementStyleSlider = 0.5f;
    weaponController.RecalculateWeaponStats();
}
```

---

*This documentation explains the calibration system architecture and functionality. For implementation details, see `CalibrationMode.cs` and `WeaponController.cs`.*
