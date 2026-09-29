using Dalamud.Configuration;
namespace EquinoxCompanion;
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public List<HouseObservation> Houses { get; set; } = [];
}

