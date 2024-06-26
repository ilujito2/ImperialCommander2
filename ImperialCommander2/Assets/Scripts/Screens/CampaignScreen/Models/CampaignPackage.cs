using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Saga
{
	public class CampaignPackage
	{
		public Guid GUID;
		public string campaignName;
		public string campaignInstructions;
		public List<CampaignMissionItem> campaignMissionItems { get; set; } = new List<CampaignMissionItem>();
		public List<CampaignStructure> campaignStructure { get; set; } = new List<CampaignStructure>();
		public List<CampaignTranslationItem> campaignTranslationItems { get; set; } = new List<CampaignTranslationItem>();


		[JsonIgnore]
		public byte[] iconBytesBuffer = new byte[0];

		public CampaignPackage() { }

		/// <summary>
		/// languageID is the two letter ID, such as "De" or "Es"
		/// </summary>
		/// <returns></returns>
		public CampaignTranslationItem GetTranslation( Guid missionGUID, string languageID )
		{
			return campaignTranslationItems.Where( x => x.assignedMissionGUID == missionGUID
				&& x.fileName.ToLower().EndsWith( $"_{languageID.ToLower()}.json" ) ).FirstOr( null );
		}

		//public TranslatedMission GetTranslatedMission( Guid missionGUID, string languageID )
		//{
		//	var item = campaignTranslationItems.Where( x => x.assignedMissionGUID == missionGUID ).FirstOr( null );
		//	if ( item != null )
		//	{
		//		var translation = FileManager.LoadEmbeddedMissionTranslation( GUID, missionGUID.ToString(), languageID );
		//		return translation;
		//	}
		//	return null;
		//}
	}

	public class CampaignMissionItem
	{
		public Guid GUID;//GUID of this object, not the mission
		public Guid missionGUID;
		public string missionName;
		public string customMissionIdentifier;

		//store the actual mission for packing as an individual file later, but don't serialize it here
		[JsonIgnore]
		public Mission mission { get; set; }
	}

	public class CampaignTranslationItem
	{
		public string fileName;
		public bool isInstruction;
		public Guid assignedMissionGUID;
		public string assignedMissionName;

		//store the actual translations for packing as individual files later, but don't serialize it here
		[JsonIgnore]
		public TranslatedMission translatedMission { get; set; }
		[JsonIgnore]
		public string campaignInstructionTranslation { get; set; }
	}
}
