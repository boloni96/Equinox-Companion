using Dalamud.Configuration;
namespace EquinoxCompanion;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 4;
    public bool SyncEnabled { get; set; }
    public string PairingKey { get; set; } = "";
    public List<string> SentEvents { get; set; } = [];
    public List<PlantingRecord> Planting { get; set; } = [];
    public List<SyncEvent> Discoveries { get; set; } = [];
    public bool SyncCharacterDetails { get; set; } = true;
    public bool SyncHouseDetails { get; set; } = true;
    public bool TrackGardens { get; set; }
    public List<TendingRecord> Tending { get; set; } = [];
    public List<HouseObservation> Houses { get; set; } = [];
}

