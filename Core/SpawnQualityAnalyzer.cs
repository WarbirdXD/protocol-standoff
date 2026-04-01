using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// RESEARCH TOOL 1: Spawn Quality Analyzer
/// Tracks spawn outcomes and identifies problematic spawn locations.
/// Based on research: Valve (2018) - "Time to First Engagement" metrics
/// bomboclaat - coding standards applied
/// </summary>
public class SpawnQualityAnalyzer : MonoBehaviour
{
	// Public inspector variables
	[Header( "Analysis Settings" )]
	[Tooltip( "Track spawn quality data" )]
	public bool enableTracking = true;
	
	[Tooltip( "Spawn kill threshold (seconds)" )]
	public float spawnKillThreshold = 3f;
	
	[Tooltip( "Good spawn threshold (seconds)" )]
	public float goodSpawnThreshold = 8f;
	
	[Header( "Visualization" )]
	public bool showHeatmap = true;
	public float heatmapCellSize = 5f;
	public Color goodSpawnColor = Color.green;
	public Color badSpawnColor = Color.red;
	
	// Private member variables
	private List<SpawnRecord> spawnHistory = new List<SpawnRecord>();
	private Dictionary<Vector3Int, SpawnLocationStats> locationStats = new Dictionary<Vector3Int, SpawnLocationStats>();
	private Dictionary<GameObject, SpawnInstance> activeSpawns = new Dictionary<GameObject, SpawnInstance>();
	private FirebaseSpawnAnalytics firebaseAnalytics;
    
	private class SpawnRecord
	{
		public Vector3 position;
		public float spawnTime;
		public float timeToFirstDamage;
		public float survivalTime;
		public bool wasSpawnKill;
		public SpawnQuality quality;
	}
	
	private class SpawnLocationStats
	{
		public Vector3Int gridCell;
		public int totalSpawns;
		public int spawnKills;
		public float averageTimeToFirstDamage;
		public float averageSurvivalTime;
		public float qualityScore;
		
		public void UpdateQuality()
		{
			float spawnKillRate = totalSpawns > 0 ? (float)spawnKills / totalSpawns : 0f;
			float survivalScore = Mathf.Clamp01( averageSurvivalTime / 30f ) * 50f;
			float engagementScore = Mathf.Clamp01( averageTimeToFirstDamage / 8f ) * 50f;
			
			qualityScore = survivalScore + engagementScore - ( spawnKillRate * 100f );
			qualityScore = Mathf.Clamp( qualityScore, 0f, 100f );
		}
	}
	
	private class SpawnInstance
	{
		public Vector3 position;
		public float spawnTime;
		public bool tookDamage;
		public float firstDamageTime;
	}
	
	public enum SpawnQuality
	{
		Excellent,
		Good,
		Fair,
		Poor,
		Terrible
	}
	
	// Unity lifecycle methods
	private void Start()
	{
		firebaseAnalytics = GetComponent<FirebaseSpawnAnalytics>();
	}
    
	// Custom functions
	public void RegisterSpawn( GameObject player, Vector3 position )
	{
		if( !enableTracking )
			return;
		
		activeSpawns[player] = new SpawnInstance
		{
			position = position,
			spawnTime = Time.time,
			tookDamage = false
		};
		
		Debug.Log( $"📊 Tracking spawn for {player.name} at {position}" );
	}
    
	public void RegisterDamage( GameObject player )
	{
		if( !enableTracking || !activeSpawns.ContainsKey( player ) )
			return;
		
		var spawn = activeSpawns[player];
		
		if( !spawn.tookDamage )
		{
			spawn.tookDamage = true;
			spawn.firstDamageTime = Time.time;
			
			float timeToFirstDamage = spawn.firstDamageTime - spawn.spawnTime;
			
			if( timeToFirstDamage < spawnKillThreshold )
			{
				Debug.LogWarning( $"⚠️ SPAWN KILL detected! {player.name} took damage {timeToFirstDamage:F1}s after spawn" );
			}
		}
	}
    
	public void RegisterDeath( GameObject player )
	{
		if( !enableTracking || !activeSpawns.ContainsKey( player ) )
			return;
		
		var spawn = activeSpawns[player];
		float survivalTime = Time.time - spawn.spawnTime;
		float timeToFirstDamage = spawn.tookDamage ? spawn.firstDamageTime - spawn.spawnTime : survivalTime;
		
		SpawnQuality quality = EvaluateSpawnQuality( timeToFirstDamage, survivalTime );
		
		var record = new SpawnRecord
		{
			position = spawn.position,
			spawnTime = spawn.spawnTime,
			timeToFirstDamage = timeToFirstDamage,
			survivalTime = survivalTime,
			wasSpawnKill = timeToFirstDamage < spawnKillThreshold,
			quality = quality
		};
		
		spawnHistory.Add( record );
		
		UpdateLocationStats( record );
		
		if( firebaseAnalytics != null )
		{
			firebaseAnalytics.RecordSpawnEvent( spawn.position, timeToFirstDamage, survivalTime, record.wasSpawnKill, quality.ToString() );
		}
		
		activeSpawns.Remove( player );
		
		string qualityColor = quality == SpawnQuality.Excellent ? "green" : 
							 quality == SpawnQuality.Good ? "cyan" :
							 quality == SpawnQuality.Fair ? "yellow" :
							 quality == SpawnQuality.Poor ? "orange" : "red";
		
		Debug.Log( $"<color={qualityColor}>Spawn Quality: {quality} | Time to damage: {timeToFirstDamage:F1}s | Survival: {survivalTime:F1}s</color>" );
	}
    
	private SpawnQuality EvaluateSpawnQuality( float timeToFirstDamage, float survivalTime )
	{
		if( timeToFirstDamage < 1f )
			return SpawnQuality.Terrible;
		
		if( timeToFirstDamage < spawnKillThreshold )
			return SpawnQuality.Poor;
		
		if( timeToFirstDamage < 5f )
			return SpawnQuality.Fair;
		
		if( timeToFirstDamage < goodSpawnThreshold && survivalTime > 10f )
			return SpawnQuality.Good;
		
		if( timeToFirstDamage >= goodSpawnThreshold && survivalTime > 20f )
			return SpawnQuality.Excellent;
		
		return SpawnQuality.Fair;
	}
    
	private void UpdateLocationStats( SpawnRecord record )
	{
		Vector3Int gridCell = GetGridCell( record.position );
		
		if( !locationStats.ContainsKey( gridCell ) )
		{
			locationStats[gridCell] = new SpawnLocationStats { gridCell = gridCell };
		}
		
		var stats = locationStats[gridCell];
		
		float totalWeight = stats.totalSpawns;
		stats.averageTimeToFirstDamage = ( stats.averageTimeToFirstDamage * totalWeight + record.timeToFirstDamage ) / ( totalWeight + 1 );
		stats.averageSurvivalTime = ( stats.averageSurvivalTime * totalWeight + record.survivalTime ) / ( totalWeight + 1 );
		
		stats.totalSpawns++;
		if( record.wasSpawnKill )
		{
			stats.spawnKills++;
		}
		
		stats.UpdateQuality();
	}
    
	private Vector3Int GetGridCell( Vector3 position )
	{
		return new Vector3Int(
			Mathf.FloorToInt( position.x / heatmapCellSize ),
			Mathf.FloorToInt( position.y / heatmapCellSize ),
			Mathf.FloorToInt( position.z / heatmapCellSize )
		);
	}
	
	public float GetLocationQualityScore( Vector3 position )
	{
		Vector3Int gridCell = GetGridCell( position );
		
		if( locationStats.ContainsKey( gridCell ) )
		{
			return locationStats[gridCell].qualityScore;
		}
		
		return 50f;
	}
	
	public bool IsSpawnKillZone( Vector3 position )
	{
		Vector3Int gridCell = GetGridCell( position );
		
		if( locationStats.ContainsKey( gridCell ) )
		{
			var stats = locationStats[gridCell];
			float spawnKillRate = stats.totalSpawns > 0 ? (float)stats.spawnKills / stats.totalSpawns : 0f;
			return spawnKillRate > 0.3f;
		}
		
		return false;
	}
    
	public string GetAnalyticsReport()
	{
		if( spawnHistory.Count == 0 )
		{
			return "No spawn data collected yet.";
		}
		
		int totalSpawns = spawnHistory.Count;
		int spawnKills = spawnHistory.Count( s => s.wasSpawnKill );
		float spawnKillRate = (float)spawnKills / totalSpawns;
		
		float avgTimeToFirstDamage = spawnHistory.Average( s => s.timeToFirstDamage );
		float avgSurvivalTime = spawnHistory.Average( s => s.survivalTime );
		
		var qualityCounts = spawnHistory.GroupBy( s => s.quality ).ToDictionary( g => g.Key, g => g.Count() );
		
		string report = "=== SPAWN QUALITY ANALYTICS ===\n\n";
		report += $"Total Spawns: {totalSpawns}\n";
		report += $"Spawn Kills: {spawnKills} ({spawnKillRate:P1})\n";
		report += $"Avg Time to First Damage: {avgTimeToFirstDamage:F1}s\n";
		report += $"Avg Survival Time: {avgSurvivalTime:F1}s\n\n";
		
		report += "Quality Distribution:\n";
		foreach( var quality in System.Enum.GetValues( typeof( SpawnQuality ) ) )
		{
			int count = qualityCounts.ContainsKey( (SpawnQuality)quality ) ? qualityCounts[(SpawnQuality)quality] : 0;
			float percentage = (float)count / totalSpawns;
			report += $"  {quality}: {count} ({percentage:P1})\n";
		}
		
		report += $"\nTracked Locations: {locationStats.Count}\n";
		report += $"Spawn Kill Zones: {locationStats.Count( kvp => kvp.Value.spawnKills > 0 )}\n";
		
		return report;
	}
    
	private void OnDrawGizmos()
	{
		if( !showHeatmap || locationStats.Count == 0 )
			return;
		
		foreach( var kvp in locationStats )
		{
			var stats = kvp.Value;
			Vector3 cellCenter = new Vector3(
				kvp.Key.x * heatmapCellSize + heatmapCellSize * 0.5f,
				kvp.Key.y * heatmapCellSize + heatmapCellSize * 0.5f,
				kvp.Key.z * heatmapCellSize + heatmapCellSize * 0.5f
			);
			
			float normalizedQuality = stats.qualityScore / 100f;
			Color cellColor = Color.Lerp( badSpawnColor, goodSpawnColor, normalizedQuality );
			cellColor.a = 0.3f;
			
			Gizmos.color = cellColor;
			Gizmos.DrawCube( cellCenter, Vector3.one * heatmapCellSize * 0.9f );
			
			if( stats.spawnKills > 0 )
			{
				Gizmos.color = Color.red;
				Gizmos.DrawWireCube( cellCenter, Vector3.one * heatmapCellSize );
			}
		}
	}
}
