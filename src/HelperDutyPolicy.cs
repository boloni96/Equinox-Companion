namespace EquinoxCompanion;
public static class HelperDutyPolicy
{
    public static string QuestTitle(string prompt)
    {
        var match=System.Text.RegularExpressions.Regex.Match(HelperConversationPolicy.NormalizePrompt(prompt),"^Duty calls\\. Commence battle for [\"“](?<quest>[^\"”]{1,100})[\"”]\\?");
        return match.Success?match.Groups["quest"].Value:"";
    }
    public static string LeaveSignature(string prompt)=>HelperPolicy.Signature(["soloDutyLeave",HelperConversationPolicy.NormalizePrompt(prompt)]);
    public static bool IsLeave(HelperAction a)=>a.Kind=="choice"&&HelperQuestScenePolicy.Quest(a.QuestId)&&a.Addon is "SelectYesno" or "DifficultySelectYesNo"&&QuestTitle(a.Text).Length>0&&a.Signature==LeaveSignature(a.Text);
    public static bool MatchesLeave(HelperAction a,string prompt,string questName,bool accepted,byte sequence)=>
        IsLeave(a)&&Matches(a with {Kind="soloDuty",Signature=HelperPolicy.Signature([a.Text])},prompt,questName,accepted,sequence);
    public static bool Matches(HelperAction action,string prompt,string questName,bool accepted,byte sequence)=>
        action.Kind=="soloDuty"&&action.QuestId>=65536&&accepted&&QuestTitle(prompt)==questName&&questName.Length>0&&HelperConversationPolicy.NormalizePrompt(prompt)==action.Text&&HelperPolicy.Signature([action.Text])==action.Signature&&action.Scene==HelperQuestScenePolicy.Step(true,sequence);
}
