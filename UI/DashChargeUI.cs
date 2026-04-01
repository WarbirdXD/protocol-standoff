using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the air dash charge status
/// Shows when boots are charged and dash is available
/// </summary>
public class DashChargeUI : MonoBehaviour
{
    [Header("References")]
    public FPSController player;
    public Image chargeBar;
    public Image chargeIcon;
    
    [Header("Colors")]
    public Color chargingColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
    public Color chargedColor = new Color(0f, 1f, 0.5f, 1f);
    public Color readyColor = new Color(1f, 1f, 0f, 1f);
    
    private void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<FPSController>();
        }
    }
    
    private void Update()
    {
        if (player == null) return;
        
        float progress = player.ChargeProgress;
        bool isCharged = player.IsCharged;
        
        // Update charge bar fill
        if (chargeBar != null)
        {
            chargeBar.fillAmount = progress;
            
            // Color based on state
            if (isCharged)
            {
                chargeBar.color = chargedColor;
            }
            else
            {
                chargeBar.color = chargingColor;
            }
        }
        
        // Update icon color
        if (chargeIcon != null)
        {
            if (isCharged)
            {
                chargeIcon.color = readyColor;
            }
            else
            {
                chargeIcon.color = Color.Lerp(chargingColor, chargedColor, progress);
            }
        }
    }
}
