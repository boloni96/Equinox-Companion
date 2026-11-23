using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
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
    private unsafe bool TrySelectWorldVisitWorld(AtkUnitBase* menu,string expected,uint destination)
    {
        if(menu->UldManager.NodeList==null||menu->UldManager.NodeListCount>512)return false;
        bool hasWorldRows=false;
        AtkComponentList* selected=null;var selectedIndex=-1;var matches=0;
        foreach(var address in WorldVisitComponents(menu)){
            var component=(AtkComponentBase*)address;
            if(component==null||component->GetComponentType()!=ComponentType.List)continue;
            var list=(AtkComponentList*)component;var count=list->GetItemCount();
            if(count>0 && component->OwnerNode!=null && component->OwnerNode->NodeId==7)hasWorldRows=true;
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
        if(matches==0 && !hasWorldRows && menu->IsReady)
            return TrySelectWorldVisitTable(menu,expected,destination);
        if(matches!=1||selected==null)return false;
        // Use the visible list, not WorldTranslate string offsets: the regional
        // selector added DC/home/current labels ahead of the world-name block.
        var worldCount=selected->GetItemCount();
        if(worldCount is <1 or >8)return false;
        var worlds=new string[worldCount];
        for(var row=0;row<worldCount;row++)worlds[row]=(TravelMenuText(selected->GetItemLabel(row).Value)??"").Trim();
        var callback=WorldVisitSelectionPolicy.Callback(worlds,expected);
        if(callback<0||callback!=selectedIndex+2||selected->GetItemDisabledState(selectedIndex))return false;
        RecordTravelPrompt("World Visit world requested",new{world=expected,row=selectedIndex,callback,worlds,source="visible world list"});
        NativeTravelCallback(menu,0,callback);
        return true;
    }
    private unsafe bool TrySelectWorldVisitTable(AtkUnitBase* menu,string expected,uint destination)
    {
        var module=RaptureAtkModule.Instance();
        int index=(int)StringArrayType.WorldTranslate;
        if(module==null||module->AtkArrayDataHolder.StringArrayCount<=index||module->AtkArrayDataHolder.StringArrays==null)return false;
        var data=module->AtkArrayDataHolder.StringArrays[index];
        if(data==null||data->StringArray==null||data->Size<4)return false;
        EnsureBidWorlds();
        var region=TravelRegion(Player.CurrentWorld.RowId);
        var centers=bidWorlds!.Where(w=>w.Region==region).Select(w=>w.Dc).ToHashSet(StringComparer.Ordinal);
        var dc=TravelDcName(destination);
        var roster=bidWorlds!.Where(w=>w.Dc==dc).Select(w=>w.Name).ToHashSet(StringComparer.Ordinal);
        var labels=new string[Math.Min(data->Size,26)];
        for(int i=0;i<labels.Length;i++)labels[i]=(TravelMenuText(data->StringArray[i].Value)??"").Trim();
        int callback=WorldVisitSelectionPolicy.TableCallback(labels,centers,dc,roster,expected);
        if(callback<0)return false;
        RecordTravelPrompt("World Visit world requested",new{world=expected,callback,source="regional destination table"});
        NativeTravelCallback(menu,0,callback);
        return true;
    }

}
