namespace EquinoxCompanion;

public static class FashionCompletion
{
    public static DateTimeOffset Cycle(DateTimeOffset at)
    {
        var anchor=new DateTimeOffset(2024,1,2,8,0,0,TimeSpan.Zero);
        return anchor.AddDays(7*Math.Floor((at-anchor).TotalDays/7));
    }

    public static (DateTimeOffset Start,DateTimeOffset End) Window(DateTimeOffset now) => (Cycle(now).AddDays(3),Cycle(now).AddDays(7));
    public static bool IsOpen(DateTimeOffset now) { var w=Window(now);return now>=w.Start&&now<w.End; }

    public static string Tooltip(Actor? actor,IEnumerable<SyncEvent> observations,IEnumerable<SharedPerson> people,DateTimeOffset now)
    {
        if(actor is null)return "Log in to see this character's Fashion Report.";
        var cycle=Cycle(now);var window=Window(now);
        var local=observations.Where(e=>e.Kind=="fashion.observed"&&e.Actor.ContentId==actor.ContentId&&e.At>=window.Start&&e.At<window.End&&e.At<=now&&e.Fashion is {} f&&DateTimeOffset.TryParse(f.Cycle,out var at)&&at==cycle).MaxBy(e=>e.At);
        var matches=people.SelectMany(p=>p.Characters).Where(c=>string.Equals(c.Name.Trim(),actor.Name.Trim(),StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(actor.HomeWorldName)&&string.Equals(c.World.Trim(),actor.HomeWorldName.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
        var shared=matches.Length==1?matches[0]:null;
        var valid=shared?.FashionCycle==cycle&&shared.FashionObservedAt>=window.Start&&shared.FashionObservedAt<=now;
        var useLocal=local is not null&&(!valid||local.At>=shared!.FashionObservedAt);
        var score=useLocal?local!.Fashion!.Score:valid?shared!.FashionScore:null;
        var atTime=useLocal?local!.At:valid?shared!.FashionObservedAt:null;
        var done=IsComplete(actor,observations,people,now);
        return (!IsOpen(now)?"Fashion Report judging is closed":done?"Fashion Report completed for this event":"Fashion Report not completed for this event")+
            "\nCharacter: "+actor.Name+(string.IsNullOrWhiteSpace(actor.HomeWorldName)?"":" @ "+actor.HomeWorldName)+
            "\nFashion Points: "+(score is {} points?$"{points}/100":"Not recorded this week")+
            (atTime is {} time?$"\nRecorded: {time.ToLocalTime():g}":"")+
            $"\nJudging opens: {window.Start.ToLocalTime():g}\nEvent ends: {window.End.ToLocalTime():g}";
    }

    public static bool IsComplete(Actor? actor,IEnumerable<SyncEvent> observations,IEnumerable<SharedPerson> people,DateTimeOffset now)
    {
        if(!IsOpen(now)||actor is null || string.IsNullOrWhiteSpace(actor.ContentId) || actor.ContentId=="0")return false;
        var cycle=Cycle(now);var window=Window(now);
        var local=observations.Where(e=>e.Kind=="fashion.observed"&&e.Actor.ContentId==actor.ContentId&&
            e.At>=window.Start&&e.At<window.End&&e.At<=now&&e.Fashion is {} f&&DateTimeOffset.TryParse(f.Cycle,out var at)&&at==cycle)
            .MaxBy(e=>e.At);
        var matches=people.SelectMany(p=>p.Characters).Where(c=>string.Equals(c.Name.Trim(),actor.Name.Trim(),StringComparison.OrdinalIgnoreCase)&&
            !string.IsNullOrWhiteSpace(actor.HomeWorldName)&&string.Equals(c.World.Trim(),actor.HomeWorldName.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
        var character=matches.Length==1?matches[0]:null;
        var shared=character?.FashionCompletedAt;
        if(shared<window.Start||shared>=window.End||shared>now)shared=null;
        var observed=character?.FashionCycle==cycle&&character.FashionScore is >=0 and <=100&&character.FashionObservedAt>=window.Start&&character.FashionObservedAt<window.End&&character.FashionObservedAt<=now?character.FashionObservedAt:null;
        var sharedAt=shared is not null&&(observed is null||shared>observed)?shared:observed;
        var sharedDone=sharedAt is not null&&(sharedAt==shared||character?.FashionScore>=80);
        if(local?.Fashion is {} result&&(sharedAt is null||local.At>=sharedAt))return result.Score>=80;
        return sharedDone;
    }
}
