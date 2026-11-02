using System.Numerics;
using System.Text.Json;
namespace EquinoxCompanion;

// Imported skip clauses are alternatives; never silently discard an unknown clause.
public static class IndependentSkipPolicy
{
    public static string Review(JsonElement conditions)
    {
        if(conditions.ValueKind!=JsonValueKind.Object)return "Invalid skip conditions";
        foreach(var group in conditions.EnumerateObject()){
            if(group.Name is not ("StepIf" or "AetheryteShortcutIf" or "AethernetShortcutIf"))return "Unknown skip group: "+group.Name;
            if(group.Value.ValueKind==JsonValueKind.Null)continue;
            if(group.Value.ValueKind!=JsonValueKind.Object)return "Invalid skip group: "+group.Name;
            foreach(var field in group.Value.EnumerateObject()){
                if(field.Name is not ("Never" or "InSameTerritory" or "InTerritory" or "NotInTerritory" or "NearPosition" or "NotNearPosition" or "QuestsAccepted" or "QuestsCompleted" or "CompletionQuestVariablesFlags"))
                    if(IndependentStepPolicy.Meaningful(field.Value))return "Skip condition needs support: "+field.Name;
                if(field.Name is "NearPosition" or "NotNearPosition"){
                    if(field.Value.ValueKind!=JsonValueKind.Object||field.Value.EnumerateObject().Any(p=>p.Name is not ("Position" or "TerritoryId" or "MaximumDistance")))return "Invalid position skip condition";
                }
            }
        }
        return "";
    }
    public static bool Matches(JsonElement step,string group,uint territory,Vector3 position,byte[] work,Func<uint,bool> accepted,Func<uint,bool> completed)
    {
        if(!step.TryGetProperty("SkipConditions",out var all)||!all.TryGetProperty(group,out var condition)||condition.ValueKind!=JsonValueKind.Object)return false;
        if(condition.TryGetProperty("Never",out var never)&&never.ValueKind==JsonValueKind.True)return false;
        foreach(var p in condition.EnumerateObject()){
            bool match=p.Name switch {
                "InSameTerritory"=>p.Value.ValueKind==JsonValueKind.True&&territory==IndependentStepPolicy.Number(step,"TerritoryId"),
                "InTerritory"=>p.Value.EnumerateArray().Any(v=>v.GetUInt32()==territory),
                "NotInTerritory"=>p.Value.GetArrayLength()>0&&!p.Value.EnumerateArray().Any(v=>v.GetUInt32()==territory),
                "NearPosition"=>Near(p.Value,territory,position,false),
                "NotNearPosition"=>Near(p.Value,territory,position,true),
                "QuestsAccepted"=>All(p.Value,accepted),
                "QuestsCompleted"=>All(p.Value,completed),
                "CompletionQuestVariablesFlags"=>IndependentStepPolicy.CompletedFlags(p.Value,work),
                _=>false
            };
            if(match)return true;
        }
        return false;
    }
    private static bool All(JsonElement values,Func<uint,bool> predicate)=>values.GetArrayLength()>0&&values.EnumerateArray().All(v=>v.ValueKind==JsonValueKind.Number&&predicate(v.GetUInt32()));
    private static bool Near(JsonElement rule,uint territory,Vector3 position,bool inverse)
    {
        if(IndependentStepPolicy.Number(rule,"TerritoryId")!=territory)return false;
        var p=rule.GetProperty("Position");var target=new Vector3(p.GetProperty("X").GetSingle(),p.GetProperty("Y").GetSingle(),p.GetProperty("Z").GetSingle());
        var distance=Vector3.Distance(target,position);var maximum=rule.GetProperty("MaximumDistance").GetSingle();
        if(!float.IsFinite(distance)||!float.IsFinite(maximum)||maximum<0)throw new InvalidOperationException("Invalid skip distance");
        return inverse?distance>=maximum:distance<=maximum;
    }
}
