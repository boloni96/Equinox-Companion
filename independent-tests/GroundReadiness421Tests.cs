using EquinoxCompanion;
using System.Numerics;
using System.Runtime.CompilerServices;
internal static class GroundReadiness421Tests
{
 [ModuleInitializer] internal static void Run(){
  var gate=new WalkingGroundReadiness();var now=DateTimeOffset.Parse("2026-11-05T22:00:00Z");var point=new Vector3(1,2,3);
  void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
  Check(gate.MayWait(point,641,1,now),"arrival ground may load");
  Check(gate.MayWait(point,641,1,now.AddSeconds(9)),"fresh reads have bounded grace");
  Check(!gate.MayWait(point,641,1,now.AddSeconds(10)),"unsupported ground stops after ten seconds");
  gate.Reset();Check(gate.MayWait(point,641,1,now.AddSeconds(11)),"successful arrival resets wait");
  Check(gate.MayWait(point,339,1,now.AddSeconds(30)),"new territory gets independent wait");
  Check(gate.MayWait(point,339,2,now.AddSeconds(50)),"new character gets independent wait");
 }
}
