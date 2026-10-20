using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class GardenMarkerSession222Tests
{
    [ModuleInitializer] public static void Run()
    {
        int checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception("GardenMarkerSession222: "+message);}
        var at=DateTimeOffset.Parse("2026-10-20T21:00:00Z");
        var actor=new Actor("12345","Example",57,57);
        var address=new Address("house",57,339,1,1,0,false,false);
        GardenSnapshot Live(int second,string? target="bed")=>new(at.AddSeconds(second),actor,address,target,"Garden",null,null,true,[],new(2003757,2,"EventObj",10,0,20,7));
        var menu=new GardenMenu(1,at,Live(0),"1st Bed, 1st Patch",["Plant Seeds","Quit"]);
        var session=new GardenMarkerSession();
        Check(!session.Matches(menu,null,at.AddSeconds(1),10,20,true,Live(1,null)),"unidentified target cannot establish a new binding");
        Check(session.Matches(menu,Live(1),at.AddSeconds(1),10,20,true,Live(1)),"native session binds even before a Companion guide is shown");
        Check(session.Matches(menu,null,at.AddSeconds(61),10,20,true,Live(61,null)),"cleared hard target does not lose same open planting window after a minute");
        Check(session.Matches(menu,null,at.AddSeconds(600),10,20,true,Live(600,null)),"continued fresh context keeps native identity for both guides");
        Check(!session.Matches(menu,null,at.AddSeconds(601),10,20,true,Live(600,null)),"stale framework context cannot keep markers active");
        Check(!session.Matches(menu,null,at.AddSeconds(600),10,20,true,Live(601,null)),"future context cannot keep markers active");
        Check(!session.Matches(menu,null,at.AddSeconds(601),10,20,true,Live(601,"other")),"explicitly different target blocks retained identity");
        Check(!session.Matches(menu,null,at.AddSeconds(601),10,20,true,Live(601,null) with {Actor=actor with {ContentId="other"}}),"another character cannot inherit cleared target");
        Check(!session.Matches(menu,null,at.AddSeconds(601),10,20,true,Live(601,null) with {Address=address with {Plot=2}}),"another estate cannot inherit cleared target");
        Check(!session.Matches(menu,null,at.AddSeconds(601),10,20,true,Live(601,null) with {Address=null}),"unknown estate cannot keep markers");
        Check(session.Matches(menu,null,at.AddSeconds(602),10,20,true,Live(602,null)),"fresh same-window context recovers without reopening a Companion guide");
        Check(!session.Matches(menu,null,at.AddSeconds(603),10,20,false,Live(603,null)),"closing native window clears binding");
        Check(!session.Matches(menu,null,at.AddSeconds(604),10,20,true,Live(604,null)),"same native address cannot reconnect to an old cleared target");
        var fresh=menu with {OpenedAt=at.AddSeconds(605),Target=Live(605)};
        Check(session.Matches(fresh,Live(605),at.AddSeconds(605),10,21,true,Live(605)),"new numbered menu binds new window");
        Check(!menu.Matches(at.AddSeconds(600),Live(600)),"action-confirmation expiry remains unchanged");
        Console.WriteLine($"PASS 222 shared native garden session and cleared target checks: {checks}");
    }
}
