using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class RetainerCapture291Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS retainer capture "+label);}
  var now=DateTimeOffset.Parse("2026-10-29T02:00:00Z");
  var slots=Enumerable.Range(0,175).Select(i=>new ChestStack((uint)(10000+i/25),i%25,0,0,false,0)).Concat(Enumerable.Range(0,18).Select(i=>new ChestStack(12001,i,(uint)(i+2),0,false,9999))).ToArray();
  slots[0]=new(10000,0,100,4,false,99);
  var gate=new RetainerCaptureStability();
  Check("first read waits",!gate.Ready(1,2,slots,1,now));
  Check("subsecond read waits",!gate.Ready(1,2,slots,1,now.AddMilliseconds(750)));
  Check("short visit recorded after one stable second",gate.Ready(1,2,slots,1,now.AddSeconds(1)));
  Check("retainer switch resets even for identical contents",!gate.Ready(1,3,slots,1,now.AddSeconds(2)));
  Check("character switch resets",!gate.Ready(4,3,slots,1,now.AddSeconds(3)));
  slots[0]=slots[0] with{Quantity=5};
  Check("quantity change resets stability",!gate.Ready(4,3,slots,1,now.AddSeconds(4)));
  Check("changed stock stabilizes",gate.Ready(4,3,slots,1,now.AddSeconds(5)));
  Check("incomplete pages rejected",!gate.Ready(4,3,slots[..^1],1,now.AddSeconds(6)));
  Check("complete read starts fresh after incomplete one",!gate.Ready(4,3,slots,1,now.AddSeconds(7)));
  Check("item count mismatch rejected",!gate.Ready(4,3,slots,2,now.AddSeconds(8)));
  gate.Reset();Check("closing resets",!gate.Ready(4,3,slots,1,now.AddSeconds(10)));
  Check("unknown owner rejected",!gate.Ready(0,3,slots,1,now.AddSeconds(11)));
 }
}
