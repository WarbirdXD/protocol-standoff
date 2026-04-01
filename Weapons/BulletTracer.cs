using UnityEngine;

/// <summary>
/// Visual bullet tracer that shows the bullet path
/// Like CS:GO tracers - instant hit but visible trail
/// </summary>
public class BulletTracer : MonoBehaviour
{
    [Header("Tracer Settings")]
    public float tracerSpeed = 300f;      // How fast the tracer moves visually
    public float tracerWidth = 0.02f;     // Width of the tracer line
    public Color tracerColor = Color.yellow;
    public float tracerLifetime = 0.1f;   // How long tracer lasts
    
    private LineRenderer lineRenderer;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private float spawnTime;
    private float distance;
    private float currentDistance;
    
    private void Awake()
    {
        // Create LineRenderer for the tracer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = tracerWidth;
        lineRenderer.endWidth = tracerWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = tracerColor;
        lineRenderer.endColor = tracerColor;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
    }
    
    /// <summary>
    /// Initialize the tracer with start and end points
    /// </summary>
    public void Initialize(Vector3 start, Vector3 end)
    {
        startPosition = start;
        endPosition = end;
        spawnTime = Time.time;
        distance = Vector3.Distance(start, end);
        currentDistance = 0f;
        
        // Set initial positions
        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, startPosition);
    }
    
    private void Update()
    {
        // Move tracer forward
        currentDistance += tracerSpeed * Time.deltaTime;
        
        if (currentDistance >= distance)
        {
            // Tracer reached end
            lineRenderer.SetPosition(0, endPosition);
            lineRenderer.SetPosition(1, endPosition);
            
            // Destroy after lifetime
            if (Time.time - spawnTime >= tracerLifetime)
            {
                Destroy(gameObject);
            }
        }
        else
        {
            // Animate tracer moving from start to end
            float t = currentDistance / distance;
            Vector3 currentPos = Vector3.Lerp(startPosition, endPosition, t);
            
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, currentPos);
        }
    }
}
