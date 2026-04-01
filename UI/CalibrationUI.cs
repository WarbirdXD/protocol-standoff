using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CalibrationUI : MonoBehaviour
{
    [Header("References")]
    public CalibrationMode calibrationMode;
    public WeaponController weaponController;
    
    [Header("UI Elements")]
    public GameObject calibrationPanel;
    public Slider tempoControlSlider;
    public Slider movementStyleSlider;
    public TextMeshProUGUI tempoControlLabel;
    public TextMeshProUGUI movementStyleLabel;
    public TextMeshProUGUI statsDisplay;
    
    [Header("Labels")]
    public string tempoLabel = "TEMPO";
    public string controlLabel = "CONTROL";
    public string agileLabel = "AGILE";
    public string heavyLabel = "HEAVY";
    
    private void Start()
    {
        Debug.Log("CalibrationUI: Start() called - script is running!");
        
        if (calibrationPanel != null)
        {
            calibrationPanel.SetActive(false);
            Debug.Log("CalibrationUI: Panel disabled at start");
        }
        else
        {
            Debug.LogError("CalibrationUI: calibrationPanel is NULL in Start!");
        }
        
        // Initialize sliders if they exist
        if (tempoControlSlider != null && weaponController != null)
        {
            tempoControlSlider.value = weaponController.tempoControlSlider;
            tempoControlSlider.onValueChanged.AddListener(OnTempoControlChanged);
        }
        
        if (movementStyleSlider != null && weaponController != null)
        {
            movementStyleSlider.value = weaponController.movementStyleSlider;
            movementStyleSlider.onValueChanged.AddListener(OnMovementStyleChanged);
        }
    }
    
    private bool lastCalibrationState = false;
    
    private void Update()
    {
        if (calibrationMode == null)
        {
            Debug.LogError("CalibrationUI: calibrationMode reference is missing!");
            return;
        }
        
        // Show/hide calibration panel
        if (calibrationPanel != null)
        {
            bool isCalibrating = calibrationMode.IsCalibrating;
            
            // Debug when state changes
            if (isCalibrating != lastCalibrationState)
            {
                Debug.Log($"CalibrationUI: Calibration changed to {isCalibrating}, setting panel active");
                lastCalibrationState = isCalibrating;
            }
            
            calibrationPanel.SetActive(isCalibrating);
        }
        else
        {
            Debug.LogError("CalibrationUI: calibrationPanel reference is missing!");
        }
        
        if (calibrationMode.IsCalibrating)
        {
            UpdateUI();
        }
    }
    
    private void UpdateUI()
    {
        if (weaponController == null) return;
        
        // Update slider values
        if (tempoControlSlider != null)
        {
            tempoControlSlider.value = weaponController.tempoControlSlider;
        }
        
        if (movementStyleSlider != null)
        {
            movementStyleSlider.value = weaponController.movementStyleSlider;
        }
        
        // Update labels
        if (tempoControlLabel != null)
        {
            float value = weaponController.tempoControlSlider;
            // INVERTED: 0 = Control (left), 1 = Tempo (right) due to Inspector values
            if (value < 0.5f)
            {
                // Control side (0 to 0.5)
                float percentage = (0.5f - value) * 200f;
                tempoControlLabel.text = $"{controlLabel} {percentage:F0}%";
            }
            else if (value > 0.5f)
            {
                // Tempo side (0.5 to 1)
                float percentage = (value - 0.5f) * 200f;
                tempoControlLabel.text = $"{tempoLabel} {percentage:F0}%";
            }
            else
            {
                // Exactly balanced
                tempoControlLabel.text = "BALANCED";
            }
        }
        
        if (movementStyleLabel != null)
        {
            float value = weaponController.movementStyleSlider;
            // INVERTED: 0 = Heavy (left), 1 = Agile (right) due to Inspector values
            if (value < 0.5f)
            {
                // Heavy side (0 to 0.5)
                float percentage = (0.5f - value) * 200f;
                movementStyleLabel.text = $"{heavyLabel} {percentage:F0}%";
            }
            else if (value > 0.5f)
            {
                // Agile side (0.5 to 1)
                float percentage = (value - 0.5f) * 200f;
                movementStyleLabel.text = $"{agileLabel} {percentage:F0}%";
            }
            else
            {
                // Exactly balanced
                movementStyleLabel.text = "BALANCED";
            }
        }
        
        // Update stats display with actual calculated values
        if (statsDisplay != null)
        {
            // Force recalculation to ensure values are current
            weaponController.RecalculateWeaponStats();
            
            // Now calculate display values
            float fireRate = CalculateFireRate();
            float recoil = CalculateRecoil();
            float spread = CalculateSpread();
            float moveSpeed = CalculateMoveSpeed();
            
            // Debug to verify calculations
            Debug.Log($"Slider: {weaponController.tempoControlSlider:F2}, FireRate: {fireRate:F0} RPM, Recoil: {recoil:F1}");
            
            statsDisplay.text = $"<b>WEAPON STATS</b>\n\n" +
                              $"Fire Rate: <color=yellow>{fireRate:F0} RPM</color>\n" +
                              $"Recoil: <color=yellow>{recoil:F1}</color>\n" +
                              $"Spread: <color=yellow>{spread:F3}</color>\n" +
                              $"Move Speed: <color=yellow>{moveSpeed:F0}%</color>";
        }
    }
    
    private float CalculateFireRate()
    {
        // Use the actual calculated fire rate from WeaponController
        float fireRate = weaponController.GetCurrentFireRate();
        return 60f / fireRate; // Convert to RPM
    }
    
    private float CalculateRecoil()
    {
        // Use the actual calculated recoil from WeaponController
        return weaponController.GetCurrentRecoil();
    }
    
    private float CalculateSpread()
    {
        // Use the actual calculated spread from WeaponController
        return weaponController.GetCurrentSpread();
    }
    
    private float CalculateMoveSpeed()
    {
        // Match WeaponController's Lerp: 0=Agile, 1=Heavy
        float moveSpeed = 100f * 
            Mathf.Lerp(weaponController.agileMoveSpeedMultiplier, 
                      weaponController.heavyMoveSpeedMultiplier, 
                      weaponController.movementStyleSlider);
        
        return moveSpeed;
    }
    
    private void OnTempoControlChanged(float value)
    {
        if (weaponController != null)
        {
            weaponController.tempoControlSlider = value;
        }
    }
    
    private void OnMovementStyleChanged(float value)
    {
        if (weaponController != null)
        {
            weaponController.movementStyleSlider = value;
        }
    }
}
