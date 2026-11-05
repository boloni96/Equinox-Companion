using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class EstateTravel411Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string command,int property,int mode){
            var actual=EstateTravelCommand.Parse(command);
            if(actual is not {} value||value.Property!=property||value.Mode!=mode)throw new Exception("Wrong estate travel mode: "+command);
        }
        Check("fc",2,0);Check("priv",1,0);Check("private",1,0);
        Check("efc",2,2);Check("epriv",1,2);Check("eprivate",1,2);Check(" WORKSHOP ",2,3);
        Check(" EPRIVATE ",1,2);
        foreach(var world in new[]{"Siren","raff","private extra","","fc@Other"})
            if(EstateTravelCommand.Parse(world)!=null)throw new Exception("Estate parser consumed world or invalid input");
        Console.WriteLine("Estate travel command modes passed.");
    }
}
