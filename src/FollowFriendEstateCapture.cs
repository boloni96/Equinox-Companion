using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    [PluginService] internal static IAddonLifecycle FollowAddonLifecycle { get; private set; } = null!;

    // Friend-estate rows are component events, not SelectString callbacks.
    // Observe only the selected row; the existing departure gate sends it later.
    private unsafe void ObserveFriendEstateRow(AddonEvent type,AddonArgs args)
    {
        if(!SharingTravel||usingSharedTravel||args is not AddonReceiveEventArgs ev||
            (AtkEventType)ev.AtkEventType is not (AtkEventType.ListItemClick or AtkEventType.ListItemDoubleClick)||
            ev.AtkEventData==0||transportCapture is not {TravelKind:"friendestate"} source||
            DateTimeOffset.UtcNow-transportCaptureAt>TimeSpan.FromSeconds(120)||
            !Player.IsLoaded||source.Name!=Player.CharacterName||source.HomeWorld!=Player.HomeWorld.RowId)return;
        try{
            var addon=(AtkUnitBase*)GardenGui.GetAddonByName("TeleportHousingFriend").Address;
            if(addon==null||!addon->IsVisible||addon->UldManager.NodeList==null||addon->UldManager.NodeListCount>512)return;
            var data=(AtkEventData.AtkListItemData*)ev.AtkEventData;
            var row=data->SelectedIndex;
            if(row is <0 or >7||data->ListItemRenderer==null)return;
            // Validate that this renderer belongs to this estate window's list.
            for(var i=0;i<addon->UldManager.NodeListCount;i++){
                var node=addon->UldManager.NodeList[i];
                if(node==null||(int)node->Type<1000||!node->IsVisible())continue;
                var component=((AtkComponentNode*)node)->Component;
                if(component==null||component->GetComponentType()!=ComponentType.List)continue;
                var list=(AtkComponentList*)component;
                if(list->GetItemCount() is <1 or >8||row>=list->GetItemCount()||list->GetItemDisabledState(row)||list->GetItemRenderer(row)!=data->ListItemRenderer)continue;
                var texts=TravelNodeText(&data->ListItemRenderer->AtkComponentButton.AtkComponentBase.UldManager);
                var estate=FollowFriendEstateSelection.Kind(texts);
                if(estate==null)return;
                transportCapture=source with {Destination=estate,Steps=[new(estate)]};
                transportSawLoading=false;
                RecordFollowTravel("Friend estate row captured",new {source.FriendContentId,estate,row});
                return;
            }
        }catch(Exception ex){errorJournal.Record("follow-friend-estate","Could not capture selected estate row",exceptionType:ex.GetType().Name);}
    }
}
