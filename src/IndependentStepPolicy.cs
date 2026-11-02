using System.Text.Json;
namespace EquinoxCompanion;
public static class IndependentStepPolicy
{
    public static readonly HashSet<string> Actions=["AcceptQuest","CompleteQuest","Interact","WalkTo","None","WaitForNpcAtPosition","UseItem","Emote","Say"];
    private static readonly HashSet<string> Fields=["DataId","Position","TerritoryId","InteractionType","StopDistance","Comment","$","Disabled","DelaySecondsAtStart","AetheryteShortcut","AethernetShortcut","TargetTerritoryId","DialogueChoices","CompletionQuestVariablesFlags","NextQuestId","ItemId","Emote","ChatMessage"];
    public static string Review(JsonElement step)
    {
        var type=Text(step,"InteractionType");
        if(type.Contains("Leve",StringComparison.OrdinalIgnoreCase))return "Levequest left for manual play.";
        if(type is "Combat" or "Duty" or "SinglePlayerDuty")return "Combat / duty left for manual play.";
        if(!Actions.Contains(type))return "Step requires manual play: "+type;
        foreach(var p in step.EnumerateObject())
            if(!Fields.Contains(p.Name)&&Meaningful(p.Value))return "Step condition not supported yet: "+p.Name;
        if(step.TryGetProperty("DialogueChoices",out var choices)&&choices.ValueKind==JsonValueKind.Array)
            foreach(var choice in choices.EnumerateArray())
            {
                if(Text(choice,"Type") is not ("YesNo" or "List"))return "Dialogue type requires manual input.";
                if(choice.TryGetProperty("SpecialCondition",out var condition)&&Meaningful(condition))return "Special dialogue condition requires manual input.";
                foreach(var p in choice.EnumerateObject())
                    if(p.Name is not ("Type" or "Prompt" or "Answer" or "Yes" or "DataId" or "ExcelSheet" or "PromptIsRegularExpression" or "AnswerIsRegularExpression" or "SpecialCondition")&&Meaningful(p.Value))return "Unknown dialogue condition: "+p.Name;
            }
        return "";
    }
    public static bool Meaningful(JsonElement value)=>value.ValueKind switch {JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.False=>false,JsonValueKind.Array=>value.EnumerateArray().Any(Meaningful),JsonValueKind.Object=>value.EnumerateObject().Any(p=>Meaningful(p.Value)),_=>true};
    public static string Text(JsonElement e,string name)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??"":"";
    public static uint Number(JsonElement e,string name,uint fallback=0)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(name,out var p)&&p.TryGetUInt32(out var n)?n:fallback;
    public static bool CompletedFlags(JsonElement flags,byte[] actual)
    {
        if(flags.ValueKind!=JsonValueKind.Array||flags.GetArrayLength()!=6||actual.Length!=6)return false;
        bool meaningful=false;int i=0;
        foreach(var check in flags.EnumerateArray())
        {
            var value=actual[i++];if(check.ValueKind==JsonValueKind.Null)continue;
            int? high=null,low=null;var exact=false;
            if(check.ValueKind==JsonValueKind.Number){var bits=check.GetByte();high=bits>>4;low=bits&15;}
            else if(check.ValueKind==JsonValueKind.Object){
                if(check.TryGetProperty("High",out var h))high=h.GetInt32();if(check.TryGetProperty("Low",out var l))low=l.GetInt32();
                var mode=Text(check,"Mode");if(mode is not ("" or "Bitwise" or "Exact"))throw new InvalidOperationException("Unsupported quest-variable mode");exact=mode=="Exact";
                if(check.EnumerateObject().Any(p=>p.Name is not ("High" or "Low" or "Mode")))throw new InvalidOperationException("Unknown quest-variable condition");
            }else throw new InvalidOperationException("Invalid quest-variable condition");
            if(high is <0 or >15||low is <0 or >15)throw new InvalidOperationException("Invalid quest-variable nibble");
            meaningful|=high.GetValueOrDefault()!=0||low.GetValueOrDefault()!=0;
            if(high is {} hv&&(exact?(value>>4)!=hv:((value>>4)&hv)!=hv))return false;
            if(low is {} lv&&(exact?(value&15)!=lv:(value&lv)!=lv))return false;
        }
        return meaningful;
    }
}
