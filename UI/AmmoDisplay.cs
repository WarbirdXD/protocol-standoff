using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays current ammo count on the HUD
/// </summary>
public class AmmoDisplay : MonoBehaviour
{
    [Header("References")]
    public WeaponController weaponController;
    
    [Header("UI Elements")]
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI reserveText;
    public Image reloadProgressBar;
    
    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color lowAmmoColor = Color.yellow;
    public Color emptyColor = Color.red;
    public Color reloadingColor = new Color(0.5f, 0.5f, 1f);
    
    [Header("Settings")]
    public int lowAmmoThreshold = 10;
    public bool showReloadProgress = true;
    
    private void Start()
    {
        if (weaponController == null)
        {
            weaponController = FindFirstObjectByType<WeaponController>();
        }
        
        if (reloadProgressBar != null)
        {
            reloadProgressBar.fillAmount = 0f;
            reloadProgressBar.gameObject.SetActive(false);
        }
    }
    
    private void Update()
    {
        if (weaponController == null || ammoText == null) return;
        
        int currentAmmo = weaponController.GetCurrentAmmo();
        int reserveAmmo = weaponController.GetReserveAmmo();
        bool isReloading = weaponController.IsReloading();
        
        // Update ammo text
        if (isReloading)
        {
            ammoText.text = "RELOADING";
            ammoText.color = reloadingColor;
        }
        else
        {
            ammoText.text = currentAmmo.ToString();
            
            // Color based on ammo count
            if (currentAmmo == 0)
            {
                ammoText.color = emptyColor;
            }
            else if (currentAmmo <= lowAmmoThreshold)
            {
                ammoText.color = lowAmmoColor;
            }
            else
            {
                ammoText.color = normalColor;
            }
        }
        
        // Update reserve text
        if (reserveText != null)
        {
            reserveText.text = $"/ {reserveAmmo}";
            reserveText.color = reserveAmmo > 0 ? normalColor : emptyColor;
        }
        
        // Update reload progress bar
        if (showReloadProgress && reloadProgressBar != null)
        {
            if (isReloading)
            {
                reloadProgressBar.gameObject.SetActive(true);
                // Progress bar would need reload progress from WeaponController
                // For now, just show it's active
            }
            else
            {
                reloadProgressBar.gameObject.SetActive(false);
            }
        }
    }
}
