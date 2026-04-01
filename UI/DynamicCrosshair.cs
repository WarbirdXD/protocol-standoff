using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic crosshair that expands based on spread, recoil, and movement
/// Like COD/Battlefield - shows weapon accuracy visually
/// </summary>
public class DynamicCrosshair : MonoBehaviour
{
    [Header("Crosshair Lines")]
    public RectTransform topLine;
    public RectTransform bottomLine;
    public RectTransform leftLine;
    public RectTransform rightLine;
    public RectTransform centerDot;
    
    [Header("Settings")]
    public float baseGap = 10f;              // Base distance from center
    public float maxGap = 50f;               // Maximum spread distance
    public float spreadMultiplier = 100f;    // How much spread affects gap
    public float recoilMultiplier = 50f;     // How much recoil affects gap
    public float smoothSpeed = 10f;          // How fast crosshair returns to normal
    
    [Header("Movement Penalties")]
    public float standingStillBonus = 0f;    // No penalty when standing still
    public float walkingPenalty = 15f;       // Small penalty when walking
    public float sprintingPenalty = 35f;     // Large penalty when sprinting
    public float jumpingPenalty = 50f;       // Huge penalty when jumping
    public float crouchBonus = -10f;         // Bonus (negative penalty) when crouched
    public float proneBonus = -15f;          // Best bonus when prone
    
    [Header("Hit Feedback")]
    public Color normalColor = Color.white;
    public Color hitColor = Color.red;
    public float hitFeedbackDuration = 0.1f;
    
    private float currentGap;
    private float targetGap;
    private float hitFeedbackTimer;
    private Image[] crosshairImages;
    
    private void Start()
    {
        // Get all images for color changes (filter out nulls)
        System.Collections.Generic.List<Image> imageList = new System.Collections.Generic.List<Image>();
        
        if (topLine != null)
        {
            Image img = topLine.GetComponent<Image>();
            if (img != null) imageList.Add(img);
        }
        if (bottomLine != null)
        {
            Image img = bottomLine.GetComponent<Image>();
            if (img != null) imageList.Add(img);
        }
        if (leftLine != null)
        {
            Image img = leftLine.GetComponent<Image>();
            if (img != null) imageList.Add(img);
        }
        if (rightLine != null)
        {
            Image img = rightLine.GetComponent<Image>();
            if (img != null) imageList.Add(img);
        }
        if (centerDot != null)
        {
            Image img = centerDot.GetComponent<Image>();
            if (img != null) imageList.Add(img);
        }
        
        crosshairImages = imageList.ToArray();
        
        currentGap = baseGap;
        targetGap = baseGap;
        
        // Debug: Check if all lines are assigned
        Debug.Log($"Crosshair Setup - Top: {topLine != null}, Bottom: {bottomLine != null}, Left: {leftLine != null}, Right: {rightLine != null}, Center: {centerDot != null}");
        Debug.Log($"Crosshair Images found: {crosshairImages.Length}");
        
        if (crosshairImages.Length == 0)
        {
            Debug.LogError("DynamicCrosshair: No crosshair images found! Please assign crosshair line references in Inspector.");
        }
        
        // Force initial position update
        UpdateCrosshairPositions();
    }
    
    private void Update()
    {
        // Smooth interpolation to target gap
        currentGap = Mathf.Lerp(currentGap, targetGap, Time.deltaTime * smoothSpeed);
        
        // Update crosshair positions
        UpdateCrosshairPositions();
        
        // Handle hit feedback color
        if (hitFeedbackTimer > 0)
        {
            hitFeedbackTimer -= Time.deltaTime;
            if (hitFeedbackTimer <= 0)
            {
                SetCrosshairColor(normalColor);
            }
        }
    }
    
    /// <summary>
    /// Update crosshair based on ACTUAL weapon spread (COD-accurate)
    /// The spread value passed in already includes all modifiers from WeaponController
    /// </summary>
    public void UpdateCrosshair(float actualSpread, float recoil, bool isMoving, bool isSprinting, bool isJumping, bool isCrouching, bool isProne)
    {
        // Spread is already calculated by WeaponController with ALL modifiers
        // Just convert it to visual crosshair gap
        float spreadGap = actualSpread * spreadMultiplier;
        float recoilGap = recoil * recoilMultiplier;
        
        // Optional: Add small visual-only adjustments for extra feedback
        float visualAdjustment = 0f;
        if (isJumping)
        {
            visualAdjustment = 5f; // Extra visual feedback when jumping
        }
        else if (isProne)
        {
            visualAdjustment = -3f; // Tighter visual when prone
        }
        else if (isCrouching)
        {
            visualAdjustment = -2f; // Slightly tighter when crouched
        }
        
        targetGap = baseGap + spreadGap + recoilGap + visualAdjustment;
        targetGap = Mathf.Clamp(targetGap, 5f, maxGap); // Min 5 to prevent negative gap
    }
    
    /// <summary>
    /// Show hit feedback (red flash)
    /// </summary>
    public void ShowHitFeedback()
    {
        SetCrosshairColor(hitColor);
        hitFeedbackTimer = hitFeedbackDuration;
    }
    
    private void UpdateCrosshairPositions()
    {
        if (topLine != null)
        {
            topLine.anchoredPosition = new Vector2(0, currentGap);
            if (!topLine.gameObject.activeSelf) topLine.gameObject.SetActive(true);
        }
        
        if (bottomLine != null)
        {
            bottomLine.anchoredPosition = new Vector2(0, -currentGap);
            if (!bottomLine.gameObject.activeSelf) bottomLine.gameObject.SetActive(true);
        }
        
        if (leftLine != null)
        {
            leftLine.anchoredPosition = new Vector2(-currentGap, 0);
            if (!leftLine.gameObject.activeSelf) leftLine.gameObject.SetActive(true);
        }
        
        if (rightLine != null)
        {
            rightLine.anchoredPosition = new Vector2(currentGap, 0);
            if (!rightLine.gameObject.activeSelf) rightLine.gameObject.SetActive(true);
        }
        
        // Center dot stays at (0,0)
    }
    
    private void SetCrosshairColor(Color color)
    {
        if (crosshairImages == null || crosshairImages.Length == 0)
        {
            return; // No images to color
        }
        
        foreach (var img in crosshairImages)
        {
            if (img != null)
                img.color = color;
        }
    }
}
