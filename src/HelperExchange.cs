using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool helperEventExchanges;
    private string helperExchangeReport="";
    private HelperAction? helperExchangePending;
    private int helperExchangeStage,helperExchangeBeforeItem,helperExchangeBeforeCost;
    private DateTimeOffset helperExchangeNext,helperExchangeUntil;
    private readonly HashSet<string> helperExchangeSent=new();
    private static unsafe bool ShopUInt(AtkUnitBase* shop,int index,out uint value)
    {
        value=0;if(shop==null||shop->AtkValues==null||index<0||index>=shop->AtkValuesCount)return false;
        var v=shop->AtkValues[index];if(v.Type is not (AtkValueType.UInt or AtkValueType.Int)||v.Int<0)return false;value=v.UInt;return true;
    }
    private string ExchangeItemName(uint id)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id)?.Name.ToString()??"";
    private unsafe HelperExchange? ReadEventExchangeRow(AtkUnitBase* shop,int row,int amount,out uint nativeIndex)
    {
        nativeIndex=0;
        if(shop==null||!shop->IsVisible||!shop->IsReady||amount is <1 or >99||!ShopUInt(shop,3,out var count)||count is <1 or >122||row<0||row>=count)return null;
        if(!ShopUInt(shop,1066+row,out var item)||!ShopUInt(shop,1310+row,out nativeIndex)||nativeIndex>=122)return null;
        uint currency=0;var cost=0;
        for(var slot=0;slot<3;slot++){
            if(!ShopUInt(shop,3141+row*3+slot,out var id)||!ShopUInt(shop,2775+row*3+slot,out var qty))return null;
            if(id==0)continue;
            if(currency!=0||qty!=1)return null;
            currency=id;cost=amount;
        }
        var result=new HelperExchange(item,ExchangeItemName(item),amount,currency,ExchangeItemName(currency),cost);
        return HelperExchangePolicy.Valid(result)?result:null;
    }
    private unsafe HelperExchange? SelectedEventExchange()
    {
        var shop=(AtkUnitBase*)GardenGui.GetAddonByName("ShopExchangeItem").Address;
        var dialog=(AtkUnitBase*)GardenGui.GetAddonByName("ShopExchangeItemDialog").Address;
        var agent=AgentShop.Instance();
        if(shop==null||dialog==null||!dialog->IsVisible||!dialog->IsReady||agent==null||!agent->IsAgentActive()||agent->DialogAddonId!=dialog->Id||agent->ItemReceive==null||agent->ItemReceiveCount is <1 or >122||agent->SelectedItemIndex<0||agent->SelectedItemIndex>=agent->ItemReceiveCount||!ShopUInt(shop,3,out var count)||count is <1 or >122)return null;
        var selected=agent->ItemReceive[agent->SelectedItemIndex];
        if(selected.ItemCount!=1)return null;
        for(var row=0;row<count;row++){
            var result=ReadEventExchangeRow(shop,row,agent->SelectedItemStackSize,out var index);
            if(result!=null&&index==agent->SelectedItemIndex&&result.ItemId==selected.ItemId)return result;
        }
        return null;
    }
    private unsafe void DrawHelperExchangeButton(HelperFollower follower)
    {
        if(!VisibleFollowAddon("ShopExchangeItemDialog")){helperExchangeSent.Clear();return;}
        var selected=SelectedEventExchange();
        if(selected==null){ImGui.TextWrapped("Event exchange could not be verified. No purchase request will be sent; export diagnostics with this window open.");return;}
        ImGui.TextWrapped($"Event exchange: {selected.Quantity} × {selected.ItemName} for {selected.CostQuantity} × {selected.CostName}.");
        var key=follower.Id+"/"+System.Text.Json.JsonSerializer.Serialize(selected);
        ImGui.BeginDisabled(!helperEventExchanges||!follower.EventExchanges||!HelperPolicy.Audience(follower,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())||helperExchangeSent.Contains(key));
        if(ImGui.Button("Follower will buy the same")){
            var map=AgentMap.Instance();var target=Targets.Target;var self=Objects.LocalPlayer;
            if(map==null||target==null||self==null||target.ObjectKind!=ObjectKind.EventNpc||target.Name.TextValue!="Ironworks hand"||Vector3.Distance(self.Position,target.Position)>target.HitboxRadius+4||helperOutgoing.Count>=32){helperError="Select the nearby Ironworks hand while the matching exchange confirmation is open.";}
            else{
                var npc=new HelperNpc(Guid.NewGuid().ToString("N"),target.BaseId,target.Name.TextValue,Client.TerritoryType,map->CurrentMapId,Player.CurrentWorld.RowId,FollowTravelPosition.From(target.Position),FollowTravelPosition.From(self.Position),self.Rotation);
                var request=new HelperAction(Guid.NewGuid().ToString("N"),Player.CharacterName,Player.HomeWorld.RowId,"vendorExchange",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),npc,Sessions:[follower.Id],Exchange:selected);
                helperOutgoing.Enqueue(request);helperExchangeSent.Add(key);helperError="Event exchange requested for "+follower.Name+"; check their status for the actual result.";
                RecordFollowTravel("Event exchange requested",new {request.Id,follower=follower.Name,exchange=selected});
            }
        }
        ImGui.EndDisabled();
        if(!helperEventExchanges)ImGui.TextWrapped("Event exchange requests require Journal V7.11.91.");
        else if(!follower.EventExchanges)ImGui.TextWrapped("The follower needs Companion 0.5.1.96 for event exchanges.");
    }
    private void ReceiveHelperExchange(HelperAction action)
    {
        if(!HelperExchangePolicy.Valid(action.Exchange)||action.Npc.Name!="Ironworks hand"){helperLastIssue="Unsupported event exchange request rejected.";return;}
        if(helperExchangePending!=null||helperIncoming.Count>0||helperReservedConversation.Length>0){helperLastIssue="Event exchange not started: finish the current helper action, then request it again.";return;}
        helperExchangeReport="";helperExchangePending=action;helperExchangeStage=0;helperExchangeNext=default;helperExchangeUntil=DateTimeOffset.UtcNow.AddSeconds(45);
        helperQuestStatus="Event exchange requested; checking the matching vendor.";followSession.Pause();RequestFollowMovementStop();nextHelperStatus=default;
    }
    private unsafe object HelperExchangeDiagnostic()
    {
        var agent=AgentShop.Instance();var shop=(AtkUnitBase*)GardenGui.GetAddonByName("ShopExchangeItem").Address;
        var rows=new List<object>();
        if(ShopUInt(shop,3,out var count)&&count is >0 and <=122)for(var row=0;row<Math.Min(count,12);row++)rows.Add(new {row,exchange=ReadEventExchangeRow(shop,row,1,out var index),index});
        return new {shopVisible=shop!=null&&shop->IsVisible,valueCount=shop==null?0:shop->AtkValuesCount,dialogVisible=VisibleFollowAddon("ShopExchangeItemDialog"),selected=SelectedEventExchange(),selectedIndex=agent==null?-1:agent->SelectedItemIndex,selectedQuantity=agent==null?0:agent->SelectedItemStackSize,receiveCount=agent==null?0:agent->ItemReceiveCount,rows,pending=helperExchangePending?.Exchange,stage=helperExchangeStage,report=helperExchangeReport};
    }
    private void FinishHelperExchange(string message,bool success=false)
    {
        RecordFollowTravel(success?"Event exchange confirmed":"Event exchange stopped",new {id=helperExchangePending?.Id,reason=message});
        helperExchangeReport=message;helperExchangePending=null;helperQuestStatus=message;helperLastIssue=success?"":message;helperError=message;nextHelperStatus=default;ResumeAfterConfirmedTravel();
    }
    private unsafe bool EventExchangeHasSpace(int needed)
    {
        var inventory=InventoryManager.Instance();if(inventory==null)return false;var free=0;
        foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4}){
            var bag=inventory->GetInventoryContainer(type);if(bag==null||!bag->IsLoaded)return false;
            for(var slot=0;slot<bag->Size;slot++){var item=bag->GetInventorySlot(slot);if(item!=null&&item->ItemId==0&&++free>=needed)return true;}
        }
        return false;
    }
    private unsafe void UpdateHelperExchange(DateTimeOffset now)
    {
        if(helperExchangePending is not {} action)return;
        if(!helperPermission.Allows("vendorExchange")||!followSession.Armed){helperExchangePending=null;return;}
        if(now>=helperExchangeUntil||!HelperPolicy.Fresh(action,now.ToUnixTimeMilliseconds(),followArmedAt)){FinishHelperExchange("Event exchange expired; nothing will be retried automatically.");return;}
        if(now<helperExchangeNext||!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return;
        helperExchangeNext=now.AddMilliseconds(250);
        var expected=action.Exchange!;var inventory=InventoryManager.Instance();var map=AgentMap.Instance();var self=Objects.LocalPlayer;
        if(inventory==null||map==null||self==null)return;
        if(helperExchangeStage==3){
            if(HelperExchangePolicy.Confirmed(expected,helperExchangeBeforeItem,inventory->GetInventoryItemCount(expected.ItemId),helperExchangeBeforeCost,inventory->GetInventoryItemCount(expected.CostItemId)))FinishHelperExchange("Event exchange confirmed: "+expected.Quantity+" × "+expected.ItemName+".",true);
            return;
        }
        if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]||FollowMovementKeysHeld()){FinishHelperExchange("Event exchange cancelled while moving, in combat or incapacitated.");return;}
        if(action.Npc.World!=Player.CurrentWorld.RowId||action.Npc.Territory!=Client.TerritoryType||action.Npc.Map!=map->CurrentMapId||HelperTravelBusy){helperQuestStatus="Event exchange waiting for the vendor's area.";return;}
        var target=Objects.FirstOrDefault(o=>o.ObjectKind==ObjectKind.EventNpc&&o.BaseId==action.Npc.BaseId&&o.Name.TextValue==action.Npc.Name&&o.IsTargetable&&Vector3.DistanceSquared(o.Position,action.Npc.Position.Point)<1);
        if(target==null||Vector3.Distance(self.Position,target.Position)>target.HitboxRadius+4){helperQuestStatus="Event exchange waiting: move within range of the same Ironworks hand.";return;}
        if(inventory->GetInventoryItemCount(expected.CostItemId)<expected.CostQuantity){FinishHelperExchange("Event exchange blocked: not enough "+expected.CostName+".");return;}
        if(!EventExchangeHasSpace(expected.Quantity)){FinishHelperExchange("Event exchange blocked: not enough free inventory slots for the requested rolls.");return;}
        if(helperExchangeStage==0){
            if(QuestConversationVisible()){helperQuestStatus="Close the current dialogue before the event exchange.";return;}
            if(VisibleFollowAddon("ShopExchangeItem")){
                if(Targets.Target?.GameObjectId!=target.GameObjectId){FinishHelperExchange("The open shop is not associated with the selected event vendor.");return;}
                helperExchangeStage=1;
            }else{
                if(HelperShopVisible()){FinishHelperExchange("Another shop is open; event exchange cancelled.");return;}
                if(FollowTransitionBusy()||followStopPending||followStopUnconfirmed)return;
                Targets.Target=target;helperExchangeStage=1;helperExchangeNext=now.AddSeconds(1);
                helperReplaying=true;relayInteracting=true;
                try{TargetSystem.Instance()->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address,false);}finally{helperReplaying=false;relayInteracting=false;}
                return;
            }
        }
        if(Targets.Target?.GameObjectId!=target.GameObjectId){FinishHelperExchange("Vendor target changed; event exchange cancelled.");return;}
        if(helperExchangeStage==1){
            var shop=(AtkUnitBase*)GardenGui.GetAddonByName("ShopExchangeItem").Address;
            if(shop==null||!shop->IsVisible||!shop->IsReady){helperQuestStatus="Open the matching Item Exchange page on the Ironworks hand.";return;}
            if(VisibleFollowAddon("ShopExchangeItemDialog")){FinishHelperExchange("An exchange confirmation is already open; close it and request the exchange again.");return;}
            if(!ShopUInt(shop,3,out var count)||count is <1 or >122){FinishHelperExchange("Unsupported event shop layout; nothing selected.");return;}
            var matches=new List<uint>();
            for(var row=0;row<count;row++)if(ReadEventExchangeRow(shop,row,expected.Quantity,out var index)==expected)matches.Add(index);
            if(matches.Count!=1){FinishHelperExchange("The requested item and exact token cost were not found uniquely in this shop.");return;}
            var args=stackalloc AtkValue[3];args[0].Type=AtkValueType.Int;args[0].Int=0;args[1].Type=AtkValueType.UInt;args[1].UInt=matches[0];args[2].Type=AtkValueType.Int;args[2].Int=expected.Quantity;
            helperExchangeStage=2;helperExchangeNext=now.AddMilliseconds(600);
            helperReplaying=true;try{shop->FireCallback(3,args,true);}finally{helperReplaying=false;}
            helperQuestStatus="Event exchange: verifying the game's confirmation.";return;
        }
        if(!VisibleFollowAddon("ShopExchangeItemDialog"))return;
        if(SelectedEventExchange()!=expected){FinishHelperExchange("The game's exchange confirmation differs from the request; nothing exchanged.");return;}
        var dialog=(AddonShopExchangeItemDialog*)GardenGui.GetAddonByName("ShopExchangeItemDialog").Address;
        var button=dialog->ExchangeButton;
        if(button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null){FinishHelperExchange("The game has disabled Exchange; nothing exchanged.");return;}
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var checkedEvents=0;
        while(evt!=null&&checkedEvents++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||checkedEvents>32){FinishHelperExchange("No verified Exchange button event was available.");return;}
        helperExchangeBeforeItem=inventory->GetInventoryItemCount(expected.ItemId);helperExchangeBeforeCost=inventory->GetInventoryItemCount(expected.CostItemId);
        helperExchangeStage=3;helperExchangeUntil=now.AddSeconds(10);
        var click=*evt;var data=new AtkEventData();helperReplaying=true;
        try{((AtkUnitBase*)dialog)->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
        helperQuestStatus="Event exchange submitted once; waiting for item and token changes.";
        RecordFollowTravel("Event exchange submitted",new {action.Id,exchange=expected});
    }
}
