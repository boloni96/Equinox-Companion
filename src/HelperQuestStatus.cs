using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe string HelperObservedQuestStatus()
    {
        if(!helperPermission.Active||!helperPermission.Quest||helperObservedQuest==0)return "";
        var id=helperObservedQuest;
        var title=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Quest>().GetRowOrDefault(id)?.Name.ToString()??"Quest";
        if(title.Length>80)title=title[..80];
        var manager=QuestManager.Instance();
        var available=Player.IsLoaded&&manager!=null&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51];
        var state=HelperQuestStatusPolicy.State(available,available&&manager->IsQuestAccepted(id),available&&QuestManager.IsQuestComplete(id),available?QuestManager.GetQuestSequence(id):0);
        return $"Leader quest: {title} ({id}) — {state}. · ";
    }
}
