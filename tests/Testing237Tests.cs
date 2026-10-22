using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Testing237Tests
{
 [ModuleInitializer] internal static void Run()
 {
  var count=0;void Check(bool value,string label){count++;if(!value)throw new Exception("Testing checklist: "+label);}
  var test=new PlayTest("G04",1,"Gardening","Batch navigation","Click Batch 2.","Now",false);
  var catalog=new TestCatalog(1,"1","Playtests",[test]);
  var a=new TestReport("report-a","G04",1,"person:a","Tester A","pass","Worked","0.5.1.237","7.11.220","7.11.220","plugin","",1,"2026-10-22T20:00:00Z");
  var b=a with {Id="report-b",TesterId="person:b",TesterName="Tester B",Status="fail",Seq=2};
  Check(TestingPolicy.Valid(catalog),"catalog valid");Check(TestingPolicy.Valid(a),"report valid");Check(!TestingPolicy.Valid(a with {Status="done"}),"reject game-style completion status");
  Check(!TestingPolicy.Valid(a with {Notes=new string('x',4001)}),"bounded notes");Check(!TestingPolicy.Valid(a with {TesterId=""}),"tester required");
  var merged=TestingPolicy.Merge([a],[a,b]);Check(merged.Length==2,"idempotent receipt");Check(TestingPolicy.Current(test,merged).Length==2,"separate testers");Check(TestingPolicy.Summary(test,merged)=="fail","failure remains visible alongside pass");
  var newer=a with {Id="report-c",BaseId=a.Id,Status="later",Seq=3};merged=TestingPolicy.Merge(merged,[newer]);Check(merged.Length==3,"history preserved");Check(TestingPolicy.Current(test,merged).Single(r=>r.TesterId==a.TesterId).Status=="later","latest per tester");
  var revised=test with {Revision=2};Check(TestingPolicy.NeedsRetest(revised,merged),"changed test needs retest");Check(!TestingPolicy.Current(revised,merged).Any(),"old pass never proves new revision");
  var local=new TestingLocal(catalog,0,[a],[new(newer)],1,true,"7.11.220");var receipt=TestingPolicy.Acknowledge(local,newer);Check(receipt.Pending!.Length==0,"only acknowledged report removed");Check(receipt.Reports!.Length==2,"ack saves history");Check(local.Pending!.Length==1,"immutable snapshot");
  var page=new TestingPage(1,catalog,0,[],1,false,"7.11.220");Check(ReferenceEquals(local,TestingPolicy.Apply(local,page)),"unchanged poll does not trigger config save");
  var changed=TestingPolicy.Apply(local,page with {Reports=[b],Cursor=2});Check(changed.Pending!.Length==1,"incoming data keeps pending report");Check(changed.Reports!.Length==2,"incoming tester merged");
  Check(!TestingPolicy.Valid(catalog with {Tests=[test,test]}),"duplicate test ids rejected");Check(!TestingPolicy.Valid(catalog with {Schema=2}),"unknown protocol rejected");
  Console.WriteLine($"PASS {count} shared testing checklist checks");
 }
}
