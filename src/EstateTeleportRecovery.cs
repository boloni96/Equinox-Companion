namespace EquinoxCompanion;
public enum EstateTeleportAction { Wait, Retry, Stop }
public static class EstateTeleportRecovery
{
    public static EstateTeleportAction Decide(TimeSpan elapsed,bool atWard,bool started,bool pending,int attempts){
        if(atWard)return EstateTeleportAction.Wait;
        if(elapsed>=TimeSpan.FromSeconds(45))return EstateTeleportAction.Stop;
        if(started||pending||elapsed<TimeSpan.FromSeconds(8))return EstateTeleportAction.Wait;
        return attempts<2?EstateTeleportAction.Retry:EstateTeleportAction.Stop;
    }
}
