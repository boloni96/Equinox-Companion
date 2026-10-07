using FFXIVClientStructs.FFXIV.Client.Game;
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
    private uint helperOfferedQuest;
    private string helperOfferedConversation="";
    private DateTimeOffset helperOfferedAt;
    private unsafe uint VisibleHelperQuest()
    {
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("JournalAccept").Address;
        if(addon==null||!addon->IsVisible||addon->AtkValues==null||addon->AtkValuesCount<=266)return 0;
        var value=addon->AtkValues[266];
        return value.Type==AtkValueType.UInt?value.UInt:0;
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
    private unsafe void UpdateHelperQuestAccept(HelperAction action,DateTimeOffset now)
    {
        var manager=QuestManager.Instance();if(manager==null)return;
        if(action.QuestId==0){BlockHelper("Quest identity is missing; accept manually.");return;}
        if(manager->IsQuestAccepted(action.QuestId)){CompleteHelperAction(now);return;}
        if(QuestManager.IsQuestComplete(action.QuestId)){SkipHelperConversation(action,"That quest is already completed; waiting for the next interaction.");return;}
        var offered=VisibleHelperQuest();
        if(offered!=0&&offered!=action.QuestId){SkipHelperConversation(action,"The offered quest differs from the leader's; nothing accepted.");return;}
        if(now-helperActionStarted>TimeSpan.FromSeconds(10)){BlockHelper("The matching quest could not be accepted. Check level, prerequisites and quest-log space.");return;}
        if(offered==0)return;
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName("JournalAccept").Address;
        var button=addon->GetComponentButtonById(44);
        if(button==null||!button->IsEnabled||button->AtkComponentBase.OwnerNode==null)return;
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;
        if(evt==null)return;
        // Dispatch only the Accept button's own registered event. Never keyboard input.
        helperReplaying=true;
        try{addon->ReceiveEvent(evt->State.EventType,(int)evt->Param,evt);}
        finally{helperReplaying=false;}
        helperNextAction=now.AddSeconds(1);helperQuestStatus="Accepting the matching quest; waiting for game confirmation.";
    }
}
