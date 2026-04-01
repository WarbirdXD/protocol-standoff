using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays hit marker when bullets hit enemies
/// </summary>
public class HitMarker : MonoBehaviour
{
    [Header("Hit Marker Elements")]
    public Image topLine;
    public Image bottomLine;
    public Image leftLine;
    public Image rightLine;
    
    [Header("Settings")]
    public Color normalHitColor = Color.white;
    public Color headshotColor = Color.red;
    public float displayDuration = 0.15f;
    public float fadeSpeed = 10f;
    
    [Header("Animation")]
    public float expandAmount = 5f;
    public float expandSpeed = 20f;
    
    private float hitTimer = 0f;
    private bool isHeadshot = false;
    private float baseDistance = 20f; // Base distance from center
    private float currentExpand = 0f;
    
    private void Start()
    {
        // Store base distance from first line
        if (topLine != null)
        {
            baseDistance = Mathf.Abs(topLine.rectTransform.anchoredPosition.y);
        }
        
        // Start invisible
        SetAlpha(0f);
    }
    
    private void Update()
    {
        if (hitTimer > 0f)
        {
            hitTimer -= Time.deltaTime;
            
            // Expand animation
            currentExpand = Mathf.Lerp(currentExpand, expandAmount, Time.deltaTime * expandSpeed);
            UpdatePositions();
            
            // Fade out
            float alpha = Mathf.Clamp01(hitTimer / displayDuration);
            SetAlpha(alpha);
        }
        else
        {
            // Reset
            currentExpand = Mathf.Lerp(currentExpand, 0f, Time.deltaTime * fadeSpeed);
            UpdatePositions();
            SetAlpha(0f);
        }
    }
    
    /// <summary>
    /// Show hit marker
    /// </summary>
    public void ShowHit(bool headshot = false)
    {
        hitTimer = displayDuration;
        isHeadshot = headshot;
        currentExpand = 0f;
        
        // Set color
        Color hitColor = headshot ? headshotColor : normalHitColor;
        if (topLine != null) topLine.color = hitColor;
        if (bottomLine != null) bottomLine.color = hitColor;
        if (leftLine != null) leftLine.color = hitColor;
        if (rightLine != null) rightLine.color = hitColor;
        
        // Play hit sound (optional)
        // AudioManager.PlayHitSound(headshot);
    }
    
    private void UpdatePositions()
    {
        float distance = baseDistance;
        
        if (topLine != null)
            topLine.rectTransform.anchoredPosition = new Vector2(20, distance);
        if (bottomLine != null)
            bottomLine.rectTransform.anchoredPosition = new Vector2(-20, -distance);
        if (leftLine != null)
            leftLine.rectTransform.anchoredPosition = new Vector2(-distance, 20);
        if (rightLine != null)
            rightLine.rectTransform.anchoredPosition = new Vector2(distance, -20);
    }
    
    private void SetAlpha(float alpha)
    {
        if (topLine != null)
        {
            Color c = topLine.color;
            c.a = alpha;
            topLine.color = c;
        }
        if (bottomLine != null)
        {
            Color c = bottomLine.color;
            c.a = alpha;
            bottomLine.color = c;
        }
        if (leftLine != null)
        {
            Color c = leftLine.color;
            c.a = alpha;
            leftLine.color = c;
        }
        if (rightLine != null)
        {
            Color c = rightLine.color;
            c.a = alpha;
            rightLine.color = c;
        }
    }
}
