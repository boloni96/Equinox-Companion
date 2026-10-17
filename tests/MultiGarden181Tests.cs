using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class MultiGarden181Tests
{
 [ModuleInitializer]internal static void Run(){
  foreach(var kind in new[]{"care","harvest","plant","inspect","inspecthouse","empty","garden"}){
   var task=new AdventureTask("mg:home:"+kind,"Garden","Character",false,false,Rule:new AdventureTaskRule(kind,"home",2,3,"character"));
   if(!AdventurePolicy.IsGardenTask(task)||AdventurePolicy.TaskHouseId(task)!="home")throw new Exception("Garden destination lost: "+kind);
   if(AdventurePolicy.IsHouseVisit(task)||AdventurePolicy.IsFashion(task)||AdventurePolicy.CanEdit(task))throw new Exception("Garden task incorrectly treated as manual/visit/fashion");
  }
  var fashion=new AdventureTask("fashion","Fashion Report","Character",false,true,Rule:new AdventureTaskRule("fashion",Cid:"character"));
  if(!AdventurePolicy.IsFashion(fashion)||AdventurePolicy.IsGardenTask(fashion))throw new Exception("Fashion travel classification");
  Console.WriteLine("PASS 181: garden kinds retain exact house, batch and character rules; fashion separated");
 }
}
