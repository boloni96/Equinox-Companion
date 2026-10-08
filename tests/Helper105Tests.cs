using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper105Tests
{
    [ModuleInitializer] public static void Run(){
        void Check(bool ok,string label){if(!ok)throw new Exception("Helper105: "+label);}
        var now=DateTimeOffset.UnixEpoch;
        Check(!HelperInteractionPolicy.Waiting(now,default),"first interaction immediate");
        var deadline=HelperInteractionPolicy.Deadline(now);
        Check(HelperInteractionPolicy.Waiting(now.AddSeconds(1),deadline),"no rapid second interaction");
        Check(HelperInteractionPolicy.Waiting(now.AddSeconds(7),deadline),"delayed duty prompt gets time");
        Check(!HelperInteractionPolicy.Waiting(now.AddSeconds(12),deadline),"bounded retry deadline");
        var third=HelperInteractionPolicy.Deadline(now.AddSeconds(24));
        Check(HelperInteractionPolicy.Waiting(now.AddSeconds(31),third),"final attempt gets full prompt wait");
        var recovery=new HelperSceneRecoveryGate();
        Check(recovery.Allowed,"initial missed click can recover");
        recovery.Committed();
        Check(!recovery.Allowed,"post-Proceed scene cannot create another NPC interaction");
        recovery.Committed();
        Check(!recovery.Allowed,"continuation stays suppressed");
        recovery.NewInteraction();
        Check(recovery.Allowed,"genuine next NPC interaction restores recovery");
    }
}
