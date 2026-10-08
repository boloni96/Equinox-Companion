using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint purchaseRequestId;
    private int purchaseRequestSlot;
    private bool purchaseRequestOptionPending;
    private unsafe void UpdatePurchaseItemRequest(HelperAction action,DateTimeOffset now)
    {
        if(!VisibleFollowAddon("Request"))return;
        var p=action.Purchase!;
        var request=(AddonRequest*)GardenGui.GetAddonByName("Request").Address;
        var agent=AgentNpcTrade.Instance();var ui=UIState.Instance();var inventory=InventoryManager.Instance();
        if(request==null||!request->IsReady||agent==null||!agent->IsAgentActive()||agent->GetAddonId()!=request->Id||ui==null||inventory==null)return;
        var trade=&ui->NpcTrade;
        if(trade->Requests.Count is <1 or >3||request->EntryCount!=trade->Requests.Count){FinishHelperPurchase("Unsupported item request; nothing handed over.");return;}
        var required=new List<(uint ItemId,int Amount)>();
        for(var i=0;i<trade->Requests.Count;i++){
            var item=trade->Requests.Items[i];
            if(item.WantHQ||item.WantCollectible||item.MinCollectibility!=0||item.WantMateriaFilledSlots!=0){
                FinishHelperPurchase("This request has quality, collectability or materia requirements; select items manually.");return;
            }
            required.Add((item.ItemId,item.RequiredQuantity));
        }
        if(!HelperPurchasePolicy.RequestMatches(p,required)){FinishHelperPurchase("Requested hand-in items differ from the authorized vendor costs; nothing handed over.");return;}
        if(p.Costs.Select((c,i)=>inventory->GetInventoryItemCount(c.ItemId)!=purchaseBeforeCosts[i]).Any(x=>x)||inventory->GetInventoryItemCount(p.ItemId)!=purchaseBeforeItem){
            FinishHelperPurchase("Inventory changed before the hand-in; check the trade manually.");return;
        }
        if(purchaseRequestId==0){
            if(agent->SelectedTurnInSlot>=0||VisibleFollowAddon("ContextIconMenu")){FinishHelperPurchase("An item selection is already active; continue the trade manually.");return;}
            purchaseRequestId=request->Id;purchaseUntil=now.AddSeconds(20);
            RecordFollowTravel("Vendor item request verified",new {action.Id,required});
        }else if(purchaseRequestId!=request->Id){FinishHelperPurchase("Item Request window changed; nothing handed over.");return;}
        if(purchaseRequestSlot<trade->Requests.Count){
            if(!purchaseRequestOptionPending){
                if(agent->SelectedTurnInSlot>=0){FinishHelperPurchase("Item Request selection changed; continue manually.");return;}
                purchaseRequestOptionPending=true;
                helperReplaying=true;try{agent->SelectTurnInSlot((ushort)purchaseRequestSlot);}finally{helperReplaying=false;}
                helperQuestStatus="Selecting the requested exchange item.";return;
            }
            if(agent->SelectedTurnInSlot!=purchaseRequestSlot||agent->SelectedTurnInSlotItemOptions is <1 or >140)return;
            var wanted=required[purchaseRequestSlot];var option=-1;
            for(var i=0;i<agent->SelectedTurnInSlotItemOptions;i++){
                var item=agent->SelectedTurnInSlotItemOptionValues[i].Value;
                if(item==null||item->IsSymbolic||item->ItemId!=wanted.ItemId||item->Quantity<wanted.Amount||item->Flags!=InventoryItem.ItemFlags.None)continue;
                if(item->Container is not (InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4))continue;
                var modified=item->GlamourId!=0;
                for(var m=0;m<5;m++)modified|=item->Materia[m]!=0;
                if(modified)continue;
                option=i;break;
            }
            if(option<0){FinishHelperPurchase("No matching plain inventory stack can satisfy this request; select the item manually.");return;}
            // Same slot-selection protocol as YesAlready Request; choose a verified inventory option, not option zero blindly.
            var args=stackalloc AtkValue[4];
            for(var i=0;i<4;i++){args[i]=default;args[i].Type=AtkValueType.Int;args[i].Int=0;}
            args[1].Int=option;var result=new AtkValue();
            helperReplaying=true;
            try{agent->ReceiveEvent(&result,args,4,1);}finally{helperReplaying=false;}
            RecordFollowTravel("Vendor requested item inserted",new {action.Id,slot=purchaseRequestSlot,item=wanted.ItemId,quantity=wanted.Amount,option});
            purchaseRequestSlot++;purchaseRequestOptionPending=false;return;
        }
        var button=request->HandOverButton;
        if(agent->SelectedTurnInSlot>=0||button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null)return;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var n=0;
        while(evt!=null&&n++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||n>32){FinishHelperPurchase("No verified Trade button event; finish manually.");return;}
        var click=*evt;var data=new AtkEventData();purchaseStage=4;purchaseUntil=now.AddSeconds(15);helperReplaying=true;
        try{request->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
        RecordFollowTravel("Vendor item hand-in submitted",new {action.Id,required});
        helperQuestStatus="Trade submitted once; waiting for the purchased item and exact cost changes.";
    }
}
