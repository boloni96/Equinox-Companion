using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe bool SelectWorldVisitDc(AtkUnitBase* menu,NativeWorldTrip trip,DateTimeOffset now)
    {
        var expected=TravelDcName(trip.Leg);
        if(menu->UldManager.NodeList==null||menu->UldManager.NodeListCount>512)return false;
        AtkComponentDropDownList* match=null;var index=-1;var matches=0;
        for(var i=0;i<menu->UldManager.NodeListCount;i++){
            var node=menu->UldManager.NodeList[i];if(node==null||(int)node->Type<1000||!node->IsVisible())continue;
            var component=((AtkComponentNode*)node)->Component;
            if(component==null||component->GetComponentType()!=ComponentType.DropDownList)continue;
            var drop=(AtkComponentDropDownList*)component;var list=drop->List;
            if(list==null||list->ListLength is <1 or >16)continue;
            var labels=new string[list->ListLength];
            for(var row=0;row<labels.Length;row++)labels[row]=TravelMenuText(list->GetItemLabel(row).Value)??"";
            var found=NativeTravelPolicy.ExactChoice(labels,expected);if(found<0||list->GetItemDisabledState(found))continue;
            match=drop;index=found;matches++;
        }
        if(matches!=1||match==null){
            // Older same-DC menus have no selector. Never fall back to a logout for a regional trip.
            if(matches==0&&TravelDc(Player.CurrentWorld.RowId)==TravelDc(trip.Leg))return true;
            nativeTravelStatus="Waiting for the World Visit DC selector · choose "+expected+" manually if it is available";return false;
        }
        if(match->GetSelectedItemIndex()==index){
            if(trip.WorldDcRequested&&now-trip.WorldDcRequestedAt<TimeSpan.FromSeconds(2))return false;
            return true;
        }
        if(trip.WorldDcRequested){nativeTravelStatus="Waiting for World Visit to select "+expected+" · select it manually if needed";return false;}
        trip.WorldDcRequested=true;trip.WorldDcRequestedAt=now;
        RecordTravelPrompt("World Visit data center requested",new{dc=expected,index,trip.Leg});
        usingSharedTravel=true;
        try{match->List->DispatchItemEvent(index,AtkEventType.ListItemClick);}finally{usingSharedTravel=false;}
        nativeTravelStatus="Selecting "+expected+" · waiting for its world list";return false;
    }
    private unsafe bool TrySelectWorldVisitWorld(AtkUnitBase* menu,string expected)
    {
        if(menu->UldManager.NodeList==null||menu->UldManager.NodeListCount>512)return false;
        AtkComponentList* selected=null;var selectedIndex=-1;var matches=0;
        for(var i=0;i<menu->UldManager.NodeListCount;i++){
            var node=menu->UldManager.NodeList[i];if(node==null||(int)node->Type<1000||!node->IsVisible())continue;
            var component=((AtkComponentNode*)node)->Component;
            if(component==null||component->GetComponentType()!=ComponentType.List)continue;
            var list=(AtkComponentList*)component;var count=list->GetItemCount();
            if(count is <1 or >16||!list->IsItemInteractionEnabled)continue;
            for(var row=0;row<count;row++){
                if(list->GetItemDisabledState(row))continue;
                var label=TravelMenuText(list->GetItemLabel(row).Value)??"";
                var renderer=list->GetItemRenderer(row);
                var texts=renderer!=null&&renderer->ListItemIndex==row?TravelNodeText(&renderer->AtkComponentButton.AtkComponentBase.UldManager):new List<string>();
                if(label!=expected&&!texts.Contains(expected))continue;
                selected=list;selectedIndex=row;matches++;
            }
        }
        if(matches!=1||selected==null)return false;
        RecordTravelPrompt("World Visit world requested",new{world=expected,row=selectedIndex});
        usingSharedTravel=true;try{selected->DispatchItemEvent(selectedIndex,AtkEventType.ListItemClick);}finally{usingSharedTravel=false;}
        return true;
    }
}
