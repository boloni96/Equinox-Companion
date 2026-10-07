using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private HelperNpc? helperCaptureNpc;
    private DateTimeOffset helperCaptureAt,lastHelperCaptureAt;
    private string lastHelperCapture="";
    private bool helperReplaying;
    private unsafe void CaptureHelperNpc(IGameObject clicked)
    {
        if(!SharingQuest||helperReplaying||relayInteracting||usingSharedTravel||clicked.ObjectKind!=ObjectKind.EventNpc||Objects.LocalPlayer is not {} self||Vector3.Distance(self.Position,clicked.Position)>clicked.HitboxRadius+4)return;
        var map=AgentMap.Instance();if(map==null)return;
        CompleteHelperRecordingBeforeNextNpc();
        ResetHelperRecording();
        helperCaptureNpc=new(Guid.NewGuid().ToString("N"),clicked.BaseId,clicked.Name.TextValue,Client.TerritoryType,map->CurrentMapId,Player.CurrentWorld.RowId,FollowTravelPosition.From(clicked.Position),FollowTravelPosition.From(self.Position),self.Rotation);
        helperDialogue.Reset();helperCaptureAt=DateTimeOffset.UtcNow;
        helperRecording=true;helperRecordAudience=helperFollowers.Where(f=>HelperPolicy.Audience(f,helperCaptureAt.ToUnixTimeMilliseconds())).Select(f=>f.Id).ToArray();
        QueueHelperEnvelope("recording",helperCaptureNpc);
        EmitHelper("interact");
    }
    private string HelperCanonical(string text)=>text.Replace(Player.CharacterName,"{player}",StringComparison.Ordinal).Replace(config.FollowThem.TargetName.Length>0?config.FollowThem.TargetName:"\0","{player}",StringComparison.Ordinal).Trim();
    private unsafe string HelperScene()
    {
        var f=EventFramework.Instance();if(f==null||f->EventState1.EventId.Id==0)return "";
        return f->EventState1.EventId.Id+":"+f->Scene;
    }
    private unsafe (string Text,string Signature) HelperTalk()
    {
        var a=(AtkUnitBase*)GardenGui.GetAddonByName("Talk").Address;
        if(a==null||!a->IsVisible||a->AtkValues==null||a->AtkValuesCount<2)return("","");
        var line=a->AtkValues[0];var who=a->AtkValues[1];
        if(((int)line.Type&15) is not (8 or 10)||((int)who.Type&15) is not (8 or 10))return("","");
        var speaker=HelperCanonical(TravelMenuText(who.String.Value)??"");var text=HelperCanonical(TravelMenuText(line.String.Value)??"");
        return(text,text.Length is >0 and <=4000?HelperPolicy.Signature([speaker,text]):"");
    }
    private void EmitHelper(string kind,string text="",string signature="",string addon="",string scene="",uint questId=0)
    {
        var now=DateTimeOffset.UtcNow;
        if(!HelperConversationPolicy.MayRecord(helperRecording,SharingQuest)||helperReplaying||helperCaptureNpc is not {} npc||now-helperCaptureAt>TimeSpan.FromMinutes(10)||npc.World!=Player.CurrentWorld.RowId||npc.Territory!=Client.TerritoryType)return;
        if(questId==0&&kind is "talk" or "choice")questId=AcceptedHelperQuestForScene(scene);
        var key=kind+"/"+npc.Conversation+"/"+signature+"/"+text+"/"+scene+"/"+questId;
        if(key==lastHelperCapture&&now-lastHelperCaptureAt<TimeSpan.FromMilliseconds(300))return;
        lastHelperCapture=key;lastHelperCaptureAt=now;
        var sessions=helperFollowers.Where(f=>HelperPolicy.Audience(f,now.ToUnixTimeMilliseconds(),kind=="skip")).Select(f=>f.Id).ToArray();
        if(sessions.Length==0)return;
        if(helperRecorded.Count>=128||helperOutgoing.Count>=32){helperRecordingFailed=true;helperError="NPC recording is full; this conversation will not be replayed partially.";return;}
        if(kind is "talk" or "choice" or "acceptQuest" or "eventReplay")RecordFollowTravel("Helper captured choice",new {kind,npc=npc.Name,text,addon,scene,questId});
        var action=new HelperAction(Guid.NewGuid().ToString("N"),Player.CharacterName,Player.HomeWorld.RowId,kind,now.ToUnixTimeMilliseconds(),npc,text,signature,addon,scene,sessions,QuestId:questId);
        if(helperRecording){helperRecorded.Add(action);helperRecordQuiet=default;}else helperOutgoing.Enqueue(action);
    }
    private readonly HelperDialogueCapture helperDialogue=new();
    private void FlushHelperTalk()
    {
        var previous=helperDialogue.Finish();if(previous!=null)EmitHelper("talk",previous.Text,previous.Signature,"Talk",previous.Scene);
    }
    private void ObserveHelperDialogue()
    {
        if(!SharingQuest||!helperRecording){helperDialogue.Reset();return;}
        if(helperReplaying)return;
        if(!VisibleFollowAddon("Talk")){FlushHelperTalk();return;}
        var talk=HelperTalk();
        var previous=helperDialogue.Observe(talk.Text,talk.Signature,HelperScene());
        if(previous!=null)EmitHelper("talk",previous.Text,previous.Signature,"Talk",previous.Scene);
    }
    private void ObserveHelperTalk(AddonEvent type,AddonArgs args)
    {
        if(!SharingQuest||helperReplaying)return;
        try{if(type==AddonEvent.PreFinalize)FlushHelperTalk();else ObserveHelperDialogue();}
        catch(Exception e){Log.Debug(e,"Helper dialogue capture unavailable");}
    }
    private unsafe List<string> HelperChoices(AtkUnitBase* addon,string name)
    {
        var result=new List<string>();if(addon==null||!addon->IsVisible)return result;
        if(name=="SelectString")return TransportChoices(addon).Select(HelperCanonical).ToList();
        if(name=="SelectIconString"){
            var popup=&((AddonSelectIconString*)addon)->PopupMenu.PopupMenu;
            if(popup->EntryNames==null||popup->EntryCount is <1 or >32)return result;
            for(var i=0;i<popup->EntryCount;i++)result.Add(HelperCanonical(TravelMenuText(popup->EntryNames[i].Value)??""));
        }else if(name=="CutSceneSelectString"&&addon->AtkValues!=null&&addon->AtkValuesCount is >5 and <=37){
            for(var i=5;i<addon->AtkValuesCount;i++){var v=addon->AtkValues[i];if(((int)v.Type&15) is not (8 or 10))return [];result.Add(HelperCanonical(TravelMenuText(v.String.Value)??""));}
        }
        return result;
    }
    private unsafe void CaptureHelperChoice(AtkUnitBase* addon,int index)
    {
        if(!SharingQuest||helperReplaying||usingSharedTravel||addon==null||index<0)return;
        if(!VisibleFollowAddon("Talk"))FlushHelperTalk();
        if(CaptureHelperEventReplay(addon,index))return;
        var cut=AgentCutscene.Instance();
        if(cut!=null&&cut->SkipDialogAddonId!=0&&addon->Id==cut->SkipDialogAddonId){
            var yes=index==0;
            if(addon==(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address){var options=TransportChoices(addon);yes=index<options.Count&&new[]{"Yes.","Yes","Ja","Oui","はい","是","예"}.Contains(options[index]);}
            var scene=HelperScene();if(yes&&scene.Length>0)EmitHelper("skip",scene:scene);return;
        }
        foreach(var name in new[]{"SelectString","SelectIconString","CutSceneSelectString"}){
            if(addon!=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address)continue;
            var list=HelperChoices(addon,name);if(index>=list.Count||list[index].Length==0)return;
            // Travel retains its established queue. Never duplicate ferry/estate actions here.
            if(SharingTravel&&FollowTransportPolicy.StepSupported(list[index],false)){ResetHelperRecording();helperCaptureNpc=null;return;}
            EmitHelper("choice",list[index],HelperPolicy.Signature(list.OrderBy(x=>x,StringComparer.Ordinal)),name,HelperScene(),questId:HelperQuestIdForName(list[index]));return;
        }
        // Quest rewards, purchases, and arbitrary Yes/No prompts remain manual.
    }
}
