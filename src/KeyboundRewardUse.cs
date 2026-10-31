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
    private int keyboundUseReady,keyboundUsed,keyboundUseSkipped,keyboundUseBefore;
    private KeyboundOffer? keyboundUsePending;
    private DateTimeOffset keyboundUseDeadline;
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
        keyboundBuyStatus="Purchases finished · preparing reward unlocks…";
    }
    private unsafe int KeyboundUnlockState(uint item)
    {
        var state=UIState.Instance();var row=ExdModule.GetItemRowById(item);
        return state==null||!state->PlayerState.IsLoaded||row==null?3:(int)state->IsItemActionUnlocked(row);
    }
    private unsafe void UpdateKeyboundRewardUse(DateTimeOffset now)
    {
        if(gearRouting||armoireRunning||retrievalRun.Running||organizeRunning||sellerRun.Running||TraderRunning)throw new InvalidOperationException("Reward use stopped: another inventory operation started.");
        if(keyboundUsePending is {} pending){
            if(KeyboundUsePolicy.Confirmed(keyboundUseBefore,KeyboundBagCount(pending.Item),KeyboundUnlockState(pending.Item))){
                keyboundCollectionRefreshItems.Add(pending.Item);keyboundUsed++;keyboundUseRemaining.Remove(pending.Item);keyboundUsePending=null;keyboundUseDeadline=now.AddSeconds(30);RequestStorageRefresh();
                keyboundBuyStatus="Unlocked: "+pending.Name;keyboundBuyNext=now.AddSeconds(1);return;
            }
            if(now>=keyboundUseDeadline)throw new InvalidOperationException("Unlock not confirmed: "+pending.Name+". Use stopped; check inventory and collections.");
            return; // Never send the same use request again while awaiting its result.
        }
        if(now>=keyboundUseDeadline)throw new InvalidOperationException("Reward use stopped: inventory or character did not become ready.");
        if(KeyboundPurchaseDialogVisible())throw new InvalidOperationException("Close the open confirmation before using rewards.");
        var shop=PurchaseShop();
        if(shop.Length>0){
            if(keyboundUseShopClosed)return;
            if(keyboundBuyerObject==0||Targets.Target?.GameObjectId!=keyboundBuyerObject||!KeyboundIsTrader(Targets.Target))throw new InvalidOperationException("Close the other shop before using rewards.");
            var addon=KeyboundAddon(shop);if(addon==null)return;
            keyboundUseShopClosed=true;helperReplaying=true;
            try{addon->Close(false);}finally{helperReplaying=false;}
            keyboundBuyNext=now.AddSeconds(1);return;
        }
        keyboundUseShopClosed=true;
        if(VisibleFollowAddon("Talk")||VisibleFollowAddon("SelectString")||Conditions[ConditionFlag.OccupiedInEvent]||Conditions[ConditionFlag.Casting]||Conditions[ConditionFlag.Mounted])return;
        foreach(var id in keyboundUseRemaining.ToArray()){
            var offer=keyboundOffers!.Single(o=>o.Item==id);var held=KeyboundBagCount(id);var unlocked=KeyboundUnlockState(id);
            if(unlocked==1){keyboundUseRemaining.Remove(id);continue;}
            if(unlocked==3)return;
            if(!KeyboundUsePolicy.CanUse(offer.Key,true,unlocked,held)){keyboundUseRemaining.Remove(id);keyboundUseSkipped++;continue;}
            var manager=InventoryManager.Instance();var agent=AgentInventoryContext.Instance();if(manager==null||agent==null)return;
            foreach(var type in KeyboundUseBags){
                var bag=manager->GetInventoryContainer(type);
                if(bag==null||!bag->IsLoaded||bag->Items==null)throw new InvalidOperationException("Inventory changed before reward use.");
                for(var slot=0;slot<bag->Size;slot++){
                    if(bag->Items[slot].ItemId!=id||bag->Items[slot].Quantity==0)continue;
                    // Select exactly this known unlock item in a normal bag.
                    // Keep an in-flight request until BOTH consumption and unlock arrive.
                    keyboundUsePending=offer;keyboundUseBefore=held;keyboundUseDeadline=now.AddSeconds(30);keyboundBuyStatus="Using: "+offer.Name;
                    helperReplaying=true;try{agent->UseItem(id,type,(uint)slot);}finally{helperReplaying=false;}
                    return;
                }
            }
            throw new InvalidOperationException("Reward moved before it could be used.");
        }
        StopKeyboundBuying($"Bought {keyboundBought} · unlocked {keyboundUsed}"+(keyboundUseSkipped>0?$" · {keyboundUseSkipped} unavailable in bags or unsupported.":" · Done.")+(keyboundHeldRewards.Count>0?$" {keyboundHeldRewards.Count} purchases held for verification.":""));
    }
}
