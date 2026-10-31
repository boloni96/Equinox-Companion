using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class ServiceAccount337Tests
{
 [ModuleInitializer] internal static void Run()
 {
  void Check(bool ok,string why){if(!ok)throw new Exception(why);}
  var key=new string('a',64);var other=new string('b',64);
  var slots=new Dictionary<string,int>{{key,1}};
  Check(CurrentLoginIdentification.RecordedIndex(key,slots)==1,"Recorded account two is selected automatically");
  Check(CurrentLoginIdentification.RecordedIndex(other,slots)==-1,"No default to account one");
  Check(CurrentLoginIdentification.RecordedIndex("",slots)==-1,"Missing fingerprint is not an account");
  slots[key]=0;Check(CurrentLoginIdentification.RecordedIndex(key,slots)==0,"Recorded account one supported");
  slots[key]=16;Check(CurrentLoginIdentification.RecordedIndex(key,slots)==-1,"Out-of-range mapping rejected");
  var probe=new CurrentLoginIdentification();probe.Selected(1);
  Check(probe.ServiceAccountSelected&&probe.MatchesSelectedAccount(1),"Selected account accepted");
  Check(!probe.MatchesSelectedAccount(0),"Other account cannot proceed");
  var threw=false;try{probe.Selected(1);}catch(InvalidOperationException){threw=true;}
  Check(threw,"Selection cannot be dispatched twice");
  var now=DateTimeOffset.UtcNow;
  Check(!probe.Observe(1,["123"],false,now),"Loading list cannot verify");
  Check(!probe.Observe(1,["123"],true,now),"First sample cannot verify");
  Check(probe.Observe(1,["123"],true,now.AddMilliseconds(250)),"Stable ready list verifies");
  Check(!probe.Observe(1,[],true,now.AddMilliseconds(500)),"Empty world is not verification");
  var target=new LoginProbeTarget("target","sir","main","123","Leonis Verelle","Rafflesia");
  Check(CharacterLoginProbe.DiscoveryContains(target,[123],[]),"Exact CID discovered on ready data centre list");
  Check(!CharacterLoginProbe.DiscoveryContains(target,[456],[]),"Other account list cannot match target");
  Check(!CharacterLoginProbe.DiscoveryContains(target,[],[new("123","Leonis Verelle","Rafflesia",true)]),"Locked character cannot match");
  Check(!CharacterLoginProbe.DiscoveryContains(target,[],[new("456","Leonis Verelle","Rafflesia",false)]),"Same name cannot replace known CID");
  var namelessId=target with{ContentId=""};
  Check(CharacterLoginProbe.DiscoveryContains(namelessId,[],[new("123","Leonis Verelle","Rafflesia",false)]),"Unrecorded CID requires exact name and home world");
  Check(!CharacterLoginProbe.DiscoveryContains(namelessId,[],[new("123","Leonis Verelle","Sophia",false)]),"Different home world cannot match");
  var discovery=new CharacterLoginProbe();Check(discovery.Begin(target,key,now),"Discovery begins from explicit login request");
  discovery.AllowAccountDiscovery(now);Check(discovery.Deadline==now.AddMinutes(3),"Service-account discovery has bounded deadline");
  Check(discovery.CheckState(now.AddMinutes(3),key,target,false)==LoginProbeResult.TimedOut,"Discovery expires");
  Console.WriteLine("PASS automatic recorded service-account selection");
 }
}
