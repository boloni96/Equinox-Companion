using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Dictionary<string,uint>? helperQuestNames;
    private uint HelperQuestIdForName(string name)
    {
        helperQuestNames??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>()
            .Where(q=>q.Name.ToString().Length>0).GroupBy(q=>q.Name.ToString(),StringComparer.Ordinal)
            .Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First().RowId,StringComparer.Ordinal);
        return helperQuestNames.GetValueOrDefault(name);
    }
    private readonly HashSet<string> helperSkippedConversations=new();
    private unsafe void SkipHelperConversation(HelperAction action,string reason)
    {
        RecordFollowTravel("Helper skipped quest",new {reason,action.QuestId,npc=action.Npc.Name});
        helperSkippedConversations.Add(action.Npc.Conversation);
        var keep=helperIncoming.Where(x=>x.Npc.Conversation!=action.Npc.Conversation).ToArray();helperIncoming.Clear();foreach(var x in keep)helperIncoming.Enqueue(x);
        if(helperNpcActive?.Conversation==action.Npc.Conversation){
            foreach(var name in new[]{"Talk","SelectString","SelectIconString","CutSceneSelectString","JournalAccept"}){
                var addon=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
                if(addon!=null&&addon->IsVisible){helperReplaying=true;try{addon->Close(true);}finally{helperReplaying=false;}}
            }
            helperNpcActive=null;
        }
        CancelHelperApproach();helperBlocked="";helperError="Quest Helper: "+reason;helperQuestStatus=helperError;
        ResumeAfterConfirmedTravel();nextHelperStatus=default;
    }
    private unsafe uint AcceptedHelperQuestForScene(string scene)
    {
        var split=scene.IndexOf(':');
        if(split<0||!uint.TryParse(scene[..split],out var id)||(id>>16)!=1)return 0;
        var manager=QuestManager.Instance();
        return manager!=null&&manager->IsQuestAccepted(id)?id:0;
    }
    private unsafe bool EnsureHelperQuestPrerequisite(HelperAction action,DateTimeOffset now)
    {
        if(!VisibleFollowAddon("JournalAccept")||action.Kind is not ("talk" or "choice"))return false;
        // A quest-menu selection is not acceptance permission: keep awaiting the leader's acceptance.
        if(action.Kind=="choice"&&action.QuestId!=0&&HelperQuestIdForName(action.Text)==action.QuestId){CompleteHelperAction(now);return true;}
        if(action.QuestId==0){BlockHelper("Quest offer is still open; the leader's active quest identity was not captured. Accept the matching quest manually, then retry this NPC.");return true;}
        UpdateHelperQuestAccept(action,now,false);return true;
    }
    private uint helperOfferedQuest;
    private string helperOfferedConversation="";
    private DateTimeOffset helperOfferedAt;
    private unsafe uint VisibleHelperQuest()
    {
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("JournalAccept").Address;
        if(addon==null||!addon->IsVisible||addon->AtkValues==null||addon->AtkValuesCount<=266)return 0;
        var value=addon->AtkValues[266];
        return value.Type==AtkValueType.UInt?HelperPolicy.QuestRowId(value.UInt):0;
    }
    private unsafe void ObserveHelperQuestAcceptance(DateTimeOffset now)
    {
        if(!SharingQuest||helperCaptureNpc is not {} npc){helperOfferedQuest=0;return;}
        var offered=VisibleHelperQuest();var manager=QuestManager.Instance();if(manager==null)return;
        if(offered!=0){
            // Only watch quests not already accepted when their offer is visible.
            if(!manager->IsQuestAccepted(offered)){helperOfferedQuest=offered;helperOfferedConversation=npc.Conversation;helperOfferedAt=now;}
            return;
        }
        if(helperOfferedQuest==0)return;
        if(npc.Conversation!=helperOfferedConversation||now-helperOfferedAt>TimeSpan.FromSeconds(10)){helperOfferedQuest=0;return;}
        if(manager->IsQuestAccepted(helperOfferedQuest)){
            EmitHelper("acceptQuest",addon:"JournalAccept",questId:helperOfferedQuest);helperOfferedQuest=0;
        }
    }
    private unsafe void UpdateHelperQuestAccept(HelperAction action,DateTimeOffset now,bool completeAction=true)
    {
        var manager=QuestManager.Instance();if(manager==null)return;
        if(action.QuestId==0){BlockHelper("Quest identity is missing; accept manually.");return;}
        var offered=VisibleHelperQuest();
        if(offered!=0&&offered!=action.QuestId){SkipHelperConversation(action,"The offered quest differs from the leader's; nothing accepted.");return;}
        if(manager->IsQuestAccepted(action.QuestId)){
            if(VisibleFollowAddon("JournalAccept")){if(now-helperActionStarted>TimeSpan.FromSeconds(10))BlockHelper("Quest acceptance is not closing its offer window; close it manually before continuing.");else helperNextAction=now.AddMilliseconds(250);return;}
            RecordFollowTravel("Helper quest acceptance confirmed",new {action.QuestId});
            if(completeAction)CompleteHelperAction(now);else{helperActionStarted=now;helperNextAction=now.AddMilliseconds(450);}return;
        }

        if(now-helperActionStarted>TimeSpan.FromSeconds(10)){BlockHelper("The matching quest could not be accepted. Check level, prerequisites and quest-log space.");return;}
        if(offered==0)return;
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("JournalAccept").Address;
        if(!addon->IsReady)return;
        var button=((AddonJournalAccept*)addon)->AcceptButton;
        if(button==null)button=addon->GetComponentButtonById(44);
        if(button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null)return;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;
        var eventCount=0;
        while(evt!=null&&eventCount++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||eventCount>32){BlockHelper("The quest Accept button has no verified click event; accept manually.");return;}
        // Use this button's click event, not its first event (which can be hover/focus).
        var click=*evt;var input=new AtkEventData();
        RecordFollowTravel("Helper quest acceptance attempted",new {action.QuestId,eventType=click.State.EventType.ToString(),parameter=click.Param});
        helperReplaying=true;
        try{addon->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&input);}
        finally{helperReplaying=false;}
        helperNextAction=now.AddSeconds(1);helperQuestStatus="Accepting the matching quest; waiting for game confirmation.";
    }
}
