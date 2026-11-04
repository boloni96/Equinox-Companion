using System.Text.Json;
namespace EquinoxCompanion;
internal sealed record QuestLevelingCandidate(uint Id,long Xp,int Steps,uint[] Previous);
internal static class QuestLevelingPolicy
{
    internal static double Score(QuestLevelingCandidate candidate,IReadOnlyList<QuestLevelingCandidate> choices,Func<uint,bool> complete)
    {
        var next=choices.Where(n=>n.Id!=candidate.Id&&n.Previous.Contains(candidate.Id)&&n.Previous.All(p=>p==candidate.Id||complete(p)))
            .OrderByDescending(n=>n.Xp/(double)Math.Max(1,n.Steps)).FirstOrDefault();
        return next==null?candidate.Xp/(double)Math.Max(1,candidate.Steps):(candidate.Xp+next.Xp)/(double)Math.Max(1,candidate.Steps+next.Steps);
    }

    // Prefer quests whose remaining recorded steps need no manual mechanics.
    // Runtime checks still own actions, eligibility and completion confirmation.
    internal static (bool SupportedNow, bool FullySupported, int Steps) Assess(JsonElement route, int sequence)
    {
        if(route.TryGetProperty("Disabled",out var disabled)&&disabled.ValueKind==JsonValueKind.True)return (false,false,0);
        if(!route.TryGetProperty("QuestSequence",out var sequences)||sequences.ValueKind!=JsonValueKind.Array)return (false,false,0);
        foreach(var property in route.EnumerateObject())
            if(property.Name is not ("$schema" or "$" or "Author" or "LastChecked" or "Comment" or "Disabled" or "Interruptible" or "QuestSequence")&&IndependentStepPolicy.Meaningful(property.Value))return (false,false,0);
        var current=sequences.EnumerateArray().Where(s=>IndependentStepPolicy.Number(s,"Sequence",999)==sequence).ToArray();
        if(current.Length!=1||!current[0].TryGetProperty("Steps",out var steps)||steps.ValueKind!=JsonValueKind.Array)return (false,false,0);
        bool Supported(JsonElement s)=>s.TryGetProperty("Disabled",out var d)&&d.ValueKind==JsonValueKind.True||IndependentStepPolicy.Review(s).Length==0;
        var first=steps.EnumerateArray().FirstOrDefault(s=>!s.TryGetProperty("Disabled",out var d)||d.ValueKind!=JsonValueKind.True);
        if(first.ValueKind==JsonValueKind.Undefined)return (false,false,0);
        var remaining=sequences.EnumerateArray().Where(s=>IndependentStepPolicy.Number(s,"Sequence",999)>=(uint)sequence)
            .SelectMany(s=>s.TryGetProperty("Steps",out var ss)&&ss.ValueKind==JsonValueKind.Array?ss.EnumerateArray().ToArray():[]).ToArray();
        return (Supported(first),remaining.Length>0&&remaining.All(Supported)&&remaining.Any(s=>IndependentStepPolicy.Text(s,"InteractionType")=="CompleteQuest"),remaining.Length);
    }
}
