using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Organizer504Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool yes,string why){if(!yes)throw new Exception("Organizer 504: "+why);}
  ChestStack[] stock=[new(0,0,100,520,false,999),new(1,0,100,12,true,999),new(2001,0,2,300,false,9999),new(10000,0,100,999,false,999),new(10000,1,200,99,false,999)];
  var selected=new Dictionary<uint,int>();
  Check(TemporaryInventorySelection.Pick(selected,100,1,stock)&&selected[100]==532,"pick all bag copies, not retainer copies or default one");
  selected[100]=5;
  Check(!TemporaryInventorySelection.Pick(selected,100,1,stock)&&selected[100]==5,"repeated hover preserves edited amount");
  Check(!TemporaryInventorySelection.Pick(selected,200,1,stock),"cannot select retainer-only item as bag stock");
  Check(TemporaryInventorySelection.Pick(selected,2,1,stock)&&selected[2]==300,"crystals select from their bag container");
  Check(TemporaryInventorySelection.Pick(selected,100,-1,[])&&!selected.ContainsKey(100),"remove works even after stock disappeared");
  Check(!TemporaryInventorySelection.Pick(selected,100,-1,stock),"no duplicate removal notification");
  Check(!StorageAssignments.HasSelections([],[]),"empty character assignments must be reported");
  var bag=stock.Where(s=>s.Container<4||s.Container==2001).ToArray();
  var inventory=new Dictionary<ulong,ChestStack[]>{{0,bag},{1,[new(10000,0,0,0,false,999),new(12001,0,2,0,false,9999)]}};
  StorageRoute[] rules=[new(0,StorageAutoRouting.AllRetainers,[],[100],Amounts:new Dictionary<uint,int>{{100,20}})];
  Check(StorageAssignments.Apply(rules,[],inventory,_=>1).Length==0,"no silent fallback from assignments to arbitrary retainers");
  var routes=StorageAssignments.Apply(rules,[new(1,[1],[])],inventory,_=>1);
  var moves=StorageRouting.Build(new("Temporary",1,routes,0),inventory,_=>1,true,true).Moves;
  Check(moves.Sum(m=>m.Move.Quantity)==20&&moves.All(m=>m.Move.Source.Item==100),"assignment chooses destination without broadening selection");
  Console.WriteLine("504 inventory shortcut quantities, repeated picks, crystals and assignment scope passed.");
 }
}
