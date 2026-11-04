using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly QuestLevelingProgress levelingProgress=new();
    private ulong levelingProgressCharacter;
    private uint levelingProgressJob;
    private bool levelingProgressStarted;
    private long LevelingLevelCost(int level)=>DataManager.GetExcelSheet<ParamGrow>().GetRowOrDefault((uint)level)?.ExpToNext??0;
    private unsafe bool ReadLevelingExperience(out int level,out long xp)
    {
        level=0;xp=0;var state=PlayerState.Instance();var self=Objects.LocalPlayer;
        if(!Player.IsLoaded||self==null||state==null)return false;
        var index=self.ClassJob.Value.ExpArrayIndex;
        if(index<0||index>=state->ClassJobExperience.Length||index>=state->ClassJobLevels.Length)return false;
        level=state->ClassJobLevels[index];xp=state->ClassJobExperience[index];return level>0&&xp>=0;
    }
    private int LevelingTarget(int level)=>config.QuestLevelingTarget>0?config.QuestLevelingTarget:level+1;
    private void DrawLevelingGoal()
    {
        if(!ReadLevelingExperience(out var level,out var xp)){ImGui.TextDisabled("Log in to read your job level and XP.");return;}
        var target=LevelingTarget(level);
        ImGui.SetNextItemWidth(100);
        if(ImGui.InputInt("Target level",ref target)){config.QuestLevelingTarget=Math.Clamp(target,1,255);SaveConfiguration();}
        target=LevelingTarget(level);
        var remaining=QuestLevelingProgress.Remaining(level,xp,target,LevelingLevelCost);
        if(level>=target){ImGui.TextWrapped($"Target level {target} reached. Quest leveling will not start.");return;}
        if(remaining==null){ImGui.TextWrapped("Target is outside the available XP table, or XP is not ready. Choose a supported level.");return;}
        ImGui.TextWrapped($"Level {level} → {target} · {remaining.Value:N0} XP remaining");
        var same=levelingProgressStarted&&levelingProgressCharacter==Player.ContentId&&levelingProgressJob==Objects.LocalPlayer?.ClassJob.RowId;
        var seconds=same&&questChainActive?levelingProgress.SecondsRemaining(remaining.Value,DateTimeOffset.UtcNow):null;
        ImGui.TextWrapped(seconds is {} s?$"Estimated time: about {Math.Max(1,Math.Ceiling(s/60)):N0} minutes at this session's pace. Travel and quest availability can change this.":"Estimated time: learning your pace (at least two completed quests). No reliable estimate yet.");
    }
    private bool CanStartLevelingGoal()
    {
        if(!ReadLevelingExperience(out var level,out var xp)){questLevelingRecommendation="Waiting for your job level and XP.";return false;}
        var target=LevelingTarget(level);
        if(level>=target){questLevelingRecommendation=$"Target level {target} reached — good job!";return false;}
        if(QuestLevelingProgress.Remaining(level,xp,target,LevelingLevelCost)==null){questLevelingRecommendation="Target level or current XP could not be verified.";return false;}
        return true;
    }
    private void BeginLevelingProgress()
    {
        levelingProgressStarted=false;
        if(!ReadLevelingExperience(out var level,out var xp)||QuestLevelingProgress.TotalBefore(level,LevelingLevelCost) is not {} prefix)return;
        if(config.QuestLevelingTarget<=0){config.QuestLevelingTarget=level+1;SaveConfiguration();}
        levelingProgress.Begin(prefix+xp,DateTimeOffset.UtcNow);levelingProgressCharacter=Player.ContentId;
        levelingProgressJob=Objects.LocalPlayer?.ClassJob.RowId??0;levelingProgressStarted=true;
    }
    private void UpdateLevelingGoal(DateTimeOffset now)
    {
        if(!questChainActive||!questLeveling||Player.ContentId!=questChainCharacter||Objects.LocalPlayer?.ClassJob.RowId!=questLevelingJob)return;
        if(!ReadLevelingExperience(out var level,out var xp))return;
        // Run before navigation/dialogue dispatch: no further quest action after reaching the goal.
        if(level>=LevelingTarget(level)){StopQuestChain($"Target level {LevelingTarget(level)} reached — good job!");return;}
        if(levelingProgressStarted&&xp<LevelingLevelCost(level)&&QuestLevelingProgress.TotalBefore(level,LevelingLevelCost) is {} prefix)levelingProgress.Observe(prefix+xp,now);
    }
}
