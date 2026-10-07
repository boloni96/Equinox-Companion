using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private const string NocturneReplayPrompt="Do you wish to replay the event?";
    private bool NocturneNpc(HelperNpc npc)=>npc.Name=="Kipih Jakkya"&&npc.Territory==130;
    private uint NocturneFirstQuest=>HelperQuestIdForName("The Man in Black");
    private unsafe bool HelperReplayPromptVisible()
    {
        var yes=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        return yes!=null&&yes->IsVisible&&yes->PromptText!=null&&yes->PromptText->NodeText.ToString()==NocturneReplayPrompt;
    }
    private unsafe bool CaptureHelperEventReplay(AtkUnitBase* addon,int index)
    {
        if(index!=0||helperCaptureNpc is not {} npc||!NocturneNpc(npc)||addon!=(AtkUnitBase*)GardenGui.GetAddonByName("SelectYesno").Address||!HelperReplayPromptVisible())return false;
        var quest=NocturneFirstQuest;if(quest==0)return false;
        EmitHelper("eventReplay",NocturneReplayPrompt,HelperPolicy.Signature([NocturneReplayPrompt]),"SelectYesno",questId:quest);
        return true;
    }
    private unsafe void UpdateHelperEventReplay(HelperAction action,DateTimeOffset now)
    {
        if(!NocturneNpc(action.Npc)||action.Text!=NocturneReplayPrompt||action.QuestId==0||action.QuestId!=NocturneFirstQuest){BlockHelper("This event replay has no verified first-time mapping.");return;}
        if(HelperReplayPromptVisible()){
            var dialog=(AtkUnitBase*)GardenGui.GetAddonByName("SelectYesno").Address;
            helperReplaying=true;try{dialog->FireCallbackInt(0);}finally{helperReplaying=false;}
            CompleteHelperAction(now);return;
        }
        // The leader chose Replay; a first-time follower accepts the same event's first quest.
        UpdateHelperQuestAccept(action,now);
    }
}
