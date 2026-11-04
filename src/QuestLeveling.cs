using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private uint questLevelingJob;
    private string questLevelingRecommendation="";
    private unsafe uint NextLevelingQuest()
    {
        if(!CanStartLevelingGoal())return 0;
        var self=Objects.LocalPlayer;var qm=QuestManager.Instance();
        if(self==null||qm==null||independentBook==null)return 0;
        var choices=new List<QuestLevelingCandidate>();
        var job=self.ClassJob.RowId;var level=self.Level;
        var abbreviation=self.ClassJob.Value.Abbreviation.ToString();
        var categoryProperty=typeof(ClassJobCategory).GetProperty(abbreviation);
        var sheet=DataManager.GetExcelSheet<Quest>();
        foreach(var entry in questCatalogue){
            var id=entry.Id;
            if(questChainVisited.Contains(id)||QuestStillDeferred(id)||QuestManager.IsQuestComplete(id))continue;
            if(!questLevelingIncludeMsq&&entry.Group.Contains("/MSQ",StringComparison.OrdinalIgnoreCase))continue;
            var data=sheet.GetRowOrDefault(id+65536);if(!data.HasValue)continue;var q=data.Value;
            var required=q.ClassJobLevel[0];
            if(q.ExpFactor==0||required>level||q.ClassJobLevel[1]>0||q.IsRepeatable||q.RepeatIntervalType!=0)continue;
            if(q.ClassJobRequired.RowId!=0&&q.ClassJobRequired.RowId!=job)continue;
            if(q.ClassJobCategory0.RowId!=0&&(categoryProperty==null||categoryProperty.GetValue(q.ClassJobCategory0.Value) is not true))continue;
            // Special availability/scaling rules are not guessed. These remain manual until modeled.
            if(q.ClassJobCategory1.RowId!=0||q.Festival.RowId!=0||q.BeastTribe.RowId!=0||q.GrandCompany.RowId!=0||q.GrandCompanyRank.RowId!=0||q.SatisfactionNpc.RowId!=0||q.DeliveryQuest.RowId!=0||q.IsHouseRequired||q.MountRequired.RowId!=0||q.BellStart!=0||q.BellEnd!=0||q.LevelMax!=0)continue;
            if(q.InstanceContent.Any(c=>c.RowId!=0)||q.QuestLock.Any(p=>p.RowId!=0&&QuestManager.IsQuestComplete(p.RowId)))continue;
            var routes=independentBook.Find(id.ToString());if(routes.Length!=1)continue;
            var accepted=qm->IsQuestAccepted(id);
            var assessment=QuestLevelingPolicy.Assess(routes[0].Definition,accepted?QuestManager.GetQuestSequence(id):0);
            if(!assessment.SupportedNow||!assessment.FullySupported)continue;
            var review=QuestRoutePolicy.Review(routes[0].Definition.GetRawText());if(!review.Readable||review.Duty)continue;
            var growth=DataManager.GetExcelSheet<ParamGrow>().GetRowOrDefault(required);if(!growth.HasValue)continue;
            // Base quest XP, before any character/world-specific modifiers.
            var xp=(long)q.ExpFactor*growth.Value.ScaledQuestXP*growth.Value.QuestExpModifier/100;
            if(xp<=0)continue;
            var previous=q.PreviousQuest.Where(p=>p.RowId!=0).Select(p=>QuestToolsPolicy.ShortId(p.RowId)).ToArray();
            choices.Add(new(id,xp,assessment.Steps,accepted?[]:previous));
        }
        // A starter gains credit for one supported follow-up it would unlock. Never skip prerequisites.
        var available=choices.Where(c=>c.Previous.All(QuestManager.IsQuestComplete)&&
            (qm->IsQuestAccepted(c.Id)||qm->NumAcceptedQuests<30));
        var best=available.OrderByDescending(c=>QuestLevelingPolicy.Score(c,choices,QuestManager.IsQuestComplete)).ThenByDescending(c=>c.Xp).ThenBy(c=>c.Id).FirstOrDefault();
        questLevelingRecommendation=best==null?$"Level {level} {abbreviation}: no verified automatic XP quests are currently eligible.":
            $"Level {level} {abbreviation}: {QuestTitle(best.Id)} · about {best.Xp:N0} base XP. Ranked by XP per recorded step, including supported follow-ups; not measured XP/hour.";
        return best?.Id??0;
    }
}
