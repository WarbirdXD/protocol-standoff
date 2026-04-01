using UnityEngine;

/// <summary>
/// Simple dummy player for testing - can be killed but doesn't move
/// </summary>
public class DummyPlayer : MonoBehaviour
{
    [Header("Dummy Settings")]
    public bool canRotate = false;          // Allow dummy to rotate
    public float rotationSpeed = 30f;       // Rotation speed if enabled
    public bool lookAtPlayer = false;       // Always face the player
    
    private Transform playerTransform;
    
    private void Start()
    {
        // Find the main player (not this dummy)
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            if (player != this.gameObject)
            {
                playerTransform = player.transform;
                break;
            }
        }
        
        // Disable movement scripts on this dummy
        var fpsController = GetComponent<FPSController>();
        if (fpsController != null) fpsController.enabled = false;
        
        var weaponController = GetComponent<WeaponController>();
        if (weaponController != null) weaponController.enabled = false;
        
        var calibrationMode = GetComponent<CalibrationMode>();
        if (calibrationMode != null) calibrationMode.enabled = false;
    }
    
    private void Update()
    {
        if (lookAtPlayer && playerTransform != null)
        {
            // Make dummy face the player
            Vector3 direction = playerTransform.position - transform.position;
            direction.y = 0; // Keep rotation horizontal only
            
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
}
