using Dalamud.Configuration;
namespace EquinoxCompanion;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public HashSet<string> GardenResyncBatches { get; set; } = [];
    public bool EnableCofferMarkers { get; set; }
    public bool CofferMinimap { get; set; } = true;
    public int CofferStyle { get; set; }
    public bool CofferMainMap { get; set; } = true;
    public bool EnableFollowThem { get; set; }
    public FollowThemSettings FollowThem { get; set; } = new();
    public QuickLootSettings QuickLoot { get; set; } = new();
    public bool EnableQuickLoot { get; set; }
    public Dictionary<string, bool> SharedAccountExpanded { get; set; } = [];
    public Dictionary<string, bool> PlantingAccountExpanded { get; set; } = [];
    public Dictionary<string, FloatingLauncherOptions> FloatingLaunchers { get; set; } = [];
    public int TabOrderVersion { get; set; }
    public List<string> TabOrder { get; set; } = [];
    public bool HouseCharactersFirst { get; set; } = true;
    public Dictionary<string, List<string>> HiddenCharacters { get; set; } = [];
    public Dictionary<string, List<string>> CharacterOrders { get; set; } = [];
    public Shortcut EquinoxShortcut { get; set; } = new();
    public Shortcut PlantingShortcut { get; set; } = new();
    public Shortcut FashionShortcut { get; set; } = new();
    public bool NotifyGardenTending { get; set; } = true;
    public bool NotifyGardenRisk { get; set; } = true;
    public bool NotifyGardenHarvest { get; set; } = true;
    public bool NotifyGardenDead { get; set; } = true;
    public bool NotifyGardenMaturity { get; set; } = true;
    public bool NotifyGardenUnknownCare { get; set; } = true;
    public bool NotifyFashionLink { get; set; } = true;
    public bool NotifyPlantingUnavailable { get; set; } = true;
    public bool NotifyBrowserErrors { get; set; } = true;
    public bool NotifyGardenCare { get; set; } = true;
    public bool NotifyHouseEntries { get; set; }
    public bool RefreshSharedInBackground { get; set; } = true;
    public bool NotifyHousingWarnings { get; set; } = true;
    public Dictionary<string, DateTimeOffset> HousingWarnings { get; set; } = [];
    public SharedRoster? SharedRoster { get; set; }
    public string WelcomeVersion { get; set; } = "";
    public int Version { get; set; } = 4;
    public bool SyncEnabled { get; set; }
    public string PairingKey { get; set; } = "";
    public List<string> SentEvents { get; set; } = [];
    public List<PlantingRecord> Planting { get; set; } = [];
    public List<SyncEvent> Discoveries { get; set; } = [];
    public bool SyncCharacterDetails { get; set; } = true;
    public bool SyncHouseDetails { get; set; } = true;
    public bool SyncCollections { get; set; } = true;
    public bool SyncAutoRetainer { get; set; } = true;
    public bool SyncActivities { get; set; } = true;
    public bool TrackGardens { get; set; }
    public List<TendingRecord> Tending { get; set; } = [];
    public List<HouseObservation> Houses { get; set; } = [];
}
