﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Saga
{
	public class FileManager
	{
		public static string baseDocumentFolder, campaignPath, customMissionPath, importedCharactersPath, classicSessionPath, sagaSessionPath, campaignSessionPath, classicDefaultsPath, customCampaignPath;
		public static List<CampaignPackage> importedCampaigns = new List<CampaignPackage>();

		/// <summary>
		/// Creates default folders and sets up folder path properties
		/// </summary>
		public static bool SetupDefaultFolders()
		{
			Debug.Log( "SETTING UP DEFAULT FOLDERS" );
			//Application.persistentDataPath for different OSes
			//Android: storage/emulated/<userid>/Android/data/com.GlowPuff.ImperialCommander2/files
			//MacOS: ~/Library/Application Support/GlowPuff/Imperial Commander 2
			//Windows: %localappdata%low\GlowPuff\Imperial Commander 2
			//Linux: $XDG_CONFIG_HOME/unity3d or $HOME/.config/unity3d

			campaignPath = Path.Combine( Application.persistentDataPath, "SagaCampaigns" );
			classicSessionPath = Path.Combine( Application.persistentDataPath, "Session" );
			sagaSessionPath = Path.Combine( Application.persistentDataPath, "SagaSession" );
			campaignSessionPath = Path.Combine( Application.persistentDataPath, "CampaignSession" );
			classicDefaultsPath = Path.Combine( Application.persistentDataPath, "Defaults" );

			//OS-specific paths that are NOT 'persistentDataPath', except on Android
#if UNITY_ANDROID
			baseDocumentFolder = Application.persistentDataPath;
			customMissionPath = Path.Combine( Application.persistentDataPath, "CustomMissions" );
			importedCharactersPath = Path.Combine( Application.persistentDataPath, "ImportedCharacters" );
			customCampaignPath = Path.Combine( Application.persistentDataPath, "CustomCampaigns" );
#else
			baseDocumentFolder = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), "ImperialCommander" );
			customMissionPath = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), "ImperialCommander", "CustomMissions" );
			importedCharactersPath = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), "ImperialCommander", "ImportedCharacters" );
			customCampaignPath = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), "ImperialCommander", "CustomCampaigns" );
#endif

			try
			{
#if !UNITY_ANDROID
				CreateFolder( baseDocumentFolder );
#endif
				CreateFolder( campaignPath );
				CreateFolder( classicSessionPath );
				CreateFolder( sagaSessionPath );
				CreateFolder( campaignSessionPath );
				CreateFolder( classicDefaultsPath );
				CreateFolder( customMissionPath );
				CreateFolder( importedCharactersPath );
				CreateFolder( customCampaignPath );

				return true;
			}
			catch ( Exception e )
			{
				Utils.LogError( "CreateFolders()::Could not create default folders.\n" + e.Message );
				return false;
			}
		}

		/// <summary>
		/// If the folder doesn't exist, create it
		/// </summary>
		public static bool CreateFolder( string path )
		{
			try
			{
				if ( !Directory.Exists( path ) )
					Directory.CreateDirectory( path );
				return true;
			}
			catch ( Exception e )
			{
				Utils.LogError( $"CreateFolder()::Failed to create path:\n{path}\n{e.Message}" );
				return false;
			}
		}

		public static Mission LoadMissionFromString( string json )
		{
			//make sure it's a mission, simple check for a property in the text
			if ( !json.Contains( "missionGUID" ) )
				return null;

			try
			{
				var m = JsonConvert.DeserializeObject<Mission>( json );
				//Debug.Log( "LoadMissionFromString: " + m.missionProperties.missionID );
				return m;
			}
			catch ( Exception e )
			{
				Utils.LogError( "LoadMissionFromString()::Could not load the Mission. Exception: " + e.Message );
				return null;
			}
		}

		/// <summary>
		/// Loads a mission from its FULL PATH .json
		/// </summary>
		public static Mission LoadMission( string filename, out string missionStringified )
		{
			string json = "";

			try
			{
				using ( StreamReader sr = new StreamReader( filename ) )
				{
					json = sr.ReadToEnd();
				}

				missionStringified = json;
				var m = JsonConvert.DeserializeObject<Mission>( json );
				//overwrite fileName so it's up-to-date
				FileInfo fi = new FileInfo( filename );
				m.fileName = fi.Name;
				Debug.Log( "Loaded Mission: " + m.fileName );
				return m;
			}
			catch ( Exception e )
			{
				Utils.LogError( "LoadMission()::Could not load the Mission. Exception: " + e.Message );
				missionStringified = null;
				return null;
			}
		}

		/// <summary>
		/// Loads a mission from its FULL PATH .json, ignoring the stringified version
		/// </summary>
		public static Mission LoadMission( string filename )
		{
			return LoadMission( filename, out string foo );
		}

		/// <summary>
		/// Load a baseline control Mission from Unity Resources, also return stringified version
		/// </summary>
		public static Mission LoadMissionFromResource( string missionID, out string stringified )
		{
			if ( string.IsNullOrEmpty( missionID ) )
			{
				Utils.LogError( "LoadMissionFromResource()::'missionID' is null" );
				stringified = "";
				return null;
			}

			stringified = "";

			try
			{
				if ( !missionID.Contains( ' ' ) )
					missionID = Utils.AddSpaceToMissionID( missionID );

				string[] resName = missionID.Split( ' ' );
				string mText = "";

				mText = Resources.Load<TextAsset>( $"SagaMissions/{resName[0]}/{resName[0]}{resName[1]}" ).text;

				var m = JsonConvert.DeserializeObject<Mission>( mText );
				stringified = mText;

				Debug.Log( "Loaded Mission: " + m.fileName );
				return m;
			}
			catch ( Exception e )
			{
				Utils.LogError( "LoadMissionFromResource()::Could not load the Mission. Exception: " + e.Message );
				return null;
			}
		}

		/// <summary>
		/// Only collect campaigns with formatVersion == Utils.expectedCampaignFormatVersion
		/// </summary>
		public static List<SagaCampaign> GetCampaigns()
		{
			List<SagaCampaign> clist = new List<SagaCampaign>();

			try
			{
				string json = "";
				List<string> filenames = Directory.GetFiles( campaignPath ).Where( x =>
				{
					FileInfo fi = new FileInfo( x );
					return fi.Extension == ".json";
				} ).ToList();

				foreach ( var item in filenames )
				{
					try
					{
						string path = Path.Combine( campaignPath, item );
						using ( StreamReader sr = new StreamReader( path ) )
						{
							json = sr.ReadToEnd();
						}
						var campaign = JsonConvert.DeserializeObject<SagaCampaign>( json );
						if ( campaign.formatVersion == Utils.expectedCampaignFormatVersion )
							clist.Add( campaign );
					}
					catch ( Exception ex )
					{
						Utils.LogError( $"GetCampaigns()::Error deserializing campaign file: {item}\n{ex.Message}" );
					}
				}

				return clist;
			}
			catch ( Exception e )
			{
				Utils.LogError( "GetCampaigns()::" + e.Message );
				return clist;
			}
		}

		public static void DeleteCampaign( Guid guid )
		{
			try
			{
				DirectoryInfo di = new DirectoryInfo( campaignPath );

				List<string> filenames = di.GetFiles().Where( file => file.Extension == ".json" ).Select( x => x.Name ).ToList();
				if ( filenames.Contains( $"{guid}.json" ) )
				{
					var file = filenames.Where( x => x.Contains( $"{guid}.json" ) ).FirstOr( null );
					if ( file == null )
					{
						Utils.LogWarning( $"DeleteCampaign()::Could not find campaign file with GUID: {guid}" );
						return;
					}
					File.Delete( Path.Combine( campaignPath, file ) );
				}
			}
			catch ( Exception e )
			{
				Utils.LogError( "DeleteCampaign()::" + e.Message );
			}
		}

		public static List<ProjectItem> GetCustomMissions()
		{
			List<ProjectItem> missions = new List<ProjectItem>();

			try
			{
				RecursiveFolderSearch( customMissionPath, missions );

				return missions;
			}
			catch ( Exception e )
			{
				Utils.LogError( "GetCustomMissions()::" + e.Message );
				return missions;
			}
		}

		/// <summary>
		/// Custom mission recursive folder search
		/// </summary>
		static void RecursiveFolderSearch( string basePath, List<ProjectItem> missions )
		{
			var di = new DirectoryInfo( basePath );
			var folders = di.GetDirectories();

			//add missions in current folder (basePath)
			ProjectItem[] projectItems = GetProjects( basePath ).ToArray();
			missions.AddRange( projectItems );

			foreach ( var folder in folders )
			{
				//iterate each folder within this one
				RecursiveFolderSearch( folder.FullName, missions );
			}
		}

		/// <summary>
		/// Custom mission ProjectItem converter 
		/// </summary>
		public static IEnumerable<ProjectItem> GetProjects( string pathToUse )
		{
			List<ProjectItem> items = new List<ProjectItem>();

			try
			{
				DirectoryInfo di = new DirectoryInfo( pathToUse );
				FileInfo[] files = di.GetFiles().Where( file => file.Extension == ".json" ).ToArray();
				//find mission files
				foreach ( FileInfo fi in files )
				{
					string text = File.ReadAllText( fi.FullName );
					var pi = CreateProjectItem( text, fi.Name, fi.FullName );
					if ( pi != null )
						items.Add( pi );
				}
				items.Sort();
				return items;
			}
			catch ( Exception )
			{
				return items;
			}
		}

		public static ProjectItem CreateProjectItem( string stringifiedMission, string fileName = "", string fullName = "" )
		{
			ProjectItem projectItem = new ProjectItem();

			var mission = LoadMissionFromString( stringifiedMission );
			if ( mission != null )
			{
				projectItem.fullPathWithFilename = fullName;
				projectItem.fileName = fileName;
				projectItem.Title = mission.missionProperties.missionName;
				projectItem.Date = mission.saveDate;
				projectItem.fileVersion = mission.fileVersion;
				projectItem.timeTicks = mission.timeTicks;
				projectItem.Description = mission.missionProperties.missionDescription;
				projectItem.missionID = mission.missionProperties.missionID;
				projectItem.missionGUID = mission.missionGUID.ToString();
				projectItem.AdditionalInfo = mission.missionProperties.additionalMissionInfo;
				projectItem.stringifiedMission = stringifiedMission;
			}
			else
				return null;

			return projectItem;
		}

		/// <summary>
		/// Import global custom characters found in importedCharactersPath, skipping ones with duplicate customCharacterGUID
		/// </summary>
		public static List<CustomToon> LoadGlobalImportedCharacters()
		{
			string json = "";
			List<CustomToon> importedToons = new List<CustomToon>();

			try
			{
				var filenames = Directory.GetFiles( importedCharactersPath );

				//ONLY grab json files - Mac adds a .DS_Store file to all folders
				foreach ( string filename in filenames.Where( x => x.EndsWith( ".json" ) ) )
				{
					using ( StreamReader sr = new StreamReader( filename ) )
					{
						json = sr.ReadToEnd();
					}
					var toon = JsonConvert.DeserializeObject<CustomToon>( json );
					toon.deploymentCard.customCharacterGUID = toon.customCharacterGUID;
					//rename the ID for global imports so they don't conflic with Mission-embedded custom characters using TC# for the ID
					toon.cardID = toon.customCharacterGUID.ToString();
					toon.deploymentCard.id = toon.cardID;
					//also rename the ID for their instructions and bonus effects
					toon.bonusEffect.bonusID = toon.cardID;
					toon.cardInstruction.instID = toon.cardID;
					//force expansion to "Other"
					toon.deploymentCard.expansion = "Other";
					//only import the characater if no others with its GUID have already been imported
					if ( !importedToons.Any( x => x.customCharacterGUID == toon.customCharacterGUID ) )
					{
						importedToons.Add( toon );
						//activation instructions
						DataStore.importedActivationInstructions.Add( toon.cardInstruction );
						//bonus effects
						DataStore.importedBonusEffects.Add( toon.bonusEffect );
					}
					else
						Debug.Log( $"LoadGlobalImportedCharacters()::DUPLICATE GUID: {toon.cardID}\n{toon.cardName}\n{filename}" );
				}

				Debug.Log( $"LoadGlobalImportedCharacters()::FOUND {importedToons.Count} IMPORTED CHARACTERS" );
				if ( importedToons.Count > 0 )
				{
					Debug.Log( $"HEROES: {importedToons.Where( x => x.deploymentCard.characterType == CharacterType.Hero ).Count()}" );
					Debug.Log( $"ALLIES: {importedToons.Where( x => x.deploymentCard.characterType == CharacterType.Ally ).Count()}" );
					Debug.Log( $"REBELS: {importedToons.Where( x => x.deploymentCard.characterType == CharacterType.Rebel ).Count()}" );
					Debug.Log( $"IMPERIALS: {importedToons.Where( x => x.deploymentCard.characterType == CharacterType.Imperial ).Count()}" );
					Debug.Log( $"VILLAINS: {importedToons.Where( x => x.deploymentCard.characterType == CharacterType.Villain ).Count()}" );
				}
				return importedToons;
			}
			catch ( Exception e )
			{
				Utils.LogError( "LoadGlobalImportedCharacters()::Could not load Imported Characters. Exception: " + e.Message );
				return importedToons;
			}
		}

		/// <param name="skipMissions">Skip loading Missions</param>
		/// <returns></returns>
		public static CampaignPackage LoadCampaignPackage( string fullFilename, bool skipMissions = false )
		{
			CampaignPackage package = null;
			byte[] iconBytesBuffer = new byte[0];
			string expectedVersion = @"""packageVersion"": ""2""";

			try
			{
				List<Mission> missionList = new List<Mission>();
				Dictionary<string, TranslatedMission> missionTranslationList = new Dictionary<string, TranslatedMission>();
				Dictionary<string, string> campaignInstList = new Dictionary<string, string>();

				//create the zip file
				using ( FileStream zipPath = new FileStream( fullFilename, FileMode.Open ) )
				{
					//open the archive
					using ( ZipArchive archive = new ZipArchive( zipPath, ZipArchiveMode.Read ) )
					{
						//skip MacOS "._" files
						//skip translation files for other languages than the currently selected one
						foreach ( var entry in archive.Entries.Where( x => !x.Name.StartsWith( "._" ) ).Where( x => !x.FullName.Contains( "Translations/" ) || (x.FullName.Contains( "Translations/" ) && x.Name.Split( '.' )[0].EndsWith( $"_{DataStore.Language.ToUpper()}" )) ) )
						{
							//deserialize the CampaignPackage
							if ( entry.Name == "campaign_package.json" )
							{
								//open the package meta file
								using ( TextReader tr = new StreamReader( entry.Open() ) )
								{
									try
									{
										string s = tr.ReadToEnd();
										//detect correct version BEFORE trying to deserialize it
										if ( !s.Contains( expectedVersion ) )
											throw new Exception( $"This Package isn't in the Version [2] format." );
										if ( Utils.IsJSONValid( s ) )
											package = JsonConvert.DeserializeObject<CampaignPackage>( s );
										else
											throw new Exception( $"`{entry.Name}` is not valid JSON" );
									}
									catch ( Exception e )
									{
										throw new Exception( $"Error while deserializing CampaignPackage: '{entry.Name}'.\n{e.Message}" );
									}
								}
							}
							else if ( (entry.Name.EndsWith( ".png" )) )//icon image
							{
								using ( var stream = new MemoryStream() )
								{
									using ( var s = entry.Open() )
									{
										try
										{
											s.CopyTo( stream );
											//get bytes
											stream.Position = 0;
											iconBytesBuffer = new byte[stream.Length];
											stream.Read( iconBytesBuffer, 0, iconBytesBuffer.Length );
										}
										catch ( Exception e )
										{
											throw new Exception( $"Error while opening PNG: '{entry.Name}'.\n{e.Message}" );
										}
									}
								}
							}
							//translated campaign instructions
							else if ( entry.FullName.EndsWith( ".txt" ) && entry.FullName.Contains( "Translations/" ) )
							{
								using ( TextReader tr = new StreamReader( entry.Open() ) )
								{
									campaignInstList.Add( entry.Name, tr.ReadToEnd() );
								}
							}
							//translated missions
							else if ( !skipMissions && entry.Name.EndsWith( ".json" ) && entry.FullName.Contains( "Translations/" ) )
							{
								using ( TextReader tr = new StreamReader( entry.Open() ) )
								{
									try
									{
										string s = tr.ReadToEnd();
										if ( Utils.IsJSONValid( s ) )
											missionTranslationList.Add( entry.Name, JsonConvert.DeserializeObject<TranslatedMission>( s ) );
										else
											throw new Exception( $"`{entry.Name}` is not valid JSON" );
									}
									catch ( Exception e )
									{
										throw new Exception( $"Error while deserializing a translation: {entry.Name}\n{e.Message}" );
									}
								}
							}
							//deserialize the individual missions
							else if ( !skipMissions && entry.Name.EndsWith( ".json" ) && entry.FullName.Contains( "Missions/" ) )
							{
								using ( TextReader tr = new StreamReader( entry.Open() ) )
								{
									try
									{
										string m = tr.ReadToEnd();
										if ( Utils.IsJSONValid( m ) )
										{
											//sanity check, make sure it's a mission
											if ( m.Contains( "missionGUID" ) )
												missionList.Add( JsonConvert.DeserializeObject<Mission>( m ) );
											else
												throw new Exception( $"Expected a Mission, `{entry.Name}` is not a Mission" );
										}
										else
											throw new Exception( $"`{entry.Name}` is not valid JSON" );
									}
									catch ( Exception e )
									{
										throw new Exception( $"Error while deserializing a Mission: {entry.Name}\n{e.Message}" );
									}
								}
							}
						}

						package.iconBytesBuffer = iconBytesBuffer;

						//add translated instruction for currently selected language
						foreach ( var item in package.campaignTranslationItems.Where( x => x.isInstruction && x.fileName.Split( '.' )[0].EndsWith( $"_{DataStore.Language.ToUpper()}" ) ) )
						{
							item.campaignInstructionTranslation = campaignInstList[item.fileName];
						}

						//add all the missions to the CampaignPackage
						if ( !skipMissions )
						{
							//main control Missions
							foreach ( var item in package.campaignMissionItems )
							{
								var m = missionList.Where( x => x.missionGUID == item.missionGUID ).FirstOr( null );
								if ( m != null )
									item.mission = m;
								else
									throw new Exception( $"Missing Mission in the zip archive:\n{item.missionName}\n{item.missionGUID}" );
							}
							//add translation for currently selected language
							foreach ( var item in package.campaignTranslationItems.Where( x => !x.isInstruction && x.fileName.Split( '.' )[0].EndsWith( $"_{DataStore.Language.ToUpper()}" ) ) )
							{
								item.translatedMission = missionTranslationList[item.fileName];
							}

							var translatedMissions = package.campaignTranslationItems.Where( x => x.translatedMission != null ).ToList();

							if ( translatedMissions.Count > 0 && DataStore.Language.ToUpper() != "EN" )
							{
								//change translated campaign name
								package.campaignName = translatedMissions
									.GroupBy( x => x.translatedMission.missionProperties.campaignName )
									.OrderByDescending( n => n.Count() )
									.First().Key;

								//change translated mission names and descriptions
								foreach ( var item in translatedMissions )
								{
									var campaignMissionItem = package.campaignMissionItems.FirstOrDefault( x => x.missionGUID == item.assignedMissionGUID );

									if ( campaignMissionItem != null )
									{
										campaignMissionItem.missionName = item.translatedMission.missionProperties.missionName;
										campaignMissionItem.mission.missionProperties.missionDescription = item.translatedMission.missionProperties.missionDescription;
										campaignMissionItem.mission.missionProperties.additionalMissionInfo = item.translatedMission.missionProperties.additionalMissionInfo;
									}

									var campaignStructureMission = package.campaignStructure.FirstOrDefault( x => x.missionID == item.assignedMissionGUID.ToString() );

									if ( campaignStructureMission != null )
									{
										campaignStructureMission.projectItem.Title = item.translatedMission.missionProperties.missionName;
										campaignStructureMission.projectItem.Description = item.translatedMission.missionProperties.missionDescription;
										campaignStructureMission.projectItem.AdditionalInfo = item.translatedMission.missionProperties.additionalMissionInfo;
									}
								}
							}
						}
					}
				}

				return package;
			}
			catch ( Exception ee )
			{
				Utils.LogWarning( $"LoadCampaignPackage()::Error loading the Campaign Package [{fullFilename}].\n{ee.Message}" );
				return null;
			}
		}

		public static List<CampaignPackage> GetCampaignPackageList( bool skipMissions )
		{
			//not reloading campaigns if previously already loaded
			if ( importedCampaigns.Count > 0 )
				return importedCampaigns;
			else
			{
				try
				{
					List<CampaignPackage> importedCampaignsNoMission = new List<CampaignPackage>();
					var filenames = Directory.GetFiles( customCampaignPath );

					//ONLY grab zip files - Mac adds a .DS_Store file to all folders
					foreach ( string filename in filenames.Where( x => x.EndsWith( ".zip" ) ) )
					{
						var cc = LoadCampaignPackage( filename, skipMissions );
						if ( cc != null )
						{
							if ( !skipMissions )
								importedCampaigns.Add( cc );
							else
								importedCampaignsNoMission.Add( cc );
						}
					}

					if ( !skipMissions )
					{
						Debug.Log( $"GetCampaignPackageList()::FOUND {importedCampaigns.Count} CUSTOM CAMPAIGNS" );
						return importedCampaigns;
					}
					else
					{
						Debug.Log( $"GetCampaignPackageList()::FOUND {importedCampaignsNoMission.Count} CUSTOM CAMPAIGNS" );
						return importedCampaignsNoMission;
					}
				}
				catch ( Exception e )
				{
					Utils.LogWarning( "GetCampaignPackageList()::Could not create Custom Campaign List. Exception: " + e.Message );
					return importedCampaigns;
				}
			}
		}

		public static CampaignPackage GetPackageByGUID( Guid guid )
		{
			var p = GetCampaignPackageList( true ).Where( x => x.GUID == guid ).FirstOr( null );
			return p;
		}

		public static Mission LoadEmbeddedMission( string campaignGUID, string missionGUID, out string missionStringified )
		{
			missionStringified = null;

			if ( string.IsNullOrEmpty( campaignGUID ) || string.IsNullOrEmpty( missionGUID ) )
				return null;
			//do a full load of the package, including the missions
			var packages = GetCampaignPackageList( false );
			var p = packages.Where( x => x.GUID.ToString() == campaignGUID ).FirstOr( null );
			if ( p != null )
			{
				var mission = p.campaignMissionItems.Where( x => x.mission.missionGUID.ToString() == missionGUID ).FirstOr( null )?.mission;
				if ( mission != null )
				{
					missionStringified = JsonConvert.SerializeObject( mission );
				}
				return mission;
			}

			return null;
		}

		/// <summary>
		/// languageID is the 2 letter language code
		/// </summary>
		/// <returns></returns>
		public static TranslatedMission LoadEmbeddedMissionTranslation( Guid packageGUID, string missionGUID, string languageID )
		{
			if ( packageGUID == Guid.Empty || string.IsNullOrEmpty( missionGUID ) )
				return null;
			//do a full load of the package, including the missions
			var packages = GetCampaignPackageList( false );
			var p = packages.Where( x => x.GUID.ToString() == packageGUID.ToString() ).FirstOr( null );
			if ( p != null )
			{
				return p.campaignTranslationItems.Where( x => x.assignedMissionGUID == Guid.Parse( missionGUID )
				&& x.fileName.ToLower().EndsWith( $"_{languageID.ToLower()}.json" ) ).FirstOr( null )?.translatedMission;
			}
			return null;
		}

		public static string LoadEmbeddedCampaignInstructions( Guid packageGUID )
		{
			if ( packageGUID == Guid.Empty )
				return null;
			//do a full load of the package, including the missions
			var packages = GetCampaignPackageList( false );
			var p = packages.Where( x => x.GUID.ToString() == packageGUID.ToString() ).FirstOr( null );
			if ( p != null )
			{
				return p.campaignTranslationItems.Where( x => x.isInstruction && x.fileName.ToLower().Contains( $"instructions_{DataStore.Language.ToLower()}" ) ).FirstOr( null )?.campaignInstructionTranslation;
			}
			return null;
		}

		public static TranslatedMission GetCustomMissionTranslation( string fullPathToMission )
		{
			//translated filename is expected to be "filename_LANGID.json", ie: JEDI_ES.json for a Mission filename of JEDI.json
			TranslatedMission translation = null;

			try
			{
				FileInfo file = new FileInfo( fullPathToMission );
				string nameOnly = file.Name.Replace( ".json", "" );
				string folder = file.Directory.FullName;
				string translationPath = Path.Combine( folder, $"{nameOnly}_{DataStore.Language.ToUpper()}.json" );

				if ( File.Exists( translationPath ) )
				{
					string json = File.ReadAllText( translationPath );
					translation = JsonConvert.DeserializeObject<TranslatedMission>( json );
					Debug.Log( $"GetCustomMissionTranslation()::Found translation for Custom Mission at '{translationPath}'" );
					return translation;
				}
				else
					Debug.Log( "GetCustomMissionTranslation()::No Custom Mission translation found" );
			}
			catch ( Exception e )
			{
				Utils.LogWarning( $"GetCustomMissionTranslation()::Error loading Custom Mission translation from '{fullPathToMission}'\n{e.Message}" );
			}

			return translation;
		}

		public static TranslatedMission GetOfficialMissionTranslation( ProjectItem projectItem )
		{
			TranslatedMission translation = null;

			try
			{
				string mText = Resources.Load<TextAsset>( $"Languages/{DataStore.Language}/Missions/{projectItem.expansion}/{projectItem.fullPathWithFilename}_{DataStore.Language}" )?.text;

				if ( mText != null )
				{
					translation = JsonConvert.DeserializeObject<TranslatedMission>( mText );
					return translation;
				}
				else
				{
					Debug.Log( $"GetOfficialMissionTranslation()::No Mission translation found at:\nLanguages/{DataStore.Language}/Missions/{projectItem.expansion}/{projectItem.fullPathWithFilename}_{DataStore.Language}" );
				}
			}
			catch ( Exception e )
			{
				Utils.LogWarning( $"GetOfficialMissionTranslation()::Error attempting to find and load a Mission translation for: '{projectItem.missionID}'\n{e.Message}" );
			}

			return translation;
		}

		public static T LoadAssetFromResource<T>( string assetName )
		{
			try
			{
				string json = Resources.Load<TextAsset>( assetName ).text;
				return JsonConvert.DeserializeObject<T>( json );
			}
			catch ( JsonReaderException e )
			{
				Debug.Log( $"LoadAssetFromResource() ERROR:\r\nError parsing data [{assetName}]" );
				Debug.Log( e.Message );
				throw e;
			}
		}

	}
}