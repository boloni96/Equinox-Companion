using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe AddonSelectYesno* FindKeyboundEquipmentWarning()
    {
        var dialog=(AddonSelectYesno*)KeyboundAddon("SelectYesno");
        if(dialog==null||dialog->PromptText==null||dialog->ConfirmCheckBox==null||dialog->YesButton==null||dialog->NoButton==null)return null;
        bool Shown(AtkResNode* node){
            if(node==null)return false;
            var depth=0;for(var p=node;p!=null;p=p->ParentNode)if(++depth>64||!p->IsVisible()||p->Alpha_2==0)return false;
            return true;
        }
        string Label(AtkComponentButton* button)=>button->ButtonTextNode==null?"":TravelMenuText(button->ButtonTextNode->NodeText.StringPtr)??"";
        var check=(AtkComponentButton*)dialog->ConfirmCheckBox;
        if(!Shown((AtkResNode*)check->OwnerNode)||!Shown((AtkResNode*)dialog->YesButton->OwnerNode)||!Shown((AtkResNode*)dialog->NoButton->OwnerNode))return null;
        return KeyboundBuyingPolicy.EquipmentWarning(TravelMenuText(dialog->PromptText->NodeText.StringPtr)??"",Label(check),Label(dialog->YesButton),Label(dialog->NoButton))?dialog:null;
    }
    private unsafe AtkEvent* KeyboundButtonEvent(AtkComponentButton* button)
    {
        if(button==null||button->OwnerNode==null||!button->IsEnabled)return null;
        var evt=button->OwnerNode->AtkResNode.AtkEventManager.Event;
        for(var i=0;evt!=null&&i<32;i++,evt=evt->NextEvent)
            if(evt->State.EventType is AtkEventType.ButtonClick or AtkEventType.MouseClick)return evt;
        return null;
    }
    private unsafe void SendKeyboundButtonEvent(AtkUnitBase* dialog,AtkEvent* evt)
    {
        var click=*evt;var data=new AtkEventData();helperReplaying=true;
        try{dialog->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
    }
    private unsafe bool TryHandleKeyboundEquipmentWarning()
    {
        var dialog=FindKeyboundEquipmentWarning();if(dialog==null)return false;
        var item=keyboundBuyItem;var intent=keyboundBuyIntent;
        if(item==null||intent==null)return true;
        // A generic equipment warning contains no item name. Bind it to the
        // exact native shop selection and durable reservation before touching it.
        var matches=0;for(var i=0;i<122;i++)if(KeyboundReadOffer(keyboundBuyShop,i,out var index)?.Item==item.Item&&index==keyboundBuyIndex)matches++;
        var agent=AgentShop.Instance();
        if(matches!=1||agent==null||agent->SelectedItemIndex!=keyboundBuyIndex||intent.Cost!=item.Cost)throw new InvalidOperationException("Equipment warning does not match the selected reward.");
        if(!KeyboundBuyingPolicy.ResumeEquipment(intent,KeyboundWallet(),ArmoireInventory(item.Item).Held,true))return true;
        var check=(AtkComponentButton*)dialog->ConfirmCheckBox;
        if(!check->IsChecked){
            if(keyboundEquipmentCheckSent)return true;
            var evt=KeyboundButtonEvent(check);if(evt==null)return true;
            keyboundEquipmentCheckSent=true;
            helperReplaying=true;try{check->SetChecked(true);}finally{helperReplaying=false;}
            SendKeyboundButtonEvent((AtkUnitBase*)dialog,evt);
            keyboundBuyStatus="Confirming equipment restriction: "+item.Name;return true;
        }
        var accept=KeyboundButtonEvent(dialog->YesButton);if(accept==null)return true;
        // Separate from the price confirmation: some shops put this warning
        // before it, others after it. Each stage can be dispatched only once.
        keyboundBuyIntent=intent with {EquipmentAccepted=true};keyboundPurchases!.Put(keyboundBuyIntent);
        keyboundBuyDeadline=DateTimeOffset.UtcNow.AddSeconds(20);
        SendKeyboundButtonEvent((AtkUnitBase*)dialog,accept);
        keyboundBuyStatus="Completing purchase: "+item.Name;return true;
    }
}
