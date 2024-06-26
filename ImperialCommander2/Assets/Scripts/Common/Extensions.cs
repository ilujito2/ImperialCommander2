﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Extensions
{
	public static void RemoveCardByID( this List<DeploymentCard> thisCD, DeploymentCard card )
	{
		DeploymentCard c = thisCD.Where( x => x.id.ToUpper() == card.id.ToUpper() ).FirstOr( null );
		if ( c != null )
			thisCD.Remove( thisCD.Where( x => x.id.ToUpper() == card.id.ToUpper() ).First() );
	}
	/// <summary>
	/// Match a card in a list by its ID
	/// </summary>
	public static bool ContainsCard( this List<DeploymentCard> thisCD, DeploymentCard comp )
	{
		try
		{
			if ( comp.isDummy || string.IsNullOrEmpty( comp.id ) )
				return false;
			return thisCD.Any( x => x.id.ToUpper() == comp.id.ToUpper() );
		}
		catch ( Exception e )
		{
			Saga.Utils.LogError( $"ContainsCard()\n{comp.name}::{comp.id}\n{e.Message}" );
			return false;
		}
	}
	public static DeploymentCard GetDeploymentCard( this List<DeploymentCard> thisCD, string cardID )
	{
		return thisCD.Where( x => x.id.ToUpper() == cardID.ToUpper() ).FirstOr( null );
	}
	public static List<DeploymentCard> Owned( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => DataStore.ownedExpansions.Contains( (Expansion)Enum.Parse( typeof( Expansion ), x.expansion ) ) ).ToList();
	}

	public static List<DeploymentCard> OwnedPlusOther( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => DataStore.ownedExpansions.Contains( (Expansion)Enum.Parse( typeof( Expansion ), x.expansion ) ) || x.expansion == "Other" ).ToList();
	}

	public static List<DeploymentCard> MinusEarnedVillains( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => !DataStore.sagaSessionData.EarnedVillains.ContainsCard( x ) ).ToList();
	}

	public static List<DeploymentCard> MinusIgnored( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => !DataStore.sagaSessionData.MissionIgnored.ContainsCard( x ) ).ToList();
	}

	public static List<DeploymentCard> MinusStarting( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => !DataStore.sagaSessionData.MissionStarting.ContainsCard( x ) ).ToList();
	}

	public static List<DeploymentCard> MinusReserved( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => !DataStore.sagaSessionData.MissionReserved.ContainsCard( x ) ).ToList();
	}

	public static List<DeploymentCard> MinusDeployed( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => !DataStore.deployedEnemies.ContainsCard( x ) ).ToList();
	}

	public static List<DeploymentCard> MinusInDeploymentHand( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => !DataStore.deploymentHand.ContainsCard( x ) ).ToList();
	}

	/// <summary>
	/// Filters and returns just the villains in the supplied list
	/// </summary>
	public static List<DeploymentCard> GetVillains( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => DataStore.villainCards.ContainsCard( x ) ).ToList();
	}

	public static List<DeploymentCard> FilterByFaction( this List<DeploymentCard> thisCD )
	{
		if ( DataStore.mission.missionProperties.factionImperial && !DataStore.mission.missionProperties.factionMercenary )
			return thisCD.Where( x => x.faction == "Imperial" ).ToList();
		else if ( DataStore.mission.missionProperties.factionMercenary && !DataStore.mission.missionProperties.factionImperial )
			return thisCD.Where( x => x.faction == "Mercenary" ).ToList();
		else
			return thisCD.Where( x => x.faction == "Imperial" || x.faction == "Mercenary" ).ToList();
	}

	public static List<DeploymentCard> GetHeroesAndAllies( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => x.characterType == Saga.CharacterType.Hero || x.characterType == Saga.CharacterType.Ally ).ToList();
	}

	/// <summary>
	/// returns list of healthy heroes/allies
	/// </summary>
	public static List<DeploymentCard> GetHealthy( this List<DeploymentCard> thisCD )
	{
		if ( thisCD.Any( x => !x.isDummy && x.heroState.isHealthy ) )
			return thisCD.Where( x => !x.isDummy && x.heroState.isHealthy ).ToList();
		else
			return null;
	}

	public static List<DeploymentCard> GetUnhealthy( this List<DeploymentCard> thisCD )
	{
		if ( thisCD.Any( x => !x.isDummy && !x.heroState.isHealthy ) )
			return thisCD.Where( x => !x.isDummy && !x.heroState.isHealthy ).ToList();
		else
			return null;
	}

	/// <summary>
	/// Filters and returns a randomly selected group (if>1) that has any of the provided traits
	/// </summary>
	public static List<DeploymentCard> WithTraits( this List<DeploymentCard> thisCD, GroupTraits[] trait )
	{
		if ( trait.Length == 0 || thisCD is null )
			return null;
		var list = (from dc in thisCD from tr in trait where dc.groupTraits.ToList().Contains( tr ) select dc).ToList();
		if ( list.Count > 0 )
		{
			Debug.Log( "WithTraits()::MATCHING TRAITS FOUND" );
			return list;
		}
		else
		{
			Debug.Log( "WithTraits()::NO MATCHING TRAITS FOUND" );
			return null;
		}
	}
	public static List<DeploymentCard> MinusCannotRedeploy( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => !DataStore.sagaSessionData.CannotRedeployList.Contains( x.id ) ).ToList();
	}

	public static List<DeploymentCard> MinusElite( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => !x.isElite ).ToList();
	}

	public static List<DeploymentCard> OnlyElite( this List<DeploymentCard> thisCD )
	{
		return thisCD.Where( x => x.isElite ).ToList();
	}

	public static T FirstOr<T>( this IEnumerable<T> thisEnum, T def )
	{
		foreach ( var item in thisEnum )
			return item;
		return def;
	}

	public static Color ToColor( this Vector3 c )
	{
		return new Color( c.x, c.y, c.z, 1 );
	}

	public static Color ToColor( this Vector4 c )
	{
		return new Color( c.x, c.y, c.z, c.w );
	}

	public static Vector3 ToUnityV3( this Saga.Vector v )
	{
		return new Vector3( v.X, v.Y, v.Z );
	}

	public static Saga.Vector ToSagaVector( this Vector3 v )
	{
		return new Saga.Vector( v.x, v.y, v.z );
	}

	/// <summary>
	/// Extract the non-zero digit(s) from a DG ID, ie M001 = 1
	/// </summary>
	public static string GetDigits( this string s )
	{
		string s1 = new string( s.Where( Char.IsDigit ).ToArray() );
		//remove leading zeroes
		return s1.TrimStart( new Char[] { '0' } );
	}

	public static Canvas GetCanvas( this RectTransform rt )
	{
		return rt.gameObject.GetComponentInParent<Canvas>();
	}

	public static float GetWidth( this RectTransform rt )
	{
		var w = (rt.anchorMax.x - rt.anchorMin.x) * Screen.width + rt.sizeDelta.x;// * rt.GetCanvas().scaleFactor;
		return w;
	}

	public static float GetHeight( this RectTransform rt )
	{
		var h = (rt.anchorMax.y - rt.anchorMin.y) * Screen.height + rt.sizeDelta.y;// * rt.GetCanvas().scaleFactor;
		return h;
	}

	public static int FindIndexByProperty<T>( this Queue<T> queue, Func<T, bool> predicate )
	{
		int index = 0;
		foreach ( var item in queue )
		{
			if ( predicate( item ) )
			{
				return index;
			}
			index++;
		}
		return -1; // Item not found
	}
}