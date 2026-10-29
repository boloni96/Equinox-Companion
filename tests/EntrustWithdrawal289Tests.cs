using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class EntrustWithdrawal289Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string n,bool ok){if(!ok)throw new Exception(n);Console.WriteLine("PASS imported withdrawal289 "+n);}
  var keep=new EntrustSelection(new(){{100,7}},new(){{9,20}},[200]);
  var entries=new[]{new RetrievalEntry(100,"Ingot",30),new RetrievalEntry(200,"Cloth",5)};
  var p=EntrustWithdrawal.Build(entries);
  Check("explicit quantities, no craft expansion",p.Materials.Length==2&&p.Materials[0].Quantity==30&&p.Steps.Length==0);
  Check("selection only",EntrustWithdrawal.Build(entries.Where(x=>x.ItemId==200)).Materials.Single().Quantity==5);
  var amounts=keep.Amounts(entries.Select(x=>x.ItemId),[new(0,0,100,40,false,99),new(0,1,200,15,false,99)],RetrievalQuality.Any,_=>9,[]);
  Check("Entrust still respects keep and protection",amounts.Count==1&&amounts[100]==33&&keep.Keep[100]==7);
  Check("withdrawal edits do not change keep",EntrustWithdrawal.Build(entries.Select(x=>x with{Quantity=2})).Materials.All(x=>x.Quantity==2)&&keep.Keep[100]==7);
 }
}
