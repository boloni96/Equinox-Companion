using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion;

// Kept separate so the native identities and events can be exercised with
// allocated structs, without a running game or an active plugin.
internal static unsafe class SellerNativeInterop
{
    internal unsafe delegate InventoryItem* LinkedItemReader(InventoryItem* item);
    internal static InventoryItem* Resolve(InventoryItem* item,LinkedItemReader? readLinked=null)
    {
        for(var depth=0;item!=null&&item->IsSymbolic&&depth<4;depth++) {
            var next=readLinked==null?item->GetLinkedItem():readLinked(item);
            if(next==item)return null;
            item=next;
        }
        return item!=null&&!item->IsSymbolic?item:null;
    }

    internal static bool ButtonEvent(AtkUnitBase* addon,AtkComponentButton* button,uint nodeId,out AtkEvent click)
    {
        click=default;
        if(addon==null||button==null||button->OwnerNode==null||!button->IsEnabled||button->OwnerNode->NodeId!=nodeId)return false;
        // The owner node can route through a component listener. The target
        // addon is already identified by its typed button and verified node ID.
        var e=button->OwnerNode->AtkEventManager.Event;
        for(var i=0;e!=null&&i<32;i++,e=e->NextEvent) {
            if(e->State.EventType!=AtkEventType.ButtonClick)continue;
            click=*e;
            click.Listener=(AtkEventListener*)addon;
            return true;
        }
        click=new() {Target=(AtkEventTarget*)button->OwnerNode,Listener=(AtkEventListener*)addon,Param=nodeId,State=new(){EventType=AtkEventType.ButtonClick}};
        return true;
    }
}
