namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly DiscoveryReadIndex discoveryReads=new();
    private DiscoveryReadIndex DiscoveryReads(){discoveryReads.Refresh(config.Discoveries);return discoveryReads;}
}
