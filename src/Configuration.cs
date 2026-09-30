using Dalamud.Configuration;
namespace EquinoxCompanion;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 3;
    public bool SyncEnabled { get; set; }
    public string PairingKey { get; set; } = "";
    public List<string> SentEvents { get; set; } = [];
    public bool TrackGardens { get; set; }
    public List<TendingRecord> Tending { get; set; } = [];
    public List<HouseObservation> Houses { get; set; } = [];
}

