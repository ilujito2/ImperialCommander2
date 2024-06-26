﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Saga
{
	public class MissionPicker : MonoBehaviour
	{
		public GameObject missionItemPrefab, folderItemPrefab;
		public Transform missionContainer;
		//UI
		public TextMeshProUGUI missionNameText, missionDescriptionText, additionalInfoText;
		public Button tilesButton, modeToggleButton;
		public TileInfoPopup tileInfoPopup;
		public Text modeToggleBtnText;
		public CanvasGroup canvasGroup;
		public Transform busyIcon;
		public GameObject busyPanel;

		[HideInInspector]
		public ProjectItem selectedMission;
		[HideInInspector]
		public bool isBusy = false;
		[HideInInspector]
		public PickerMode pickerMode { get; set; }

		string basePath;
		ProjectItem[] projectItems;
		ToggleGroup toggleGroup;
		List<string> expansionsAvailable = new List<string>();
		Stack<string> folderBreadcrumbs = new Stack<string>();

		private void Start()
		{
			pickerMode = PickerMode.BuiltIn;
			selectedMission = null;
			toggleGroup = missionContainer.GetComponent<ToggleGroup>();

			if ( !FileManager.CreateFolder( FileManager.customMissionPath ) )
			{
				Utils.LogError( "Custom Mission folder doesn't exist.\r\nTried to create: " + FileManager.customMissionPath );
				FindObjectOfType<SagaSetup>()?.errorPanel?.Show( $"Custom Mission folder doesn't exist.\nTried to create: {FileManager.customMissionPath}" );
			}

			busyIcon.DORotate( new Vector3( 0, 0, 360 ), 1f, RotateMode.FastBeyond360 ).SetEase( Ease.InOutQuad ).SetLoops( -1 );

			expansionsAvailable.AddRange( new string[] { "Core", "Twin", "Hoth", "Bespin", "Jabba", "Empire", "Lothal", "Other" } );

			basePath = "BuiltIn";
			OnChangeBuiltinFolder( basePath );
		}

		/// <summary>
		/// Change folder for CUSTOM mission mode
		/// </summary>
		public void OnChangeFolder( string path )
		{
			selectedMission = null;
			OnMissionSelected( null );
			if ( !folderBreadcrumbs.Contains( path ) )
				folderBreadcrumbs.Push( path );
			//populate mission picker items
			for ( int i = 1; i < missionContainer.childCount; i++ )
			{
				//get rid of all items except the UP FOLDER item
				Destroy( missionContainer.GetChild( i ).gameObject );
			}

			//disable UP folder if we're at the root
			if ( FileManager.customMissionPath == folderBreadcrumbs.Peek() )
				missionContainer.GetChild( 0 ).gameObject.SetActive( false );
			else
				missionContainer.GetChild( 0 ).gameObject.SetActive( true );

			//add folders first
			if ( Directory.Exists( folderBreadcrumbs.Peek() ) )
			{
				var di = new DirectoryInfo( folderBreadcrumbs.Peek() );
				var folders = di.GetDirectories();
				try
				{
					foreach ( var folder in folders )
					{
						var fi = Instantiate( folderItemPrefab, missionContainer );
						fi.GetComponent<MissionPickerFolder>().Init( folder );
					}

					//then add files
					projectItems = FileManager.GetProjects( folderBreadcrumbs.Peek() ).ToArray();
					//sort alphabetically
					projectItems = projectItems.Where( x => x.missionGUID != null ).OrderBy( x => x.Title ).ToArray();
					bool first = true;
					foreach ( var item in projectItems )
					{
						var picker = Instantiate( missionItemPrefab, missionContainer );
						var pi = picker.GetComponent<MissionPickerItem>();
						pi.GetComponent<Toggle>().group = toggleGroup;
						pi.Init( item, first, PickerMode.Custom );
						first = false;
					}

					if ( projectItems.Length > 0 )
					{
						selectedMission = projectItems[0];
						OnMissionSelected( selectedMission );
					}
				}
				catch ( Exception e )
				{
					Utils.LogError( $"OnChangeFolder()::Error iterating over '{path}'" );
					FindObjectOfType<SagaSetup>().errorPanel.Show( "OnChangeFolder()", $"Error iterating over '{path}'\n\n{e.Message}" );
				}
			}
		}

		public void OnChangeBuiltinFolder( string expansion )
		{
			selectedMission = null;
			OnMissionSelected( null );
			folderBreadcrumbs.Push( (expansion.ToString()) );

			//populate mission picker items
			for ( int i = 1; i < missionContainer.childCount; i++ )
			{
				//get rid of all items except the UP FOLDER item
				Destroy( missionContainer.GetChild( i ).gameObject );
			}
			//disable UP folder if we're at the root
			if ( basePath == folderBreadcrumbs.Peek() )
				missionContainer.GetChild( 0 ).gameObject.SetActive( false );
			else
				missionContainer.GetChild( 0 ).gameObject.SetActive( true );

			//if this is the top, add root level folders
			if ( folderBreadcrumbs.Peek() == "BuiltIn" )
			{
				foreach ( var folder in expansionsAvailable )
				{
					var fi = Instantiate( folderItemPrefab, missionContainer );
					fi.GetComponent<MissionPickerFolder>().InitBuiltin( folder.ToString() );
				}
			}
			else//otherwise we're in a built-in subfolder, so populate with missions
			{
				StartCoroutine( CreateBuiltInPickers( expansion ) );
			}
		}

		private List<string> getMissionNames( string expansion ) => expansion switch
		{
			//"Core", "Twin", "Hoth", "Bespin", "Jabba", "Empire", "Lothal", "Other"
			"Core" => Enumerable.Range( 1, 32 ).Select( i => $"{expansion}{i}" ).ToList(),
			"Twin" => Enumerable.Range( 1, 6 ).Select( i => $"{expansion}{i}" ).ToList(),
			"Hoth" => Enumerable.Range( 1, 16 ).Select( i => $"{expansion}{i}" ).ToList(),
			"Bespin" => Enumerable.Range( 1, 6 ).Select( i => $"{expansion}{i}" ).ToList(),
			"Jabba" => Enumerable.Range( 1, 16 ).Select( i => $"{expansion}{i}" ).ToList(),
			"Empire" => Enumerable.Range( 1, 16 ).Select( i => $"{expansion}{i}" ).ToList(),
			"Lothal" => Enumerable.Range( 1, 6 ).Select( i => $"{expansion}{i}" ).ToList(),
			"Other" => Enumerable.Range( 1, 40 ).Select( i => $"{expansion}{i}" ).ToList(),
			_ => new List<string>()
		};

		IEnumerator CreateBuiltInPickers( string expansion )
		{
			isBusy = true;
			//Dictionary of <asset name (CORE1, JABBA4, etc), stringified mission json>
			Dictionary<string, string> assetList = new Dictionary<string, string>();
			List<ProjectItem> piList = new List<ProjectItem>();
			List<string> missionNames = getMissionNames( expansion );
			//asset name (CORE1, JABBA4), bool if found translation
			Dictionary<string, bool> foundTranslations = new Dictionary<string, bool>();

			foreach ( var item in missionNames )
			{
				//stringified Mission
				ResourceRequest loadedMission = null;
				ResourceRequest translation = null;

				try
				{
					loadedMission = Resources.LoadAsync<TextAsset>( $"SagaMissions/{expansion}/{item}" );
					//check if the Mission has a translation
					translation = Resources.LoadAsync<TextAsset>( $"Languages/{DataStore.Language}/Missions/{expansion}/{item}_{DataStore.Language}" );
				}
				catch ( Exception e )
				{
					Utils.LogError( $"CreateBuiltInPickers()::Error loading Mission from Resources\n{e.Message}" );
					isBusy = false;
					yield break;
				}

				while ( !loadedMission.isDone )
					yield return null;
				while ( !translation.isDone )
					yield return null;

				if ( translation?.asset != null )
					foundTranslations.Add( item, true );
				else
					foundTranslations.Add( item, false );

				if ( loadedMission.asset != null )
					assetList.Add( item, ((TextAsset)loadedMission.asset).text );
				else
				{
					Utils.LogError( $"CreateBuiltInPickers()::Loaded Mission is null" );
					yield break;
				}
			}

			try
			{
				//create project items from mission list
				//the Keys are CORE1, JABBA4, etc
				foreach ( var item in assetList.Keys )
				{
					var projectItem = FileManager.CreateProjectItem( assetList[item], item, item );
					projectItem.hasTranslation = foundTranslations[item];
					projectItem.expansion = expansion;

					//process auto-description and TRANSLATED title for known missions
					if ( projectItem != null && projectItem.missionID != "Custom" )
					{
						string[] id = projectItem.missionID.Split( ' ' );
						var card = DataStore.missionCards[id[0]].Where( x => x.id == $"{id[0]}{id[1]}" ).FirstOr( null );
						if ( card != null )
							projectItem.Description = card.descriptionText;
						else
							projectItem.Description = "Error parsing description.";

						//for built-in missions, get TRANSLATED mission title also
						if ( pickerMode == PickerMode.BuiltIn )
						{
							if ( card != null )
								projectItem.Title = card.name;
						}
					}

					piList.Add( projectItem );
				}
			}
			catch ( Exception e )
			{
				FindObjectOfType<SagaSetup>().errorPanel.Show( "CreateBuiltInPickers()", $"Error creating in CreateProjectItem():\n{e.Message}" );
				isBusy = false;
				yield break;
			}

			//sort the pi list
			piList.Sort( ( ProjectItem i1, ProjectItem i2 ) =>
			{
				//Debug.Log( $"SORTING::{i1.Title}.......{i2.Title}" );
				if ( i1.missionID != "Custom" && i2.missionID != "Custom" )
				{
					int n1 = int.Parse( i1.missionID.Split( ' ' )[1] );
					int n2 = int.Parse( i2.missionID.Split( ' ' )[1] );
					if ( n1 < n2 )
						return -1;
					else if ( n1 > n2 )
						return 1;
					else
						return 0;
				}
				else
				{
					Debug.Log( $"ERROR PARSING MISSION::i1={i1.missionID}, i2={i2.missionID}" );
					return 1;
				}
			} );

			//instantiate each Mission picker button into the panel
			bool first = true;
			foreach ( var item in piList )
			{
				var picker = Instantiate( missionItemPrefab, missionContainer );
				var pi = picker.GetComponent<MissionPickerItem>();
				pi.GetComponent<Toggle>().group = toggleGroup;
				pi.Init( item, first, PickerMode.BuiltIn );
				first = false;
			}

			isBusy = false;
		}

		public void OnChangeMode()
		{
			folderBreadcrumbs.Clear();
			//clear any custom mission allies/heroes
			FindObjectOfType<SagaSetup>().missionCustomHeroes.Clear();
			FindObjectOfType<SagaSetup>().missionCustomAllies.Clear();

			if ( pickerMode == PickerMode.Custom )
			{
				pickerMode = PickerMode.BuiltIn;
				modeToggleBtnText.text = DataStore.uiLanguage.sagaUISetup.officialBtn;
				basePath = "BuiltIn";
				OnChangeBuiltinFolder( basePath );
			}
			else
			{
				pickerMode = PickerMode.Custom;
				modeToggleBtnText.text = DataStore.uiLanguage.sagaUISetup.customBtn;
				folderBreadcrumbs.Clear();
				OnChangeFolder( FileManager.customMissionPath );
			}

			FindObjectOfType<SagaSetup>().OnModeChange( pickerMode );
		}

		public void OnMissionSelected( ProjectItem pi )
		{
			EventSystem.current.SetSelectedGameObject( null );
			if ( pi != null )
			{
				selectedMission = pi;
				missionNameText.text = pi?.Title;
				missionDescriptionText.text = pi?.Description;
				additionalInfoText.text = pi?.AdditionalInfo;
				if ( pickerMode == PickerMode.BuiltIn )//official mission
				{
					FindObjectOfType<SagaSetup>().OnOfficialMissionSelected( pi );
				}
				else//custom mission
					FindObjectOfType<SagaSetup>().OnCustomMissionSelected( pi );
			}
			else
			{
				selectedMission = null;
				missionNameText.text = "";
				missionDescriptionText.text = "";
				additionalInfoText.text = "";
			}
		}

		public void OnFolderUp()
		{
			EventSystem.current.SetSelectedGameObject( null );
			OnMissionSelected( null );
			if ( pickerMode == PickerMode.Custom )
			{
				folderBreadcrumbs.Pop();
				OnChangeFolder( folderBreadcrumbs.Peek() );
			}
			else
				OnChangeBuiltinFolder( "BuiltIn" );
		}

		public void OnTiles()
		{
			Debug.Log( $"OnTiles()::Mode: {pickerMode}" );

			EventSystem.current.SetSelectedGameObject( null );
			Mission m = null;
			Action doneAction = () =>
			{
				List<string> tiles = new List<string>();
				if ( m != null )
				{
					foreach ( var section in m.mapSections )
					{
						foreach ( var tile in section.mapTiles )
							tiles.Add( tile.expansion + " " + tile.tileID );
					}
					tileInfoPopup.Show( tiles.ToArray() );
				}
			};

			if ( pickerMode == PickerMode.Custom )
			{
				m = FileManager.LoadMission( selectedMission.fullPathWithFilename );
				if ( m != null )
					doneAction();
				else
					FindObjectOfType<SagaSetup>().errorPanel.Show( "OnTiles()", $"Mission (Custom) is null\n'{selectedMission.fullPathWithFilename}'" );
			}
			else if ( pickerMode == PickerMode.Embedded )
			{
				var structure = RunningCampaign.campaignStructure;
				m = FileManager.LoadEmbeddedMission( structure.packageGUID.ToString(), structure.projectItem.missionGUID, out DataStore.sagaSessionData.missionStringified );
				if ( m != null )
					doneAction();
				else
					FindObjectOfType<SagaSetup>().errorPanel.Show( "OnTiles()", $"Mission (Embedded) is null\n'{selectedMission.fullPathWithFilename}'" );
			}
			else if ( pickerMode == PickerMode.BuiltIn )//official
			{
				isBusy = true;
				try
				{
					m = FileManager.LoadMissionFromResource( selectedMission.fullPathWithFilename, out string stringified );
					if ( m != null )
						doneAction();
					else
						FindObjectOfType<SagaSetup>().errorPanel.Show( "OnTiles()", $"Mission (Official) is null\n'{selectedMission.fullPathWithFilename}'" );
					isBusy = false;
				}
				catch ( Exception e )
				{
					FindObjectOfType<SagaSetup>().errorPanel.Show( $"OnTiles() :: '{selectedMission.fullPathWithFilename}'", e );
					isBusy = false;
				}
			}
		}

		private void Update()
		{
			tilesButton.interactable = selectedMission != null && !isBusy;
			modeToggleButton.interactable = !isBusy;
			canvasGroup.interactable = !isBusy;
			busyPanel.SetActive( isBusy );
		}
	}
}