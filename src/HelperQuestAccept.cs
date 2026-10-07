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
        if(helperReservedConversation==action.Npc.Conversation)ClearHelperReservation();
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
        if(addon==null||!addon->IsVisible||addon->AtkValues==null||addon->AtkValuesCount<=5)return 0;
        var titleValue=addon->AtkValues[5];
        if(((int)titleValue.Type&15) is not (8 or 10))return 0;
        var title=(TravelMenuText(titleValue.String.Value)??"").Trim();
        foreach(var slot in new[]{261,266}){
            if(slot>=addon->AtkValuesCount)continue;
            var value=addon->AtkValues[slot];if(value.Type is not (AtkValueType.UInt or AtkValueType.Int))continue;
            var id=HelperPolicy.QuestRowId(value.UInt);if(id==0)continue;
            var row=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(id);
            var name=row?.Name.ToString()??"";
            if(name.Length>0&&(title==name||title.EndsWith(" "+name,StringComparison.Ordinal)))return id;
        }
        return 0;
    }
    private unsafe void CaptureHelperQuestCallback(AtkUnitBase* addon,uint count,AtkValue* values)
    {
        if(!SharingQuest||helperReplaying||addon==null||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("JournalAccept").Address||values==null||count==0)return;
        if(((int)values[0].Type&15) is not (3 or 5))return;
        var offered=VisibleHelperQuest();if(offered==0)return;
        var code=values[0].Int;
        if(count==2&&code==3&&((int)values[1].Type&15) is 3 or 5&&HelperPolicy.QuestRowId(values[1].UInt)==offered){
            helperOfferedQuest=offered;helperOfferedConversation=helperCaptureNpc?.Conversation??"";helperOfferedAt=DateTimeOffset.UtcNow;helperAcceptIntent=true;
            EmitHelper("acceptQuest",addon:"JournalAccept",scene:"offer",questId:offered);
        }else if(count==1&&code==1){
            helperAcceptIntent=false;helperOfferedQuest=0;
            EmitHelper("acceptQuest",addon:"JournalAccept",scene:"decline",questId:offered);
        }
    }
    private bool helperAcceptIntent,helperQuestMenuSelected;
    private unsafe void ObserveHelperQuestAcceptance(DateTimeOffset now)
    {
        if(!SharingQuest||helperCaptureNpc is not {} npc){helperOfferedQuest=0;helperAcceptIntent=false;return;}
        var manager=QuestManager.Instance();if(manager==null||!helperAcceptIntent)return;
        if(npc.Conversation!=helperOfferedConversation||now-helperOfferedAt>TimeSpan.FromMinutes(10)){helperOfferedQuest=0;helperAcceptIntent=false;return;}
        if(!VisibleFollowAddon("JournalAccept")&&manager->IsQuestAccepted(helperOfferedQuest)){
            FlushHelperTalk();EmitHelper("acceptQuest",addon:"JournalAccept",scene:"confirmed",questId:helperOfferedQuest);helperOfferedQuest=0;helperAcceptIntent=false;
        }
    }
    private unsafe void UpdateHelperQuestAccept(HelperAction action,DateTimeOffset now,bool completeAction=true)
    {
        var manager=QuestManager.Instance();if(manager==null)return;
        if(action.QuestId==0){BlockHelper("Quest identity is missing; accept manually.");return;}
        var offered=VisibleHelperQuest();
        if(offered!=0&&offered!=action.QuestId){SkipHelperConversation(action,"The offered quest differs from the leader's; nothing accepted.");return;}
        if(action.Scene=="confirmed"){
            if(manager->IsQuestAccepted(action.QuestId)){RecordFollowTravel("Helper quest acceptance confirmed",new {action.QuestId});CompleteHelperAction(now);return;}
            if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The final quest acceptance was not confirmed by the game.");return;
        }
        if(helperActionSubmitted&&!VisibleFollowAddon("JournalAccept")){
            if(action.Scene=="decline"){FinishHelperConversation("Matching quest declined.");return;}
            if(completeAction)CompleteHelperAction(now);else{helperActionSubmitted=false;helperNextAction=now.AddMilliseconds(450);}return;
        }
        if(action.Scene!="decline"&&manager->IsQuestAccepted(action.QuestId)&&!VisibleFollowAddon("JournalAccept")){
            if(completeAction)CompleteHelperAction(now);return;
        }
        if(helperActionSubmitted){if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The quest offer did not change after the selected button was pressed.");return;}


        if(now-helperActionStarted>TimeSpan.FromSeconds(10)){BlockHelper("The matching quest could not be accepted. Check level, prerequisites and quest-log space.");return;}
        if(offered==0){
            if(helperQuestMenuSelected)return;
            foreach(var name in new[]{"SelectString","SelectIconString"}){
                var menu=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;
                var choices=HelperChoices(menu,name);
                var matches=choices.Select((text,index)=>(text,index)).Where(x=>HelperQuestIdForName(x.text)==action.QuestId).ToArray();
                if(matches.Length!=1)continue;
                helperReplaying=true;try{SelectTravelChoice(menu,matches[0].index);}finally{helperReplaying=false;}
                helperQuestMenuSelected=true;helperNextAction=now.AddMilliseconds(450);return;
            }
            return;
        }
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("JournalAccept").Address;
        if(!addon->IsReady)return;
        var button=action.Scene=="decline"?((AddonJournalAccept*)addon)->DeclineButton:((AddonJournalAccept*)addon)->AcceptButton;
        if(button==null)button=addon->GetComponentButtonById(action.Scene=="decline"?45u:44u);
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
        helperActionSubmitted=true;helperNextAction=now.AddMilliseconds(450);helperQuestStatus="Accepting the matching quest; waiting for game confirmation.";
    }
}
