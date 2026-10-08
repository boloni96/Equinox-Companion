using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint helperDutyPending,helperDutySource;
    private DateTimeOffset helperDutyUntil;
    private bool helperDutyLoading;
    private unsafe bool HelperSoloDutyVisible()=>ReadHelperDutyPrompt("SelectYesno",out _,out _);
    private unsafe bool ReadHelperDutyPrompt(string name,out string prompt,out AtkComponentButton* proceed)
    {
        prompt="";proceed=null;var addon=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;var cut=AgentCutscene.Instance();
        if(addon==null||!addon->IsVisible||!addon->IsReady||cut!=null&&cut->SkipDialogAddonId==addon->Id)return false;
        if(name=="DifficultySelectYesNo"){
            var duty=(AddonDifficultySelectYesNo*)addon;if(duty->PromptText==null)return false;
            prompt=duty->PromptText->NodeText.ToString();proceed=duty->ProceedButton;
        }else if(name=="SelectYesno"){
            var duty=(AddonSelectYesno*)addon;if(duty->PromptText==null)return false;
            prompt=duty->PromptText->NodeText.ToString();proceed=duty->YesButton;
        }else return false;
        prompt=HelperConversationPolicy.NormalizePrompt(prompt);
        return HelperDutyPolicy.QuestTitle(prompt).Length>0&&proceed!=null&&proceed->IsEnabled&&proceed->AtkComponentBase.OwnerNode!=null;
    }
    private unsafe void ObserveHelperDutyChoice(AddonEvent type,AddonArgs args)
    {
        if(!SharingQuest||helperReplaying||!helperRecording||helperCaptureNpc==null||args is not AddonReceiveEventArgs ev||(AtkEventType)ev.AtkEventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))return;
        try{
            if(!ReadHelperDutyPrompt(args.AddonName,out var prompt,out var button))return;
            var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var count=0;var matching=false;
            while(evt!=null&&count++<32){if(evt->State.EventType==(AtkEventType)ev.AtkEventType&&evt->Param==ev.EventParam){matching=true;break;}evt=evt->NextEvent;}
            if(!matching)return;
            var quest=HelperQuestIdForName(HelperDutyPolicy.QuestTitle(prompt));var manager=QuestManager.Instance();
            if(quest==0||manager==null||!manager->IsQuestAccepted(quest))return;
            FlushHelperTalk();EmitHelper("soloDuty",prompt,HelperPolicy.Signature([prompt]),args.AddonName,HelperQuestScenePolicy.Step(true,QuestManager.GetQuestSequence(quest)),quest);
            // Commit before the leader's loading transition changes the source territory.
            if(helperCaptureNpc is {} npc)CommitHelperRecording(npc);
        }catch(Exception e){Log.Debug(e,"Solo duty choice capture unavailable");}
    }
    private unsafe void UpdateHelperSoloDuty(HelperAction action,DateTimeOffset now)
    {
        if(!config.FollowThem.AcceptDutyReady){BlockHelper("Enable Accept duty-ready prompts to mirror solo quest entry, or press Proceed manually.");return;}
        if(!ReadHelperDutyPrompt(action.Addon,out var prompt,out var button)){
            if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The matching solo quest battle prompt did not appear.");return;
        }
        var manager=QuestManager.Instance();var name=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(action.QuestId)?.Name.ToString()??"";
        if(manager==null||!HelperDutyPolicy.Matches(action,prompt,name,manager->IsQuestAccepted(action.QuestId),QuestManager.GetQuestSequence(action.QuestId))){BlockHelper("Solo battle quest, prompt or progress differs from the leader; nothing confirmed.");return;}
        var evt=button->AtkComponentBase.OwnerNode->AtkResNode.AtkEventManager.Event;var count=0;
        while(evt!=null&&count++<32&&evt->State.EventType is not (AtkEventType.ButtonClick or AtkEventType.MouseClick))evt=evt->NextEvent;
        if(evt==null||count>32){BlockHelper("Solo duty Proceed button has no verified click event.");return;}
        var addon=(AtkUnitBase*)GardenGui.GetAddonByName(action.Addon).Address;var click=*evt;var data=new AtkEventData();
        CompleteHelperAction(now);
        helperDutyPending=action.QuestId;helperDutySource=Client.TerritoryType;helperDutyUntil=now.AddSeconds(45);helperDutyLoading=false;
        helperQuestStatus="Solo battle Proceed submitted; waiting for duty entry.";helperReplaying=true;
        try{addon->ReceiveEvent(click.State.EventType,(int)click.Param,&click,&data);}finally{helperReplaying=false;}
        RecordFollowTravel("Solo quest duty entry submitted",new {action.QuestId,action.Addon});
    }
    private unsafe void UpdateHelperDutyEntry(DateTimeOffset now)
    {
        if(helperDutyPending==0)return;
        var loading=Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51];helperDutyLoading|=loading;
        var game=GameMain.Instance();
        if(Player.IsLoaded&&!loading&&helperDutyLoading&&Client.TerritoryType!=helperDutySource&&Conditions[ConditionFlag.BoundByDuty]&&game!=null&&game->CurrentContentFinderConditionId!=0){
            RecordFollowTravel("Solo quest duty entry confirmed",new {questId=helperDutyPending,territory=Client.TerritoryType,duty=game->CurrentContentFinderConditionId});
            helperDutyPending=0;helperQuestStatus="Solo battle entered. Complete the battle manually.";nextHelperStatus=default;
        }else if(now>=helperDutyUntil){helperDutyPending=0;helperLastIssue="Solo battle entry was not confirmed; check the game's message. Proceed will not be retried automatically.";nextHelperStatus=default;}
    }
}
