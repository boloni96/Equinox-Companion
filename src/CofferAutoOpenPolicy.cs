using System.Numerics;
namespace EquinoxCompanion;

public sealed class CofferAutoOpenPolicy
{
    public const float Range = 2.5f;
    private readonly Dictionary<ulong,(uint BaseId,Vector3 Position,int Count,DateTimeOffset Last)> attempts=[];
    private DateTimeOffset nextAttempt;
    public void Clear(){attempts.Clear();nextAttempt=default;}
    public static bool Eligible(bool enabled,bool ready,bool treasure,bool targetable,bool present,bool opened,float distance) =>
        enabled && ready && treasure && targetable && present && !opened && float.IsFinite(distance) && distance >= 0 && distance <= Range;
    public bool CanAttempt(CofferPoint point,DateTimeOffset now)
    {
        if(point.Opened || !point.Present || now < nextAttempt)return false;
        if(!attempts.TryGetValue(point.Id,out var prior))return attempts.Count<128;
        if(prior.BaseId!=point.BaseId || Vector3.DistanceSquared(prior.Position,point.Position)>=.25f)return true;
        return prior.Count<2 && now-prior.Last>=TimeSpan.FromSeconds(3);
    }
    // Reserve before native interaction, even if the game rejects it or a callback throws.
    public bool Reserve(CofferPoint point,DateTimeOffset now)
    {
        if(!CanAttempt(point,now))return false;
        var count=1;
        if(attempts.TryGetValue(point.Id,out var prior) && prior.BaseId==point.BaseId && Vector3.DistanceSquared(prior.Position,point.Position)<.25f)count+=prior.Count;
        attempts[point.Id]=(point.BaseId,point.Position,count,now);
        nextAttempt=now.AddMilliseconds(250);
        return true;
    }
}
