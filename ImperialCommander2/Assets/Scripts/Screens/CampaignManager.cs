using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Saga
{
	public class CampaignManager : MonoBehaviour
	{
		[HideInInspector]
		public SagaCampaign sagaCampaign;

		public Fader fader;
		public VolumeProfile volume;
		public GameObject leftPanel, rightPanel, switchButton;
		//prefabs
		public GameObject forceMissionItemPrefab, listItemPrefab, missionItemPrefab, customAddMissionBarPrefab;
		//UI
		public CampaignJournal campaignJournal;
		public AddCampaignItemPopup addCampaignItemPopup;
		public ModifyCustomPropsPopup modifyCustomPropsPopup;
		public TMP_InputField campaignNameInputField;
		public Transform villainContainer, allyContainer, itemContainer, structureContainer, rewardContainer;
		public CampaignHeroPrefab[] heroPrefabs;
		public TextMeshProUGUI xpText, creditsText, fameText, awardsText;
		public MWheelHandler xpWheel, creditsWheel, fameWheel, awardsWheel;
		public Text campaignExpansion;
		public CanvasGroup topButtonsGroup, rightPanelGroup;
		public ValueAdjuster valueAdjuster;
		public Image expansionLogo;
		public Sprite[] expansionSprites;
		public HelpPanel helpPanel;
		public GameObject campaignHelpMockup;
		public ErrorPanel errorPanel;
		public Button campaignInfoButton;
		//translatable UI
		public CampaignLanguageController languageController;

		public bool isDebugMode = false;

		[HideInInspector]
		public Sound sound;
		int view = 1;//0=left, 1=right
		float aspect;

		void LogCallback( string condition, string stackTrace, LogType type )
		{
			//only capture errors, exceptions, asserts
			if ( type != LogType.Warning && type != LogType.Log )
				errorPanel.Show( $"An Error Occurred of Type <color=green>{type}</color>", $"<color=yellow>{condition}</color>\n\n{stackTrace.Replace( "(at", "\n\n(at" )}" );
		}

		private void OnDestroy()
		{
			Application.logMessageReceived -= LogCallback;
		}

		private void Awake()
		{
			//Exception handling for any Unity thrown exception, such as from asset management
			Application.logMessageReceived += LogCallback;
		}

		void Start()
		{
			Debug.Log( "ENTERING CAMPAIGN MANAGER" );

			System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
			System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
			System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

			Screen.sleepTimeout = SleepTimeout.NeverSleep;

			float pixelHeightOfCurrentScreen = Screen.height;//.currentResolution.height;
			float pixelWidthOfCurrentScreen = Screen.width;//.currentResolution.width;
			aspect = pixelWidthOfCurrentScreen / pixelHeightOfCurrentScreen;
			if ( aspect < 1.7f )//less than 16:9, such as 16:10 and 4:3
			{
				//switch to single view
				switchButton.SetActive( true );
				leftPanel.SetActive( false );
			}
			else
				campaignHelpMockup.SetActive( true );

			FileManager.SetupDefaultFolders();

			//re-initialize all card data, otherwise deployed cards and other items carry over when coming back from a campaign game
			DataStore.InitData();

			//BOOTSTRAP CAMPAIGN
			bootstrapCampaign( !isDebugMode );//true = production build

			//sagaCampaign is set at this point

			fader.UnFade( 2 );

			//apply settings
			sound = FindObjectOfType<Sound>();
			sound.PlayMusicAndMenuAmbient();

			//set translated UI
			languageController.SetTranslatedUI();

			if ( volume.TryGet<Bloom>( out var bloom ) )
				bloom.active = PlayerPrefs.GetInt( "bloom2" ) == 1;
			if ( volume.TryGet<Vignette>( out var vig ) )
				vig.active = PlayerPrefs.GetInt( "vignette" ) == 1;

			//popuplate UI with loaded campaign data
			InitUI();
		}

		void bootstrapCampaign( bool isProduction )
		{
			Debug.Log( $"***BOOTSTRAP (Campaign Manager) PRODUCTION={isProduction}***" );

			//campaign is already setup from Title screen
			if ( RunningCampaign.sagaCampaignGUID != null && RunningCampaign.sagaCampaignGUID != Guid.Empty )
			{
				sagaCampaign = SagaCampaign.LoadCampaignState( RunningCampaign.sagaCampaignGUID );
				//sagaCampaign.FixExpansionCodes();
			}
			else//error or debugging, setup new test campaign
			{
				RunningCampaign.expansionCode = "Core";
				sagaCampaign = SagaCampaign.CreateNewCampaign( "Error/Debug - Not Found", RunningCampaign.expansionCode );
			}

			//set the campaign into the persistent RunningCampaign structure
			RunningCampaign.sagaCampaign = sagaCampaign;
		}

		/// <summary>
		/// fill in name and hero/item/villain/ally lists from campaign data
		/// </summary>
		private void InitUI()
		{
			if ( sagaCampaign.campaignType == CampaignType.Custom )
				campaignInfoButton.gameObject.SetActive( false );

			campaignNameInputField.text = sagaCampaign.campaignName;
			//use translated expansion name
			if ( sagaCampaign.campaignType == CampaignType.Official )
			{
				campaignExpansion.text = DataStore.translatedExpansionNames[sagaCampaign.campaignExpansionCode];
				//expansion icon
				expansionLogo.gameObject.SetActive( true );
				expansionLogo.sprite = expansionSprites[(int)Enum.Parse( typeof( Expansion ), sagaCampaign.campaignExpansionCode )];
			}
			else if ( sagaCampaign.campaignType == CampaignType.Custom )
			{
				campaignExpansion.text = DataStore.uiLanguage.uiCampaign.customCampaign;
				//expansion icon
				expansionLogo.gameObject.SetActive( true );
				expansionLogo.sprite = expansionSprites[8];//rebel icon
			}
			else if ( sagaCampaign.campaignType == CampaignType.Imported )
			{
				var packages = FileManager.GetCampaignPackageList( false );
				var p = packages.Where( x => x.GUID == sagaCampaign.campaignPackage.GUID ).FirstOr( null );
				if ( p != null )
				{
					//expansion icon
					Texture2D tex = new Texture2D( 2, 2 );
					if ( tex.LoadImage( p.iconBytesBuffer ) )
					{
						expansionLogo.gameObject.SetActive( true );
						Sprite iconSprite = Sprite.Create( tex, new Rect( 0, 0, tex.width, tex.height ), new Vector2( 0, 0 ), 100f );
						expansionLogo.sprite = iconSprite;
					}

					//checking if campaign/missions names should be updated to current language
					var currentLanguageIntro = p.campaignStructure.Where( x => x.missionType == MissionType.Introduction ).FirstOr( null );
					var loadedIntro = sagaCampaign.campaignStructure.Where( x => x.missionType == MissionType.Introduction ).FirstOr( null );
					if ( currentLanguageIntro != null && loadedIntro != null && currentLanguageIntro.projectItem.Title != loadedIntro.projectItem.Title )
					{
						sagaCampaign.campaignImportedName = p.campaignName;

						foreach ( var item in sagaCampaign.campaignStructure )
						{
							var currentLanguageItem = p.campaignMissionItems.Where( x => x.missionGUID.ToString() == item.missionID ).FirstOr( null );
							if ( currentLanguageItem != null )
							{
								item.projectItem.Title = currentLanguageItem.missionName;
								item.projectItem.Description = currentLanguageItem.mission.missionProperties.missionDescription;
								item.projectItem.AdditionalInfo = currentLanguageItem.mission.missionProperties.additionalMissionInfo;
							}
						}

						foreach ( var item in sagaCampaign.campaignPackage.campaignMissionItems )
						{
							var currentLanguageItem = p.campaignMissionItems.Where( x => x.missionGUID == item.missionGUID ).FirstOr( null );
							if ( currentLanguageItem != null )
							{
								item.missionName = currentLanguageItem.missionName;
							}
						}
					}
					else
					{
						var introMissionItem = p.campaignMissionItems.Where( x => x.missionGUID.ToString() == loadedIntro.missionID ).FirstOr( null );
						if ( introMissionItem != null )
						{
							loadedIntro.projectItem.Description = introMissionItem.mission.missionProperties.missionDescription;
							loadedIntro.projectItem.AdditionalInfo = introMissionItem.mission.missionProperties.additionalMissionInfo;
						}
					}
				}
				else
					Utils.LogError( $"CampaignManager::InitUI()::The original Campaign Package is either missing or was manipulated, and no longer exists in its original form.\nCampaign Name: [{sagaCampaign.campaignImportedName}], GUID= {sagaCampaign.campaignPackage.GUID}" );

				campaignExpansion.text = sagaCampaign.campaignImportedName;
			}

			creditsWheel.ResetWheeler( sagaCampaign.credits );
			xpWheel.ResetWheeler( sagaCampaign.XP );
			fameWheel.ResetWheeler( sagaCampaign.fame );
			awardsWheel.ResetWheeler( sagaCampaign.awards );

			//items
			Dictionary<string, string> items = new Dictionary<string, string>();
			foreach ( var campaignItem in sagaCampaign.campaignItems )
			{
				if ( SagaCampaign.campaignDataItems.Any( x => x.id == campaignItem ) )
					items.Add( campaignItem, SagaCampaign.campaignDataItems.First( x => x.id == campaignItem ).name );
			}
			foreach ( var item in items.OrderBy( x => x.Value ) )
				AddItemToUI( sagaCampaign.GetItemFromID( item.Key ) );
			//allies
			foreach ( var ally in sagaCampaign.campaignAllies.OrderBy( x => x.name ) )
				AddAllyToUI( ally );
			//villains
			foreach ( var villain in sagaCampaign.campaignVillains.OrderBy( x => x.name ) )
				AddVillainToUI( villain );
			//heroes
			int c = sagaCampaign.campaignHeroes.Count;
			for ( int i = 0; i < c; i++ )
			{
				heroPrefabs[i].AddHeroToUI( sagaCampaign.campaignHeroes[i] );
				heroPrefabs[i].mWheelHandler.valueAdjuster = valueAdjuster;
			}
			//rewards
			Dictionary<string, string> rewards = new Dictionary<string, string>();
			foreach ( var campaignReward in sagaCampaign.campaignRewards )
			{
				if ( SagaCampaign.campaignDataRewards.Any( x => x.id == campaignReward ) )
					rewards.Add( campaignReward, SagaCampaign.campaignDataRewards.First( x => x.id == campaignReward ).name );
			}
			foreach ( var reward in rewards.OrderBy( x => x.Value ) )
				AddRewardToUI( sagaCampaign.GetRewardFromID( reward.Key ) );
			//campaign structure
			foreach ( Transform item in structureContainer )
				Destroy( item.gameObject );
			//if it's a custom campaign, add the custom add mission bar
			if ( sagaCampaign.campaignType == CampaignType.Custom )
			{
				var cgo = Instantiate( customAddMissionBarPrefab, structureContainer );
			}
			//add campaign structure
			foreach ( var item in sagaCampaign.campaignStructure )
			{
				var go = Instantiate( missionItemPrefab, structureContainer );
				go.GetComponent<MissionItemPrefab>().Init( item, valueAdjuster, sagaCampaign );
			}
			//add forced mission bar
			if ( sagaCampaign.campaignExpansionCode != "Custom" && sagaCampaign.campaignExpansionCode != "Imported" )
			{
				//add 1 force mission item
				var fgo = Instantiate( forceMissionItemPrefab, structureContainer );
				fgo.GetComponent<ForceMissionItemPrefab>().Init( OnAddForcedMissionClick );
			}
		}

		#region UI callbacks from "AddCampaignItemPopup" window
		void AddAllyToUI( DeploymentCard a )
		{
			if ( a == null )
			{
				Utils.LogError( $"AddAllyToUI()::card is null" );
				return;
			}

			var index = sagaCampaign.campaignAllies.OrderBy( x => x.name ).ToList().FindIndex( x => x.name == a.name );

			var go = Instantiate( listItemPrefab, allyContainer );

			if ( index < allyContainer.childCount )
				go.transform.SetSiblingIndex( index );

			go.GetComponent<CampaignListItemPrefab>().InitAlly( a.name, ( n ) =>
			{
				sagaCampaign.campaignAllies.Remove( a );
				Destroy( go );
			} );
		}

		void AddVillainToUI( DeploymentCard v )
		{
			if ( v == null )
			{
				Utils.LogError( $"AddVillainToUI()::card is null" );
				return;
			}

			var index = sagaCampaign.campaignVillains.OrderBy( x => x.name ).ToList().FindIndex( x => x.name == v.name );

			var go = Instantiate( listItemPrefab, villainContainer );

			if ( index < villainContainer.childCount )
				go.transform.SetSiblingIndex( index );

			go.GetComponent<CampaignListItemPrefab>().InitVillain( v.name, ( n ) =>
			{
				sagaCampaign.campaignVillains.Remove( v );
				Destroy( go );
			} );
		}

		void AddItemToUI( CampaignItem item, bool showCoinIcon = false )
		{
			if ( item == null )
			{
				Utils.LogWarning( $"AddItemToUI()::item is null" );
				return;
			}

			List<string> items = new List<string>();
			foreach ( var campaignItem in sagaCampaign.campaignItems )
			{
				if ( SagaCampaign.campaignDataItems.Any( x => x.id == campaignItem ) )
					items.Add( SagaCampaign.campaignDataItems.First( x => x.id == campaignItem ).name );
			}

			var index = items.OrderBy( x => x ).ToList().FindIndex( x => x == item.name );

			var go = Instantiate( listItemPrefab, itemContainer );

			if ( index < itemContainer.childCount )
				go.transform.SetSiblingIndex( index );

			go.GetComponent<CampaignListItemPrefab>().InitGeneralItem( item.name, ( n ) =>
			{
				//add item cost back to credits
				var sc = FindObjectOfType<CampaignManager>();
				sc.UpdateCreditsUI( RunningCampaign.sagaCampaign.credits + item.cost );
				sc.sound.PlaySound( 1 );

				//remove item from hero items as well
				foreach ( var hero in sagaCampaign.campaignHeroes )
				{
					if ( hero.campaignItems.Any( x => x.id == item.id ) )
					{
						hero.campaignItems.Remove( hero.campaignItems.Where( x => x.id == item.id ).First() );

						int c = sagaCampaign.campaignHeroes.Count;
						for ( int i = 0; i < c; i++ )
						{
							if ( sagaCampaign.campaignHeroes[i].heroID == hero.heroID )
								heroPrefabs[i].AddHeroToUI( sagaCampaign.campaignHeroes[i] );
						}
					}
				}

				sagaCampaign.campaignItems.Remove( item.id );
				Destroy( go );
			} );
		}

		void AddRewardToUI( CampaignReward item )
		{
			if ( item == null )
			{
				Utils.LogWarning( $"AddRewardToUI()::item is null" );
				return;
			}

			List<string> rewards = new List<string>();
			foreach ( var campaignReward in sagaCampaign.campaignRewards )
			{
				if ( SagaCampaign.campaignDataRewards.Any( x => x.id == campaignReward ) )
					rewards.Add( SagaCampaign.campaignDataRewards.First( x => x.id == campaignReward ).name );
			}

			var index = rewards.OrderBy( x => x ).ToList().FindIndex( x => x == item.name );

			var go = Instantiate( listItemPrefab, rewardContainer );

			if ( index < rewardContainer.childCount )
				go.transform.SetSiblingIndex( index );

			go.GetComponent<CampaignListItemPrefab>().InitGeneralItem( item.name, ( n ) =>
			{
				sagaCampaign.campaignRewards.Remove( item.id );
				Destroy( go );
			} );
		}

		public void OnXPChanged()
		{
			sagaCampaign.XP = xpWheel.wheelValue;
		}

		public void OnCreditsChanged()
		{
			sagaCampaign.credits = creditsWheel.wheelValue;
		}

		public void OnFameChanged()
		{
			sagaCampaign.fame = fameWheel.wheelValue;
		}

		public void OnAwardsChanged()
		{
			sagaCampaign.awards = awardsWheel.wheelValue;
		}
		#endregion

		#region ADD BUTTONS
		public void OnAddAlly()
		{
			addCampaignItemPopup.AddAlly( ( a ) =>
			{
				if ( !sagaCampaign.campaignAllies.Contains( a ) )
				{
					sagaCampaign.campaignAllies.Add( a );
					AddAllyToUI( a );
				}
			} );
		}

		public void OnAddVillain()
		{
			addCampaignItemPopup.AddVillain( ( v ) =>
			{
				if ( !sagaCampaign.campaignVillains.Contains( v ) )
				{
					sagaCampaign.campaignVillains.Add( v );
					AddVillainToUI( v );
				}
			} );
		}

		public void OnAddItem()
		{
			addCampaignItemPopup.AddItem( ( item ) =>
			{
				if ( !sagaCampaign.campaignItems.Contains( item.id ) )
				{
					sagaCampaign.campaignItems.Add( item.id );
					AddItemToUI( item );
				}
			}, false );
		}

		public void OnAddReward()
		{
			addCampaignItemPopup.AddReward( ( item ) =>
			{
				if ( !sagaCampaign.campaignRewards.Contains( item.id ) )
				{
					sagaCampaign.campaignRewards.Add( item.id );
					AddRewardToUI( item );
				}
			} );
		}
		#endregion

		public void AddHeroToCampaign( CampaignHero hero )
		{
			sagaCampaign.campaignHeroes.Add( hero );
		}

		public void RemoveHeroFromCampaign( CampaignHero hero )
		{
			sagaCampaign.campaignHeroes.Remove( hero );
		}

		public void OnAddForcedMissionClick()
		{
			addCampaignItemPopup.AddMission( sagaCampaign.campaignType, sagaCampaign.campaignExpansionCode, MissionType.Forced, AddForcedMission );
		}

		public void OnMissionNameClick( MissionType missionType, Action<MissionCard> callback )
		{
			addCampaignItemPopup.AddMission( sagaCampaign.campaignType, sagaCampaign.campaignExpansionCode, missionType, callback );
		}

		public void OnAddCustomMission( MissionType missionType )
		{
			modifyCustomPropsPopup.Show( new CampaignModify()
			{
				missionType = missionType,
				threatValue = 1,
				itemTierArray = new string[] { "1" },
				missionID = "",
				expansionCode = ""
			}, AddCustomMission );
		}

		public void OnModifyCustomMission( MissionItemPrefab missionItem )
		{
			CampaignModify modifier = new CampaignModify()
			{
				missionType = missionItem.campaignStructure.missionType,
				threatValue = missionItem.campaignStructure.threatLevel,
				itemTierArray = missionItem.campaignStructure.itemTier,
				agendaToggle = missionItem.campaignStructure.isAgendaMission,
				missionID = missionItem.campaignStructure.missionID,
				expansionCode = missionItem.campaignStructure.expansionCode
			};

			modifyCustomPropsPopup.Show( modifier, ( mod ) =>
			{
				missionItem.OnModify( mod );
				//go back to normal campaign list mode
				FindObjectOfType<CustomAddMissionBarPrefab>().DeactivateModifyMode();
			} );
		}

		void AddCustomMission( CampaignModify modifier )
		{
			var cs = new CampaignStructure()
			{
				missionType = modifier.missionType,
				missionID = modifier.missionID,
				threatLevel = modifier.threatValue,
				itemTier = modifier.itemTierArray,
				expansionCode = modifier.expansionCode,
				isAgendaMission = modifier.agendaToggle,
				missionSource = MissionSource.Custom
			};
			//add the newly added mission
			var go = Instantiate( missionItemPrefab, structureContainer );
			go.GetComponent<MissionItemPrefab>().Init( cs, valueAdjuster, sagaCampaign );
		}

		void AddForcedMission( MissionCard card )
		{
			//custom missions:
			//full path to custom mission is stored in 'hero'
			//additional info is stored in 'bonusText'

			string path = "";
			if ( card.id == "Custom" )
				path = card.hero;
			else
				path = $"{card.id.ToUpper()}-{card.name}";

			var cs = new CampaignStructure()
			{
				missionType = MissionType.Forced,
				missionID = card.id,
				threatLevel = 2,//default because of removal of preset dependence
				isForced = true,
				expansionCode = card.expansion.ToString(),
				missionSource = card.id == "Custom" ? MissionSource.Custom : MissionSource.Official,
				projectItem = new ProjectItem()
				{
					missionID = card.id,
					Description = card.descriptionText,
					AdditionalInfo = card.bonusText,
					fullPathWithFilename = path,
					Title = DataStore.missionCards[card.expansion.ToString()].Where( x => x.id == card.id ).FirstOr( new MissionCard() { name = card.name } )?.name
				}
			};
			//remove the "add forced mission" prefab
			foreach ( Transform item in structureContainer )
			{
				if ( item.GetComponent<ForceMissionItemPrefab>() != null )
					Destroy( item.gameObject );
			}
			//add the newly added mission
			var go = Instantiate( missionItemPrefab, structureContainer );
			go.GetComponent<MissionItemPrefab>().Init( cs, valueAdjuster, sagaCampaign );
			//add the "forced mission" prefab back to bottom of list
			var fgo = Instantiate( forceMissionItemPrefab, structureContainer );
			fgo.GetComponent<ForceMissionItemPrefab>().Init( OnAddForcedMissionClick );
		}

		public void RemoveMission( Guid GUID )
		{
			foreach ( Transform item in structureContainer )
			{
				var pf = item.GetComponent<MissionItemPrefab>();
				if ( pf != null )
				{
					if ( pf.campaignStructure.missionSource == MissionSource.Custom )
						FindObjectOfType<CustomAddMissionBarPrefab>()?.DeactivateModifyMode();
					if ( pf.campaignStructure.structureGUID == GUID )
						Destroy( item.gameObject );
				}
			}
		}

		public void ToggleDisableUI( bool disable )
		{
			topButtonsGroup.interactable = rightPanelGroup.interactable = disable;
			foreach ( Transform item in structureContainer )
			{
				var prefab = item.GetComponent<MissionItemPrefab>();
				if ( prefab != null )
				{
					prefab.ToggleModifyMode( !disable );
				}
			}
		}

		public void OnSaveCampaign()
		{
			var list = new List<CampaignStructure>();
			foreach ( Transform item in structureContainer )
			{
				var cs = item.GetComponent<MissionItemPrefab>();
				if ( cs != null )
					list.Add( cs.campaignStructure );
			}

			sagaCampaign.SaveCampaignState( list );
		}

		public void OnInfoClicked()
		{
			GlowEngine.FindUnityObject<CampaignMessagePopup>().Show( DataStore.uiLanguage.uiCampaign.campaignSetup, Utils.ReplaceGlyphs( sagaCampaign.GetCampaignInfo() ), 700 );
		}

		public void OnJournalClicked()
		{
			campaignJournal.Show( sagaCampaign.campaignJournal, ( text ) => sagaCampaign.campaignJournal = text );
		}

		public void OnSwitchViewClicked()
		{
			view = view == 0 ? 1 : 0;
			leftPanel.SetActive( view == 0 );
			rightPanel.SetActive( view == 1 );
			campaignHelpMockup.SetActive( view == 0 );
		}

		public void OnExitCampaignScreen()
		{
			//auto-save
			OnSaveCampaign();

			RunningCampaign.Reset();

			fader.Fade();
			float foo = 1;
			DOTween.To( () => foo, x => foo = x, 0, .5f ).OnComplete( () =>
			 SceneManager.LoadScene( "Title" ) );
		}

		public void OnEndEditCampaignName()
		{
			sagaCampaign.campaignName = campaignNameInputField.text;
		}

		public void StartMission( CampaignStructure campaignStructure )
		{
			//auto-save
			OnSaveCampaign();

			RunningCampaign.campaignStructure = campaignStructure;
			RunningCampaign.sagaCampaignGUID = sagaCampaign.GUID;

			fader.Fade();
			float foo = 1;
			DOTween.To( () => foo, x => foo = x, 0, .5f ).OnComplete( () =>
			 SceneManager.LoadScene( "SagaSetup" ) );
		}

		public void OnHelpClick()
		{
			helpPanel.Show();
		}

		public void UpdateCreditsUI( int newValue )
		{
			sagaCampaign.credits = newValue;
			creditsWheel.ResetWheeler( sagaCampaign.credits );
		}
	}
}