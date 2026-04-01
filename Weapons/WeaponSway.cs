using UnityEngine;

/// <summary>
/// Adds realistic weapon sway based on mouse movement and idle breathing
/// </summary>
public class WeaponSway : MonoBehaviour
{
    [Header("Sway Settings")]
    public float swayAmount = 0.02f;
    public float maxSwayAmount = 0.06f;
    public float swaySmooth = 6f;
    
    [Header("Rotation Sway")]
    public float rotationSwayAmount = 4f;
    public float maxRotationSwayAmount = 5f;
    public float rotationSwaySmooth = 12f;
    
    [Header("Idle Sway (Breathing)")]
    public bool enableIdleSway = true;
    public float idleSwayAmount = 0.005f;
    public float idleSwaySpeed = 1f;
    
    [Header("Movement Bob")]
    public bool enableMovementBob = true;
    public float bobAmount = 0.05f;
    public float bobSpeed = 14f;
    
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float idleTimer = 0f;
    private float bobTimer = 0f;
    
    private FPSController fpsController;
    
    private void Start()
    {
        initialPosition = transform.localPosition;
        initialRotation = transform.localRotation;
        
        fpsController = GetComponentInParent<FPSController>();
    }
    
    private void Update()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        
        // Calculate sway
        float swayX = Mathf.Clamp(-mouseX * swayAmount, -maxSwayAmount, maxSwayAmount);
        float swayY = Mathf.Clamp(-mouseY * swayAmount, -maxSwayAmount, maxSwayAmount);
        
        Vector3 targetPosition = new Vector3(swayX, swayY, 0f);
        
        // Add idle sway (breathing)
        if (enableIdleSway)
        {
            idleTimer += Time.deltaTime * idleSwaySpeed;
            float idleX = Mathf.Sin(idleTimer) * idleSwayAmount;
            float idleY = Mathf.Cos(idleTimer * 0.5f) * idleSwayAmount;
            targetPosition += new Vector3(idleX, idleY, 0f);
        }
        
        // Add movement bob
        if (enableMovementBob && fpsController != null && fpsController.IsMoving())
        {
            bobTimer += Time.deltaTime * bobSpeed;
            float bobX = Mathf.Cos(bobTimer) * bobAmount * 0.5f;
            float bobY = Mathf.Sin(bobTimer * 2f) * bobAmount;
            targetPosition += new Vector3(bobX, bobY, 0f);
        }
        
        // Apply position sway
        transform.localPosition = Vector3.Lerp(transform.localPosition, initialPosition + targetPosition, Time.deltaTime * swaySmooth);
        
        // Calculate rotation sway
        float tiltX = Mathf.Clamp(-mouseX * rotationSwayAmount, -maxRotationSwayAmount, maxRotationSwayAmount);
        float tiltY = Mathf.Clamp(-mouseY * rotationSwayAmount, -maxRotationSwayAmount, maxRotationSwayAmount);
        
        Quaternion targetRotation = initialRotation * Quaternion.Euler(tiltY, tiltX, tiltX);
        
        // Apply rotation sway
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * rotationSwaySmooth);
    }
}
