using System.Runtime.CompilerServices;
using System.Text.Json;
using EquinoxCompanion;
internal static class ServiceAccount508Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(bool ok,string why){if(!ok)throw new Exception("Service account 508: "+why);}
  var scope=new string('a',64);var account=new string('b',64);
  var characters=new Dictionary<string,int>{{scope+":123",2},{scope+":456",1}};
  var accounts=new Dictionary<string,int>{{account,0}};
  int Resolve(string id,IEnumerable<string> members)=>CurrentLoginIdentification.RecordedServiceSlot(scope,id,account,members,characters,accounts);
  Check(Resolve("123",["123","456"])==2,"exact character overrides conflicting account mapping and sibling records");
  // Recreate both persisted dictionaries as on plugin reload; no session state required.
  characters=JsonSerializer.Deserialize<Dictionary<string,int>>(JsonSerializer.Serialize(characters))!;
  accounts=JsonSerializer.Deserialize<Dictionary<string,int>>(JsonSerializer.Serialize(accounts))!;
  Check(Resolve("123",["123","456"])==2,"saved character survives serialization and a fresh resolver");
  Check(Resolve("789",["123","456"])==0,"unseen character uses recorded account fingerprint");
  accounts.Clear();Check(Resolve("789",["123"])==2,"unseen character uses consistent same-account member evidence");
  Check(Resolve("789",["123","456"])==-1,"conflicting member evidence does not guess");
  Check(Resolve("789",[])==-1,"no saved evidence requests discovery instead of MAIN ALT assumptions");
  Check(CurrentLoginIdentification.RecordedServiceSlot(new string('c',64),"123",account,["123"],characters,accounts)==-1,"different pairing does not reuse character records");
  characters[scope+":123"]=16;Check(Resolve("123",["123"])==-1,"invalid recorded slot rejected");
  var loginA=new string('d',64);var loginA2=new string('e',64);var loginB=new string('f',64);
  var groups=new List<SquareEnixLoginGroup>();var firstClient=new SquareEnixLoginState();
  firstClient.Observe(scope,loginA,groups);firstClient.Observe(scope,loginA2,groups);
  groups=JsonSerializer.Deserialize<List<SquareEnixLoginGroup>>(JsonSerializer.Serialize(groups))!;
  var restarted=new SquareEnixLoginState();
  Check(!restarted.Recognizes(scope,loginA2,groups),"saved email/login group does not identify an unknown new client");
  restarted.Observe(scope,loginA,groups);
  Check(restarted.Recognizes(scope,loginA2,groups),"saved same-login accounts restored after current client identity verified");
  var otherClient=new SquareEnixLoginState();otherClient.Observe(scope,loginB,groups);
  Check(!otherClient.Recognizes(scope,loginA2,groups),"different email/login stays separate even with identical service slot numbers");
  Console.WriteLine("508 service-account persistence and selection precedence checks passed.");
 }
}
