using Dalamud.Configuration;
namespace EquinoxCompanion;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;
    public bool TrackGardens { get; set; }
    public List<TendingRecord> Tending { get; set; } = [];
    public List<HouseObservation> Houses { get; set; } = [];
}

