using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private ulong storageRetainer;
    private string storageEstate="";
    private DateTimeOffset storageEstateReady;
    private DateTimeOffset storageRetainerReady, dresserReady;
    private Dictionary<uint,uint>? cabinetItems;
    private bool collectingStorage, storageChanged;
    private readonly StorageCaptureGate storageCaptureGate=new();
    private void ObserveStorage(DateTimeOffset now,bool saddlebagsOnly=false,bool retainersOnly=false)
    {
        collectingStorage=true;storageChanged=false;
        try { ObserveStorageCore(now,saddlebagsOnly,retainersOnly); }
        finally { collectingStorage=false;if(storageChanged)SaveConfiguration(); }
    }
    private unsafe void ObserveStorageCore(DateTimeOffset now,bool saddlebagsOnly,bool retainersOnly)
    {
        if (!config.SyncCollections || !Player.IsLoaded || now < collectionReadyAt) return;
        var actor = ReadActor();
        if (!SyncValidation.ActorReady(actor) || collectionCharacter != Player.ContentId) return;
        var manager = InventoryManager.Instance();
        if (manager == null) return;
        void Save(string key, string name, IEnumerable<uint> items, StorageStack[]? stacks = null)
        {
            var ids = items.Where(i=>i>0 && i<1000000).Distinct().Order().ToArray();
            var last=DiscoveryReads().Last("storage.observed",actor.ContentId,storage:key);
            var details = new StorageDetails(key, name, ids, stacks);
            // Compare unchanged containers without allocating two JSON documents.
            if(last?.Storage is {} previous && last.Actor == actor && now-last.At < TimeSpan.FromMinutes(30) && StorageObservationEquality.Same(previous, details)) return;
            KeepDiscovery(new(Guid.NewGuid().ToString("N"), "storage.observed", now, actor, null, Storage:details),last is not null&&now-last.At>=TimeSpan.FromMinutes(30));
        }
        StorageStack[] ReadStacks(InventoryContainer* container)
        {
            var stacks=new List<StorageStack>();
            for(var i=0;i<container->Size;i++)
            {
                var item=container->Items[i];var id=item.ItemId%1000000;
                if(item.Quantity<=0||id==0)continue;
                var name=SearchItemName(id);
                stacks.Add(new(id,item.Quantity,(item.Flags&InventoryItem.ItemFlags.HighQuality)!=0,name,[new(i+1,item.Quantity)]));
            }
            return stacks.GroupBy(x=>(x.ItemId,x.Hq)).Select(g=>new StorageStack(g.Key.ItemId,g.Sum(x=>x.Quantity),g.Key.Hq,g.First().Name,g.SelectMany(x=>x.Positions??[]).OrderBy(x=>x.Slot).ToArray())).OrderBy(x=>x.ItemId).ThenBy(x=>x.Hq).ToArray();
        }
        if(!retainersOnly)
        {
        Span<StorageSlotValue> slotValues=stackalloc StorageSlotValue[200];
        foreach (var type in Enum.GetValues<InventoryType>())
        {
            var id=(uint)type;
            if(saddlebagsOnly&&id is not (4000 or 4001 or 4100 or 4101))continue;
            if (!(id<=3 || id==1000 || id==2001 || id is >=3200 and <=3500 || id is >=4000 and <=4101)) continue;
            var container=manager->GetInventoryContainer(type);
            if (container==null || !container->IsLoaded || container->Items==null || container->Size is <=0 or >200) continue;
            for(var i=0;i<container->Size;i++){var item=container->Items[i];slotValues[i]=new(item.ItemId,item.Quantity,(item.Flags&InventoryItem.ItemFlags.HighQuality)!=0);}
            var slots=slotValues[..container->Size];var bagKey="bag:"+id;
            if(!storageCaptureGate.Needs(actor,bagKey,slots,now))continue;
            var items=new List<uint>();
            for(var i=0;i<container->Size;i++)if(container->Items[i].Quantity>0)items.Add(container->Items[i].ItemId % 1000000);
            Save("bag:"+id, id<=3?$"Inventory {id+1}":id==1000?"Equipped":id==2001?"Crystals":id>=4000?$"{(id>=4100?"Premium chocobo saddlebag":"Chocobo saddlebag")} {id%100+1}":"Armoury Chest · "+type.ToString().Replace("Armory", "").Replace("Feets","Feet").Replace("OffHand","Off hand").Replace("MainHand","Main hand").Replace("SoulCrystal","Soul crystal"),items,ReadStacks(container));
            storageCaptureGate.Remember(actor,bagKey,slots,now);
        }
        if(saddlebagsOnly)return;
        var housing=HousingManager.Instance();
        var agents=AgentModule.Instance();
        var editor=agents==null?null:agents->GetAgentByInternalId(AgentId.HousingEditContainer);
        var loaded=housing!=null&&housing->CurrentTerritory!=null&&housing->CurrentTerritory->IsLoaded();
        var inside=loaded&&housing->IsInside();
        var address=loaded?AddressOf(inside?housing->GetCurrentIndoorHouseId():housing->GetCurrentHouseId()):null;
        if(editor==null||!editor->IsAgentActive()||address is null||address.Workshop||!housing->HasHousePermissions())storageEstate="";
        else
        {
            var estateContext=actor.ContentId+":"+address.HouseId+":"+inside;
            if(storageEstate!=estateContext){storageEstate=estateContext;storageEstateReady=now.AddSeconds(10);}
            else if(now>=storageEstateReady)
            {
                var place=$"{address.WorldName} · {address.DistrictName} W{address.Ward} · "+(address.Apartment?$"Apartment {address.Room}":$"P{address.Plot}"+(address.Room>0?$" · Chamber {address.Room}":""));
                foreach(var type in Enum.GetValues<InventoryType>())
                {
                    var id=(uint)type;
                    var supported=inside?(id is >=25002 and <=25014 or >=27001 and <=27011):(id is 25000 or 25001 or 25200 or 27000 or 27200);
                    if(!supported)continue;
                    var container=manager->GetInventoryContainer(type);
                    if(container==null||!container->IsLoaded||container->Items==null||container->Size is <=0 or >200)continue;
                    var stacks=ReadStacks(container);
                    var section=id is 25000 or 25002?"fixtures":id>=27000?"storeroom":"placed";
                    var page=inside?(id>=27000?id-27000:id-25002):(id is 25200 or 27200?2u:1u);
                    var label=$"{place} · {(inside?"Indoor":"Outdoor")} {section}"+(id is 25000 or 25002?"":$" {page}");
                    Save($"house:{address.HouseId}:{id}",label.Length>100?label[..100]:label,stacks.Select(s=>s.ItemId),stacks);
                }
            }
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
        var chest=AgentFreeCompanyChest.Instance();
        var proxy=InfoProxyFreeCompany.Instance();
        var membership=InfoProxyFreeCompanyMember.Instance();
        if(chest!=null && chest->IsAgentActive() && proxy!=null && proxy->Id>0 && membership!=null && membership->FreeCompanyId==proxy->Id && membership->CharData!=null && membership->EntryCount is >0 and <=512)
        {
            var member=false;
            for(var i=0;i<(int)membership->EntryCount;i++)if(membership->CharData[i].ContentId==Player.ContentId && membership->CharData[i].HomeWorld==actor.HomeWorldId)member=true;
            if(member)foreach(var type in Enum.GetValues<InventoryType>().Where(t=>(uint)t is >=20000 and <=20004 or 22001))
            {
                var container=manager->GetInventoryContainer(type);
                if(container==null || !container->IsLoaded || container->Items==null || container->Size is <=0 or >200)continue;
                var items=new List<uint>();
                for(var i=0;i<container->Size;i++)if(container->Items[i].Quantity>0)items.Add(container->Items[i].ItemId % 1000000);
                Save($"fc:{proxy->Id}:{(uint)type}",$"Available in FC Chest · {((uint)type==22001?"Crystals":$"Inventory {(uint)type-19999}")} · {proxy->NameString}",items,ReadStacks(container));
            }
        }
        }
        // A stable, open retainer inventory is required before attributing its containers.
        var retainers=RetainerManager.Instance();
        var agent=AgentRetainer.Instance();
        var active=retainers==null?0:retainers->LastSelectedRetainerId;
        if(agent==null || !agent->IsAgentActive() || active==0){storageRetainer=0;return;}
        // The fast path has already verified native ownership and a stable full snapshot.
        if(retainersOnly){storageRetainer=active;storageRetainerReady=now;}
        if(storageRetainer!=active){storageRetainer=active;storageRetainerReady=now.AddSeconds(10);return;}
        if(now<storageRetainerReady)return;
        if(retainers==null)return;
        var retainer=retainers->GetActiveRetainer();
        if(retainer==null || retainer->RetainerId!=active)return;
        foreach(var type in Enum.GetValues<InventoryType>().Where(t=>(uint)t is >=10000 and <=10006 or 11000 or 12001 or 12002))
        {
            var container=manager->GetInventoryContainer(type);
            if(container==null || !container->IsLoaded || container->Items==null || container->Size is <=0 or >200)continue;
            var items=new List<uint>();
            for(var i=0;i<container->Size;i++)if(container->Items[i].Quantity>0)items.Add(container->Items[i].ItemId % 1000000);
            Save($"retainer:{active}:{(uint)type}",$"Retainer {retainer->NameString} · {((uint)type==11000?"Equipped":(uint)type==12002?"Market listings":(uint)type==12001?"Crystals":$"Inventory {(uint)type-9999}")}",items,ReadStacks(container));
        }
    }
}
