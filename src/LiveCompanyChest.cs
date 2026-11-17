using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly ChestSnapshotGate liveChestGate=new();
    private DateTimeOffset liveChestNext,workshopChestRefreshDue;
    private readonly Dictionary<(ulong Character,ulong Company,uint World,uint Page),(DateTimeOffset At,StorageStack[] Stacks)> liveChestRecords=[];
    private (ulong Character,ulong Company,uint World,uint Page)? liveChestIdentity;
    private ChestStack[]? liveChestLastSlots;
    private unsafe bool TryReadReadyCompanyChest(out ulong company,out uint page,out ChestStack[] slots)
    {
        company=0;page=0;slots=[];
        var addon=SellerAddon("FreeCompanyChest");var agent=AgentFreeCompanyChest.Instance();
        var proxy=InfoProxyFreeCompany.Instance();var membership=InfoProxyFreeCompanyMember.Instance();
        var manager=InventoryManager.Instance();
        if(!Player.IsLoaded||Player.ContentId==0||Player.CurrentWorld.RowId!=Player.HomeWorld.RowId||addon==null||!addon->IsReady||
            agent==null||!agent->IsAgentActive()||proxy==null||proxy->Id==0||membership==null||membership->FreeCompanyId!=proxy->Id||manager==null)return false;
        // Identity comes from the character's FC ID, not a separately loaded member roster.
        if(*((byte*)addon+0x4EC)!=0||*((byte*)addon+0x4F4)!=0)return false;
        foreach(var operation in manager->PendingOperations)if(!operation.IsEmpty)return false;
        var kind=*((byte*)addon+0x4D8);var index=*((byte*)addon+0x4D9);
        if(kind==1)page=22001;else if(kind==0&&index<5)page=20000u+index;else return false;
        var tab=ChestPageButton(page);if(tab==null||!tab->IsEnabled||!tab->IsSelected)return false;
        var container=manager->GetInventoryContainer((InventoryType)page);
        if(container==null||!container->IsLoaded||container->Items==null||container->Size is <=0 or >200)return false;
        company=proxy->Id;slots=ReadChestContainer(page);return true;
    }
    private void UpdateLiveCompanyChest(DateTimeOffset now)
    {
        if(now<liveChestNext)return;liveChestNext=now.AddMilliseconds(250);
        try{
            if(!TryReadReadyCompanyChest(out var company,out var page,out var slots)){liveChestGate.Reset();liveChestIdentity=null;return;}
            if(!liveChestGate.Observe(Player.ContentId,company,Player.CurrentWorld.RowId,page,slots,now))return;
            var identity=(Player.ContentId,company,Player.CurrentWorld.RowId,page);
            if(liveChestIdentity==identity&&liveChestLastSlots!=null&&liveChestLastSlots.AsSpan().SequenceEqual(slots))return;
            liveChestIdentity=identity;liveChestLastSlots=slots;
            var stacks=slots.Where(s=>s.Item>0&&s.Quantity>0).GroupBy(s=>(s.Item,s.Hq))
                .Select(g=>new StorageStack(g.Key.Item,g.Sum(s=>s.Quantity),g.Key.Hq,SearchItemName(g.Key.Item),g.Select(s=>new StoragePosition(s.Slot+1,s.Quantity)).ToArray())).ToArray();
            // Keep empty snapshots too: zero replaces the previous page, never merges with it.
            liveChestRecords[identity]=(now,stacks);
            if(config.SyncCollections){
                var actor=ReadActor();
                if(SyncValidation.ActorReady(actor)){
                    var details=new StorageDetails($"fc:{company}:{page}",$"Available in FC Chest · {(page==22001?"Crystals":"Inventory "+(page-19999))}",stacks.Select(s=>s.ItemId).Distinct().Order().ToArray(),stacks);
                    KeepDiscovery(new(Guid.NewGuid().ToString("N"),"storage.observed",now,actor,null,Storage:details));
                }
            }
            if(workshopPreparation!=null||workshopView==2&&retrieverWindow.IsOpen)workshopChestRefreshDue=now.AddMilliseconds(500);
        }catch(Exception ex){liveChestGate.Reset();Log.Warning(ex,"Company chest observation failed");}
    }
}
