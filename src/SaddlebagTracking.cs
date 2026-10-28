using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset nextSaddleRead;
    private ulong saddleCharacter;
    private void InitializeSaddleTracking()
    {
        foreach(var name in new[]{"InventoryBuddy","InventoryBuddy2"})
        foreach(var type in new[]{AddonEvent.PostSetup,AddonEvent.PostRefresh,AddonEvent.PreFinalize})
            Addons.RegisterListener(type,name,ObserveSaddleAddon);
    }
    private void ObserveSaddleAddon(AddonEvent type,AddonArgs args)
    {
        nextSaddleRead=default;
        // Capture before the game releases the loaded container when closing it.
        if(SaddleTrackingReady)ObserveSafely("saddlebag capture",()=>ObserveStorage(DateTimeOffset.UtcNow,true));
    }
    private bool SaddleTrackingReady=>config.SyncCollections&&Player.IsLoaded&&Player.ContentId!=0&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51];
    private void UpdateSaddleTracking(DateTimeOffset now)
    {
        var id=Player.IsLoaded?Player.ContentId:0;
        if(id!=saddleCharacter){saddleCharacter=id;storageCaptureGate.Clear();nextSaddleRead=default;}
        if(!SaddleTrackingReady||now<nextSaddleRead)return;
        nextSaddleRead=now.AddMilliseconds(250);
        ObserveSafely("saddlebag capture",()=>ObserveStorage(now,true));
    }
}
