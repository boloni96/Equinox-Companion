using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class RoutineStatus500Tests
{
 [ModuleInitializer] internal static void Run(){
  var h=new RoutineStatusHistory();var now=DateTimeOffset.UtcNow;
  h.Observe(now,"Travel",false,"Ready");if(h.Entries.Length!=0)throw new Exception("Do not record idle startup");
  h.Observe(now,"Travel",true,"Waiting for party to clear");h.Observe(now,"Travel",true,"Waiting for party to clear");
  if(h.Entries.Length!=1)throw new Exception("Do not repeat unchanged waits");
  h.Observe(now,"Travel",false,"Stopped by your movement input");
  if(h.Entries[^1].Active||!h.Entries[^1].Detail.Contains("movement"))throw new Exception("Retain exact terminal cause");
  h.Observe(now,"Storage",true,"");if(!h.Entries[^1].Detail.Contains("No status reason"))throw new Exception("Never invent missing reason");
  h.Observe(now,"Storage",false,"");if(!h.Entries[^1].Detail.Contains("without a new reason"))throw new Exception("Detect silent end");
  for(int i=0;i<250;i++)h.Observe(now,"Travel",true,"step "+i);
  if(h.Entries.Length!=200||h.Dropped==0)throw new Exception("History must stay bounded");
  Console.WriteLine("500 routine reason retention, unknown causes, deduplication and bounds passed.");
 }
}
