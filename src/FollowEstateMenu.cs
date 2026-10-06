using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe List<string> TravelNodeText(AtkUldManager* manager)
    {
        var texts=new List<string>();
        if(manager==null||manager->NodeList==null||manager->NodeListCount>512)return texts;
        for(var i=0;i<manager->NodeListCount;i++){
            var node=manager->NodeList[i];if(node==null||node->Type!=NodeType.Text||!node->IsVisible())continue;
            var text=TravelMenuText(((AtkTextNode*)node)->NodeText.StringPtr.Value);
            if(!string.IsNullOrEmpty(text))texts.Add(text);
        }
        return texts;
    }
    private unsafe bool TrySelectFriendEstate(FollowPortalSignal signal,string estate)
    {
        if(estate is not ("Private Estate" or "Free Company Estate"))return false;
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("TeleportHousingFriend").Address;
        if(addon==null||!addon->IsVisible||!addon->IsReady)return false;
        var friends=InfoProxyFriendList.Instance();
        if(friends==null||friends->CharData==null||friends->EntryCount>200||!ulong.TryParse(signal.FriendContentId,out var id))return false;
        string owner="";foreach(var friend in friends->CharDataSpan)if(friend.ContentId==id){owner=friend.NameString;break;}
        if(owner.Length==0)return false;
        var header=TravelNodeText(&addon->UldManager);
        if(!header.Contains(owner)){
            if(addon->AtkValues==null||addon->AtkValuesCount>1024)return false;
            for(var i=0;i<addon->AtkValuesCount;i++)if(((int)addon->AtkValues[i].Type&15) is 8 or 10&&TravelMenuText(addon->AtkValues[i].String.Value)==owner){header.Add(owner);break;}
        }
        if(!header.Contains(owner)){TravelDiagnostic("Estate window belongs to another character; waiting for the selected friend's estates.");return false;}
        if(addon->UldManager.NodeList==null||addon->UldManager.NodeListCount>512)return false;
        AtkComponentList* selected=null;int selectedIndex=-1;uint selectedFee=0;var matches=0;
        for(var i=0;i<addon->UldManager.NodeListCount;i++){
            var node=addon->UldManager.NodeList[i];if(node==null||(int)node->Type<1000||!node->IsVisible())continue;
            var component=((AtkComponentNode*)node)->Component;if(component==null||component->GetComponentType()!=ComponentType.List)continue;
            var list=(AtkComponentList*)component;var count=list->GetItemCount();if(count is <1 or >8||!list->IsItemInteractionEnabled)continue;
            for(var row=0;row<count;row++){
                if(list->GetItemDisabledState(row))continue;
                var renderer=list->GetItemRenderer(row);if(renderer==null||renderer->ListItemIndex!=row)continue;
                var texts=TravelNodeText(&renderer->AtkComponentButton.AtkComponentBase.UldManager);
                if(!texts.Contains(estate))continue;
                var parsedFee=FollowEstatePrice.Read(texts);
                if(parsedFee is not {} fee){TravelDiagnostic("Estate row found, but its price could not be verified; waiting without selecting.");return false;}
                matches++;selected=list;selectedIndex=row;selectedFee=fee;
            }
        }
        if(matches!=1||selected==null){TravelDiagnostic("Waiting for one enabled "+estate+" row in the friend's estate window.");return false;}
        var inventory=InventoryManager.Instance();
        if(inventory==null||selectedFee>Math.Max(0,config.FollowThem.TeleportGilLimit)||selectedFee>inventory->GetGil()){TravelDiagnostic("Estate teleport exceeds your gil limit or available gil; waiting.");return false;}
        RecordFollowTravel("Estate row requested",new {owner,estate,row=selectedIndex,fee=selectedFee});
        usingSharedTravel=true;try{selected->DispatchItemEvent(selectedIndex,AtkEventType.ListItemClick);}finally{usingSharedTravel=false;}
        TravelDiagnostic("Requested "+estate+" from "+owner+"'s estate list.");return true;
    }
}
