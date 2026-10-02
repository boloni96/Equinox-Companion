using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private ulong storageRetainer;
    private DateTimeOffset storageRetainerReady, dresserReady;
    private Dictionary<uint,uint>? cabinetItems;
    private bool collectingStorage, storageChanged;
    private void ObserveStorage(DateTimeOffset now)
    {
        collectingStorage=true;storageChanged=false;
        try { ObserveStorageCore(now); }
        finally { collectingStorage=false;if(storageChanged)Pi.SavePluginConfig(config); }
    }
    private unsafe void ObserveStorageCore(DateTimeOffset now)
    {
        if (!config.SyncCollections || !Player.IsLoaded || now < collectionReadyAt) return;
        var actor = ReadActor();
        if (!SyncValidation.ActorReady(actor) || collectionCharacter != Player.ContentId) return;
        var manager = InventoryManager.Instance();
        if (manager == null) return;
        void Save(string key, string name, IEnumerable<uint> items)
        {
            var ids = items.Where(i=>i>0 && i<1000000).Distinct().Order().ToArray();
            KeepDiscovery(new(Guid.NewGuid().ToString("N"), "storage.observed", now, actor, null, Storage:new(key,name,ids)));
        }
        foreach (var type in Enum.GetValues<InventoryType>())
        {
            var id=(uint)type;
            if (!(id<=3 || id==1000 || id is >=3200 and <=3500 || id is >=4000 and <=4101)) continue;
            var container=manager->GetInventoryContainer(type);
            if (container==null || !container->IsLoaded || container->Items==null || container->Size is <=0 or >200) continue;
            var items=new List<uint>();
            for(var i=0;i<container->Size;i++)if(container->Items[i].Quantity>0)items.Add(container->Items[i].ItemId % 1000000);
            Save("bag:"+id, id<=3?"Inventory":id==1000?"Equipped":id>=4000?"Chocobo saddlebag":"Armoury Chest",items);
        }
        var state=UIState.Instance();
        if(state!=null && state->Cabinet.IsCabinetLoaded())
        {
            cabinetItems ??= DataManager.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>().ToDictionary(x=>x.RowId,x=>x.Item.RowId);
            var items=new List<uint>();
            foreach(var (row,item) in cabinetItems)if(state->Cabinet.IsItemInCabinet(row))items.Add(item);
            Save("armoire","Armoire",items);
        }
        var dresser=AgentMiragePrismPrismBox.Instance();
        if(dresser==null || !dresser->IsAgentActive() || dresser->Data==null || !dresser->IsDataLoaded) dresserReady=default;
        else if(dresserReady==default) dresserReady=now.AddSeconds(5);
        else if(now>=dresserReady)
        {
            var items=new List<uint>();
            foreach(var item in dresser->Data->PrismBoxItems)if(item.ItemId>0)items.Add(item.ItemId % 1000000);
            Save("dresser","Glamour Dresser",items);
        }
        // A stable, open retainer inventory is required before attributing its containers.
        var retainers=RetainerManager.Instance();
        var agent=AgentRetainer.Instance();
        var active=retainers==null?0:retainers->LastSelectedRetainerId;
        if(agent==null || !agent->IsAgentActive() || active==0){storageRetainer=0;return;}
        if(storageRetainer!=active){storageRetainer=active;storageRetainerReady=now.AddSeconds(10);return;}
        if(now<storageRetainerReady)return;
        if(retainers==null)return;
        var retainer=retainers->GetActiveRetainer();
        if(retainer==null || retainer->RetainerId!=active)return;
        foreach(var type in Enum.GetValues<InventoryType>().Where(t=>(uint)t is >=10000 and <=10006 or 11000 or 12002))
        {
            var container=manager->GetInventoryContainer(type);
            if(container==null || !container->IsLoaded || container->Items==null || container->Size is <=0 or >200)continue;
            var items=new List<uint>();
            for(var i=0;i<container->Size;i++)if(container->Items[i].Quantity>0)items.Add(container->Items[i].ItemId % 1000000);
            Save($"retainer:{active}:{(uint)type}","Retainer · "+retainer->NameString,items);
        }
    }
}
