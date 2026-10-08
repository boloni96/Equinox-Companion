using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool HelperInCutscene()=>Conditions[ConditionFlag.OccupiedInCutSceneEvent]||Conditions[ConditionFlag.WatchingCutscene]||Conditions[ConditionFlag.WatchingCutscene78];
    private unsafe bool TryHelperSkipMenu(out AtkUnitBase* menu,out string prompt,out List<string> choices)
    {
        menu=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;prompt="";choices=[];
        var agent=AgentCutscene.Instance();
        if(agent==null||agent->SkipCallback==null||agent->SkipDialogAddonId==0||menu==null||!menu->IsVisible||!menu->IsReady||menu->Id!=agent->SkipDialogAddonId)return false;
        var node=menu->GetTextNodeById(2);if(node==null)return false;
        prompt=(TravelMenuText(node->NodeText.StringPtr)??"").Trim();
        choices=TransportChoices(menu);
        return HelperCutscenePolicy.Menu(prompt,choices);
    }
    private unsafe void UpdateHelperCutsceneSkip(HelperAction a,DateTimeOffset now)
    {
        if(!helperPermission.Skip){BlockHelper("Cutscene skipping is not enabled for this session.");return;}
        if(helperTextAdvanceAction==a.Id&&(!HelperInCutscene()||HelperScene()!=a.Scene)){
            ReleaseHelperTextAdvance();CompleteHelperAction(now);return;
        }
        if(now-helperActionStarted>TimeSpan.FromSeconds(15)){
            ReleaseHelperTextAdvance();BlockHelper("TextAdvance did not finish the matching cutscene within 15 seconds; no retry.");return;
        }
        if(a.Scene.Length==0||HelperScene()!=a.Scene||!HelperInCutscene())return;
        if(helperTextAdvanceAction!=a.Id){
            if(HelperCutsceneReplayPolicy.BlockingAddons.Any(VisibleFollowAddon)&&!TryHelperSkipMenu(out _,out _,out _)||FollowStopTextEntryActive())return;
            if(!StartHelperTextAdvance(a,now))return;
        }
        if(!helperTextAdvanceOwned)return;
        if(!helperActionSubmitted&&TryHelperSkipMenu(out _,out var prompt,out var choices)){
            if(prompt!=a.Text||HelperPolicy.Signature(choices)!=a.Signature){ReleaseHelperTextAdvance();BlockHelper("Cutscene skip prompt differs; no response selected.");return;}
            try{
                // TextAdvance owns the native skip operation. Companion never calls the menu callback.
                if(!Pi.GetIpcSubscriber<string,HelperTextAdvanceOptions,bool>("TextAdvance.EnableExternalControl")
                    .InvokeFunc(HelperTextAdvanceOwner,new HelperTextAdvanceOptions(true))){
                    ReleaseHelperTextAdvance();BlockHelper("TextAdvance control was lost; no confirmation requested.");return;
                }
                helperActionSubmitted=true;
                RecordFollowTravel("Helper TextAdvance confirmation enabled",new {a.Scene,a.QuestId});
            }catch(Exception ex){ReleaseHelperTextAdvance();BlockHelper("TextAdvance confirmation unavailable: "+ex.GetType().Name);}
        }
    }
}
