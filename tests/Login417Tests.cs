using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Login417Tests
{
    [ModuleInitializer] internal static void Run(){
        bool Confirm(ulong displayed=42,int slot=1,int matches=1,bool sent=true,string prompt="Log in with Ryker Vaxveil?")=>
            EstateTravelCommand.CanConfirmDisplayedLogin(sent,42,displayed,1,slot,matches,"Ryker Vaxveil",prompt);
        if(!Confirm()||Confirm(displayed:0)||Confirm(displayed:43)||Confirm(slot:0)||Confirm(matches:0)||Confirm(matches:2)||
            Confirm(sent:false)||Confirm(prompt:"Log in with Simba Mooncloud?"))
            throw new Exception("Login confirmation requires our request, exact displayed identity, reserved slot and unique native entry.");
        Console.WriteLine("Displayed login identity passed.");
    }
}
