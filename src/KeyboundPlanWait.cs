namespace EquinoxCompanion;
public enum KeyboundPlanDecision { Wait, Proceed, Stop }
public static class KeyboundPlanWait
{
    public static KeyboundPlanDecision Decide(bool hasPlan,bool pending)=>pending?KeyboundPlanDecision.Wait:hasPlan?KeyboundPlanDecision.Proceed:KeyboundPlanDecision.Stop;
}
public sealed record KeyboundStartRequest(bool UseRewards,string Source,string Run,ulong Character,uint World,uint Territory,ulong Target,DateTimeOffset Deadline)
{
    public bool Valid(DateTimeOffset now,ulong character,uint world,uint territory,ulong target)=>
        now<Deadline&&Character!=0&&Character==character&&World==world&&Territory==territory&&Target==target;
}
