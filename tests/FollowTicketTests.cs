using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class FollowTicketTests
{
 [ModuleInitializer] public static void Run(){
    void Check(bool ok,string label){if(!ok)throw new Exception("Ticket: "+label);}
    foreach(var n in new[]{"1","13","999","1,000"})
        Check(FollowTicketPolicy.Prompt("Use an aetheryte ticket to teleport free of charge? (Total: "+n+")"),"inventory counts");
    Check(FollowTicketPolicy.Prompt("Use an aetheryte ticket to teleport free of\r\ncharge? (Total: 13)"),"wrapped prompt");
    foreach(var text in new[]{"Skip cutscene?","Buy an aetheryte ticket?","Use an aetheryte ticket to teleport free of charge? (Total: 0)","Use an aetheryte ticket to teleport free of charge? (Total: 13) Extra"})
        Check(!FollowTicketPolicy.Prompt(text),"unrelated or invalid prompt");
    Check(!new FollowThemSettings().UseAetheryteTickets,"preserve tickets by default");
    Check(FollowTicketPolicy.Pending("a","a",1,1,10,20,true,true,false,false,true),"owned request");
    Check(!FollowTicketPolicy.Pending("a","b",1,1,10,20,true,true,false,false,true),"other trip");
    Check(!FollowTicketPolicy.Pending("a","a",1,2,10,20,true,true,false,false,true),"new session");
    Check(!FollowTicketPolicy.Pending("a","a",1,1,20,20,true,true,false,false,true),"expired");
    Check(!FollowTicketPolicy.Pending("a","a",1,1,10,20,false,true,false,false,true),"stopped");
    Check(!FollowTicketPolicy.Pending("a","a",1,1,10,20,true,false,false,false,true),"disabled");
    Check(!FollowTicketPolicy.Pending("a","a",1,1,10,20,true,true,true,false,true),"paused");
    Check(!FollowTicketPolicy.Pending("a","a",1,1,10,20,true,true,false,true,true),"loading");
    Check(!FollowTicketPolicy.Pending("a","a",1,1,10,20,true,true,false,false,false),"area changed");
 }
}
