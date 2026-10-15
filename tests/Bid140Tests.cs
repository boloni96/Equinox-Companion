using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Bid140Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string name){if(!ok)throw new Exception("140: "+name);}
  var now=DateTimeOffset.UtcNow;
  var p=new BidPlot(86,"Mist",1,2,0,3000000,4,1,now.ToUnixTimeSeconds()+600,5,now.ToUnixTimeSeconds(),now.ToUnixTimeSeconds());
  var f=new BidFilters();Check(f.Matches(p,now),"entry default");Check(!f.Matches(p with{Phase=2},now),"results excluded by default");
  f.Phases.Add(1);Check(f.Matches(p with{Phase=2},now)&&f.Matches(p,now),"OR within phase selections");
  f.Tenants.Clear();f.Tenants.Add(2);Check(!f.Matches(p,now)&&f.Matches(p with{Purchase=7},now),"tenant flag overlap");
  f.Clear();f.Wards.Remove(1);Check(!f.Matches(p,now),"AND across filters");f.Wards.Add(1);Check(f.Matches(p,now),"ward reselected");
  f.Phases.Clear();Check(!f.Matches(p,now),"none means no results");
  Check((p with{Purchase=4,Phase=null,PhaseUntil=null}).PhaseLabel(now)=="FCFS","FCFS recognized without lottery data");
  Check((p with{Phase=3}).PhaseLabel(now)=="Unavailable","unavailable phase");
  Check((p with{PhaseUntil=now.ToUnixTimeSeconds()-1}).PhaseLabel(now)=="Missing/Outdated","expired cannot look accepting");
  f.Clear();Check(f.Matches(p with{Phase=null,PhaseUntil=null},now),"clear includes missing");
  Console.WriteLine("PASS 140 column filter intersections, multiple selections, tenant flags, FCFS and outdated phases");
 }
}
