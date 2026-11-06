using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.Exd;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool keyboundBuyAndUse,keyboundUsingRewards,keyboundUseShopClosed;
    private readonly HashSet<uint> keyboundUseCandidates=[],keyboundUseRemaining=[];
    private int keyboundCoffersOpened;
    private int keyboundUseReady,keyboundUsed,keyboundUseSkipped,keyboundUseBefore;
    private KeyboundOffer? keyboundUsePending;
    private DateTimeOffset keyboundUseDeadline;
    private CatalogueEntry[][]? keyboundUnlockCatalogue;
    private readonly Dictionary<uint,CatalogueEntry?> keyboundUnlockEntries=[];
    private static readonly InventoryType[] KeyboundUseBags=[InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4];
    private unsafe int KeyboundBagCount(uint item)
    {
        var manager=InventoryManager.Instance();if(manager==null)throw new InvalidOperationException("Waiting for inventory.");
        var count=0;
        foreach(var type in KeyboundUseBags){
            var bag=manager->GetInventoryContainer(type);
            if(bag==null||!bag->IsLoaded||bag->Items==null||bag->Size is <=0 or >200)throw new InvalidOperationException("Waiting for inventory bags.");
            for(var i=0;i<bag->Size;i++)if(bag->Items[i].ItemId==item)count+=checked((int)bag->Items[i].Quantity);
        }
        return count;
    }
    private void FinishKeyboundBuying()
    {
        if(!keyboundBuyAndUse){StopKeyboundBuying(KeyboundBuyFinished());return;}
        keyboundUsingRewards=true;keyboundUseShopClosed=false;keyboundUseDeadline=DateTimeOffset.UtcNow.AddSeconds(30);keyboundBuyNext=default;
        keyboundBuyStatus="Purchases finished · preparing reward use…";
        TraceKeyboundRun(keyboundBuyerRun,"use-phase-started",keyboundBuyStatus);
    }
    private int KeyboundUnlockState(uint item)=>ReadKeyboundUnlockState(item,out _,out _);
    private unsafe int ReadKeyboundUnlockState(uint item,out int native,out bool? collection)
    {
        native=3;collection=null;
        var state=UIState.Instance();var row=ExdModule.GetItemRowById(item);
        if(state==null||!state->PlayerState.IsLoaded||row==null)return 3;
        native=(int)state->IsItemActionUnlocked(row);
        if(native==1||catalogueBase==null)return native;
        if(!ReferenceEquals(keyboundUnlockCatalogue,catalogueBase)){keyboundUnlockCatalogue=catalogueBase;keyboundUnlockEntries.Clear();}
        if(!keyboundUnlockEntries.TryGetValue(item,out var entry)){
            var entries=catalogueBase.SelectMany(g=>g).Where(e=>e.ItemId==item&&e.Category is "mount" or "minion" or "orchestrion" or "emote" or "ornament" or "framerkit")
            .DistinctBy(e=>(e.Category,e.Id)).Take(2).ToArray();
            entry=entries.Length==1?entries[0]:null;keyboundUnlockEntries[item]=entry;
        }
        if(entry==null)return native;
        collection=entry.Category switch{
            "mount"=>state->PlayerState.IsMountUnlocked(entry.Id),
            "minion"=>state->IsCompanionUnlocked(entry.Id),
            "orchestrion"=>state->PlayerState.IsOrchestrionRollUnlocked(entry.Id),
            "emote" when entry.Id<=ushort.MaxValue=>state->IsEmoteUnlocked((ushort)entry.Id),
            "ornament"=>state->PlayerState.IsOrnamentUnlocked(entry.Id),
            "framerkit"=>state->PlayerState.IsFramersKitUnlocked(entry.Id),
            _=>null
        };
        return KeyboundUsePolicy.ResolveUnlock(native,collection);
    }
    private unsafe void UpdateKeyboundRewardUse(DateTimeOffset now)
    {
        if(gearRouting||armoireRunning||retrievalRun.Running||organizeRunning||sellerRun.Running||TraderRunning)throw new InvalidOperationException("Reward use stopped: another inventory operation started.");
        if(keyboundUsePending is {} pending){
            if(KeyboundUsePolicy.IsEventCoffer(pending.Key)){
                var remaining=KeyboundBagCount(pending.Item);
                if(KeyboundUsePolicy.CofferConsumed(keyboundUseBefore,remaining)){
                    TraceKeyboundRun(keyboundBuyerRun,"coffer-consumption-confirmed",pending.Name);
                    keyboundCoffersOpened++;keyboundUsePending=null;
                    if(remaining==0)keyboundUseRemaining.Remove(pending.Item);
                    keyboundUseDeadline=now.AddSeconds(30);keyboundBuyNext=now.AddSeconds(1);RequestStorageRefresh();
                    keyboundBuyStatus=$"Opened {keyboundCoffersOpened} Keybound Coffer(s) · {remaining} left";
                    return;
                }
            }
            else if(KeyboundUsePolicy.Confirmed(keyboundUseBefore,KeyboundBagCount(pending.Item),KeyboundUnlockState(pending.Item))){
                TraceKeyboundRun(keyboundBuyerRun,"unlock-confirmed",pending.Name);
                keyboundCollectionRefreshItems.Add(pending.Item);keyboundUsed++;keyboundUseRemaining.Remove(pending.Item);keyboundUsePending=null;keyboundUseDeadline=now.AddSeconds(30);RequestStorageRefresh();
                keyboundBuyStatus="Unlocked: "+pending.Name;keyboundBuyNext=now.AddSeconds(1);return;
            }
            if(now>=keyboundUseDeadline.AddSeconds(30)){
                var held=KeyboundBagCount(pending.Item);var unlocked=KeyboundUnlockState(pending.Item);
                var reason=$"Reward use needs verification: {pending.Name}. Bags {keyboundUseBefore} → {held}; unlock state {unlocked}. No repeat use sent. Export diagnostics before retrying.";
                TraceKeyboundRun(keyboundBuyerRun,"item-use-confirmation-timeout",reason);
                StopKeyboundBuying(reason);return;
            }
            if(now>=keyboundUseDeadline){
                var waiting="Verifying reward use: "+pending.Name+". Waiting up to 30 more seconds for collection confirmation; no repeat use sent.";
                if(keyboundBuyStatus!=waiting){
                    keyboundBuyStatus=waiting;
                    TraceKeyboundRun(keyboundBuyerRun,"item-use-confirmation-delayed",waiting);
                    keyboundCollectionRefreshItems.Add(pending.Item);RequestStorageRefresh();
                }
            }
            return; // Never send the same use request again while awaiting its result.
        }
        if(now>=keyboundUseDeadline)throw new InvalidOperationException("Reward use stopped: "+keyboundBuyStatus+". No further item-use request sent.");
        if(KeyboundPurchaseDialogVisible())throw new InvalidOperationException("Close the open confirmation before using rewards.");
        var shop=PurchaseShop();
        if(shop.Length>0){
            if(keyboundUseShopClosed){keyboundBuyStatus="Waiting for MGC shop to close";return;}
            if(keyboundBuyerObject==0||Targets.Target?.GameObjectId!=keyboundBuyerObject||!KeyboundIsTrader(Targets.Target))throw new InvalidOperationException("Close the other shop before using rewards.");
            var addon=KeyboundAddon(shop);if(addon==null)return;
            keyboundUseShopClosed=true;helperReplaying=true;
            keyboundBuyStatus="Closing MGC shop before using rewards";
            Log.Information("Keybound rewards: closing MGC shop with its close callback");
            // Notify the shop agent/server as well as closing its window.
            // Close(false) can leave the NPC event active with no visible shop.
            try{if(!addon->Close(true))throw new InvalidOperationException("MGC shop did not accept closing; reward use stopped.");}finally{helperReplaying=false;}
            keyboundBuyNext=now.AddSeconds(1);return;
        }
        keyboundUseShopClosed=true;
        if(VisibleFollowAddon("Talk")||VisibleFollowAddon("SelectString")||Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.OccupiedInQuestEvent]){
            keyboundBuyStatus="Waiting for NPC interaction to finish before using rewards";return;
        }
        if(Conditions[ConditionFlag.Casting]||Conditions[ConditionFlag.Mounted]){
            keyboundBuyStatus="Waiting: finish casting or dismount to use rewards";return;
        }
        foreach(var id in keyboundUseRemaining.OrderBy(id=>keyboundOffers!.Single(o=>o.Item==id).Key=="coffer"?1:0).ToArray()){
            var offer=keyboundOffers!.Single(o=>o.Item==id);var held=KeyboundBagCount(id);
            var coffer=KeyboundUsePolicy.IsEventCoffer(offer.Key);
            if(coffer){
                if(held==0){keyboundUseRemaining.Remove(id);continue;}
                if(!KeyboundUsePolicy.CanOpenCoffer(offer.Key,held,EventExchangeHasSpace(1)))
                    throw new InvalidOperationException($"Coffers paused: free an inventory slot, then Buy all & use again. Opened {keyboundCoffersOpened}; {held} left.");
            }else{
                var unlocked=KeyboundUnlockState(id);
                if(unlocked==1){TraceKeyboundRun(keyboundBuyerRun,"already-unlocked",offer.Name);keyboundUseRemaining.Remove(id);continue;}
                if(unlocked==3){keyboundBuyStatus="Waiting for unlock information: "+offer.Name;return;}
                if(!KeyboundUsePolicy.CanUse(offer.Key,true,unlocked,held)){TraceKeyboundRun(keyboundBuyerRun,"use-skipped",offer.Name+" · held "+held+" · unlock state "+unlocked);keyboundUseRemaining.Remove(id);keyboundUseSkipped++;continue;}
            }
            var manager=InventoryManager.Instance();var agent=AgentInventoryContext.Instance();if(manager==null||agent==null)return;
            foreach(var type in KeyboundUseBags){
                var bag=manager->GetInventoryContainer(type);
                if(bag==null||!bag->IsLoaded||bag->Items==null)throw new InvalidOperationException("Inventory changed before reward use.");
                for(var slot=0;slot<bag->Size;slot++){
                    if(bag->Items[slot].ItemId!=id||bag->Items[slot].Quantity==0)continue;
                    // Only catalogue-approved unlocks and the exact event coffer.
                    // One request stays in flight until its consumption is confirmed.
                    keyboundUsePending=offer;keyboundUseBefore=held;keyboundUseDeadline=now.AddSeconds(30);keyboundBuyStatus=coffer?$"Opening Keybound Coffer · {held} left":"Using: "+offer.Name;
                    TraceKeyboundRun(keyboundBuyerRun,"item-use-requested",keyboundBuyStatus);
                    Log.Information("Keybound rewards: requesting item use {ItemId} from {Inventory} slot {Slot}",id,type,slot);
                    helperReplaying=true;try{agent->UseItem(id,type,(uint)slot);}finally{helperReplaying=false;}
                    return;
                }
            }
            throw new InvalidOperationException("Reward moved before it could be used.");
        }
        TraceKeyboundRun(keyboundBuyerRun,"completed",$"Unlocked {keyboundUsed}; coffers opened {keyboundCoffersOpened}; skipped {keyboundUseSkipped}");
        StopKeyboundBuying($"Bought {keyboundBought} · unlocked {keyboundUsed}"+(keyboundCoffersOpened>0?$" · coffers opened {keyboundCoffersOpened}":"")+(keyboundUseSkipped>0?$" · {keyboundUseSkipped} unavailable in bags or unsupported.":" · Done.")+(keyboundHeldRewards.Count>0?$" {keyboundHeldRewards.Count} purchases held for verification.":""));
    }
}


