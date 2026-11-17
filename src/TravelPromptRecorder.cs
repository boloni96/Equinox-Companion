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
    private static readonly string[] RecordedTravelMenus=["SelectString","SelectIconString","SelectYesno","SelectOk","ContextMenu","WorldTravelSelect","WorldTravelFinderReady","WorldTravelFinderStatus","LobbyDKTCheck","LobbyDKTWorldList","LobbyDKTCheckExec","LobbyWKTCheck","LobbyWKTCheckHome","LobbyWKTWorldList","TitleDCWorldMap","_TitleMenu","_CharaSelectListMenu","_CharaSelectWorldServer","_CharaSelectReturn","Teleport","TelepotTown","TeleportHousingFriend","HousingSelectBlock","HousingSelectRoom","MansionSelectRoom","CompanyCraftRecipeNoteBook","SubmarinePartsMenu","Request","InputNumeric"];
    private readonly Queue<object> travelPromptHistory=new();
    private readonly Queue<object> workshopPromptHistory=new();
    private DateTimeOffset workshopPromptUntil;
    private long workshopPromptDropped;
    private void RecordWorkshopPrompt(string kind,string addon,object detail)
    {
        var now=DateTimeOffset.UtcNow;
        if(addon is "SubmarinePartsMenu" or "CompanyCraftRecipeNoteBook")workshopPromptUntil=now.AddMinutes(2);
        else if(now>workshopPromptUntil||addon is not ("Request" or "SelectString" or "SelectYesno" or "SelectOk" or "ContextMenu" or "InputNumeric"))return;
        while(workshopPromptHistory.Count>=600){workshopPromptHistory.Dequeue();workshopPromptDropped++;}
        workshopPromptHistory.Enqueue(new{at=now,kind,addon,detail});
    }
    private readonly Dictionary<string,string> travelPromptLast=new();
    private int travelPromptCursor;private DateTimeOffset travelPromptNext;private long travelPromptSequence,travelPromptDropped;
    private string travelLoginFingerprint="";
    private void InitializeTravelPromptRecorder(){
        foreach(var name in RecordedTravelMenus){Addons.RegisterListener(AddonEvent.PostSetup,name,RecordTravelUiEvent);Addons.RegisterListener(AddonEvent.PreFinalize,name,RecordTravelUiEvent);Addons.RegisterListener(AddonEvent.PreReceiveEvent,name,RecordTravelUiEvent);}
    }
    private unsafe void RecordTravelUiEvent(AddonEvent type,AddonArgs args){
        try{
            if(type==AddonEvent.PreReceiveEvent&&args is AddonReceiveEventArgs e){
                var kind=e.AtkEventType.ToString();if(!kind.Contains("Click",StringComparison.Ordinal)&&!kind.Contains("Change",StringComparison.Ordinal)||kind=="TimelineActiveLabelChanged")return;
                var detail=new{addon=args.AddonName,eventType=kind,parameter=e.EventParam,source=usingSharedTravel||helperReplaying?"Companion":"game or other input",menu=ReadTravelMenuDiagnostic((AtkUnitBase*)args.Addon.Address)};
                RecordTravelPrompt("UI input",detail);RecordWorkshopPrompt("UI input",args.AddonName,detail);
            }else{
                var detail=new{addon=args.AddonName,eventType=type.ToString(),menu=ReadTravelMenuDiagnostic((AtkUnitBase*)args.Addon.Address)};
                RecordTravelPrompt("UI lifecycle",detail);RecordWorkshopPrompt("UI lifecycle",args.AddonName,detail);
            }
        }catch(Exception ex){Log.Debug(ex,"Travel UI observation failed");}
    }
    private void RecordTravelPrompt(string kind,object detail)
    {
        while(travelPromptHistory.Count>=600){travelPromptHistory.Dequeue();travelPromptDropped++;}
        travelPromptHistory.Enqueue(new {sequence=++travelPromptSequence,at=DateTimeOffset.UtcNow,kind,trip=nativeWorldTrip?.Id,stage=nativeWorldTrip?.Stage.ToString(),status=nativeTravelStatus,detail});
    }
    private object TravelPromptExport()=>new {schema=5,workshop=new{capacity=600,dropped=workshopPromptDropped,coverage="Crafting log and SubmarinePartsMenu snapshots, followed by nearby contribution/HQ confirmations and callbacks. Separate bounded history; no automatic actions.",records=workshopPromptHistory.ToArray()},mapClick=new{enabled=config.MapAethernetTravel,inputs=mapTravelInputCount,lastResult=mapTravelLastResult},capacity=600,dropped=travelPromptDropped,coverage="Known travel/login menus, World Visit controls and FC workshop crafting-log/material-supply windows, contribution requests, input parameters, callbacks and wait reasons. Bounded snapshots on changes; not every game UI event.",records=travelPromptHistory.ToArray()};
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
                    var state=new {addon=name,menu=snapshot,buttons,options,trees=name=="LobbyDKTWorldList"?ReadNativeTravelTrees(addon):null,worldVisit=name=="WorldTravelSelect"?ReadWorldVisitControls(addon):null,loginList=name=="_CharaSelectListMenu"?ReadLoginListEvidence():null};var fingerprint=JsonSerializer.Serialize(state);
                    if(!travelPromptLast.TryGetValue(name,out var old)||old!=fingerprint){travelPromptLast[name]=fingerprint;RecordTravelPrompt(old==null?"Menu opened":"Menu changed",state);RecordWorkshopPrompt(old==null?"Menu opened":"Menu changed",name,state);}
                }
                if(Stopwatch.GetElapsedTime(started).TotalMilliseconds>=1){travelPromptCursor++;break;}
            }
            if(travelPromptCursor>=RecordedTravelMenus.Length){
                travelPromptCursor=0;travelPromptNext=now.AddMilliseconds(250);
                var lobby=AgentLobby.Instance();
                var state=new {loaded=Player.IsLoaded,character=Player.ContentId,world=Player.CurrentWorld.RowId,home=Player.HomeWorld.RowId,serviceAccount=lobby==null?-1:(int)lobby->ServiceAccountIndex,locked=lobby!=null&&lobby->TemporaryLocked,loginStatus=characterTravelStatus,travelStatus=nativeTravelStatus,travelStage=nativeWorldTrip?.Stage.ToString(),destination=nativeWorldTrip?.Leg};
                var fingerprint=JsonSerializer.Serialize(state);
                if(fingerprint!=travelLoginFingerprint){travelLoginFingerprint=fingerprint;RecordTravelPrompt("Travel identity changed",state);}
            }
        }catch(Exception ex){travelPromptCursor=0;travelPromptNext=now.AddSeconds(5);errorJournal.Record("travel-recorder",ex.Message,exceptionType:ex.GetType().Name);}
        finally{RecordPerformanceElapsed("Travel: prompt recorder slice",started);}
    }
    private unsafe void RecordNativeTravelCallback(AtkUnitBase* addon,uint count,AtkValue* args)
    {
        if(addon==null||args==null||count>16||!RecordedTravelMenus.Contains(addon->NameString))return;
        var values=new List<object>();
        for(var i=0;i<count;i++){
            var type=(int)args[i].Type&15;
            if(type==2)values.Add(new{index=i,type="Bool",value=args[i].Bool});
            else if(type==3)values.Add(new{index=i,type="Int",value=args[i].Int});
            else if(type==5)values.Add(new{index=i,type="UInt",value=args[i].UInt});
            else values.Add(new{index=i,type=args[i].Type.ToString(),omitted=true});
        }
        var detail=new {addon=addon->NameString,source=usingSharedTravel||helperReplaying?"Companion":"game or other input",values,menu=ReadTravelMenuDiagnostic(addon)};
        RecordTravelPrompt("Menu callback",detail);RecordWorkshopPrompt("Menu callback",addon->NameString,detail);
    }
}
