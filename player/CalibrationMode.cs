using UnityEngine;
using UnityEngine.InputSystem;

public class CalibrationMode : MonoBehaviour
{
    [Header("Calibration Settings")]
    public KeyCode calibrationKey = KeyCode.C;  // Changed from Tab (Tab = Scoreboard)
    public float calibrationMoveSpeedMultiplier = 0.25f; // More vulnerable (was 0.3f)
    public float sliderChangeSpeed = 0.4f;               // Slower tuning (was 0.5f)
    
    [Header("References")]
    private WeaponController weaponController;
    private FPSController fpsController;
    
    private bool isCalibrating = false;
    private float normalMoveSpeed;
    private float normalSprintSpeed;
    
    public bool IsCalibrating => isCalibrating;
    
    private void Start()
    {
        weaponController = GetComponent<WeaponController>();
        fpsController = GetComponent<FPSController>();
        
        if (fpsController != null)
        {
            normalMoveSpeed = fpsController.walkSpeed;
            normalSprintSpeed = fpsController.sprintSpeed;
        }
    }
    
    private void Update()
    {
        // Toggle calibration mode (Tab or Y button)
        if (Input.GetKeyDown(calibrationKey) || Input.GetButtonDown("Calibrate"))
        {
            ToggleCalibrationMode();
        }
        
        if (isCalibrating)
        {
            HandleCalibrationInput();
        }
    }
    
    private void ToggleCalibrationMode()
    {
        isCalibrating = !isCalibrating;
        Debug.Log($"CalibrationMode: Toggled to {isCalibrating}");
        
        if (fpsController != null)
        {
            if (isCalibrating)
            {
                // Slow down movement
                fpsController.walkSpeed = normalMoveSpeed * calibrationMoveSpeedMultiplier;
                fpsController.sprintSpeed = normalSprintSpeed * calibrationMoveSpeedMultiplier;
                
                // Lock cursor for UI interaction
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
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
            }
        }
    }
    
    private void HandleCalibrationInput()
    {
        if (weaponController == null) return;
        
        // Tempo/Control slider (Q/E keys or D-pad Left/Right)
        float tempoInput = 0f;
        if (Input.GetKey(KeyCode.Q))
            tempoInput -= 1f;
        if (Input.GetKey(KeyCode.E))
            tempoInput += 1f;
        
        // Add D-pad horizontal input (new Input System)
        if (Gamepad.current != null)
        {
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            tempoInput += dpad.x; // Left/Right
        }
        
        bool slidersChanged = false;
        
        if (tempoInput != 0f)
        {
            weaponController.tempoControlSlider = Mathf.Clamp01(
                weaponController.tempoControlSlider + tempoInput * sliderChangeSpeed * Time.deltaTime
            );
            slidersChanged = true;
        }
        
        // Movement Style slider (Z/C keys or D-pad Up/Down)
        float movementInput = 0f;
        if (Input.GetKey(KeyCode.Z))
            movementInput -= 1f;
        if (Input.GetKey(KeyCode.C))
            movementInput += 1f;
        
        // Add D-pad vertical input (new Input System)
        if (Gamepad.current != null)
        {
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            movementInput += dpad.y; // Up/Down
        }
        
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
    
    public void ForceExitCalibration()
    {
        if (isCalibrating)
        {
            ToggleCalibrationMode();
        }
    }
}
