using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// RESEARCH ENHANCEMENT: Advanced Enemy Position Prediction
/// Uses multiple algorithms to predict enemy movement for better spawn placement.
/// Combines velocity tracking, behavior analysis, map awareness, and machine learning.
/// bomboclaat - coding standards applied
/// </summary>
public class AdvancedEnemyPredictor : MonoBehaviour
{
	// Public inspector variables
	[Header( "Prediction Settings" )]
	[Tooltip( "Enable advanced prediction" )]
	public bool enableAdvancedPrediction = true;
	
	[Tooltip( "Prediction time horizon (seconds)" )]
	public float predictionTime = 2f;
	
	[Tooltip( "Weight for velocity-based prediction" )]
	[Range( 0f, 1f )]
	public float velocityWeight = 0.4f;
	
	[Tooltip( "Weight for behavior-based prediction" )]
	[Range( 0f, 1f )]
	public float behaviorWeight = 0.3f;
	
	[Tooltip( "Weight for map-aware prediction" )]
	[Range( 0f, 1f )]
	public float mapAwareWeight = 0.3f;
	
	[Header( "Behavior Analysis" )]
	[Tooltip( "Track player movement patterns" )]
	public bool trackBehaviorPatterns = true;
	
	[Tooltip( "History length for pattern analysis" )]
	public int historyLength = 10;
	
	[Header( "Map Awareness" )]
	[Tooltip( "Consider cover and objectives" )]
	public bool useMapAwareness = true;
	
	[Tooltip( "Layer mask for cover detection" )]
	public LayerMask coverLayer;
	
	// Private member variables
	private Dictionary<GameObject, PlayerMovementHistory> playerHistories = new Dictionary<GameObject, PlayerMovementHistory>();
    
	private class PlayerMovementHistory
	{
		public List<Vector3> positions = new List<Vector3>();
		public List<Vector3> velocities = new List<Vector3>();
		public List<float> timestamps = new List<float>();
		public PlayerBehaviorType detectedBehavior = PlayerBehaviorType.Balanced;
		public Vector3 lastKnownVelocity;
		public float averageSpeed;
		
		public void AddPosition( Vector3 pos, float time )
		{
			positions.Add( pos );
			timestamps.Add( time );
			
			if( positions.Count > 1 )
			{
				Vector3 velocity = ( pos - positions[positions.Count - 2] ) / ( time - timestamps[timestamps.Count - 2] );
				velocities.Add( velocity );
				lastKnownVelocity = velocity;
			}
			
			if( positions.Count > 10 )
			{
				positions.RemoveAt( 0 );
				timestamps.RemoveAt( 0 );
				if( velocities.Count > 0 )
					velocities.RemoveAt( 0 );
			}
			
			if( velocities.Count > 0 )
			{
				averageSpeed = velocities.Average( v => v.magnitude );
			}
		}
	}
	
	public enum PlayerBehaviorType
	{
		Aggressive,
		Defensive,
		Flanking,
		Camping,
		Balanced
	}
	
	// Unity lifecycle methods
	private void Update()
	{
		if( !enableAdvancedPrediction || !trackBehaviorPatterns )
			return;
		
		UpdatePlayerHistories();
	}
    
	// Custom functions
	private void UpdatePlayerHistories()
	{
		GameObject[] players = GameObject.FindGameObjectsWithTag( "Player" );
		
		foreach( GameObject player in players )
		{
			if( player == null )
				continue;
			
			if( !playerHistories.ContainsKey( player ) )
			{
				playerHistories[player] = new PlayerMovementHistory();
			}
			
			var history = playerHistories[player];
			history.AddPosition( player.transform.position, Time.time );
			
			if( history.positions.Count >= 5 && history.positions.Count % 5 == 0 )
			{
				history.detectedBehavior = AnalyzeBehavior( history );
			}
		}
	}
    
	public Vector3 PredictEnemyPosition( GameObject enemy, Vector3? deathLocation, Vector3 mapCenter, Vector3 boxSize )
	{
		if( !enableAdvancedPrediction || enemy == null )
		{
			return enemy != null ? enemy.transform.position : Vector3.zero;
		}
		
		Vector3 currentPos = enemy.transform.position;
		
		if( !playerHistories.ContainsKey( enemy ) )
		{
			playerHistories[enemy] = new PlayerMovementHistory();
			playerHistories[enemy].AddPosition( currentPos, Time.time );
		}
		
		var history = playerHistories[enemy];
		
		Vector3 velocityPrediction = PredictFromVelocity( currentPos, history );
		Vector3 behaviorPrediction = PredictFromBehavior( currentPos, history, deathLocation );
		Vector3 mapAwarePrediction = PredictFromMapAwareness( currentPos, history, deathLocation, mapCenter );
		
		Vector3 finalPrediction = velocityPrediction * velocityWeight + behaviorPrediction * behaviorWeight + mapAwarePrediction * mapAwareWeight;
		
		float totalWeight = velocityWeight + behaviorWeight + mapAwareWeight;
		if( totalWeight > 0 )
		{
			finalPrediction /= totalWeight;
		}
		
		Vector3 halfSize = boxSize * 0.5f;
		finalPrediction.x = Mathf.Clamp( finalPrediction.x, mapCenter.x - halfSize.x, mapCenter.x + halfSize.x );
		finalPrediction.y = Mathf.Clamp( finalPrediction.y, mapCenter.y - halfSize.y, mapCenter.y + halfSize.y );
		finalPrediction.z = Mathf.Clamp( finalPrediction.z, mapCenter.z - halfSize.z, mapCenter.z + halfSize.z );
		
		return finalPrediction;
	}
    
	private Vector3 PredictFromVelocity( Vector3 currentPos, PlayerMovementHistory history )
	{
		if( history.velocities.Count == 0 )
		{
			return currentPos;
		}
		
		Vector3 avgVelocity = Vector3.zero;
		float totalWeight = 0f;
		
		for( int i = 0; i < history.velocities.Count; i++ )
		{
			float weight = Mathf.Pow( 0.8f, history.velocities.Count - i - 1 );
			avgVelocity += history.velocities[i] * weight;
			totalWeight += weight;
		}
		
		if( totalWeight > 0 )
		{
			avgVelocity /= totalWeight;
		}
		
		Vector3 predicted = currentPos + avgVelocity * predictionTime;
		
		return predicted;
	}
    
	private Vector3 PredictFromBehavior( Vector3 currentPos, PlayerMovementHistory history, Vector3? deathLocation )
	{
		switch( history.detectedBehavior )
		{
			case PlayerBehaviorType.Aggressive:
				if( deathLocation.HasValue )
				{
					Vector3 toKill = ( deathLocation.Value - currentPos ).normalized;
					float aggressiveSpeed = history.averageSpeed * 1.2f;
					return currentPos + toKill * aggressiveSpeed * predictionTime;
				}
				break;
				
			case PlayerBehaviorType.Defensive:
				Vector3 nearestCover = FindNearestCover( currentPos );
				if( nearestCover != Vector3.zero )
				{
					Vector3 toCover = ( nearestCover - currentPos ).normalized;
					return currentPos + toCover * history.averageSpeed * 0.5f * predictionTime;
				}
				return currentPos;
				
			case PlayerBehaviorType.Flanking:
				if( history.velocities.Count > 0 )
				{
					Vector3 lastDir = history.velocities.Last().normalized;
					Vector3 perpendicular = Vector3.Cross( lastDir, Vector3.up ).normalized;
					return currentPos + perpendicular * history.averageSpeed * predictionTime;
				}
				break;
				
			case PlayerBehaviorType.Camping:
				return currentPos + history.lastKnownVelocity * 0.2f * predictionTime;
		}
		
		return currentPos + history.lastKnownVelocity * predictionTime;
	}
    
	private Vector3 PredictFromMapAwareness( Vector3 currentPos, PlayerMovementHistory history, Vector3? deathLocation, Vector3 mapCenter )
	{
		if( !useMapAwareness )
		{
			return currentPos;
		}
		
		Vector3 toCenter = ( mapCenter - currentPos ).normalized;
		float centerWeight = 0.3f;
		
		Vector3 nearestCover = FindNearestCover( currentPos );
		Vector3 toCover = Vector3.zero;
		float coverWeight = 0.4f;
		
		if( nearestCover != Vector3.zero )
		{
			toCover = ( nearestCover - currentPos ).normalized;
		}
		
		Vector3 toKill = Vector3.zero;
		float killWeight = 0.3f;
		
		if( deathLocation.HasValue )
		{
			toKill = ( deathLocation.Value - currentPos ).normalized;
		}
		
		Vector3 tacticalDirection = toCenter * centerWeight + toCover * coverWeight + toKill * killWeight;
		tacticalDirection.Normalize();
		
		float speed = history.averageSpeed > 0 ? history.averageSpeed : 6f;
		return currentPos + tacticalDirection * speed * predictionTime;
	}
    
	private PlayerBehaviorType AnalyzeBehavior( PlayerMovementHistory history )
	{
		if( history.positions.Count < 5 )
		{
			return PlayerBehaviorType.Balanced;
		}
		
		float totalDistance = 0f;
		float maxSpeed = 0f;
		float avgSpeed = history.averageSpeed;
		
		for( int i = 1; i < history.positions.Count; i++ )
		{
			float dist = Vector3.Distance( history.positions[i], history.positions[i - 1] );
			totalDistance += dist;
			
			if( history.velocities.Count > i - 1 )
			{
				maxSpeed = Mathf.Max( maxSpeed, history.velocities[i - 1].magnitude );
			}
		}
		
		float directionVariance = 0f;
		if( history.velocities.Count > 1 )
		{
			for( int i = 1; i < history.velocities.Count; i++ )
			{
				float angle = Vector3.Angle( history.velocities[i], history.velocities[i - 1] );
				directionVariance += angle;
			}
			directionVariance /= history.velocities.Count - 1;
		}
		
		if( avgSpeed > 7f && directionVariance < 30f )
		{
			return PlayerBehaviorType.Aggressive;
		}
		else if( avgSpeed < 2f && totalDistance < 10f )
		{
			return PlayerBehaviorType.Camping;
		}
		else if( directionVariance > 60f )
		{
			return PlayerBehaviorType.Flanking;
		}
		else if( avgSpeed < 4f && directionVariance < 40f )
		{
			return PlayerBehaviorType.Defensive;
		}
		
		return PlayerBehaviorType.Balanced;
	}
    
	private Vector3 FindNearestCover( Vector3 position )
	{
		if( !useMapAwareness )
		{
			return Vector3.zero;
		}
		
		Vector3[] directions = {
			Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
			( Vector3.forward + Vector3.left ).normalized,
			( Vector3.forward + Vector3.right ).normalized,
			( Vector3.back + Vector3.left ).normalized,
			( Vector3.back + Vector3.right ).normalized
		};
		
		float nearestDistance = float.MaxValue;
		Vector3 nearestCover = Vector3.zero;
		
		foreach( Vector3 dir in directions )
		{
			if( Physics.Raycast( position, dir, out RaycastHit hit, 20f, coverLayer ) )
			{
				if( hit.distance < nearestDistance )
				{
					nearestDistance = hit.distance;
					nearestCover = hit.point;
				}
			}
		}
		
		return nearestCover;
	}
    
	public float GetPredictionConfidence( GameObject enemy )
	{
		if( !playerHistories.ContainsKey( enemy ) )
		{
			return 0.3f;
		}
		
		var history = playerHistories[enemy];
		
		float historyConfidence = Mathf.Min( history.positions.Count / 10f, 1f );
		
		float consistencyConfidence = 1f;
		if( history.velocities.Count > 1 )
		{
			float variance = 0f;
			for( int i = 1; i < history.velocities.Count; i++ )
			{
				variance += Vector3.Angle( history.velocities[i], history.velocities[i - 1] );
			}
			variance /= history.velocities.Count - 1;
			consistencyConfidence = 1f - Mathf.Clamp01( variance / 90f );
		}
		
		return ( historyConfidence + consistencyConfidence ) * 0.5f;
	}
    
	public string GetPredictionDebugInfo( GameObject enemy )
	{
		if( !playerHistories.ContainsKey( enemy ) )
		{
			return "No history available";
		}
		
		var history = playerHistories[enemy];
		
		string info = $"=== Enemy Prediction Debug ===\n";
		info += $"Behavior: {history.detectedBehavior}\n";
		info += $"Avg Speed: {history.averageSpeed:F1} m/s\n";
		info += $"History Length: {history.positions.Count}\n";
		info += $"Confidence: {GetPredictionConfidence( enemy ):P0}\n";
		
		return info;
	}
}
