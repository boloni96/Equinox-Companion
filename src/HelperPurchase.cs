using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static readonly string[] PurchaseShops=["Shop","ShopExchangeCurrency","ShopExchangeItem"];
    private bool helperVendorPurchases;
    private HelperPurchase? purchaseQuote;
    private HelperNpc? purchaseVendor;
    private DateTimeOffset purchaseQuoteUntil,purchaseUntil,purchaseNext;
    private readonly HashSet<string> purchaseSent=new();
    private HelperAction? purchasePending;
    private int purchaseStage,purchaseBeforeItem;
    private bool purchaseOpenedVendor;
    private int purchaseGreetingClicks;
    private DateTimeOffset purchaseGreetingNext;
    private int[] purchaseBeforeCosts=[];
    private string PurchaseShop()=>PurchaseShops.FirstOrDefault(VisibleFollowAddon)??"";
    private unsafe HelperPurchase? ReadPurchaseRow(string name,int row,int quantity,out uint index)
    {
        index=0;var shop=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
        if(shop==null||!shop->IsVisible||!shop->IsReady||quantity is <1 or >99||!ShopUInt(shop,name=="Shop"?2:name=="ShopExchangeCurrency"?4:3,out var count)||count is <1 or >122||row<0||row>=count)return null;
        if(!ShopUInt(shop,(name=="Shop"?441:1066)+row,out var item)||item==0||item>=1000000)return null;
        index=(uint)row;
        if(name!="Shop"&&(!ShopUInt(shop,1310+row,out index)||index>=122))return null;
        var costs=new List<HelperPurchaseCost>();
        if(name=="Shop"){
            if(!ShopUInt(shop,75+row,out var cost)||cost==0||cost>int.MaxValue/quantity)return null;
            costs.Add(new(1,ExchangeItemName(1),(int)cost*quantity));
        }else{
            var agent=AgentShop.Instance();
            if(agent==null||!agent->IsAgentActive()||agent->ItemReceive==null||agent->ItemReceiveCount is <1 or >122||index>=agent->ItemReceiveCount)return null;
            var received=agent->ItemReceive[index];if(received.ItemId!=item||received.ItemCount!=1)return null;
            if(name=="ShopExchangeCurrency"){
                if(!ShopUInt(shop,87,out var icon)||icon==0||!ShopUInt(shop,456+row,out var cost)||cost==0||cost>int.MaxValue/quantity)return null;
                var currencies=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().Where(i=>i.Icon==icon).Take(2).ToArray();
                if(currencies.Length!=1)return null;
                costs.Add(new(currencies[0].RowId,currencies[0].Name.ToString(),(int)cost*quantity));
            }else{
                for(var slot=0;slot<3;slot++){
                    if(!ShopUInt(shop,3141+row*3+slot,out var id)||!ShopUInt(shop,2775+row*3+slot,out var cost))return null;
                    if(id==0&&cost==0)continue;
                    if(id==0||cost==0||cost>int.MaxValue/quantity)return null;
                    costs.Add(new(id,ExchangeItemName(id),(int)cost*quantity));
                }
            }
        }
        var quote=new HelperPurchase(name,item,ExchangeItemName(item),quantity,costs.ToArray());
        return HelperPurchasePolicy.Valid(quote)?quote:null;
    }
    private unsafe HelperPurchase? FindPurchase(HelperPurchase expected,out uint index)
    {
        index=0;HelperPurchase? found=null;var matches=0;
        for(var row=0;row<122;row++){
            var quote=ReadPurchaseRow(expected.Shop,row,expected.Quantity,out var candidate);
            if(!HelperPurchasePolicy.Same(expected,quote))continue;
            found=quote;index=candidate;matches++;
        }
        return matches==1?found:null;
    }
    private unsafe void CaptureHelperPurchase(AtkUnitBase* addon,uint count,AtkValue* values)
    {
        if(helperReplaying||!SharingQuest||values==null||count is <3 or >4||values[0].Type is not (AtkValueType.Int or AtkValueType.UInt)||values[0].Int!=0||values[1].Type is not (AtkValueType.Int or AtkValueType.UInt)||values[2].Type is not (AtkValueType.Int or AtkValueType.UInt))return;
        var name=PurchaseShops.FirstOrDefault(n=>(nint)addon==GardenGui.GetAddonByName(n).Address);
        if(name==null)return;
        ObservePurchaseMirroring(DateTimeOffset.UtcNow);
        // A manual button sends immediately; mirroring waits for confirmed inventory deltas.
        purchaseQuote=null;purchaseVendor=null;purchaseSent.Clear();
        var target=Targets.Target;var self=Objects.LocalPlayer;var map=AgentMap.Instance();
        if(target==null||self==null||map==null||target.ObjectKind!=ObjectKind.EventNpc||Vector3.Distance(self.Position,target.Position)>target.HitboxRadius+4)return;
        HelperPurchase? selected=null;var matches=0;
        for(var row=0;row<122;row++){
            var q=ReadPurchaseRow(name,row,values[2].Int,out var index);
            if(q!=null&&index==values[1].UInt){selected=q;matches++;}
        }
        if(matches!=1)return;
        purchaseQuote=selected;purchaseVendor=new(Guid.NewGuid().ToString("N"),target.BaseId,target.Name.TextValue,Client.TerritoryType,map->CurrentMapId,Player.CurrentWorld.RowId,FollowTravelPosition.From(target.Position),FollowTravelPosition.From(self.Position),self.Rotation);
        purchaseQuoteUntil=DateTimeOffset.UtcNow.AddSeconds(60);
        CapturePurchaseMirror();
    }
    private unsafe string PurchasePrompt()
    {
        var addon=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;var cut=AgentCutscene.Instance();
        if(addon==null||!addon->IsVisible||!addon->IsReady||addon->PromptText==null||cut!=null&&cut->SkipDialogAddonId==addon->Id)return "";
        return HelperConversationPolicy.NormalizePrompt(TravelMenuText(addon->PromptText->NodeText.StringPtr)??"");
    }
    private unsafe void DrawHelperPurchaseButton(HelperFollower follower)
    {
        var name=PurchaseShop();
        if(name.Length==0){if(HelperShopVisible()){ImGui.BeginDisabled();ImGui.Button("Follower will buy the same");ImGui.EndDisabled();ImGui.TextWrapped("This shop layout is not supported yet. Export diagnostics with its purchase window open.");}return;}
        DrawPurchaseMirrorToggle(follower);
        var now=DateTimeOffset.UtcNow;var quote=purchaseQuote;var vendor=purchaseVendor;
        var valid=quote!=null&&vendor!=null&&now<purchaseQuoteUntil&&quote.Shop==name&&Targets.Target?.BaseId==vendor.BaseId&&Targets.Target?.Name.TextValue==vendor.Name&&vendor.World==Player.CurrentWorld.RowId&&vendor.Territory==Client.TerritoryType&&FindPurchase(quote,out _)!=null;
        if(valid&&PurchasePrompt() is {Length:>0} prompt)purchaseQuote=quote=quote! with {Prompt=prompt};
        if(valid)ImGui.TextWrapped($"{quote!.Quantity} × {quote.ItemName} — {string.Join(" + ",quote.Costs.Select(c=>$"{c.Amount:N0} {c.Name}"))}");
        else ImGui.TextWrapped("Select an item and quantity in the vendor's Buy/Exchange window first.");
        ImGui.BeginDisabled(purchaseMirrorFollowers.Contains(follower.Id)||!valid||!helperVendorPurchases||!follower.VendorPurchases||!HelperPolicy.Audience(follower,now.ToUnixTimeMilliseconds())||purchaseSent.Contains(follower.Id));
        if(ImGui.Button("Follower will buy the same")){
            if(helperOutgoing.Count<32){
                var request=new HelperAction(Guid.NewGuid().ToString("N"),Player.CharacterName,Player.HomeWorld.RowId,"vendorPurchase",now.ToUnixTimeMilliseconds(),vendor!,Sessions:[follower.Id],Purchase:quote);
                helperOutgoing.Enqueue(request);purchaseSent.Add(follower.Id);helperError="Purchase requested for "+follower.Name+"; waiting for their result.";
                RecordFollowTravel("Vendor purchase requested",new {request.Id,quote});
            }
        }
        ImGui.EndDisabled();
        if(!helperVendorPurchases)ImGui.TextWrapped("Vendor purchases require Journal V7.11.92.");
        else if(!follower.VendorPurchases)ImGui.TextWrapped("Update the follower to Companion 0.5.1.99.");
    }
    private void ReceiveHelperPurchase(HelperAction action)
    {
        if(!HelperPurchasePolicy.Valid(action.Purchase)){helperLastIssue="Invalid vendor purchase rejected.";return;}
        if(purchasePending is {} active){
            if(purchaseQueue.Count>=16||!HelperPurchasePolicy.SameVendor(active.Npc,action.Npc)){helperLastIssue="Purchase queue full or vendor differs; request rejected.";RecordFollowTravel("Vendor purchase queue rejected",new {action.Id,reason=helperLastIssue});return;}
            purchaseQueue.Enqueue(action);RecordFollowTravel("Vendor purchase queued",new {action.Id,item=action.Purchase!.ItemName,queued=purchaseQueue.Count});return;
        }
        if(helperExchangePending!=null||helperIncoming.Count>0||helperReservedConversation.Length>0){helperLastIssue="Finish the current helper action before requesting a purchase.";return;}
        purchasePending=action;purchaseWaitingPreviousClose=false;purchaseRequestId=0;purchaseRequestSlot=0;purchaseRequestOptionPending=false;purchaseOpenedVendor=false;purchaseGreetingClicks=0;purchaseGreetingNext=default;purchaseStage=0;purchaseNext=default;purchaseUntil=DateTimeOffset.UtcNow.AddSeconds(60);helperExchangeReport="";
        helperQuestStatus="Purchase requested; checking vendor and exact costs.";followSession.Pause();RequestFollowMovementStop();nextHelperStatus=default;
    }
    private void FinishHelperPurchase(string reason,bool success=false)
    {
        RecordFollowTravel(success?"Vendor purchase confirmed":"Vendor purchase stopped",new {id=purchasePending?.Id,reason});purchasePending=null;
        helperExchangeReport=reason;helperLastIssue=success?"":reason;helperQuestStatus=reason;nextHelperStatus=default;
        if(success&&purchaseQueue.TryDequeue(out var next)){
            if(HelperPolicy.Fresh(next,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),followArmedAt)&&helperPermission.Allows("vendorPurchase")){ReceiveHelperPurchase(next);purchaseWaitingPreviousClose=true;return;}
            reason="Queued purchases expired or permission ended; remaining purchases cancelled.";helperExchangeReport=reason;helperLastIssue=reason;helperQuestStatus=reason;
        }
        purchaseQueue.Clear();ResumeAfterConfirmedTravel();
    }
    private unsafe void UpdateHelperPurchase(DateTimeOffset now)
    {
        if(purchasePending is not {} action)return;
        if(!helperPermission.Allows("vendorPurchase")||!followSession.Armed){purchasePending=null;purchaseQueue.Clear();return;}
        if(now>=purchaseUntil||!HelperPolicy.Fresh(action,now.ToUnixTimeMilliseconds(),followArmedAt)){FinishHelperPurchase("Purchase timed out; no automatic retry. Check inventory before requesting again.");return;}
        if(now<purchaseNext||!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
        purchaseNext=now.AddMilliseconds(300);
        if(purchaseWaitingPreviousClose){
            if(VisibleFollowAddon("Request")||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("ShopExchangeItemDialog")||VisibleFollowAddon("ShopExchangeCurrencyDialog"))return;
            purchaseWaitingPreviousClose=false;
        }
        var p=action.Purchase!;var inventory=InventoryManager.Instance();var map=AgentMap.Instance();var self=Objects.LocalPlayer;if(inventory==null||map==null||self==null)return;
        if(purchaseStage>=2&&HelperPurchasePolicy.Confirmed(p,purchaseBeforeItem,inventory->GetInventoryItemCount(p.ItemId),purchaseBeforeCosts,p.Costs.Select(c=>inventory->GetInventoryItemCount(c.ItemId)).ToArray())){FinishHelperPurchase($"Purchased {p.Quantity} × {p.ItemName}.",true);return;}
        if(purchaseStage==4)return;
        if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]||FollowMovementKeysHeld()){FinishHelperPurchase("Purchase cancelled while moving, in combat or incapacitated.");return;}
        if(action.Npc.World!=Player.CurrentWorld.RowId||action.Npc.Territory!=Client.TerritoryType||action.Npc.Map!=map->CurrentMapId||HelperTravelBusy){helperQuestStatus="Purchase waiting for the vendor's area.";return;}
        var target=Objects.FirstOrDefault(o=>o.ObjectKind==ObjectKind.EventNpc&&o.BaseId==action.Npc.BaseId&&o.Name.TextValue==action.Npc.Name&&(o.IsTargetable||purchaseOpenedVendor&&purchaseStage>0&&Targets.Target?.GameObjectId==o.GameObjectId)&&Vector3.DistanceSquared(o.Position,action.Npc.Position.Point)<1);
        if(target==null||Vector3.Distance(self.Position,target.Position)>target.HitboxRadius+4){helperQuestStatus="Purchase waiting: move within reach of "+action.Npc.Name+".";return;}
        if(purchaseStage==0){
            if(PurchaseShop().Length>0){if(Targets.Target?.GameObjectId!=target.GameObjectId){FinishHelperPurchase("Another vendor is selected; purchase cancelled.");return;}purchaseStage=1;}
            else{
                if(QuestConversationVisible()||HelperShopVisible()||FollowTransitionBusy()||followStopPending||followStopUnconfirmed)return;
                Targets.Target=target;purchaseOpenedVendor=true;purchaseStage=1;purchaseNext=now.AddSeconds(1);helperReplaying=true;relayInteracting=true;
                try{TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address,false);}finally{helperReplaying=false;relayInteracting=false;}return;
            }
        }
        if(Targets.Target?.GameObjectId!=target.GameObjectId){FinishHelperPurchase("Vendor target changed; purchase cancelled.");return;}
        if(purchaseStage==1&&purchaseOpenedVendor&&VisibleFollowAddon("Talk")&&PurchaseShop().Length==0){
            if(Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78]||Conditions[ConditionFlag.OccupiedInCutSceneEvent]){helperQuestStatus="Waiting for the vendor cutscene to finish.";return;}
            var talk=(AtkUnitBase*)GardenGui.GetAddonByName("Talk").Address;
            if(talk==null||!talk->IsReady||talk->AtkValues==null||talk->AtkValuesCount<2)return;
            var who=talk->AtkValues[1];var line=talk->AtkValues[0];
            if(((int)who.Type&15) is not (8 or 10)||((int)line.Type&15) is not (8 or 10))return;
            var speaker=TravelMenuText(who.String.Value)??"";var text=TravelMenuText(line.String.Value)??"";
            if(!HelperPurchasePolicy.Greeting(purchaseOpenedVendor,purchaseGreetingClicks,speaker,action.Npc.Name,text)){FinishHelperPurchase("Vendor greeting could not be verified or exceeded the dialogue limit; continue manually.");return;}
            if(now<purchaseGreetingNext)return;
            purchaseGreetingNext=now.AddSeconds(1);helperReplaying=true;
            try{if(ClickVisibleTalk()){purchaseGreetingClicks++;RecordFollowTravel("Vendor greeting advanced",new {npc=action.Npc.Name,purchaseGreetingClicks});}}finally{helperReplaying=false;}
            helperQuestStatus="Opening shop: advancing "+action.Npc.Name+"'s greeting.";return;
        }
        if(purchaseStage==3){UpdatePurchaseItemRequest(action,now);return;}
        if(FindPurchase(p,out var index)==null){helperQuestStatus="Open the matching vendor category: "+p.ItemName+". Exact item and cost must match.";return;}
        if(purchaseStage==1){
            if(VisibleFollowAddon("Request")||VisibleFollowAddon("SelectYesno")||VisibleFollowAddon("ShopExchangeItemDialog")||VisibleFollowAddon("ShopExchangeCurrencyDialog")){FinishHelperPurchase("Close the existing purchase confirmation, then request again.");return;}
            if(p.Costs.Any(c=>inventory->GetInventoryItemCount(c.ItemId)<c.Amount)){FinishHelperPurchase("Purchase blocked: insufficient currency or exchange items.");return;}
            var stack=Math.Max(1u,DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(p.ItemId)?.StackSize??1);
            if(!EventExchangeHasSpace((int)(((uint)p.Quantity+stack-1)/stack))){FinishHelperPurchase("Purchase blocked: insufficient free inventory slots.");return;}
            purchaseBeforeItem=inventory->GetInventoryItemCount(p.ItemId);purchaseBeforeCosts=p.Costs.Select(c=>inventory->GetInventoryItemCount(c.ItemId)).ToArray();
            var shop=(AtkUnitBase*)GardenGui.GetAddonByName(p.Shop).Address;var args=stackalloc AtkValue[4];
            for(var i=0;i<4;i++){args[i].Type=AtkValueType.Int;args[i].Int=0;}args[1].Int=(int)index;args[2].Int=p.Quantity;
            purchaseStage=2;purchaseUntil=now.AddSeconds(15);helperReplaying=true;
            try{shop->FireCallback(p.Shop=="ShopExchangeCurrency"?4u:3u,args,true);}finally{helperReplaying=false;}
            helperQuestStatus="Purchase selected once; checking confirmation and inventory.";return;
        }
        if(p.Costs.Select((c,i)=>inventory->GetInventoryItemCount(c.ItemId)!=purchaseBeforeCosts[i]).Any(changed=>changed)||inventory->GetInventoryItemCount(p.ItemId)!=purchaseBeforeItem){FinishHelperPurchase("Inventory or currency changed before confirmation; check the purchase manually.");return;}
        AtkUnitBase* confirmation=null;AtkComponentButton* button=null;
        if(VisibleFollowAddon("SelectYesno")){
            var actualPrompt=PurchasePrompt();
            // Visible is not ready: the native text node may still be empty during opening.
            if(actualPrompt.Length==0){helperQuestStatus="Waiting for the purchase confirmation to finish opening.";return;}
            if(p.Prompt.Length==0||actualPrompt!=p.Prompt){
                RecordFollowTravel("Vendor confirmation mismatch",new {expected=p.Prompt,actual=actualPrompt,item=p.ItemId});
                FinishHelperPurchase("Purchase confirmation differs or was not captured on the leader. Nothing confirmed.");return;
            }
            var yes=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;confirmation=(AtkUnitBase*)yes;button=yes->YesButton;
        }else if(p.Shop=="ShopExchangeItem"&&VisibleFollowAddon("ShopExchangeItemDialog")){
            var agent=AgentShop.Instance();var dialog=(AddonShopExchangeItemDialog*)GardenGui.GetAddonByName("ShopExchangeItemDialog").Address;
            if(agent==null||agent->DialogAddonId!=dialog->Id||agent->SelectedItemIndex!=index||agent->SelectedItemStackSize!=p.Quantity){FinishHelperPurchase("Exchange selection changed; nothing confirmed.");return;}
            confirmation=(AtkUnitBase*)dialog;button=dialog->ExchangeButton;
        }else if(p.Shop=="ShopExchangeCurrency"&&VisibleFollowAddon("ShopExchangeCurrencyDialog")){
            var agent=AgentShop.Instance();var dialog=(AtkUnitBase*)GardenGui.GetAddonByName("ShopExchangeCurrencyDialog").Address;
            if(dialog==null||!dialog->IsReady||agent==null||agent->SelectedItemIndex!=index||dialog->UldManager.NodeListCount<=8||dialog->UldManager.NodeList[8]==null)return;
            var input=dialog->UldManager.NodeList[8]->GetAsAtkComponentNumericInput();
            if(input==null||input->Value!=p.Quantity){FinishHelperPurchase("Currency exchange quantity differs; nothing confirmed.");return;}
            confirmation=dialog;button=dialog->GetComponentButtonById(17);
        }else return;
        if(button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null)return;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var n=0;
        while(evt!=null&&n++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||n>32)return;
        var click=*evt;var data=new AtkEventData();purchaseStage=3;helperReplaying=true;
        try{confirmation->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
        helperQuestStatus="Purchase confirmation submitted; checking for an item hand-in and inventory changes.";
    }
}
