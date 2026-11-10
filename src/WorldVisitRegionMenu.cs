using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    // Scan roots before descendants: decorative component trees must not consume
    // the budget before the data-center selector and world list are reached.
    private static unsafe nint[] WorldVisitComponents(AtkUnitBase* menu)
    {
        if(menu==null)return [];
        var pending=new Queue<(nint Manager,int Depth)>();pending.Enqueue(((nint)(&menu->UldManager),0));
        var result=new List<nint>();var seen=new HashSet<nint>();var scanned=0;
        while(pending.Count>0&&scanned<1024){
            var (address,depth)=pending.Dequeue();var manager=(AtkUldManager*)address;
            if(manager==null||manager->NodeList==null||manager->NodeListCount>512)continue;
            for(var i=0;i<manager->NodeListCount&&scanned++<1024;i++){
                var node=manager->NodeList[i];if(node==null||(int)node->Type<1000||!node->IsVisible())continue;
                var component=node->GetComponent();if(component==null||!seen.Add((nint)component))continue;
                result.Add((nint)component);
                if(depth<2)pending.Enqueue(((nint)(&component->UldManager),depth+1));
            }
        }
        return result.ToArray();
    }
    private unsafe bool SelectWorldVisitDc(AtkUnitBase* menu,NativeWorldTrip trip,DateTimeOffset now)
    {
        var expected=TravelDcName(trip.Leg);
        if(menu->UldManager.NodeList==null||menu->UldManager.NodeListCount>512)return false;
        AtkComponentDropDownList* match=null;var index=-1;var matches=0;
        foreach(var address in WorldVisitComponents(menu)){
            var component=(AtkComponentBase*)address;
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
        foreach(var address in WorldVisitComponents(menu)){
            var component=(AtkComponentBase*)address;
            if(component==null||component->GetComponentType()!=ComponentType.List)continue;
            var list=(AtkComponentList*)component;var count=list->GetItemCount();
            if(count is <1 or >64||!list->IsItemInteractionEnabled)continue;
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
        // The renderer row is not the addon callback index. Native world choices
        // are offsets 3..10 of WorldTranslate; their callback values start at 2.
        var module=RaptureAtkModule.Instance();var arrayIndex=(int)StringArrayType.WorldTranslate;
        if(module==null||module->AtkArrayDataHolder.StringArrayCount<=arrayIndex||module->AtkArrayDataHolder.StringArrays==null)return false;
        var strings=module->AtkArrayDataHolder.StringArrays[arrayIndex];
        if(strings==null||strings->Size<11||strings->StringArray==null)return false;
        var worlds=new string[8];
        for(var i=0;i<worlds.Length;i++)worlds[i]=(TravelMenuText(strings->StringArray[i+3].Value)??"").Trim();
        var callback=WorldVisitSelectionPolicy.Callback(worlds,expected);
        if(callback<0)return false;
        RecordTravelPrompt("World Visit world requested",new{world=expected,row=selectedIndex,callback,worlds});
        NativeTravelCallback(menu,0,callback);
        return true;
    }
}
