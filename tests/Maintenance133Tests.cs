using EquinoxCompanion;
internal static class Maintenance133Tests
{
 internal static void Run()
 {
  void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS 133 "+name);}
  var now=DateTimeOffset.Parse("2026-10-12T09:00:00Z");var gate=new MaintenanceSession();
  gate.Observe(1,now);Check(!gate.Ready(now.AddMinutes(4.99)),"no chat before five minutes");Check(gate.Ready(now.AddMinutes(5)),"ready after five minutes");
  gate.Observe(1,now.AddMinutes(2));Check(gate.Due==now.AddMinutes(5),"frame updates do not restart delay");
  gate.Observe(2,now.AddMinutes(3));Check(!gate.Ready(now.AddMinutes(5))&&gate.Ready(now.AddMinutes(8)),"character switch restarts delay");
  gate.Reset();gate.Observe(2,now.AddMinutes(9));Check(!gate.Ready(now.AddMinutes(10)),"same character relog restarts delay");
  string Html(string line)=>"<header class=\"news__header\"><h1>All Worlds Maintenance</h1><script>ldst_strftime(1791453600, 'YMD')</script></header><div class=\"news__detail__wrapper\">[Date &amp; Time]<br>"+line+"<br>Maintenance notice</div>";
  var n=MaintenanceService.Parse(Html("Oct. 12, 2026 3:00 a.m. to Oct. 13, 2026 3:00 a.m. (PDT)"),"https://na.finalfantasyxiv.com/lodestone/news/detail/24dff898070f0d2a8bafb3b1a862e344abecc9b5");
  Check(n.Scheduled(now),"upcoming schedule is eligible");
  Check(n.Scheduled(now.AddHours(2)),"active scheduled window is eligible");
  Check(!n.Scheduled(now.AddHours(25)),"scheduled end hides notice immediately");
  Check(!(n with{Completed=true}).Scheduled(now),"completion hides notice");
  Check(!(n with{Start=null}).Scheduled(now)&&!(n with{End=null}).Scheduled(now),"unknown schedule remains silent");
  Check(!(n with{End=n.Start}).Scheduled(now),"invalid window remains silent");
  Check(n.Start==now.AddHours(1)&&n.End==now.AddHours(25),"official PDT date span converts to UTC");
  Check(n.Published==DateTimeOffset.Parse("2026-10-08T10:00:00Z"),"publication used for unknown timing expiry");
  Check(gate.New(n)&&!gate.New(n),"duplicate notices suppressed within login");Check(gate.New(n with{End=n.End!.Value.AddHours(1)}),"schedule revision can notify");
  var winter=MaintenanceService.Parse(Html("Jan. 12, 2027 1:00 a.m. to 5:00 a.m. (PST)"),n.Url);Check(winter.End-winter.Start==TimeSpan.FromHours(4)&&winter.Start!.Value.Hour==9,"PST and same-day time range");
  Check(MaintenanceService.Parse(Html("Schedule to be announced"),n.Url).Start is null,"unknown schedule never fabricated");
  Check(!MaintenanceService.Game("Companion App Maintenance")&&!MaintenanceService.Game("The Lodestone Maintenance")&&MaintenanceService.Game("Gilgamesh World Emergency Maintenance"),"game versus website maintenance");
  var zone=TimeZoneInfo.CreateCustomTimeZone("Player local",TimeSpan.FromHours(2),"Player local","Player local");
  Check(n.Message(now,zone).Contains("12:00 PM")&&n.Message(now,zone).Contains("1h 0m"),"player local AM/PM time and countdown");
  Check(n.Message(now.AddHours(2),zone).Contains("scheduled maintenance window"),"active schedule labelled as schedule");
  Check(n.Message(now.AddDays(2),zone).Contains("unconfirmed"),"elapsed countdown never declares servers online");
  Check(n.Relevant("Rafflesia","Dynamis")&&!(n with{Title="Gilgamesh World Maintenance"}).Relevant("Rafflesia","Dynamis"),"regional notice only for matching world or DC");
 }
}
