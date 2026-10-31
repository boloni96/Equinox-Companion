using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Component.Exd;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed record KeyboundOffer(uint Item,string Key,string Name,int Cost);
    private KeyboundPurchaseBook? keyboundPurchases;
    private KeyboundOffer[]? keyboundOffers;
    private bool keyboundEquipmentCheckSent;
    private bool keyboundBuying,keyboundBuySelected,keyboundBuyConfirmed,keyboundBuyerOpened;
    private ulong keyboundBuyerCharacter,keyboundBuyerObject;
    private uint keyboundBuyerWorld,keyboundBuyerTerritory,keyboundBuyIndex;
    private KeyboundOffer? keyboundBuyItem;
    private KeyboundPurchaseReceipt? keyboundBuyIntent;
    private string keyboundBuyShop="",keyboundBuyStatus="";
    private readonly HashSet<uint> keyboundBuyRemaining=[];
    private DateTimeOffset keyboundBuyNext,keyboundBuyDeadline,keyboundBuyRefresh;
    private readonly HashSet<uint> keyboundOwned=[];
    private Dictionary<string,int>? keyboundMissingPlan;
    private string keyboundPlanError="";
    private readonly HashSet<string> keyboundHeldRewards=[];
    private Dictionary<string,int>? keyboundReadyPlan;
    private int keyboundBought,keyboundGreetingCount;

    private void LoadKeyboundOffers()
    {
        keyboundPurchases??=new(Path.Combine(Pi.GetPluginConfigDirectory(),"keybound-purchases.json"));
        if(keyboundOffers!=null)return;
        var items=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
        var prizes=KeyboundShoppingPolicy.Prizes.ToDictionary(p=>KeyboundShoppingPolicy.Normalize(p.Name));
        var offers=new List<KeyboundOffer>();
        foreach(var item in items){
            if(prizes.TryGetValue(KeyboundShoppingPolicy.Normalize(item.Name.ToString()),out var prize))
                offers.Add(new(item.RowId,prize.Key,prize.Name,prize.Cost));
        }
        if(offers.Count!=prizes.Count||offers.Select(o=>o.Key).Distinct().Count()!=prizes.Count)throw new InvalidOperationException("Reward catalogue does not match the current game.");
        LoadArmoireCatalogue();keyboundOffers=offers.ToArray();
    }
    private unsafe void RefreshKeyboundBuyPlan()
    {
        keyboundMissingPlan=null;keyboundReadyPlan=null;keyboundPlanError="";keyboundHeldRewards.Clear();
        try{
            if(!Client.IsLoggedIn||!Player.IsLoaded||Player.ContentId==0)return;
            LoadKeyboundOffers();keyboundPurchases!.Reload();var character=Player.ContentId;keyboundOwned.Clear();
            var roster=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>c.ContentId==character.ToString()).DistinctBy(c=>c.Id).Take(2).ToArray();
            var wishes=roster.Length==1?roster[0].KeyboundWishlist:null;
            if(config.KeyboundUseJournalWishlist&&wishes==null)throw new InvalidOperationException("Waiting for Journal wishlist sync.");
            Dictionary<string,int>? plan=null;
            if(config.KeyboundUseJournalWishlist){plan=KeyboundShoppingPolicy.WishlistPlan(wishes!.Select(w=>(w.Name,w.Wanted,w.Obtained)),out var unmatched);if(unmatched>0)throw new InvalidOperationException($"{unmatched} wishlist reward(s) could not be matched; nothing purchased.");}
            var state=UIState.Instance();if(state==null||!state->PlayerState.IsLoaded)throw new InvalidOperationException("Waiting for character collections.");
            // Positive storage observations also protect items moved into storage
            // that is not currently open. Never borrow another character's stock.
            var observed=DiscoveryReads().Kind("storage.observed").Where(e=>e.Actor.ContentId==character.ToString()&&e.Storage!=null&&(e.Storage.Key.StartsWith("bag:")||e.Storage.Key.StartsWith("retainer:")||e.Storage.Key is "armoire" or "dresser")).SelectMany(e=>e.Storage!.Items).ToHashSet();
            if(roster.Length==1)observed.UnionWith((config.SharedRoster?.ItemLocations??[])
                .Where(l=>l.OwnerId==roster[0].Id&&(l.Key.StartsWith("bag:")||l.Key.StartsWith("retainer:")||l.Key is "armoire" or "dresser"))
                .SelectMany(l=>l.Items));
            var wanted=new Dictionary<string,int>();
            foreach(var offer in keyboundOffers!){
                var receipt=keyboundPurchases!.Find(character,offer.Item);
                var row=ExdModule.GetItemRowById(offer.Item);var unlocked=row==null?3:state->IsItemActionUnlocked(row);
                var owned=ArmoireInventory(offer.Item).Held>0||unlocked==1||observed.Contains(offer.Item)||receipt?.Confirmed==true||
                    wishes?.Any(w=>w.Obtained&&KeyboundShoppingPolicy.PrizeKey(w.Name)==offer.Key)==true;
                if(!owned&&armoireRows!.TryGetValue(offer.Item,out var cabinet)&&state->Cabinet.IsCabinetLoaded())owned=state->Cabinet.IsItemInCabinet(cabinet);
                var dresser=MirageManager.Instance();if(!owned&&dresser!=null&&dresser->PrismBoxLoaded)owned=GearInDresser(offer.Item);
                if(owned)keyboundOwned.Add(offer.Item);
                if(KeyboundShoppingPolicy.Quantity(plan,offer.Key)>0&&!owned){
                    if(!KeyboundBuyingPolicy.CanSelect(receipt,KeyboundWallet(),ArmoireInventory(offer.Item).Held)&&!(receipt!=null&&CanResumeKeyboundPurchase(offer,receipt,out _,out _)))keyboundHeldRewards.Add(offer.Key);
                    if(unlocked==3)throw new InvalidOperationException("Waiting for reward unlock information.");
                    wanted[offer.Key]=1;
                }
            }
            keyboundMissingPlan=wanted;
            keyboundReadyPlan=KeyboundBuyingPolicy.ReadyPlan(wanted,keyboundHeldRewards);
        }catch(Exception e){keyboundPlanError=e.Message;}
    }
    private void DrawKeyboundBuyButton()
    {
        if(keyboundBuying){if(ImGui.Button("Stop buying"))StopKeyboundBuying("Stopped; confirmed purchases are saved.");}
        else{
            ImGui.BeginDisabled(keyboundReadyPlan==null||keyboundReadyPlan.Count==0);
            if(ImGui.Button("Buy all missing"))StartKeyboundBuying();
            ImGui.EndDisabled();
            if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("At the MGC trader: buy one of each missing reward, or your Journal wishlist when enabled. Checks this character's inventory, unlocks, recorded storage and saved purchases. Visit storage to refresh older holdings. No reward list is added to this window.");
        }
        if(keyboundBuyStatus.Length>0)ImGui.TextWrapped(keyboundBuyStatus);
    }
    private unsafe uint KeyboundWallet(){var c=CurrencyManager.Instance();if(c==null||!c->HasItem(52322))throw new InvalidOperationException("MGC wallet is not loaded.");return c->GetItemCount(52322);}
    private bool KeyboundIsTrader(Dalamud.Game.ClientState.Objects.Types.IGameObject? obj)=>obj!=null&&obj.ObjectKind==ObjectKind.EventNpc&&string.Equals(DataManager.GetExcelSheet<Lumina.Excel.Sheets.ENpcResident>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(obj.BaseId)?.Singular.ToString(),"MGC trader",StringComparison.OrdinalIgnoreCase);
    private unsafe void StartKeyboundBuying()
    {
        try{
            RefreshKeyboundBuyPlan();if(keyboundReadyPlan==null)throw new InvalidOperationException(keyboundPlanError);
            if(keyboundReadyPlan.Count==0)throw new InvalidOperationException(keyboundHeldRewards.Count>0?"Remaining purchases need verification; duplicate purchases are held.":"All selected rewards are already obtained.");
            if(!config.EnableKeyboundBrawler||Client.TerritoryType==1359||Conditions[ConditionFlag.InDutyQueue]||Conditions[ConditionFlag.WaitingForDuty])throw new InvalidOperationException("Finish the round / leave the queue before buying.");
            if(followSession.Armed||purchasePending!=null||helperExchangePending!=null||gearRouting||armoireRunning||retrievalRun.Running||organizeRunning||sellerRun.Running||TraderRunning)throw new InvalidOperationException("Finish the other helper or storage operation first.");
            var self=Objects.LocalPlayer??throw new InvalidOperationException("Character unavailable.");
            var trader=Objects.Where(o=>KeyboundIsTrader(o)&&Vector3.Distance(self.Position,o.Position)<=o.HitboxRadius+4).OrderBy(o=>Vector3.DistanceSquared(self.Position,o.Position)).FirstOrDefault()??throw new InvalidOperationException("Move beside the MGC trader, then Buy all missing.");
            if(KeyboundShoppingPolicy.Total(keyboundReadyPlan)>KeyboundWallet())throw new InvalidOperationException("Not enough MGC for all missing rewards in this list.");
            if(!EventExchangeHasSpace(keyboundReadyPlan.Count))throw new InvalidOperationException($"Keep {keyboundReadyPlan.Count} inventory slots free for these rewards.");
            var resume=keyboundOffers!.FirstOrDefault(o=>keyboundReadyPlan.ContainsKey(o.Key)&&keyboundPurchases!.Find(Player.ContentId,o.Item) is {} r&&CanResumeKeyboundPurchase(o,r,out _,out _));
            if(KeyboundPurchaseDialogVisible()&&resume==null)throw new InvalidOperationException("Close the unrelated purchase confirmation first.");
            if((PurchaseShop().Length>0||VisibleFollowAddon("SelectString")||VisibleFollowAddon("Talk"))&&Targets.Target?.GameObjectId!=trader.GameObjectId)throw new InvalidOperationException("Close the other NPC's window first.");
            StopKeybound();keyboundBuying=true;keyboundBuyerCharacter=Player.ContentId;keyboundBuyerWorld=Player.CurrentWorld.RowId;keyboundBuyerTerritory=Client.TerritoryType;keyboundBuyerObject=trader.GameObjectId;
            keyboundBuyRemaining.Clear();keyboundBuyRemaining.UnionWith(keyboundOffers!.Where(o=>keyboundReadyPlan.ContainsKey(o.Key)).Select(o=>o.Item));
            keyboundEquipmentCheckSent=false;keyboundBuySelected=keyboundBuyConfirmed=keyboundBuyerOpened=false;keyboundBuyItem=null;keyboundBuyIntent=null;keyboundBought=keyboundGreetingCount=0;keyboundVisitedTabs.Clear();keyboundTabFingerprint="";keyboundTabStableAt=default;keyboundRequestedTab=null;keyboundTabPrimed=false;keyboundRowFingerprint="";
            keyboundBuyNext=default;keyboundBuyDeadline=DateTimeOffset.UtcNow.AddSeconds(45);keyboundBuyStatus="Buying missing rewards…";
            Targets.Target=trader;
            if(resume!=null){
                var receipt=keyboundPurchases!.Find(Player.ContentId,resume.Item)!;
                if(!CanResumeKeyboundPurchase(resume,receipt,out var resumeShop,out var resumeIndex))throw new InvalidOperationException("Pending confirmation changed.");
                keyboundBuyItem=resume;keyboundBuyIntent=receipt;keyboundBuyShop=resumeShop;keyboundBuyIndex=resumeIndex;keyboundBuySelected=true;keyboundBuyConfirmed=receipt.Submitted;
                keyboundBuyStatus="Resuming confirmation: "+resume.Name;
            }
        }catch(Exception e){StopKeyboundBuying(e.Message);}
    }
    private string KeyboundBuyFinished()=>$"Bought {keyboundBought} rewards"+(keyboundHeldRewards.Count>0?$" · {keyboundHeldRewards.Count} held for verification.":" · Done.");
    private void StopKeyboundBuying(string reason){keyboundBuying=false;keyboundBuyStatus=reason;keyboundBuyRefresh=default;}
    private bool KeyboundPurchaseDialogVisible()=>new[]{"SelectYesno","ShopExchangeItemDialog","ShopExchangeCurrencyDialog","Request"}.Any(VisibleFollowAddon);
    private unsafe KeyboundOffer? KeyboundReadOffer(string shop,int row,out uint index)
    {
        index=0;var addon=KeyboundAddon(shop);if(addon==null||shop is not ("ShopExchangeCurrency" or "ShopExchangeItem"))return null;
        if(!ShopUInt(addon,shop=="ShopExchangeCurrency"?4:3,out var count)||count is <1 or >122||row<0||row>=count||!ShopUInt(addon,1066+row,out var item)||!ShopUInt(addon,1310+row,out index))return null;
        var offer=keyboundOffers!.FirstOrDefault(o=>o.Item==item);if(offer==null)return null;
        // Reuse the established exchange reader, which verifies the row,
        // native receive index, quantity, currency identity and visible price.
        var quote=ReadPurchaseRow(shop,row,1,out var verifiedIndex);
        if(quote!=null&&verifiedIndex==index&&quote.ItemId==item&&quote.Costs.Length==1&&
            KeyboundBuyingPolicy.ExactCost(quote.Costs[0].ItemId,(uint)quote.Costs[0].Amount,offer.Cost,1,quote.Quantity))return offer;
        var agent=AgentShop.Instance();if(agent==null||!agent->IsAgentActive()||agent->ItemReceive==null||agent->ItemCost==null||agent->ItemReceiveCount is <1 or >122||index>=agent->ItemReceiveCount||agent->ItemCostCount<index*3+3||agent->ItemCostCount>366)return null;
        var receive=agent->ItemReceive[index];var cost=agent->ItemCost[index*3];
        if(receive.ItemId!=item||!KeyboundBuyingPolicy.ExactCost(cost.ItemId,cost.ItemCount,offer.Cost,(int)receive.ItemCount,1))return null;
        for(var i=1;i<3;i++)if(agent->ItemCost[index*3+i].ItemId!=0||agent->ItemCost[index*3+i].ItemCount!=0)return null;
        return offer;
    }
    private unsafe void UpdateKeyboundBuying(IFramework framework)
    {
        var now=DateTimeOffset.UtcNow;
        if(!keyboundBuying){
            if(config.EnableKeyboundBrawler&&keyboundWindow.IsOpen&&now>=keyboundBuyRefresh){keyboundBuyRefresh=now.AddSeconds(2);RefreshKeyboundBuyPlan();}return;
        }
        if(now<keyboundBuyNext)return;keyboundBuyNext=now.AddMilliseconds(350);
        try{
            if(!config.EnableKeyboundBrawler||!Player.IsLoaded||Player.ContentId!=keyboundBuyerCharacter||Player.CurrentWorld.RowId!=keyboundBuyerWorld||Client.TerritoryType!=keyboundBuyerTerritory||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InCombat]||followSession.Armed||purchasePending!=null||helperExchangePending!=null)throw new InvalidOperationException("Buying stopped: character, location or active helper changed.");
            var held=keyboundBuyItem==null?0:ArmoireInventory(keyboundBuyItem.Item).Held;
            if(keyboundBuyIntent is {} intent&&KeyboundBuyingPolicy.Confirmed(intent,KeyboundWallet(),held)){
                keyboundPurchases!.Put(intent with {Confirmed=true});keyboundBuyRemaining.Remove(intent.Item);keyboundBought++;keyboundBuyItem=null;keyboundBuyIntent=null;keyboundBuySelected=keyboundBuyConfirmed=false;keyboundBuyDeadline=now.AddSeconds(45);RequestStorageRefresh();keyboundVisitedTabs.Clear();RefreshKeyboundBuyPlan();keyboundBuyStatus=$"Bought {keyboundBought} · {keyboundBuyRemaining.Count} remaining";return;
            }
            if(now>=keyboundBuyDeadline)throw new InvalidOperationException(keyboundBuyIntent==null?"Shop did not become ready. Buying stopped.":"Purchase was not confirmed. Check inventory; this item will not be retried automatically.");
            var trader=Objects.FirstOrDefault(o=>o.GameObjectId==keyboundBuyerObject);var self=Objects.LocalPlayer;
            if(!KeyboundIsTrader(trader)||self==null||Vector3.Distance(self.Position,trader!.Position)>trader.HitboxRadius+4||Targets.Target?.GameObjectId!=keyboundBuyerObject)throw new InvalidOperationException("MGC trader changed or is out of reach.");
            if(keyboundBuySelected&&TryHandleKeyboundEquipmentWarning())return;
            if(keyboundBuyConfirmed)return;
            if(keyboundBuySelected){ConfirmKeyboundBuy();return;}
            if(KeyboundPurchaseDialogVisible())return;
            if(keyboundBuyRemaining.Count==0){StopKeyboundBuying(KeyboundBuyFinished());return;}
            var shop=PurchaseShop();
            if(shop.Length>0){
                if(!KeyboundShopTabReady(shop,now))return;
                var rows=new List<(KeyboundOffer Offer,uint Index)>();
                for(var i=0;i<122;i++)if(KeyboundReadOffer(shop,i,out var index) is {} offer)rows.Add((offer,index));
                RefreshKeyboundBuyPlan();if(keyboundReadyPlan==null)throw new InvalidOperationException(keyboundPlanError);
                keyboundBuyRemaining.RemoveWhere(id=>keyboundOwned.Contains(id)||!keyboundOffers!.Any(o=>o.Item==id&&keyboundReadyPlan.ContainsKey(o.Key)));
                // Recover unchanged unsubmitted reservations before spending on
                // fresh rewards, otherwise their original wallet baseline is lost.
                var recovery=keyboundOffers!.FirstOrDefault(o=>keyboundBuyRemaining.Contains(o.Item)&&keyboundPurchases!.Find(keyboundBuyerCharacter,o.Item)!=null);
                var next=rows.FirstOrDefault(r=>keyboundBuyRemaining.Contains(r.Offer.Item)&&(recovery==null||r.Offer.Item==recovery.Item));
                if(next.Offer==null){
                    if(keyboundBuyRemaining.Count==0){StopKeyboundBuying(KeyboundBuyFinished());return;}
                    if(recovery==null&&KeyboundVisibleMissingRow(shop))throw new InvalidOperationException("The visible reward price could not be verified; nothing purchased.");
                    AdvanceKeyboundShopTab(shop,now);return;
                }
                if(rows.Count(r=>r.Offer.Item==next.Offer.Item)!=1)throw new InvalidOperationException("Reward appears more than once in this shop.");
                if(KeyboundWallet()<next.Offer.Cost||!EventExchangeHasSpace(1))throw new InvalidOperationException("MGC or inventory space changed; buying stopped.");
                var reserved=keyboundPurchases!.Find(keyboundBuyerCharacter,next.Offer.Item);
                if(!KeyboundBuyingPolicy.CanSelect(reserved,KeyboundWallet(),ArmoireInventory(next.Offer.Item).Held)||reserved!=null&&reserved.Cost!=next.Offer.Cost)throw new InvalidOperationException("Pending reward changed; purchase held for verification.");
                keyboundBuyItem=next.Offer;keyboundBuyShop=shop;keyboundBuyIndex=next.Index;
                keyboundBuyIntent=reserved??new(keyboundBuyerCharacter,next.Offer.Item,next.Offer.Cost,KeyboundWallet(),ArmoireInventory(next.Offer.Item).Held);
                if(reserved==null)keyboundPurchases.Put(keyboundBuyIntent); // Must succeed before the callback.
                keyboundEquipmentCheckSent=false;keyboundBuySelected=true;keyboundBuyConfirmed=false;keyboundBuyDeadline=now.AddSeconds(20);
                var args=stackalloc AtkValue[4];for(var i=0;i<4;i++)args[i]=new(){Type=AtkValueType.Int,Int=0};args[1].Int=(int)next.Index;args[2].Int=1;
                helperReplaying=true;try{KeyboundAddon(shop)->FireCallback(shop=="ShopExchangeCurrency"?4u:3u,args,true);}finally{helperReplaying=false;}
                keyboundBuyStatus="Buying: "+next.Offer.Name;return;
            }
            if(VisibleFollowAddon("SelectString"))throw new InvalidOperationException("Open the MGC trader's Currency Exchange window, then Buy all missing.");
            if(VisibleFollowAddon("Talk")){
                if(!keyboundBuyerOpened||++keyboundGreetingCount>8)throw new InvalidOperationException("Continue the vendor greeting, then try buying again.");
                helperReplaying=true;try{ClickVisibleTalk();}finally{helperReplaying=false;}keyboundBuyNext=now.AddSeconds(1);return;
            }
            if(Conditions[ConditionFlag.OccupiedInEvent])return;
            if(keyboundBuyerOpened)return;
            keyboundBuyerOpened=true;helperReplaying=relayInteracting=true;
            try{TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)trader.Address,false);}finally{helperReplaying=relayInteracting=false;}
            keyboundBuyNext=now.AddSeconds(1);
        }catch(Exception e){StopKeyboundBuying(e.Message);Log.Warning(e,"Keybound reward buyer stopped");}
    }
    private unsafe void ConfirmKeyboundBuy()
    {
        var item=keyboundBuyItem!;var intent=keyboundBuyIntent!;
        if(KeyboundWallet()!=intent.Balance||ArmoireInventory(item.Item).Held!=intent.Held){
            // Item and wallet notifications can land on different frames. Never
            // click again after either changes; wait for the paired receipt.
            keyboundBuyConfirmed=true;return;
        }
        var matches=0;for(var i=0;i<122;i++)if(KeyboundReadOffer(keyboundBuyShop,i,out var index)?.Item==item.Item&&index==keyboundBuyIndex)matches++;
        if(matches!=1)throw new InvalidOperationException("Shop selection changed; nothing confirmed.");
        var agent=AgentShop.Instance();if(agent==null||agent->SelectedItemIndex!=keyboundBuyIndex)throw new InvalidOperationException("Selected reward changed.");
        if(!FindKeyboundConfirmation(item,keyboundBuyShop,keyboundBuyIndex,out var dialog,out var button))return;
        if(button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null)return;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var n=0;
        while(evt!=null&&n++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||n>32)return;
        var click=*evt;var data=new AtkEventData();
        keyboundBuyIntent=intent with {Submitted=true};keyboundPurchases!.Put(keyboundBuyIntent);
        keyboundBuyConfirmed=true;helperReplaying=true;
        try{dialog->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
    }
}
