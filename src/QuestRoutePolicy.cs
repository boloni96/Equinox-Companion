using System.Text.Json;
namespace EquinoxCompanion;
internal sealed record QuestRouteReview(bool Readable,bool Duty,uint[] Next,string Reason);
internal static class QuestRoutePolicy
{
    internal static QuestRouteReview Review(string json)
    {
        try{
            using var doc=JsonDocument.Parse(json.TrimStart('\uFEFF'));
            if(!doc.RootElement.TryGetProperty("QuestSequence",out var sequences)||sequences.ValueKind!=JsonValueKind.Array)
                return new(false,false,[],"Quest sequence missing.");
            var next=new HashSet<uint>();var duty=false;
            void Walk(JsonElement node){
                if(node.ValueKind==JsonValueKind.Array){foreach(var n in node.EnumerateArray())Walk(n);return;}
                if(node.ValueKind!=JsonValueKind.Object)return;
                foreach(var property in node.EnumerateObject()){
                    if(property.Name=="InteractionType"&&property.Value.ValueKind==JsonValueKind.String){
                        var type=property.Value.GetString()??"";
                        if(type.Contains("Duty",StringComparison.OrdinalIgnoreCase)||type is "Macro" or "WaitForManualProgress" or "Instruction")duty=true;
                    }
                    if(property.Name is "DutyOptions" or "SinglePlayerDutyOptions" or "ContentFinderConditionId" or "AutoDuty"&&property.Value.ValueKind!=JsonValueKind.Null)duty=true;
                    if(property.Name=="NextQuestId"){
                        var value=property.Value.ToString();
                        if(uint.TryParse(value,out var id)&&id>0)next.Add(QuestToolsPolicy.ShortId(id));
                    }
                    Walk(property.Value);
                }
            }
            Walk(sequences);
            return new(true,duty,next.Order().ToArray(),duty?"Duty or macro step: left pending for manual play.":"");
        }catch(JsonException){return new(false,false,[],"Quest route could not be verified.");}
    }
}
