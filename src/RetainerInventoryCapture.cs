using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly RetainerCaptureStability retainerCaptureStability=new();
    private DateTimeOffset nextRetainerCapture;
    private string retainerCaptureStatus="No retainer inventory open";
    private unsafe void UpdateRetainerInventoryCapture(DateTimeOffset now)
    {
        if(now<nextRetainerCapture)return;
        nextRetainerCapture=now.AddMilliseconds(250);
        var rm=RetainerManager.Instance();var agent=AgentRetainer.Instance();
        var ui=UIModule.Instance();var order=ui==null?null:ui->GetItemOrderModule();
        var active=rm==null?null:rm->GetActiveRetainer();
        if(!config.EnableRetriever||!Player.IsLoaded||Player.ContentId==0||
            Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||
            !Conditions[ConditionFlag.OccupiedSummoningBell]||rm==null||!rm->IsReady||
            agent==null||!agent->IsAgentActive()||SellerAddon("RetainerList")!=null||
            (SellerAddon("InventoryRetainer")==null&&SellerAddon("InventoryRetainerLarge")==null)||
            active==null||active->RetainerId==0||active->RetainerId!=rm->LastSelectedRetainerId||
            order==null||order->ActiveRetainerId!=active->RetainerId){
            retainerCaptureStability.Reset();retainerCaptureStatus="No verified retainer inventory open";return;
        }
        var manager=InventoryManager.Instance();if(manager==null){retainerCaptureStability.Reset();return;}
        foreach(var type in Enumerable.Range(10000,7).Append(12001)){
            var c=manager->GetInventoryContainer((InventoryType)type);
            if(c==null||!c->IsLoaded||c->Items==null||c->Size!=(type==12001?18:25)){
                retainerCaptureStability.Reset();retainerCaptureStatus=$"{active->NameString}: waiting for container {type}";return;
            }
        }
        var slots=OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray();
        if(!retainerCaptureStability.Ready(Player.ContentId,active->RetainerId,slots,active->ItemCount,now)){
            retainerCaptureStatus=$"{active->NameString}: waiting for stable, complete inventory ({slots.Count(x=>x.Container is >=10000 and <=10006&&x.Item>0)}/{active->ItemCount} slots)";return;
        }
        var owner=active->RetainerId;var character=Player.ContentId;
        var old=config.OrganizerInventories.Where(x=>x.Character==character&&x.Retainer==owner).MaxBy(x=>x.At);
        retainerCaptureStatus=$"{active->NameString}: inventory recorded";
        if(old!=null&&old.Slots.SequenceEqual(slots))return;
        config.OrganizerInventories.RemoveAll(x=>x.Character==character&&x.Retainer==owner);
        config.OrganizerInventories.Add(new(character,owner,now,slots));
        SaveConfiguration();InvalidateOrganizePreview();retrievalPreviewDirty=true;
        ObserveStorage(now,retainersOnly:true);retrievalRefreshPending=true;
    }
}
