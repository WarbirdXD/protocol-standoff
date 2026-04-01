using UnityEngine;

/// <summary>
/// Handles camera shake effects for weapon fire, explosions, etc.
/// </summary>
public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    public float shakeIntensity = 0.1f;
    public float shakeFrequency = 25f;
    public float shakeDuration = 0.1f;
    
    private float shakeTimer = 0f;
    private Vector3 originalPosition;
    private Transform cameraTransform;
    
    private void Start()
    {
        cameraTransform = transform;
        originalPosition = cameraTransform.localPosition;
    }
    
    private void Update()
    {
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            
            // Perlin noise for smooth shake
            float x = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * 2f * shakeIntensity;
            float y = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * 2f * shakeIntensity;
            
            cameraTransform.localPosition = originalPosition + new Vector3(x, y, 0f);
        }
        else
        {
            // Return to original position
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, originalPosition, Time.deltaTime * 10f);
        }
    }
    
    /// <summary>
    /// Trigger camera shake
    /// </summary>
    public void Shake(float intensity = 1f, float duration = 0.1f)
    {
        shakeTimer = duration;
        shakeIntensity = intensity;
    }
}
