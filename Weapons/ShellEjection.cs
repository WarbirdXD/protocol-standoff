using UnityEngine;

/// <summary>
/// Ejects bullet shell casings when firing
/// </summary>
public class ShellEjection : MonoBehaviour
{
    [Header("Shell Prefab")]
    public GameObject shellPrefab;
    public Transform ejectionPoint;
    
    [Header("Ejection Settings")]
    public Vector3 ejectionForce = new Vector3(2f, 3f, 0f);
    public Vector3 ejectionTorque = new Vector3(10f, 5f, 15f);
    public float randomForceVariation = 0.5f;
    
    [Header("Shell Lifetime")]
    public float shellLifetime = 5f;
    public bool fadeOut = true;
    public float fadeStartTime = 4f;
    
    /// <summary>
    /// Eject a shell casing
    /// </summary>
    public void EjectShell()
    {
        if (shellPrefab == null || ejectionPoint == null) return;
        
        // Spawn shell
        GameObject shell = Instantiate(shellPrefab, ejectionPoint.position, ejectionPoint.rotation);
        
        // Get rigidbody
        Rigidbody rb = shell.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Add random variation
            Vector3 randomForce = new Vector3(
                Random.Range(-randomForceVariation, randomForceVariation),
                Random.Range(-randomForceVariation, randomForceVariation),
                Random.Range(-randomForceVariation, randomForceVariation)
            );
            
            // Apply ejection force
            Vector3 force = ejectionPoint.TransformDirection(ejectionForce + randomForce);
            rb.AddForce(force, ForceMode.Impulse);
            
            // Apply spin
            Vector3 torque = new Vector3(
                Random.Range(-ejectionTorque.x, ejectionTorque.x),
                Random.Range(-ejectionTorque.y, ejectionTorque.y),
                Random.Range(-ejectionTorque.z, ejectionTorque.z)
            );
            rb.AddTorque(torque, ForceMode.Impulse);
        }
        
        // Destroy after lifetime
        Destroy(shell, shellLifetime);
    }
}
