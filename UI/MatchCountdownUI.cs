using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Displays match countdown (3... 2... 1... GO!)
/// </summary>
public class MatchCountdownUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject countdownPanel;
    public TextMeshProUGUI countdownText;
    
    [Header("Settings")]
    public Color countdownColor = Color.white;
    public Color goColor = Color.green;
    public float textScalePulse = 1.2f;
    public float pulseSpeed = 5f;
    
    private MatchManager matchManager;
    
    private void Start()
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
        
        // Find MatchManager dynamically (it's a singleton in the scene)
        matchManager = FindFirstObjectByType<MatchManager>();
        
        // Subscribe to countdown events
        if (matchManager != null)
        {
            matchManager.OnCountdown.AddListener(OnCountdownTick);
            matchManager.OnMatchStart.AddListener(OnMatchStarted);
            Debug.Log("MatchCountdownUI: Subscribed to MatchManager events");
        }
        else
        {
            Debug.LogError("MatchCountdownUI: MatchManager not found in scene!");
        }
    }
    
    /// <summary>
    /// Called each countdown tick (3, 2, 1, 0)
    /// </summary>
    private void OnCountdownTick(int number)
    {
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(true);
        }
        
        if (countdownText != null)
        {
            if (number > 0)
            {
                // Show number (3, 2, 1)
                countdownText.text = number.ToString();
                countdownText.color = countdownColor;
            }
            else
            {
                // Show GO!
                countdownText.text = "GO!";
                countdownText.color = goColor;
            }
            
            // Pulse animation
            StopAllCoroutines();
            StartCoroutine(PulseText());
        }
    }
    
    /// <summary>
    /// Called when match actually starts (after countdown)
    /// </summary>
    private void OnMatchStarted()
    {
        // Hide countdown panel after brief delay
        StartCoroutine(HideCountdownDelayed());
    }
    
    /// <summary>
    /// Pulse animation for countdown text
    /// </summary>
    private IEnumerator PulseText()
    {
        if (countdownText == null) yield break;
        
        float elapsed = 0f;
        float duration = 0.3f;
        Vector3 startScale = Vector3.one * textScalePulse;
        Vector3 endScale = Vector3.one;
        
        countdownText.transform.localScale = startScale;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease out back effect
            float easeT = 1f - Mathf.Pow(1f - t, 3f);
            countdownText.transform.localScale = Vector3.Lerp(startScale, endScale, easeT);
            yield return null;
        }
        
        countdownText.transform.localScale = endScale;
    }
    
    /// <summary>
    /// Hide countdown panel after delay
    /// </summary>
    private IEnumerator HideCountdownDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (countdownPanel != null)
        {
            countdownPanel.SetActive(false);
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (matchManager != null)
        {
            matchManager.OnCountdown.RemoveListener(OnCountdownTick);
            matchManager.OnMatchStart.RemoveListener(OnMatchStarted);
        }
    }
}
