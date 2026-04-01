using UnityEngine;

/// <summary>
/// Example integration showing how to use the Firebase-enhanced spawn system.
/// This demonstrates the complete flow from spawn to death tracking.
/// bomboclaat - coding standards applied
/// </summary>
public class SpawnSystemIntegrationExample : MonoBehaviour
{
	// Public inspector variables
	[Header( "References" )]
	public DynamicSpawnSystem spawnSystem;
	
	// Private member variables
	private SpawnQualityAnalyzer qualityAnalyzer;
	private TemporalSafetyTracker safetyTracker;
	
	// Unity lifecycle methods
	private void Start()
	{
		if( spawnSystem == null )
		{
			spawnSystem = FindFirstObjectByType<DynamicSpawnSystem>();
		}
		
		qualityAnalyzer = spawnSystem.GetComponent<SpawnQualityAnalyzer>();
		safetyTracker = spawnSystem.GetComponent<TemporalSafetyTracker>();
		
		Debug.Log( "✅ Spawn system with Firebase integration ready!" );
	}
    
	// Custom functions
	public void OnPlayerSpawn( GameObject player )
	{
		Vector3 spawnPosition = player.transform.position;
		
		if( qualityAnalyzer != null )
		{
			qualityAnalyzer.RegisterSpawn( player, spawnPosition );
		}
		
		if( safetyTracker != null )
		{
			safetyTracker.TrackPlayerSpawn( player );
		}
		
		Debug.Log( $"📍 Player {player.name} spawned at {spawnPosition}" );
	}
    
	public void OnPlayerDamaged( GameObject player, float damage )
	{
		if( qualityAnalyzer != null )
		{
			qualityAnalyzer.RegisterDamage( player );
		}
	}
    
	public void OnPlayerDeath( GameObject player, Vector3 deathPosition )
	{
		if( qualityAnalyzer != null )
		{
			qualityAnalyzer.RegisterDeath( player );
		}
		
		if( spawnSystem != null )
		{
			spawnSystem.RegisterDeath( deathPosition );
		}
		
		Debug.Log( $"💀 Player {player.name} died at {deathPosition}" );
	}
    
	public void OnPlayerKill( GameObject killer, Vector3 killPosition )
	{
		if( spawnSystem != null )
		{
			spawnSystem.RegisterKill( killPosition );
		}
		
		if( safetyTracker != null )
		{
			safetyTracker.RegisterCombatEvent( killPosition, TemporalSafetyTracker.CombatEventType.Gunfire, 0.8f );
		}
	}
    
	public void ShowAnalyticsReport()
	{
		if( qualityAnalyzer != null )
		{
			Debug.Log( qualityAnalyzer.GetAnalyticsReport() );
		}
		
		var firebaseAnalytics = spawnSystem.GetComponent<FirebaseSpawnAnalytics>();
		if( firebaseAnalytics != null )
		{
			Debug.Log( firebaseAnalytics.GetCloudAnalyticsReport() );
		}
		
		var firebaseSafety = spawnSystem.GetComponent<FirebaseTemporalSafety>();
		if( firebaseSafety != null )
		{
			Debug.Log( firebaseSafety.GetCloudSafetyReport() );
		}
	}
}
