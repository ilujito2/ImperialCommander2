﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DG.Tweening;
using UnityEngine;

namespace Saga
{
	public partial class SagaEventManager
	{
		public void NotifyMissionInfoChanged()
		{
			infoButtonTX.DOScale( 1.2f, .15f ).SetLoops( -1, LoopType.Yoyo );
			//GlowEngine.FindUnityObject<QuickMessage>().Show( "Mission Info Updated" );
		}

		/// <summary>
		/// Parses text for glyphs, NOT an event action, does NOT call NextEventAction()
		/// </summary>
		public void ShowTextBox( string m, Action callback = null )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ShowTextBox" );
			if ( string.IsNullOrEmpty( m?.Trim() ) )
			{
				Debug.Log( "ShowTextBox()::NO TEXT" );
				callback?.Invoke();
				return;
			}
			var go = Instantiate( textBoxPrefab, transform );
			var tb = go.transform.Find( "TextBox" ).GetComponent<TextBox>();
			tb.Show( m, callback );
			DataStore.sagaSessionData.missionLogger.LogEvent( MissionLogType.TextBox, m );
		}

		//GENERAL
		void MissionManagement( MissionManagement mm )
		{
			Debug.Log( "SagaEventManager()::PROCESSING MissionManagement" );
			var sc = FindObjectOfType<SagaController>();

			if ( mm.incRoundCounter )
			{
				Debug.Log( "MissionManagement()::Increase Round Counter" );
				sc.IncreaseRound();
				CheckIfEventsTriggered();
				GlowEngine.FindUnityObject<QuickMessage>().Show( DataStore.uiLanguage.sagaMainApp.roundIncreasedUC );
			}
			if ( mm.pauseDeployment )
			{
				Debug.Log( "MissionManagement()::Pause Deployment" );
				DataStore.sagaSessionData.gameVars.pauseDeployment = true;
				//GlowEngine.FindUnityObject<QuickMessage>().Show( DataStore.uiLanguage.uiMainApp.pauseDepMsgUC );
			}
			if ( mm.unpauseDeployment )
			{
				Debug.Log( "MissionManagement()::Unpause Deployment" );
				DataStore.sagaSessionData.gameVars.pauseDeployment = false;
				//GlowEngine.FindUnityObject<QuickMessage>().Show( DataStore.uiLanguage.uiMainApp.unPauseDepMsgUC );
			}
			if ( mm.pauseThreat )
			{
				Debug.Log( "MissionManagement()::Pause Threat" );
				DataStore.sagaSessionData.gameVars.pauseThreatIncrease = true;
				//GlowEngine.FindUnityObject<QuickMessage>().Show( DataStore.uiLanguage.uiMainApp.pauseThreatMsgUC );
			}
			if ( mm.unpauseThreat )
			{
				Debug.Log( "MissionManagement()::Unpause Threat" );
				DataStore.sagaSessionData.gameVars.pauseThreatIncrease = false;
				//GlowEngine.FindUnityObject<QuickMessage>().Show( DataStore.uiLanguage.uiMainApp.UnPauseThreatMsgUC );
			}
			if ( mm.endMission )
			{
				//don't process any more events or event actions after this one
				Debug.Log( "MissionManagement()::End Mission:Clearing Event queue / Event Action queue / endProcessingCallback" );
				endProcessingCallback = null;
				eventQueue.Clear();
				eventActionQueue.Clear();

				if ( !DataStore.sagaSessionData.setupOptions.useAdaptiveDifficulty )
				{
					GlowEngine.FindUnityObject<QuickMessage>().Show( DataStore.uiLanguage.sagaMainApp.endOfMissionUC );
					GlowTimer.SetTimer( 3, () =>
					{
						//load title screen
						FindObjectOfType<SagaController>().EndMission();
					} );
				}
				else
				{
					int awards = Mathf.FloorToInt( DataStore.sagaSessionData.gameVars.fame / 12 );
					if ( DataStore.sagaSessionData.gameVars.round >= 8 )
						awards = 0;

					ShowTextBox( $"<color=orange><uppercase><b>{DataStore.uiLanguage.sagaMainApp.endOfMissionUC}</color>\n\n{DataStore.uiLanguage.uiMainApp.fameHeading}: <color=orange>{DataStore.sagaSessionData.gameVars.fame}</color>\n\n{DataStore.uiLanguage.uiMainApp.awardsHeading}: <color=orange>{awards}</color>", () =>
					{
						//load title screen
						FindObjectOfType<SagaController>().EndMission();
					} );
				}
			}

			NextEventAction();
		}

		void ChangeMissionInfo( ChangeMissionInfo ci )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ChangeMissionInfo" );
			FindObjectOfType<Sound>().PlaySound( FX.Notify );
			DataStore.sagaSessionData.gameVars.currentMissionInfo = ci.theText;
			NotifyMissionInfoChanged();
			NextEventAction();
		}

		void ShowChangeObjective( ChangeObjective changeObjective )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ChangeObjective" );
			//objective bar handles glyphs itself
			if ( !string.IsNullOrEmpty( changeObjective.longText ) )
			{
				//FindObjectOfType<SagaEventManager>().toggleVisButton.SetActive( true );
				//FindObjectOfType<SagaController>().ToggleNavAndEntitySelection( false );
				ShowTextBox( changeObjective.longText, () =>
				{
					FindObjectOfType<SagaController>().OnChangeObjective( changeObjective.theText, () =>
					{
						//FindObjectOfType<SagaEventManager>().toggleVisButton.SetActive( false );
						//FindObjectOfType<SagaController>().ToggleNavAndEntitySelection( true );
						NextEventAction();
					} );
				} );
			}
			else
			{
				FindObjectOfType<SagaController>().OnChangeObjective( changeObjective.theText, () => NextEventAction() );
			}
		}

		void ModifyVariable( ModifyVariable mv )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ModifyVariable" );
			foreach ( var tm in mv.triggerList )
			{
				FindObjectOfType<TriggerManager>().ModifyVariable( tm );
			}
			CheckIfEventsTriggered( () => NextEventAction() );
		}

		void ModifyThreat( ModifyThreat mt )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ModifyVariable" );
			if ( mt.threatModifierType == ThreatModifierType.Fixed )
				DataStore.sagaSessionData.ModifyThreat( mt.fixedValue );
			else if ( mt.threatModifierType == ThreatModifierType.Multiple )
				DataStore.sagaSessionData.ModifyThreat( DataStore.sagaSessionData.setupOptions.threatLevel * mt.threatValue );
			NextEventAction();
		}

		void ActivateEventGroup( ActivateEventGroup ag )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ActivateEventGroup" );
			EventGroup eg = DataStore.mission.eventGroups.Where( x => x.GUID == ag.eventGroupGUID ).FirstOr( null );
			if ( eg != null )
				FindObjectOfType<SagaController>().triggerManager.FireEventGroup( eg.GUID );
			NextEventAction();
		}

		void ShowInputBox( InputPrompt ip )
		{
			Debug.Log( "ShowInputBox()::PROCESSING ShowTextBox" );
			var go = Instantiate( inputBoxPrefab, transform );
			var tb = go.transform.Find( "InputBox" ).GetComponent<InputBox>();
			tb.Show( ip, NextEventAction );
		}

		void ModifyRndLimit( ModifyRoundLimit mrl )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ModifyRndLimit" );
			int old = DataStore.sagaSessionData.gameVars.roundLimit;

			if ( mrl.disableRoundLimit )
			{
				Debug.Log( $"Round Limit [{DataStore.sagaSessionData.gameVars.roundLimit}] was DISABLED" );
				DataStore.sagaSessionData.gameVars.roundLimit = -1;
				DataStore.sagaSessionData.gameVars.roundLimitEvent = Guid.Empty;
			}
			else if ( !mrl.setRoundLimit )
			{
				//modify the limit
				DataStore.sagaSessionData.gameVars.roundLimit += mrl.roundLimitModifier;
				//set the new event, if any
				if ( mrl.eventGUID != Guid.Empty )
					DataStore.sagaSessionData.gameVars.roundLimitEvent = mrl.eventGUID;

				Debug.Log( $"Round limit [{old}] MODIFIED by [{mrl.roundLimitModifier}] to [{DataStore.sagaSessionData.gameVars.roundLimit}]" );
			}
			else if ( mrl.setRoundLimit )
			{
				//set the limit
				DataStore.sagaSessionData.gameVars.roundLimit = mrl.setLimitTo;
				//set the new event, if any
				if ( mrl.eventGUID != Guid.Empty )
					DataStore.sagaSessionData.gameVars.roundLimitEvent = mrl.eventGUID;

				Debug.Log( $"Round limit [{old}] SET to [{DataStore.sagaSessionData.gameVars.roundLimit}]" );
			}

			var sc = FindObjectOfType<SagaController>();
			sc.UpdateRoundNumberUI();

			NextEventAction();
		}

		void SetCountdown( SetCountdown scd )
		{
			Debug.Log( "SagaEventManager()::PROCESSING SetCountdown" );

			if ( string.IsNullOrEmpty( scd.countdownTimerName ) )
			{
				Debug.Log( $"WARNING: Countdown Timer name is null or empty" );
				scd.countdownTimerName = Guid.NewGuid().ToString();
			}

			//check if this is a disable command
			if ( scd.countdownTimer == -1 )
			{
				Debug.Log( $"Countdown Timer with name {scd.countdownTimerName} is now DISABLED" );
				DataStore.sagaSessionData.gameVars.countdownTimers.Remove( scd.countdownTimerName );
				//remove it from the UI
				var sc = FindObjectOfType<SagaController>();
				sc.OnSetCountdownTimer();
			}
			else
			{
				Debug.Log( $"Created Countdown Timer [{scd.countdownTimerName}] with value of [{scd.countdownTimer}] rounds" );
				//set the ending round based on the timer value and the current round
				int ending = DataStore.sagaSessionData.gameVars.round + scd.countdownTimer;
				scd.endRound = ending;
				//add the timer with the name trimmed and lowercase
				DataStore.sagaSessionData.gameVars.countdownTimers.Add( scd.countdownTimerName.Trim().ToLower(), scd );
				//if it's visible, notify to show it
				if ( scd.showPlayerCountdown )
				{
					var sc = FindObjectOfType<SagaController>();
					sc.OnSetCountdownTimer();
				}

				Debug.Log( $"Countdown [{scd.countdownTimerName}] will expire at the end of round [{ending}]" );
			}

			NextEventAction();
		}

		//DEPLOYMENTS
		void EnemyDeployment( EnemyDeployment ed )
		{
			Debug.Log( "SagaEventManager()::PROCESSING EnemyDeployment" );
			DeploymentCard card = DataStore.GetEnemy( ed.deploymentGroup );

			var ovrd = DataStore.sagaSessionData.gameVars.CreateDeploymentOverride( ed.deploymentGroup );
			//name
			if ( !string.IsNullOrEmpty( ed.enemyName ) )
			{
				ovrd.nameOverride = ed.enemyName;
				ed.enemyGroupData.cardName = ed.enemyName;
			}
			else
			{
				ovrd.nameOverride = card.name;
				ed.enemyGroupData.cardName = card.name;
			}
			//custom instructions
			if ( ed.useCustomInstructions )
				ovrd.SetInstructionOverride( new ChangeInstructions()
				{
					instructionType = ed.enemyGroupData.customInstructionType,
					theText = ed.enemyGroupData.customText
				} );
			//set the main override data
			ovrd.SetEnemyDeploymentOverride( ed );
			//generic mugshot (DEPRECATED)
			ovrd.useGenericMugshot = ed.useGenericMugshot;

			//check if this deployment uses threat cost, and apply any modification
			if ( ed.useThreat )
			{
				Debug.Log( $"EnemyDeployment::APPLYING THREAT COST ({card.cost})::MODIFIER ({ed.threatCost})" );
				DataStore.sagaSessionData.ModifyThreat( -(Mathf.Clamp( card.cost + ed.threatCost, 0, 100 )) );
			}

			//finally, do the actual deployment
			//deploy this group to the board, unmodified by elite RNG
			FindObjectOfType<SagaController>().dgManager.DeployGroup( card, true );
			FindObjectOfType<SagaController>().dgManager.HandleMapDeployment( card, NextEventAction, ovrd );
		}

		void AllyDeployment( AllyDeployment ad )
		{
			Debug.Log( "SagaEventManager()::PROCESSING AllyDeployment" );
			DeploymentCard card = DataStore.allyCards.Where( x => x.id == ad.allyID ).FirstOr( null );
			if ( card != null )
			{
				var ovrd = DataStore.sagaSessionData.gameVars.CreateDeploymentOverride( ad.allyID );
				//generic mugshot
				ovrd.useGenericMugshot = ad.useGenericMugshot;
				if ( ovrd.useGenericMugshot )
					card.mugShotPath = "CardThumbnails/genericAlly";
				//name
				if ( !string.IsNullOrEmpty( ad.allyName ) )
					ovrd.nameOverride = ad.allyName;
				else
					ovrd.nameOverride = card.name;
				//trigger
				ovrd.setTrigger = ad.setTrigger;
				//event
				ovrd.setEvent = ad.setEvent;
				//dp
				ovrd.deploymentPoint = ad.deploymentPoint;
				ovrd.specificDeploymentPoint = ad.specificDeploymentPoint;
				//threat cost to ADD
				if ( ad.useThreat )
				{
					Debug.Log( $"EnemyDeployment::APPLYING THREAT COST ({card.cost})::MODIFIER ({ad.threatCost})" );
					DataStore.sagaSessionData.ModifyThreat( Mathf.Clamp( card.cost + ad.threatCost, 0, 100 ) );
				}
				//finally, do the actual deployment
				FindObjectOfType<SagaController>().dgManager.DeployHeroAlly( card );
				FindObjectOfType<SagaController>().dgManager.HandleMapDeployment( card, NextEventAction, ovrd );
			}
			else
				NextEventAction();
		}

		void OptionalDeployment( OptionalDeployment od )
		{
			Debug.Log( "SagaEventManager()::PROCESSING OptionalDeployment" );
			DeploymentGroupOverride ovrd = new DeploymentGroupOverride( "" );
			ovrd.deploymentPoint = od.deploymentPoint;
			ovrd.specificDeploymentPoint = od.specificDeploymentPoint;
			ovrd.useThreat = od.useThreat;
			ovrd.threatCost = od.threatCost;
			if ( od.isOnslaught )
				FindObjectOfType<SagaController>().deploymentPopup.Show( DeployMode.Onslaught, true, true, NextEventAction, ovrd );
			else
				FindObjectOfType<SagaController>().deploymentPopup.Show( DeployMode.Landing, true, true, NextEventAction, ovrd );
		}

		void RandomDeployment( RandomDeployment rd )
		{
			Debug.Log( "SagaEventManager()::PROCESSING RandomDeployment" );
			DeploymentCard cd = null;
			List<DeploymentCard> list = new List<DeploymentCard>();

			DeploymentGroupOverride ovrd = new DeploymentGroupOverride( "" );
			ovrd.deploymentPoint = rd.deploymentPoint;
			ovrd.specificDeploymentPoint = rd.specificDeploymentPoint;
			int threatLimit = 0;

			if ( rd.threatType == ThreatModifierType.Fixed )
				threatLimit = Math.Abs( rd.fixedValue );
			else
				threatLimit = Math.Abs( rd.threatLevel ) * DataStore.sagaSessionData.setupOptions.threatLevel;

			Debug.Log( $"THREAT COST LIMIT::{threatLimit}" );

			do
			{
				var p = DataStore.deploymentCards
					.OwnedPlusOther()
					.MinusDeployed()
					.MinusInDeploymentHand()
					.MinusStarting()
					.MinusReserved()
					.MinusIgnored()
					.FilterByFaction()
					.MinusCannotRedeploy()
					.Concat( DataStore.sagaSessionData.EarnedVillains )
					.Where( x => x.cost <= threatLimit && !list.ContainsCard( x ) )
					.ToList();
				if ( p.Count > 0 )
				{
					int[] rnd = GlowEngine.GenerateRandomNumbers( p.Count );
					cd = p[rnd[0]];
					list.Add( cd );
					threatLimit -= cd.cost;
				}
				else
					cd = null;
			} while ( cd != null );

			if ( list.Count > 0 )
			{
				//deploy any groups picked (skips elite upgrade RNG)
				FindObjectOfType<SagaController>().dgManager.DeployGroupListWithOverride( list, ovrd, NextEventAction );
			}
			else
				NextEventAction();
		}

		void AddGroupToHand( AddGroupDeployment ag )
		{
			foreach ( var dg in ag.groupsToAdd )
			{
				var group = DataStore.allEnemyDeploymentCards.Where( x => x.id == dg.id ).FirstOr( null );
				if ( group != null )
				{
					DataStore.deploymentHand.Add( group );
					Debug.Log( $"SagaEventManager()::AddGroupDeployment::ADDED GROUP TO HAND::{group.name}, {group.id}" );
				}
			}
			NextEventAction();
		}

		void CustomDeployment( CustomEnemyDeployment ced )
		{
			Debug.Log( "SagaEventManager()::PROCESSING CustomDeployment" );
			var ovrd = DataStore.sagaSessionData.gameVars.CreateCustomDeploymentOverride( ced );
			if ( ced.useDeductCost )
				DataStore.sagaSessionData.ModifyThreat( -ced.groupCost );

			DeploymentCard card = DeploymentCard.CreateCustomCard( ced );
			ovrd.customCard = card;
			if ( ovrd.customType == MarkerType.Imperial )
				FindObjectOfType<SagaController>().dgManager.DeployGroup( card, true );
			else
				FindObjectOfType<SagaController>().dgManager.DeployHeroAlly( card );
			FindObjectOfType<SagaController>().dgManager.HandleMapDeployment( card, NextEventAction, ovrd );
		}

		//GROUP MANIPULATIONS
		void ChangeGroupInstructions( ChangeInstructions ci )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ChangeInstructions" );
			if ( ci.groupsToAdd.Count == 0 )//apply to ALL
			{
				var ovrd = DataStore.sagaSessionData.gameVars.CreateDeploymentOverride();
				ovrd.SetInstructionOverride( ci );
			}
			else//apply to specific groups
			{
				foreach ( var dg in ci.groupsToAdd )
				{
					var ovrd = DataStore.sagaSessionData.gameVars.CreateDeploymentOverride( dg.id );
					ovrd.SetInstructionOverride( ci );
				}
			}
			NextEventAction();
		}

		void ChangeGroupTarget( ChangeTarget ct )
		{
			if ( ct.groupType == GroupType.All )//apply to ALL
			{
				Debug.Log( "SagaEventManager()::PROCESSING ChangeTarget::ALL" );
				var ovrd = DataStore.sagaSessionData.gameVars.CreateDeploymentOverride();
				ovrd.SetTargetOverride( ct );
			}
			else
			{
				foreach ( var dg in ct.groupsToAdd )
				{
					Debug.Log( $"SagaEventManager()::PROCESSING ChangeTarget::SPECIFIC::{dg.id}::{dg.name}" );
					var ovrd = DataStore.sagaSessionData.gameVars.CreateDeploymentOverride( dg.id );
					ovrd.SetTargetOverride( ct );
				}
			}
			NextEventAction();
		}

		void ChangeGroupStatus( ChangeGroupStatus cs )
		{
			string readied = "", exhausted = "";
			foreach ( var grp in cs.readyGroups )
			{
				readied += $"<color=orange>{grp.name} [{grp.id}]</color> \r\n";
				FindObjectOfType<SagaController>().dgManager.ReadyGroup( grp.id );
			}
			foreach ( var grp in cs.exhaustGroups )
			{
				exhausted += $"<color=orange>{grp.name} [{grp.id}]</color> \r\n";
				FindObjectOfType<SagaController>().dgManager.ExhaustGroup( grp.id );
			}

			//string output = "";
			//if ( !string.IsNullOrEmpty( readied ) )
			//	output = $"{DataStore.uiLanguage.sagaMainApp.groupsReadyUC}:\r\n" + readied + "\r\n";
			//if ( !string.IsNullOrEmpty( exhausted ) )
			//	output += $"{DataStore.uiLanguage.sagaMainApp.groupsExhaustUC}:\r\n" + exhausted;

			//if ( !string.IsNullOrEmpty( output ) )
			//{
			//	ShowTextBox( output, NextEventAction );
			//}
			//else
			NextEventAction();
		}

		void ChangeReposition( ChangeReposition cr )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ChangeReposition" );
			if ( cr.useSpecific )
			{
				foreach ( var item in cr.repoGroups )
				{
					var ovrd = DataStore.sagaSessionData.gameVars.CreateDeploymentOverride( item.id );
					ovrd.repositionInstructions = cr.theText;
				}
			}
			else//apply to all
			{
				var ovrd = DataStore.sagaSessionData.gameVars.CreateDeploymentOverride();
				ovrd.repositionInstructions = cr.theText;
			}

			NextEventAction();
		}

		void ResetGroup( ResetGroup rg )
		{
			Debug.Log( "SagaEventManager()::PROCESSING ResetGroup" );
			if ( rg.resetAll )
			{
				DataStore.sagaSessionData.gameVars.RemoveAllOverrides();
			}
			else
			{
				foreach ( var item in rg.groupsToAdd )
				{

					DataStore.sagaSessionData.gameVars.RemoveOverride( item.id );
				}
			}

			NextEventAction();
		}

		void RemoveGroup( RemoveGroup rg )
		{
			Debug.Log( "SagaEventManager()::PROCESSING RemoveGroup" );
			foreach ( var item in rg.groupsToRemove )
			{
				bool returnToHand = true;
				var ovrd = DataStore.sagaSessionData.gameVars.GetDeploymentOverride( item.id );
				//test if it can redeploy
				if ( ovrd != null && !ovrd.canRedeploy )
				{
					DataStore.sagaSessionData.CannotRedeployList.Add( item.id );
					//completely reset if it can't redeploy, so it can be manually deployed "clean" later
					DataStore.sagaSessionData.gameVars.RemoveOverride( item.id );
					returnToHand = false;
				}

				var card = DataStore.allEnemyDeploymentCards.GetDeploymentCard( item.id );
				if ( card != null )
				{
					//return it to the Hand if it can redeploy
					if ( card.id != "DG070" && card.characterType != CharacterType.Villain && returnToHand )
					{
						DataStore.deploymentHand.Add( card );
					}
					//remove it from deployed list
					DataStore.deployedEnemies.RemoveCardByID( card );
				}
				//if it is an EARNED villain, add it back into manual deploy list
				if ( DataStore.sagaSessionData.EarnedVillains.ContainsCard( card ) && !DataStore.manualDeploymentList.ContainsCard( card ) )
				{
					DataStore.manualDeploymentList.Add( card );
					DataStore.SortManualDeployList();
				}
				//finally, reset the group if needed
				if ( ovrd != null && ovrd.canRedeploy )
				{
					if ( ovrd.useResetOnRedeployment )
						DataStore.sagaSessionData.gameVars.RemoveOverride( ovrd.ID );
					else if ( !ovrd.useResetOnRedeployment )
						ovrd.ResetDP();
				}

				if ( DataStore.deployedEnemies.Count == 0 )
					FindObjectOfType<SagaController>().eventManager.CheckIfEventsTriggered();

				//remove icon from the enemy column
				FindObjectOfType<SagaController>().dgManager.RemoveGroup( card.id );

				//remove any override
				//DataStore.sagaSessionData.gameVars.RemoveOverride( item.id );
				//if ( card != null )
				//{
				//	//remove it from the hand
				//	DataStore.deploymentHand.Add( card );
				//	//remove it from the deployment list
				//	DataStore.deployedEnemies.Remove( card );
				//	//remove icon from the enemy column
				//	FindObjectOfType<SagaController>().dgManager.RemoveGroup( card.id );
				//}
			}

			foreach ( var item in rg.allyGroupsToRemove )
			{
				//remove any override
				DataStore.sagaSessionData.gameVars.RemoveOverride( item.id );
				var card = DataStore.allyCards.GetDeploymentCard( item.id );
				if ( card != null )
				{
					//remove it from the deployed heroes list
					DataStore.deployedHeroes.Remove( card );
					//remove icon from the ally column
					FindObjectOfType<SagaController>().dgManager.RemoveGroup( card.id );
				}
			}

			NextEventAction();
		}

		void QueryGroup( QueryGroup group )
		{
			Debug.Log( "SagaEventManager()::PROCESSING QueryGroup" );
			bool found = false;
			if ( group.groupEnemyToQuery != null )
			{
				if ( DataStore.deployedEnemies.Any( x => x.id == group.groupEnemyToQuery.id ) )
					found = true;
				if ( DataStore.deploymentHand.Any( x => x.id == group.groupEnemyToQuery.id ) )
					found = true;
			}
			else if ( group.groupRebelToQuery != null )
			{
				found = DataStore.deployedHeroes.Any( x => x.id == group.groupRebelToQuery.id );
			}

			if ( found )
			{
				Debug.Log( $"QueryGroup()::FOUND {group.groupEnemyToQuery?.name ?? group.groupRebelToQuery.name}" );
				if ( group.foundEvent != Guid.Empty )
				{
					DoEvent( group.foundEvent );
				}
				if ( group.foundTrigger != Guid.Empty )
				{
					FindObjectOfType<SagaController>().triggerManager.FireTrigger( group.foundTrigger );
				}
			}

			NextEventAction();
		}

		//MAPS & TOKENS
		void MapManagement( MapManagement mm )
		{
			Debug.Log( "SagaEventManager()::PROCESSING MapManagement" );

			var tileExpansionTranslatedNames = new Dictionary<string, string>()
			{
				{ Expansion.Core.ToString(), $"{DataStore.uiLanguage.sagaMainApp.mmCoreTileNameUC}" },
				{ Expansion.Twin.ToString(), $"{DataStore.uiLanguage.sagaMainApp.mmTwinTileNameUC}" },
				{ Expansion.Hoth.ToString(), $"{DataStore.uiLanguage.sagaMainApp.mmHothTileNameUC}" },
				{ Expansion.Bespin.ToString(), $"{DataStore.uiLanguage.sagaMainApp.mmBespinTileNameUC}" },
				{ Expansion.Jabba.ToString(), $"{DataStore.uiLanguage.sagaMainApp.mmJabbaTileNameUC}" },
				{ Expansion.Empire.ToString(), $"{DataStore.uiLanguage.sagaMainApp.mmEmpireTileNameUC}" },
				{ Expansion.Lothal.ToString(), $"{DataStore.uiLanguage.sagaMainApp.mmLothalTileNameUC}" },
			};

			//activate map section
			if ( mm.mapSection != Guid.Empty )
			{
				var tiles = FindObjectOfType<SagaController>().tileManager.ActivateMapSection( mm.mapSection );
				FindObjectOfType<TileManager>().CamToSection( mm.mapSection );

				//sort and group tiles by number, i.e. "Core 2A", "Core 11A", "Empire 2B"
				var orderedAndGrouped = tiles.Item1
					.OrderBy( str => str.Split( ' ' )[0] )  // Order alphabetically
					.ThenBy( str => int.Parse( str.Split( ' ' )[1].TrimEnd( 'A', 'B' ) ) ) // Then order by entire numerical values
					.ThenBy( str => str.EndsWith( "A" ) ? 0 : 1 ) // Finally, order by A/B values
					.GroupBy( str => str ) // Group the strings
					.Select( group => new
					{
						Tile = group.Key,
						Count = group.Count()
					} );
				var tilesWithCount = new List<string>();

				foreach ( var item in orderedAndGrouped )
				{
					if ( item.Count > 1 )
					{
						tilesWithCount.Add( $"{item.Tile} x {item.Count}" );
					}
					else
					{
						tilesWithCount.Add( item.Tile );
					}
				}

				// Replacing tile expansion name with translated name
				tilesWithCount = tilesWithCount.Select(x => { var name = x.Split(' ')[0]; return tileExpansionTranslatedNames.ContainsKey(name) ? x.Replace(name, tileExpansionTranslatedNames[name]) : x; }).ToList();
				// If translated name is an expansion symbol, removing whitespace between symbol and tile number
				tilesWithCount = tilesWithCount.Select(x => Regex.IsMatch(x, "\\{[0-6]+\\}\\s") ? x.Replace("} ", "}") : x).ToList();

				var tmsg = string.Join( ", ", tilesWithCount );
				var emsg = DataStore.uiLanguage.sagaMainApp.mmAddEntitiesUC + ":\n\n";
				var emsg2 = string.Join( "\n", tiles.Item2 );
				emsg = string.IsNullOrEmpty( emsg2.Trim() ) ? "" : emsg + emsg2;

				ShowTextBox( $"{DataStore.uiLanguage.sagaMainApp.mmAddTilesUC}:\n\n<color=orange>{tmsg}</color>\n\n{emsg}", () =>
				{
					//see if there is an optional deployment waiting for an Active DP
					if ( DataStore.sagaSessionData.gameVars.delayOptionalDeployment )
					{
						var dp = FindObjectOfType<SagaController>().mapEntityManager.GetActiveDeploymentPoint( null );
						if ( dp != Guid.Empty )
						{
							DataStore.sagaSessionData.gameVars.delayOptionalDeployment = false;
							FindObjectOfType<SagaController>().deploymentPopup.Show( DeployMode.Landing, false, true, NextEventAction );
						}
						else
							NextEventAction();
					}
					else
						NextEventAction();
				} );
			}
			//deactivate map section
			if ( mm.mapSectionRemove != Guid.Empty )
			{
				var tiles = FindObjectOfType<SagaController>().tileManager.DeactivateMapSection( mm.mapSectionRemove );

				// Replacing tile expansion name with translated name
				tiles = tiles.Select(x => { var name = x.Split(' ')[0]; return tileExpansionTranslatedNames.ContainsKey(name) ? x.Replace(name, tileExpansionTranslatedNames[name]) : x; }).ToList();
				// If translated name is an expansion symbol, removing whitespace between symbol and tile number
				tiles = tiles.Select(x => Regex.IsMatch(x, "\\{[0-6]+\\}\\s") ? x.Replace("} ", "}") : x).ToList();

				FindObjectOfType<TileManager>().CamToSection( mm.mapSectionRemove );
				ShowTextBox( $"{DataStore.uiLanguage.sagaMainApp.mmRemoveTilesUC}:\n\n<color=orange>{string.Join( ", ", tiles )}</color>", () =>
					{
						NextEventAction();
					} );
			}
			//activate tile
			if ( mm.mapTile != Guid.Empty )
			{
				string t = FindObjectOfType<SagaController>().tileManager.ActivateTile( mm.mapTile );

				name = t.Split(' ')[0];
				// Replacing tile expansion name with translated name
				t = tileExpansionTranslatedNames.ContainsKey(name) ? t.Replace(name, tileExpansionTranslatedNames[name]) : t;
				// If translated name is an expansion symbol, removing whitespace between symbol and tile number
				t = Regex.IsMatch(t, "\\{[0-6]+\\}\\s") ? t.Replace("} ", "}") : t;

				FindObjectOfType<TileManager>().CamToTile( mm.mapTile );
				ShowTextBox( $"{DataStore.uiLanguage.sagaMainApp.mmAddTilesUC}:\n\n<color=orange>{t}</color>", () =>
				{
					NextEventAction();
				} );
			}
			//deactivate tile
			if ( mm.mapTileRemove != Guid.Empty )
			{
				string t = FindObjectOfType<SagaController>().tileManager.DeactivateTile( mm.mapTileRemove );

				name = t.Split(' ')[0];
				// Replacing tile expansion name with translated name
				t = tileExpansionTranslatedNames.ContainsKey(name) ? t.Replace(name, tileExpansionTranslatedNames[name]) : t;
				// If translated name is an expansion symbol, removing whitespace between symbol and tile number
				t = Regex.IsMatch(t, "\\{[0-6]+\\}\\s") ? t.Replace("} ", "}") : t;

				FindObjectOfType<TileManager>().CamToTile( mm.mapTileRemove );
				ShowTextBox( $"{DataStore.uiLanguage.sagaMainApp.mmRemoveTilesUC}:\n\n<color=orange>{t}</color>", () =>
				{
					NextEventAction();
				} );
			}
		}

		void ModifyMapEntity( ModifyMapEntity mod )
		{
			var em = FindObjectOfType<MapEntityManager>();
			em.ModifyPrefabs( mod, () =>
			{
				//see if there is an optional deployment waiting for an Active DP
				if ( DataStore.sagaSessionData.gameVars.delayOptionalDeployment )
				{
					var dp = FindObjectOfType<SagaController>().mapEntityManager.GetActiveDeploymentPoint( null );
					if ( dp != Guid.Empty )
					{
						DataStore.sagaSessionData.gameVars.delayOptionalDeployment = false;
						FindObjectOfType<SagaController>().deploymentPopup.Show( DeployMode.Landing, false, true, NextEventAction );
					}
					else
						NextEventAction();
				}
				else
					NextEventAction();
			} );
		}

		/// <summary>
		/// MUST BE PUBLIC, this is a special case because it can be called directly WITHOUT an event to fire it, it does NOT call NextEventAction(), does parse text for glyphs
		/// </summary>
		public void ShowPromptBox( QuestionPrompt prompt, Action callback = null )
		{
			Debug.Log( "SagaEventManager()::PROCESSING QuestionPrompt" );

			if ( string.IsNullOrEmpty( prompt.theText?.Trim() ) )
			{
				Debug.Log( "ShowPromptBox()::NO TEXT" );
				callback?.Invoke();
				return;
			}
			var go = Instantiate( promptBoxPrefab, transform );
			var tb = go.transform.Find( "TextBox" ).GetComponent<PromptBox>();
			tb.Show( prompt, callback );

			DataStore.sagaSessionData.missionLogger.LogEvent( MissionLogType.PromptBox, prompt.theText );
		}

		//CAMPAIGN MANAGEMENT
		void ModifyXP( CampaignModifyXP mxp )
		{
			if ( RunningCampaign.sagaCampaignGUID != Guid.Empty )
			{
				Debug.Log( $"SagaEventManager()::PROCESSING ModifyXP: {mxp.xpToAdd}" );
				RunningCampaign.sagaCampaign.campaignHeroes.ForEach( x => x.xpAmount += mxp.xpToAdd );
				RunningCampaign.sagaCampaign.SaveCampaignState();
			}
			else
				Debug.Log( $"SagaEventManager()::ModifyXP({mxp.xpToAdd})::Campaign isn't active" );

			NextEventAction();
		}

		void ModifyCredits( CampaignModifyCredits mcredits )
		{
			if ( RunningCampaign.sagaCampaignGUID != Guid.Empty )
			{
				Debug.Log( $"SagaEventManager()::PROCESSING ModifyCredits: {mcredits.creditsToModify}" );
				Debug.Log( $"SagaEventManager()::ModifyCredits: Multiply by hero count: {mcredits.multiplyByHeroCount}" );
				if ( mcredits.multiplyByHeroCount )
					RunningCampaign.sagaCampaign.credits += mcredits.creditsToModify * RunningCampaign.sagaCampaign.campaignHeroes.Count;
				else
					RunningCampaign.sagaCampaign.credits += mcredits.creditsToModify;

				RunningCampaign.sagaCampaign.SaveCampaignState();
				Debug.Log( $"ModifyCredits()::New Credits: {RunningCampaign.sagaCampaign.credits}" );
			}
			else
				Debug.Log( $"SagaEventManager({mcredits.creditsToModify})::ModifyCredits()::Campaign isn't active" );

			NextEventAction();
		}

		void ModifyFameAwards( CampaignModifyFameAwards mfa )
		{
			if ( RunningCampaign.sagaCampaignGUID != Guid.Empty )
			{
				Debug.Log( $"SagaEventManager()::PROCESSING ModifyFameAwards: Fame: {mfa.fameToAdd}, Awards: {mfa.awardsToAdd}" );
				RunningCampaign.sagaCampaign.fame += mfa.fameToAdd;
				RunningCampaign.sagaCampaign.awards += mfa.awardsToAdd;
				RunningCampaign.sagaCampaign.SaveCampaignState();
				Debug.Log( $"ModifyFameAwards()::New Fame: {RunningCampaign.sagaCampaign.fame}" );
				Debug.Log( $"ModifyFameAwards()::New Awards: {RunningCampaign.sagaCampaign.awards}" );
			}
			else
				Debug.Log( $"SagaEventManager(Fame: {mfa.fameToAdd}, Awards: {mfa.awardsToAdd})::ModifyFameAwards()::Campaign isn't active" );

			NextEventAction();
		}

		void SetNextMission( CampaignSetNextMission snm )
		{
			if ( RunningCampaign.sagaCampaignGUID != Guid.Empty )
			{
				Debug.Log( $"SagaEventManager()::PROCESSING SetNextMission: Mission ID: [{snm.missionID}], Custom Mission ID: [{snm.customMissionID}]" );
				if ( snm.missionID == "Custom" )
					RunningCampaign.sagaCampaign.SetNextStoryMission( snm.customMissionID, MissionSource.Embedded );
				else
					RunningCampaign.sagaCampaign.SetNextStoryMission( snm.missionID, MissionSource.Official );
				RunningCampaign.sagaCampaign.SaveCampaignState();
			}
			else
				Debug.Log( $"SagaEventManager(CUSTOMID:[{snm.customMissionID}] / ID:[{snm.missionID}])::SetNextMission()::Campaign isn't active" );

			NextEventAction();
		}

		void AddCampaignRewards( AddCampaignReward acr )
		{
			var sagaController = FindObjectOfType<SagaController>();

			if ( RunningCampaign.sagaCampaignGUID != Guid.Empty )
			{
				Debug.Log( $"SagaEventManager()::PROCESSING AddCampaignRewards" );

				foreach ( var item in acr.campaignItems )
				{
					if ( !RunningCampaign.sagaCampaign.campaignItems.Contains( item ) )
					{
						RunningCampaign.sagaCampaign.campaignItems.Add( item );
						var reward = DataStore.campaignDataItems.Where( x => x.id == item ).FirstOr( null );
						sagaController.toastManager.ShowToast( $"{DataStore.uiLanguage.uiCampaign.itemsUC}: + {reward.name}" );
						Debug.Log( $"SagaEventManager()::AddCampaignRewards:Added Item: {reward} / {item}" );
					}
				}
				foreach ( var item in acr.campaignRewards )
				{
					if ( !RunningCampaign.sagaCampaign.campaignRewards.Contains( item ) )
					{
						RunningCampaign.sagaCampaign.campaignRewards.Add( item );
						var reward = DataStore.campaignDataRewards.Where( x => x.id == item ).FirstOr( null );
						sagaController.toastManager.ShowToast( $"{DataStore.uiLanguage.uiCampaign.rewardsUC}: {reward.name}" );
						Debug.Log( $"SagaEventManager()::AddCampaignRewards:Added Reward: {reward} / {item}" );
					}
				}
				foreach ( var item in acr.earnedVillains )
				{
					DeploymentCard card = DataStore.villainCards.GetDeploymentCard( item );
					if ( RunningCampaign.sagaCampaign.campaignVillains.GetDeploymentCard( item ) == null )
					{
						RunningCampaign.sagaCampaign.campaignVillains.Add( card );
						sagaController.toastManager.ShowToast( $"{DataStore.uiLanguage.uiCampaign.villainsUC}: {card.name}" );
						Debug.Log( $"SagaEventManager()::AddCampaignRewards:Added Villain: {card.name} / {card.id}" );
					}
				}
				foreach ( var item in acr.earnedAllies )
				{
					DeploymentCard card = DataStore.allyCards.GetDeploymentCard( item );
					if ( RunningCampaign.sagaCampaign.campaignAllies.GetDeploymentCard( item ) == null )
					{
						RunningCampaign.sagaCampaign.campaignAllies.Add( card );
						sagaController.toastManager.ShowToast( $"{DataStore.uiLanguage.uiCampaign.alliesUC}: {card.name}" );
						Debug.Log( $"SagaEventManager()::AddCampaignRewards:Added Ally: {card.name} / {card.id}" );
					}
				}

				//next mission threat level
				if ( acr.threatToModify != 0 )
				{
					string mod = acr.threatToModify > 0 ? "+" : "-";
					RunningCampaign.sagaCampaign.ModifyNextMissionThreatLevel( acr.threatToModify );
					sagaController.toastManager.ShowToast( $"{DataStore.uiLanguage.uiMainApp.modThreatHeading.ToUpper()} {mod}{acr.threatToModify}" );
					Debug.Log( $"SagaEventManager()::AddCampaignRewards::Modify Threat: {acr.threatToModify}" );
				}

				RunningCampaign.sagaCampaign.SaveCampaignState();
			}
			else
				Debug.Log( $"SagaEventManager(ITEMS:{acr.campaignItems.Count} / REWARDS:{acr.campaignRewards.Count} / VILLAINS:{acr.earnedVillains.Count} / ALLIES:{acr.earnedAllies.Count} / THREAT:{acr.threatToModify})::AddCampaignRewards()::Campaign isn't active" );

			//medpacks
			if ( acr.medpacsToModify != 0 )
			{
				string mod = acr.medpacsToModify > 0 ? "+" : "-";
				DataStore.sagaSessionData.gameVars.medPacCount = Math.Max( 0, DataStore.sagaSessionData.gameVars.medPacCount + acr.medpacsToModify );
				FindObjectOfType<SagaController>().UpdateMedPacCountUI();
				sagaController.toastManager.ShowToast( $"Medpac: {mod}{acr.medpacsToModify}" );
				Debug.Log( $"SagaEventManager()::AddCampaignRewards::Modify MedPacs: {acr.medpacsToModify}, new MedPac count: {DataStore.sagaSessionData.gameVars.medPacCount}" );
			}

			NextEventAction();
		}
	}
}