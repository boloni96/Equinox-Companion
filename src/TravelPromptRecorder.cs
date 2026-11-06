using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using System.Diagnostics;
using System.Text.Json;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    // Bounded local history, independent of FollowThem and garden recording.
    // Scan a few known UI surfaces per frame, never a recursive whole-UI dump.
    private static readonly string[] RecordedTravelMenus=["SelectString","SelectIconString","SelectYesno","SelectOk","ContextMenu","WorldTravelSelect","WorldTravelFinderReady","WorldTravelFinderStatus","LobbyDKTCheck","LobbyDKTWorldList","LobbyDKTCheckExec","LobbyWKTCheck","LobbyWKTCheckHome","LobbyWKTWorldList","TitleDCWorldMap","_TitleMenu","_CharaSelectListMenu","_CharaSelectWorldServer","_CharaSelectReturn","Teleport","TelepotTown","TeleportHousingFriend","HousingSelectBlock","HousingSelectRoom","MansionSelectRoom"];
    private readonly Queue<object> travelPromptHistory=new();
    private readonly Dictionary<string,string> travelPromptLast=new();
    private int travelPromptCursor;private DateTimeOffset travelPromptNext;private long travelPromptSequence,travelPromptDropped;
    private string travelLoginFingerprint="";
    private void InitializeTravelPromptRecorder(){
        foreach(var name in RecordedTravelMenus){Addons.RegisterListener(AddonEvent.PostSetup,name,RecordTravelUiEvent);Addons.RegisterListener(AddonEvent.PreFinalize,name,RecordTravelUiEvent);Addons.RegisterListener(AddonEvent.PreReceiveEvent,name,RecordTravelUiEvent);}
    }
    private unsafe void RecordTravelUiEvent(AddonEvent type,AddonArgs args){
        try{
            if(type==AddonEvent.PreReceiveEvent&&args is AddonReceiveEventArgs e){
                var kind=e.AtkEventType.ToString();if(!kind.Contains("Click",StringComparison.Ordinal)&&!kind.Contains("Change",StringComparison.Ordinal))return;
                RecordTravelPrompt("UI input",new{addon=args.AddonName,eventType=kind,parameter=e.EventParam,source=usingSharedTravel||helperReplaying?"Companion":"game or other input",menu=ReadTravelMenuDiagnostic((AtkUnitBase*)args.Addon.Address)});
            }else RecordTravelPrompt("UI lifecycle",new{addon=args.AddonName,eventType=type.ToString(),menu=ReadTravelMenuDiagnostic((AtkUnitBase*)args.Addon.Address)});
        }catch(Exception ex){Log.Debug(ex,"Travel UI observation failed");}
    }
    private void RecordTravelPrompt(string kind,object detail)
    {
        while(travelPromptHistory.Count>=600){travelPromptHistory.Dequeue();travelPromptDropped++;}
        travelPromptHistory.Enqueue(new {sequence=++travelPromptSequence,at=DateTimeOffset.UtcNow,kind,trip=nativeWorldTrip?.Id,stage=nativeWorldTrip?.Stage.ToString(),detail});
    }
    private object TravelPromptExport()=>new {schema=1,capacity=600,dropped=travelPromptDropped,coverage="Known travel and login menus; text/buttons on changes, callbacks when hooked. Not every game UI event.",records=travelPromptHistory.ToArray()};
    private unsafe void UpdateTravelPromptRecorder(DateTimeOffset now)
    {
        if(now<travelPromptNext)return;
        var started=Stopwatch.GetTimestamp();
        try{
            for(var n=0;n<4&&travelPromptCursor<RecordedTravelMenus.Length;n++,travelPromptCursor++){
                var name=RecordedTravelMenus[travelPromptCursor];
                var addon=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
                if(addon==null||!addon->IsVisible){if(travelPromptLast.Remove(name))RecordTravelPrompt("Menu closed",new {addon=name});}
                else{
                    var snapshot=ReadTravelMenuDiagnostic(addon);
                    var buttons=new List<object>();
                    for(uint id=1;id<=32;id++){
                        var button=addon->GetComponentButtonById(id);if(button==null||button->AtkComponentBase.OwnerNode==null||!button->AtkComponentBase.OwnerNode->AtkResNode.IsVisible())continue;
                        buttons.Add(new{id,enabled=button->IsEnabled,texts=TravelNodeText(&button->AtkComponentBase.UldManager)});
                    }
                    var options=name=="SelectString"?TransportChoices(addon):null;
                    var state=new {menu=snapshot,buttons,options};var fingerprint=JsonSerializer.Serialize(state);
                    if(!travelPromptLast.TryGetValue(name,out var old)||old!=fingerprint){travelPromptLast[name]=fingerprint;RecordTravelPrompt(old==null?"Menu opened":"Menu changed",state);}
                }
                if(Stopwatch.GetElapsedTime(started).TotalMilliseconds>=1){travelPromptCursor++;break;}
            }
            if(travelPromptCursor>=RecordedTravelMenus.Length){
                travelPromptCursor=0;travelPromptNext=now.AddMilliseconds(250);
                var lobby=AgentLobby.Instance();
                var state=new {loaded=Player.IsLoaded,character=Player.ContentId,world=Player.CurrentWorld.RowId,home=Player.HomeWorld.RowId,serviceAccount=lobby==null?-1:(int)lobby->ServiceAccountIndex,locked=lobby!=null&&lobby->TemporaryLocked,loginStatus=characterTravelStatus};
                var fingerprint=JsonSerializer.Serialize(state);
                if(fingerprint!=travelLoginFingerprint){travelLoginFingerprint=fingerprint;RecordTravelPrompt("Travel identity changed",state);}
            }
        }catch(Exception ex){travelPromptCursor=0;travelPromptNext=now.AddSeconds(5);errorJournal.Record("travel-recorder",ex.Message,exceptionType:ex.GetType().Name);}
        finally{RecordPerformanceElapsed("Travel: prompt recorder slice",started);}
    }
    private unsafe void RecordNativeTravelCallback(AtkUnitBase* addon,uint count,AtkValue* args)
    {
        if(addon==null||args==null||count>16||!RecordedTravelMenus.Contains(addon->NameString))return;
        var values=new List<object>();for(var i=0;i<count;i++)if(((int)args[i].Type&15) is 3 or 5)values.Add(new{index=i,value=args[i].Int});
        RecordTravelPrompt("Menu callback",new {addon=addon->NameString,source=usingSharedTravel||helperReplaying?"Companion":"game or other input",values,menu=ReadTravelMenuDiagnostic(addon)});
    }
}
