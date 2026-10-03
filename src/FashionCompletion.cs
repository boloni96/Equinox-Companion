namespace EquinoxCompanion;

public static class FashionCompletion
{
    public static DateTimeOffset Cycle(DateTimeOffset at)
    {
        var anchor=new DateTimeOffset(2024,1,2,8,0,0,TimeSpan.Zero);
        return anchor.AddDays(7*Math.Floor((at-anchor).TotalDays/7));
    }

    public static bool IsComplete(Actor? actor,IEnumerable<SyncEvent> observations,IEnumerable<SharedPerson> people,DateTimeOffset now)
    {
        if(actor is null || string.IsNullOrWhiteSpace(actor.ContentId) || actor.ContentId=="0")return false;
        var cycle=Cycle(now);
        var local=observations.Where(e=>e.Kind=="fashion.observed"&&e.Actor.ContentId==actor.ContentId&&
            e.At>=cycle&&e.At<=now&&e.Fashion is {} f&&DateTimeOffset.TryParse(f.Cycle,out var at)&&at==cycle)
            .MaxBy(e=>e.At);
        var matches=people.SelectMany(p=>p.Characters).Where(c=>string.Equals(c.Name.Trim(),actor.Name.Trim(),StringComparison.OrdinalIgnoreCase)&&
            !string.IsNullOrWhiteSpace(actor.HomeWorldName)&&string.Equals(c.World.Trim(),actor.HomeWorldName.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
        var shared=matches.Length==1?matches[0].FashionCompletedAt:null;
        if(shared<cycle||shared>now)shared=null;
        if(local?.Fashion is {} result && (shared is null || local.At>=shared))return result.Score>=80;
        return shared is not null || local?.Fashion?.Score>=80;
    }
}
