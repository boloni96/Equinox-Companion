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
        if(!EstateTravelCommand.CanResume(42,42,100,100)||
            EstateTravelCommand.CanResume(42,43,100,100)||
            EstateTravelCommand.CanResume(42,42,100,101)||
            EstateTravelCommand.CanResume(0,0,0,0))
            throw new Exception("Estate continuation must require the same character on its home world.");
        bool Confirm(bool sent=true,ulong selected=42,int slot=1,string prompt="Log in with Gideon Bulwark?") =>
            EstateTravelCommand.CanConfirmLogin(sent,42,selected,1,slot,"Gideon Bulwark",prompt);
        if(!Confirm()||!Confirm(prompt:"Mit Gideon Bulwark einloggen?")||
            Confirm(sent:false)||Confirm(selected:43)||Confirm(selected:0)||Confirm(slot:0)||Confirm(slot:-1)||
            Confirm(prompt:"Log in with Another Character?")||Confirm(prompt:"Log in with Gideon Bulwarkson?"))
            throw new Exception("Estate login confirmation must accept name-only prompts for the reserved identity only.");
        if(EstateTravelCommand.EntranceName("[en]Additional Chambers")!="Additional Chambers"||
            EstateTravelCommand.EntranceName("Additional Chambers")!="Additional Chambers"||
            !EstateTravelCommand.WaitForEntrance(TimeSpan.FromSeconds(14))||
            EstateTravelCommand.WaitForEntrance(TimeSpan.FromSeconds(15)))
            throw new Exception("Estate entrance loading and localized name policy failed.");
        for(var steps=0;steps<6;steps++)
            if(!EstateTravelCommand.CanExtendEntry(false,steps,0))
                throw new Exception("A plot farther than three yalms must allow another checked segment.");
        if(EstateTravelCommand.CanExtendEntry(false,6,0)||
            !EstateTravelCommand.CanExtendEntry(true,6,0)||
            EstateTravelCommand.CanExtendEntry(true,1,1))
            throw new Exception("Plot entry and the extra door-reveal segment need separate bounded budgets.");
        if(!EstateTravelCommand.InteriorArrived(3,true,true,1023)||
            EstateTravelCommand.InteriorArrived(3,false,true,1023)||
            EstateTravelCommand.InteriorArrived(3,true,false,0)||
            EstateTravelCommand.InteriorArrived(2,true,true,1023)||
            EstateTravelCommand.InteriorArrived(2,true,false,7)||
            !EstateTravelCommand.InteriorArrived(2,true,false,0))
            throw new Exception("Workshop completion must allow its special room number and require the requested estate.");
        Console.WriteLine("Estate travel command modes passed.");
    }
}
