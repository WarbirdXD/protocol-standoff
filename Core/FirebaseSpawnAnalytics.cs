using UnityEngine;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Firebase integration for spawn analytics - enables cloud-based learning.
/// Collects data from all players to improve spawn quality over time.
/// RESEARCH ENHANCEMENT: Demonstrates machine learning through collective data.
/// bomboclaat - coding standards applied
/// </summary>
public class FirebaseSpawnAnalytics : MonoBehaviour
{
	// Public inspector variables
	[Header( "Firebase Settings" )]
	[Tooltip( "Enable cloud data collection" )]
	public bool enableCloudSync = true;
	
	[Tooltip( "Upload interval (seconds) - don't spam Firebase" )]
	public float uploadInterval = 30f;
	
	[Tooltip( "Download interval (seconds) - get latest cloud data" )]
	public float downloadInterval = 60f;
	
	[Header( "Map Identification" )]
	[Tooltip( "Current map/scene name" )]
	public string mapId = "default_map";
	
	// Private member variables
	private DatabaseReference databaseRef;
	private bool firebaseInitialized = false;
	private List<SpawnDataPoint> pendingUploads = new List<SpawnDataPoint>();
	private Dictionary<string, CloudSpawnLocationData> cloudData = new Dictionary<string, CloudSpawnLocationData>();
	private float lastUploadTime;
	private float lastDownloadTime;
    
	/// <summary>
	/// Single spawn data point to upload
	/// </summary>
	[Serializable]
	public class SpawnDataPoint
	{
		public string mapId;
		public float posX, posY, posZ;
		public float timeToFirstDamage;
		public float survivalTime;
		public bool wasSpawnKill;
		public long timestamp;
		public string quality;
		
		public SpawnDataPoint( string map, Vector3 pos, float ttfd, float survival, bool spawnKill, string qual )
		{
			mapId = map;
			posX = pos.x;
			posY = pos.y;
			posZ = pos.z;
			timeToFirstDamage = ttfd;
			survivalTime = survival;
			wasSpawnKill = spawnKill;
			timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			quality = qual;
		}
	}
    
	/// <summary>
	/// Aggregated cloud data for a spawn location
	/// </summary>
	[Serializable]
	public class CloudSpawnLocationData
	{
		public string locationKey;
		public int totalSpawns;
		public int spawnKills;
		public float avgTimeToFirstDamage;
		public float avgSurvivalTime;
		public float qualityScore;
		public long lastUpdated;
		
		public CloudSpawnLocationData()
		{
			totalSpawns = 0;
			spawnKills = 0;
			avgTimeToFirstDamage = 0f;
			avgSurvivalTime = 0f;
			qualityScore = 50f;
			lastUpdated = 0;
		}
	}
    
	// Unity lifecycle methods
	private void Start()
	{
		InitializeFirebase();
	}
	
	private void Update()
	{
		if( !firebaseInitialized || !enableCloudSync )
			return;
		
		if( Time.time - lastUploadTime > uploadInterval && pendingUploads.Count > 0 )
		{
			UploadPendingData();
			lastUploadTime = Time.time;
		}
		
		if( Time.time - lastDownloadTime > downloadInterval )
		{
			DownloadCloudData();
			lastDownloadTime = Time.time;
		}
	}
	
	private void OnApplicationQuit()
	{
		if( pendingUploads.Count > 0 )
		{
			UploadPendingData();
		}
	}
    
	// Custom functions
	private void InitializeFirebase()
	{
		FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread( task =>
		{
			if( task.Result == DependencyStatus.Available )
			{
				databaseRef = FirebaseDatabase.DefaultInstance.RootReference;
				firebaseInitialized = true;
				
				Debug.Log( "✅ Firebase Spawn Analytics initialized" );
				
				DownloadCloudData();
			}
			else
			{
				Debug.LogError( $"❌ Firebase initialization failed: {task.Result}" );
			}
		} );
	}
    
	public void RecordSpawnEvent( Vector3 position, float timeToFirstDamage, float survivalTime, bool wasSpawnKill, string quality )
	{
		if( !enableCloudSync )
			return;
		
		var dataPoint = new SpawnDataPoint( mapId, position, timeToFirstDamage, survivalTime, wasSpawnKill, quality );
		
		pendingUploads.Add( dataPoint );
		
		if( pendingUploads.Count >= 10 )
		{
			UploadPendingData();
		}
	}
    
	private async void UploadPendingData()
	{
		if( !firebaseInitialized || pendingUploads.Count == 0 )
			return;
		
		Debug.Log( $"📤 Uploading {pendingUploads.Count} spawn data points to Firebase..." );
		
		try
		{
			var groupedData = new Dictionary<string, List<SpawnDataPoint>>();
			
			foreach( var point in pendingUploads )
			{
				string locationKey = GetLocationKey( new Vector3( point.posX, point.posY, point.posZ ) );
				
				if( !groupedData.ContainsKey( locationKey ) )
				{
					groupedData[locationKey] = new List<SpawnDataPoint>();
				}
				
				groupedData[locationKey].Add( point );
			}
			
			foreach( var kvp in groupedData )
			{
				await UploadLocationData( kvp.Key, kvp.Value );
			}
			
			pendingUploads.Clear();
			Debug.Log( "✅ Upload complete" );
		}
		catch( Exception e )
		{
			Debug.LogError( $"❌ Upload failed: {e.Message}" );
		}
	}
    
	private async Task UploadLocationData( string locationKey, List<SpawnDataPoint> dataPoints )
	{
		string path = $"spawn_analytics/{mapId}/locations/{locationKey}";
		var snapshot = await databaseRef.Child( path ).GetValueAsync();
		CloudSpawnLocationData locationData;
		
		if( snapshot.Exists )
		{
			string json = snapshot.GetRawJsonValue();
			locationData = JsonUtility.FromJson<CloudSpawnLocationData>( json );
		}
		else
		{
			locationData = new CloudSpawnLocationData { locationKey = locationKey };
		}
		
		foreach( var point in dataPoints )
		{
			float totalWeight = locationData.totalSpawns;
			
			locationData.avgTimeToFirstDamage = ( locationData.avgTimeToFirstDamage * totalWeight + point.timeToFirstDamage ) / ( totalWeight + 1 );
			locationData.avgSurvivalTime = ( locationData.avgSurvivalTime * totalWeight + point.survivalTime ) / ( totalWeight + 1 );
			locationData.totalSpawns++;
			
			if( point.wasSpawnKill )
			{
				locationData.spawnKills++;
			}
		}
		
		float spawnKillRate = locationData.totalSpawns > 0 ? (float)locationData.spawnKills / locationData.totalSpawns : 0f;
		float survivalScore = Mathf.Clamp01( locationData.avgSurvivalTime / 30f ) * 50f;
		float engagementScore = Mathf.Clamp01( locationData.avgTimeToFirstDamage / 8f ) * 50f;
		
		locationData.qualityScore = survivalScore + engagementScore - ( spawnKillRate * 100f );
		locationData.qualityScore = Mathf.Clamp( locationData.qualityScore, 0f, 100f );
		locationData.lastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		
		string updatedJson = JsonUtility.ToJson( locationData );
		await databaseRef.Child( path ).SetRawJsonValueAsync( updatedJson );
		
		cloudData[locationKey] = locationData;
	}
    
	private async void DownloadCloudData()
	{
		if( !firebaseInitialized )
			return;
		
		Debug.Log( "📥 Downloading spawn analytics from Firebase..." );
		
		try
		{
			string path = $"spawn_analytics/{mapId}/locations";
			var snapshot = await databaseRef.Child( path ).GetValueAsync();
			
			if( snapshot.Exists )
			{
				cloudData.Clear();
				
				foreach( var child in snapshot.Children )
				{
					string json = child.GetRawJsonValue();
					var locationData = JsonUtility.FromJson<CloudSpawnLocationData>( json );
					cloudData[locationData.locationKey] = locationData;
				}
				
				Debug.Log( $"✅ Downloaded data for {cloudData.Count} spawn locations" );
			}
			else
			{
				Debug.Log( "ℹ️ No cloud data available yet" );
			}
		}
		catch( Exception e )
		{
			Debug.LogError( $"❌ Download failed: {e.Message}" );
		}
	}
    
	public float GetCloudQualityScore( Vector3 position )
	{
		string locationKey = GetLocationKey( position );
		
		if( cloudData.ContainsKey( locationKey ) )
		{
			return cloudData[locationKey].qualityScore;
		}
		
		return 50f;
	}
    
	public bool IsCloudSpawnKillZone( Vector3 position )
	{
		string locationKey = GetLocationKey( position );
		
		if( cloudData.ContainsKey( locationKey ) )
		{
			var data = cloudData[locationKey];
			
			if( data.totalSpawns >= 5 )
			{
				float spawnKillRate = (float)data.spawnKills / data.totalSpawns;
				return spawnKillRate > 0.3f;
			}
		}
		
		return false;
	}
    
	public CloudSpawnLocationData GetCloudStats( Vector3 position )
	{
		string locationKey = GetLocationKey( position );
		
		if( cloudData.ContainsKey( locationKey ) )
		{
			return cloudData[locationKey];
		}
		
		return null;
	}
    
	public int GetTotalCloudSpawns()
	{
		return cloudData.Values.Sum( d => d.totalSpawns );
	}
    
	private string GetLocationKey( Vector3 position )
	{
		int gridX = Mathf.FloorToInt( position.x / 5f );
		int gridY = Mathf.FloorToInt( position.y / 5f );
		int gridZ = Mathf.FloorToInt( position.z / 5f );
		
		return $"{gridX}_{gridY}_{gridZ}";
	}
    
	public string GetCloudAnalyticsReport()
	{
		if( cloudData.Count == 0 )
		{
			return "No cloud data available yet.";
		}
		
		int totalSpawns = cloudData.Values.Sum( d => d.totalSpawns );
		int totalSpawnKills = cloudData.Values.Sum( d => d.spawnKills );
		float avgQuality = cloudData.Values.Average( d => d.qualityScore );
		
		var spawnKillZones = cloudData.Values.Where( d => d.totalSpawns >= 5 && (float)d.spawnKills / d.totalSpawns > 0.3f ).ToList();
		
		string report = "=== CLOUD SPAWN ANALYTICS ===\n\n";
		report += $"Total Spawns (All Players): {totalSpawns}\n";
		report += $"Total Spawn Kills: {totalSpawnKills} ({(float)totalSpawnKills / totalSpawns:P1})\n";
		report += $"Average Quality Score: {avgQuality:F1}/100\n";
		report += $"Tracked Locations: {cloudData.Count}\n";
		report += $"Spawn Kill Zones: {spawnKillZones.Count}\n\n";
		
		if( spawnKillZones.Count > 0 )
		{
			report += "⚠️ PROBLEMATIC SPAWN ZONES:\n";
			foreach( var zone in spawnKillZones.OrderByDescending( z => (float)z.spawnKills / z.totalSpawns ).Take( 5 ) )
			{
				float rate = (float)zone.spawnKills / zone.totalSpawns;
				report += $"  {zone.locationKey}: {rate:P0} spawn kill rate ({zone.spawnKills}/{zone.totalSpawns})\n";
			}
		}
		
		return report;
	}
}
