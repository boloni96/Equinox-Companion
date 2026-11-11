using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class PendingRegistration548Tests
{
    [ModuleInitializer] internal static void Run(){
        var scope=new string('a',64);
        var actor=new Actor("123","Meowt Nolobi",77,77,"Carbuncle");
        var roster=new SharedRoster(1,DateTimeOffset.UtcNow,[new("sir","Sir",[],[new("main","MAIN")])]);
        var registration=new CharacterRegistration("sir","main",PairingScope:scope);
        var pending=CharacterRegistrationPolicy.PendingLogin(roster,actor,registration,scope);
        if(pending?.Person.Id!="sir"||pending?.Character.ContentId!="123"||pending?.Character.AccountId!="main")throw new Exception("Local registration must identify current account before roster sync");
        if(CharacterRegistrationPolicy.PendingLogin(roster,actor,registration,new string('b',64))!=null)throw new Exception("Registration leaked across pairings");
        if(CharacterRegistrationPolicy.PendingLogin(roster,actor,registration with {AccountId="deleted"},scope)!=null)throw new Exception("Missing account inherited");
        var confirmed=roster with {People=[new("other","Other",[pending!.Value.Character with {AccountId="other"}])]};
        if(CharacterRegistrationPolicy.PendingLogin(confirmed,actor,registration,scope)!=null)throw new Exception("Stale registration overrode confirmed roster membership");
        var slots=new Dictionary<string,int>{{scope+":456",1}};
        if(CurrentLoginIdentification.RecordedServiceSlot(scope,"123","",["456"],slots,new Dictionary<string,int>())!=1)throw new Exception("Pending member cannot inherit recorded sibling slot");
        slots[scope+":789"]=0;
        if(CurrentLoginIdentification.RecordedServiceSlot(scope,"123","",["456","789"],slots,new Dictionary<string,int>())!=-1)throw new Exception("Conflicting native slots must stay unresolved");
    }
}
