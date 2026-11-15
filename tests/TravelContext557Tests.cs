using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class TravelContext557Tests
{
    [ModuleInitializer] internal static void Run(){
        var t=DateTimeOffset.Parse("2026-11-15T21:04:35Z");var recovery=new TravelContextRecovery();
        void Check(bool yes,string why){if(!yes)throw new Exception("557 "+why);}
        Check(recovery.Check(true,false,123,123,t)==TravelContextAction.Reopen,"unowned context adopted");
        recovery.Requested(t);
        Check(recovery.Check(true,true,0,123,t.AddSeconds(1))==TravelContextAction.Wait,"late hover rejected immediately");
        Check(recovery.Check(true,true,123,123,t.AddSeconds(1))==TravelContextAction.Use,"verified context not used");
        Check(recovery.Check(true,true,456,123,t.AddSeconds(3))==TravelContextAction.Reopen,"wrong character clicked");
        recovery.Requested(t.AddSeconds(3));
        Check(recovery.Check(false,true,0,123,t.AddSeconds(4))==TravelContextAction.Wait,"missing menu retried too early");
        Check(recovery.Check(false,true,0,123,t.AddSeconds(6))==TravelContextAction.Reopen,"missing menu not retried");
        Check(recovery.Check(true,true,456,123,t.AddSeconds(9))==TravelContextAction.Stop,"retry limit ignored");
        recovery.Reset();Check(recovery.Retries==0,"new transfer leg retained retries");
        Console.WriteLine("557 delayed/missing character menus, exact identity and bounded recovery passed.");
    }
}
