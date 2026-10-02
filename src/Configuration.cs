using Dalamud.Configuration;
namespace EquinoxCompanion;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public bool HouseCharactersFirst { get; set; } = true;
    public Dictionary<string, List<string>> HiddenCharacters { get; set; } = [];
    public Dictionary<string, List<string>> CharacterOrders { get; set; } = [];
    public bool NotifyHouseEntries { get; set; }
    public bool RefreshSharedInBackground { get; set; } = true;
    public bool NotifyHousingWarnings { get; set; } = true;
    public Dictionary<string, DateTimeOffset> HousingWarnings { get; set; } = [];
    public SharedRoster? SharedRoster { get; set; }
    public int Version { get; set; } = 4;
    public bool SyncEnabled { get; set; }
    public string PairingKey { get; set; } = "";
    public List<string> SentEvents { get; set; } = [];
    public List<PlantingRecord> Planting { get; set; } = [];
    public List<SyncEvent> Discoveries { get; set; } = [];
    public bool SyncCharacterDetails { get; set; } = true;
    public bool SyncHouseDetails { get; set; } = true;
    public bool SyncCollections { get; set; } = true;
    public bool SyncActivities { get; set; } = true;
    public bool TrackGardens { get; set; }
    public List<TendingRecord> Tending { get; set; } = [];
    public List<HouseObservation> Houses { get; set; } = [];
}
