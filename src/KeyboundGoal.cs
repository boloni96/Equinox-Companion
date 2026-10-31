using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset keyboundGoalCheckAt;
    private bool keyboundGoalWaiting;
    private bool CheckKeyboundGoal(DateTimeOffset now)
    {
        // Complete an existing match/queue. Decide before a new registration,
        // with the current character's live wallet and a freshly rebuilt list.
        if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51])return true;
        if(Conditions[ConditionFlag.InDutyQueue]||Conditions[ConditionFlag.WaitingForDuty]||Conditions[ConditionFlag.WaitingForDutyFinder]||Conditions[ConditionFlag.WaitingForRaceOrMatch]||Conditions[ConditionFlag.RegisteringForRaceOrMatch])return false;
        if(now<keyboundGoalCheckAt)return keyboundGoalWaiting;
        keyboundGoalCheckAt=now.AddSeconds(1);keyboundGoalWaiting=true;
        RefreshKeyboundBuyPlan();
        if(keyboundMissingPlan==null){
            keyboundStatus=keyboundPlanError.Length>0?keyboundPlanError:"Waiting for reward target";
            return true;
        }
        try{
            var balance=KeyboundWallet();
            var total=KeyboundShoppingPolicy.Total(keyboundMissingPlan);
            if(KeyboundShoppingPolicy.GoalReached(true,total,balance)){
                StopKeybound(total==0?"Stopped: all selected rewards are obtained":"Stopped: enough MGC for your reward list");
                return true;
            }
        }catch(Exception){keyboundStatus="Waiting for current MGC balance";return true;}
        keyboundGoalWaiting=false;return false;
    }
}
