using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly Queue<StorageWithdrawalPage> localStorageQueue=[];
    private int localStorageBatchMoves,localStorageBatchFilter;
    private DateTimeOffset localStorageReceiptAt;
    private readonly Dictionary<uint,int> localStorageWithdrawn=[];
    private bool localStorageRunning,localStorageStatusShown,localStorageDuplicates;
    private uint[] localStorageDuplicatePages=[];
    private string localStorageKey="";
    private uint localStorageType,localStorageTerritory;
    private bool localStorageEntrust;
    private int localStorageFilter;
    private RetrievalQuality localStorageQuality;
    private Dictionary<uint,int> localStorageRemaining=[];
    private ChestMove? localStoragePending;
    private readonly HashSet<(uint,int)> localStorageVisited=[];
    private HashSet<uint>? indoorFurniture,outdoorFurniture;
    private static bool TryLocalStorageKey(string key,out uint type){type=0;var parts=key.Split(':');return (parts.Length==2&&parts[0]=="bag"||parts.Length==3&&parts[0]=="house")&&uint.TryParse(parts[^1],out type);}
    private unsafe void RequireLocalStorage(string key,uint type)
    {
        if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||HelperInCutscene())throw new InvalidOperationException("Storage transfer needs a loaded character outside combat and cutscenes.");
        if(LocalStoragePolicy.Saddle(type)){
            if(SellerAddon("InventoryBuddy")==null&&SellerAddon("InventoryBuddy2")==null)throw new InvalidOperationException("Open your chocobo saddlebag first.");
        }else if(LocalStoragePolicy.Storeroom(type)){
            var housing=HousingManager.Instance();var agents=AgentModule.Instance();var editor=agents==null?null:agents->GetAgentByInternalId(AgentId.HousingEditContainer);
            if(housing==null||housing->CurrentTerritory==null||!housing->CurrentTerritory->IsLoaded()||!housing->HasHousePermissions()||!HousingStorageUiActive())throw new InvalidOperationException("Open the housing furnishing/storeroom menu at this estate with storage permissions first.");
            var inside=housing->IsInside();var address=AddressOf(inside?housing->GetCurrentIndoorHouseId():housing->GetCurrentHouseId());
            if(address==null||address.Workshop||key!=$"house:{address.HouseId}:{type}"||inside!=(type is >=27001 and <=27011))throw new InvalidOperationException("This recorded storeroom is not the currently open estate and indoor/outdoor area.");
        }else if(!LocalStoragePolicy.Armoury(type))throw new InvalidOperationException("This storage is display-only in this view.");
        var inv=InventoryManager.Instance();var c=inv==null?null:inv->GetInventoryContainer((InventoryType)type);
        if(c==null||!c->IsLoaded||c->Items==null||c->Size is <=0 or >200)throw new InvalidOperationException("Storage data is not loaded. Open the selected storage page first.");
    }
    private unsafe void StartLocalStorage(string key,bool entrust,uint[]? selectedItems=null,bool continuation=false)
    {
        if(chestRunning||organizeRunning||retrievalRun.Running||gearRouting||armoireRunning||craftingWithdrawalPending||sellerRun.Running||TraderRunning||retainerGilRunning)throw new InvalidOperationException("Finish the current inventory operation first.");
        if(!TryLocalStorageKey(key,out var type))throw new InvalidOperationException("Choose a storage location first.");
        if(entrust&&LocalStoragePolicy.Armoury(type))throw new InvalidOperationException("Use the game to store gear in the Armoury Chest. This action withdraws unequipped gear to bags.");
        RequireLocalStorage(key,type);RefreshGearSetProtection();
        if(!continuation){localStorageQueue.Clear();localStorageBatchMoves=0;localStorageWithdrawn.Clear();}
        localStorageReceiptAt=default;
        localStorageDuplicates=false;localStorageDuplicatePages=[];
        localStorageEntrust=entrust;localStorageFilter=entrust?myInventoryTradeFilter:continuation?localStorageBatchFilter:allStorageFilter;localStorageQuality=entrust?myInventoryQuality:RetrievalQuality.Any;
        localStorageRemaining=entrust?MyInventoryAmounts(ReadMyInventory()):(selectedItems??allStorageSelected.Items(key).Where(AllStorageItemMatches).ToArray()).Where(id=>!OrganizerGearProtected(id)).ToDictionary(id=>id,_=>int.MaxValue);
        if(localStorageRemaining.Count==0)throw new InvalidOperationException("No selected items match the current filters and protections.");
        if(entrust&&LocalStoragePolicy.Storeroom(type)){
            indoorFurniture??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.HousingFurniture>().Select(x=>x.Item.RowId).ToHashSet();
            outdoorFurniture??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.HousingYardObject>().Select(x=>x.Item.RowId).ToHashSet();
            var allowed=type is >=27001 and <=27011?indoorFurniture:outdoorFurniture;
            localStorageRemaining=localStorageRemaining.Where(x=>allowed.Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value);
            if(localStorageRemaining.Count==0)throw new InvalidOperationException("Select furnishings for this indoor/outdoor storeroom. Other item types cannot be stored in housing.");
        }
        localStorageKey=key;localStorageType=type;localStorageTerritory=Client.TerritoryType;chestOwner=Player.ContentId;chestWorld=Player.CurrentWorld.RowId;
        localStoragePending=null;localStorageVisited.Clear();chestMoves=localStorageBatchMoves;chestNext=default;chestDeadline=DateTimeOffset.UtcNow.AddSeconds(15);
        localStorageRunning=chestRunning=localStorageStatusShown=true;chestStatus=entrust?"Preparing storage deposit":"Preparing storage withdrawal";
    }
    private unsafe void TickLocalStorage(DateTimeOffset now)
    {
        if(now<chestNext)return;chestNext=now.AddMilliseconds(250);
        try{
            if(!config.EnableRetriever||!Player.IsLoaded||Player.ContentId!=chestOwner||Player.CurrentWorld.RowId!=chestWorld||Client.TerritoryType!=localStorageTerritory||organizeRunning||retrievalRun.Running||gearRouting||armoireRunning||sellerRun.Running||TraderRunning)throw new InvalidOperationException("Character, location or active operation changed.");
            RequireLocalStorage(localStorageKey,localStorageType);
            var manager=InventoryManager.Instance();if(manager==null)throw new InvalidOperationException("Inventory unavailable.");
            foreach(var operation in manager->PendingOperations)if(!operation.IsEmpty){if(now>chestDeadline)throw new InvalidOperationException("Inventory transaction timed out; no retry sent.");return;}
            var store=localStorageDuplicates?localStorageDuplicatePages.SelectMany(ReadChestContainer).ToArray():ReadChestContainer(localStorageType);var bags=new uint[]{0,1,2,3}.SelectMany(ReadChestContainer).ToArray();
            if(localStorageDuplicates){TickSaddlebagDuplicateDeposit(store,bags,now,manager);return;}
            if(localStoragePending is {} sent){
                var all=store.Concat(bags).ToArray();var src=all.Single(s=>s.Container==sent.Source.Container&&s.Slot==sent.Source.Slot);var dst=all.Single(s=>s.Container==sent.Destination.Container&&s.Slot==sent.Destination.Slot);
                if(!ChestTransferPlanner.Confirmed(sent,src,dst)){localStorageReceiptAt=default;if(now>chestDeadline)throw new InvalidOperationException("Transfer was not confirmed. Check permissions, furnishing restrictions and any game prompt; no retry sent.");chestStatus="Waiting for the game to confirm the storage transfer";return;}
                // Native inventory changes may appear before the server settles them.
                if(localStorageReceiptAt==default){localStorageReceiptAt=now;return;}
                if(now-localStorageReceiptAt<TimeSpan.FromSeconds(2))return;
                localStorageReceiptAt=default;
                if(!localStorageEntrust)localStorageWithdrawn[sent.Source.Item]=localStorageWithdrawn.GetValueOrDefault(sent.Source.Item)+sent.Quantity;
                if(!localStorageDuplicates)localStorageRemaining[sent.Source.Item]-=sent.Quantity;localStorageVisited.Add((sent.Source.Container,sent.Source.Slot));localStoragePending=null;chestMoves++;
                ObserveStorage(now);myInventoryRefresh=default;RequestStorageRefresh();
            }
            RefreshGearSetProtection();
            var sources=localStorageEntrust?bags:store;var destinations=localStorageEntrust?store:bags;
            var candidates=sources.Where(s=>s.Item>0&&s.Quantity>0&&localStorageRemaining.GetValueOrDefault(s.Item)>0&&!localStorageVisited.Contains((s.Container,s.Slot))&&!OrganizerGearProtected(s.Item)&&RetainerRetrieval.AllowsQuality(s.Hq,localStorageQuality)&&InventoryTradeFilterAllows(s,localStorageFilter)).ToArray();
            foreach(var source in candidates){
                if(source.Quantity>localStorageRemaining[source.Item])continue;
                // Empty destination preserves the exact copy's dyes, binding and other metadata.
                var destination=destinations.FirstOrDefault(s=>s.Item==0&&s.Quantity==0);
                if(destination==null)throw new InvalidOperationException("No empty destination slot is available. Confirmed transfers are kept; free space and retry remaining items.");
                if(!LocalStoragePolicy.CanMove(source.Container,destination.Container))throw new InvalidOperationException("Transfer is outside the selected storage and bags.");
                localStoragePending=new(source,destination,source.Quantity);chestDeadline=now.AddSeconds(12);
                chestStatus=$"{chestMoves} confirmed · {(localStorageEntrust?"Storing":"Withdrawing")} {SearchItemName(source.Item)}";
                manager->MoveItemSlot((InventoryType)source.Container,(ushort)source.Slot,(InventoryType)destination.Container,(ushort)destination.Slot);
                return;
            }
            if(candidates.Length==0&&localStorageQueue.Count>0){
                localStorageBatchMoves=chestMoves;var next=localStorageQueue.Dequeue();
                // The preceding page has no pending move; only now start another page.
                localStorageRunning=chestRunning=false;
                StartLocalStorage(next.Key,false,next.Items,true);return;
            }
            StopChest(candidates.Length>0?$"{chestMoves} transfers confirmed. Remaining selections require a partial stack; select the full quantity to transfer here.":$"Complete · {chestMoves} storage transfers confirmed; no further selected copies match filters and protections.");
        }catch(Exception e){StopChest("Stopped: "+e.Message);}
    }
}
