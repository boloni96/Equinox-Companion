using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private nint helperEscapeWindow;
    private string helperEscapeAction="";
    private void ReleaseHelperEscape()
    {
        if(helperEscapeWindow!=0){FollowWindowInput.Release(helperEscapeWindow,0x1B);helperEscapeWindow=0;}
    }
    private bool HelperInCutscene()=>Conditions[ConditionFlag.OccupiedInCutSceneEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78];
    private unsafe bool TryHelperSkipMenu(out AtkUnitBase* menu,out string prompt,out List<string> choices)
    {
        menu=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;prompt="";choices=[];
        var agent=AgentCutscene.Instance();
        if(agent==null||agent->SkipCallback==null||agent->SkipDialogAddonId==0||menu==null||!menu->IsVisible||menu->Id!=agent->SkipDialogAddonId)return false;
        var node=menu->GetTextNodeById(2);if(node==null)return false;
        prompt=(TravelMenuText(node->NodeText.StringPtr)??"").Trim();
        choices=TransportChoices(menu);
        return HelperCutscenePolicy.Menu(prompt,choices);
    }
    private unsafe void UpdateHelperCutsceneSkip(HelperAction a,DateTimeOffset now)
    {
        if(!helperPermission.Skip){BlockHelper("Cutscene skipping is not enabled for this session.");return;}
        // A changed scene alone is not proof: require the matching scene to have been observed.
        if(helperActionSubmitted){
            if(!HelperInCutscene()||HelperScene()!=a.Scene){CompleteHelperAction(now);return;}
            if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("Cutscene did not finish after one confirmation; no retry.");
            return;
        }
        if(a.Scene.Length==0||HelperScene()!=a.Scene){
            if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The recorded cutscene did not match; no skip requested.");
            return;
        }
        if(!HelperInCutscene()){
            if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The matching cutscene is not skippable now.");
            return;
        }
        if(TryHelperSkipMenu(out var menu,out var prompt,out var choices)){
            if(prompt!=a.Text||HelperPolicy.Signature(choices)!=a.Signature){BlockHelper("Cutscene skip prompt differs; no response selected.");return;}
            // Use the full, typed SelectString callback like TextAdvance/ECommons.
            // Never OpenSkipDialog(null), call AgentCutscene.ReceiveEvent, or FireCallbackInt.
            var value=new AtkValue();value.Type=FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;value.Int=0;
            helperActionSubmitted=true;helperReplaying=true;
            try{menu->FireCallback(1,&value,true);}
            finally{helperReplaying=false;}
            RecordFollowTravel("Helper cutscene Yes submitted",new {a.Scene,a.QuestId});
            helperNextAction=now.AddMilliseconds(450);return;
        }
        if(helperEscapeAction!=a.Id){
            if(QuestConversationVisible()||VisibleFollowAddon("SelectYesno")||FollowStopTextEntryActive()){
                if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("Another window prevents requesting the cutscene skip.");
                return;
            }
            helperEscapeAction=a.Id;
            if(!FollowWindowInput.PressCutsceneEscape(out helperEscapeWindow)){BlockHelper("Could not request Escape in this game window.");return;}
            RecordFollowTravel("Helper cutscene Escape requested",new {a.Scene,a.QuestId});
            helperQuestStatus="Waiting for the game's cutscene skip prompt.";
            return;
        }
        if(now-helperActionStarted>TimeSpan.FromSeconds(15))BlockHelper("The game did not open a valid cutscene skip prompt; no retry.");
    }
}
