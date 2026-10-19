using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private readonly OwnedCompanyRefresh ownedCompanyRefresh = new();
    private unsafe void RefreshOwnedCompany(Actor actor, bool verified, DateTimeOffset now)
    {
        var local = Objects.LocalPlayer;
        var proxy = InfoProxyFreeCompany.Instance();
        var members = InfoProxyFreeCompanyMember.Instance();
        var agent = AgentFreeCompany.Instance();
        var profile = AgentFreeCompanyProfile.Instance();
        var estate = AddressOf(HousingManager.GetOwnedHouseId(EstateType.FreeCompanyEstate));
        var needed = config.SyncHouseDetails && !verified && estate is not null && estate.WorldId == actor.HomeWorldId;
        var safe = now >= characterReadyAt && actor.CurrentWorldId == actor.HomeWorldId && local != null &&
            proxy != null && members != null && !(agent != null && agent->IsAgentActive()) &&
            !(profile != null && profile->IsAgentActive());
        switch (ownedCompanyRefresh.Tick(actor.ContentId, now, needed, safe))
        {
            case 1:
                proxy->RequestDataForCharacter(local!.EntityId);
                break;
            case 2:
                members->RequestData();
                break;
        }
    }
}
