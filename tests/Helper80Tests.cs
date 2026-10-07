using EquinoxCompanion;
using System.Numerics;
using System.Runtime.CompilerServices;
internal static class Helper80Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string n,bool ok){if(!ok)throw new Exception(n);Console.WriteLine("PASS 80 "+n);}
  var p=new HelperPermission();Test("disabled by default",!p.Active&&!p.Allows("talk"));
  p.Start(true,false);Test("session quest permitted",p.Allows("talk"));Test("skip separate opt in",!p.Allows("skip"));
  p.PauseLocal(true);p.Control("resume");Test("leader cannot override follower pause",p.Paused&&!p.Allows("talk"));
  p.Control("pause");p.PauseLocal(false);Test("local resume cannot override leader pause",p.Paused);p.Control("resume");Test("both pauses released",p.Allows("choice"));
  p.Stop();p.Control("resume");Test("leader cannot restart stopped follower",!p.Active);
  p.Start(true,true);p.Control("stop");p.Control("resume");Test("leader stop revokes until local start",!p.Active&&!p.Skip);
  p.Start(false,true);Test("skip requires quest permission",!p.Skip);p.Start(true,true);Test("fresh session skip",p.Allows("skip"));
  Test("text match reordered",HelperPolicy.Match(["Two","One"],"One")==1);Test("duplicate response blocked",HelperPolicy.Match(["One","One"],"One")==-1);Test("missing response blocked",HelperPolicy.Match(["Two"],"One")==-1);
  Test("right side north",Vector3.Distance(HelperPolicy.Right(Vector3.Zero,0),new(.9f,0,0))<.001f);Test("right side east",Vector3.Distance(HelperPolicy.Right(Vector3.Zero,MathF.PI/2),new(0,0,-.9f))<.001f);
  var npc=new HelperNpc("a",1,"NPC",1,1,1,new(0,0,0),new(1,0,0),0);var a=new HelperAction("id","Leader",1,"talk",1000,npc);
  Test("current action fresh",HelperPolicy.Fresh(a,2000,1000));Test("past session rejected",!HelperPolicy.Fresh(a,2000,1001));Test("expired action rejected",!HelperPolicy.Fresh(a,121000,1000));
  var f=new HelperFollower("id","Follower",1,"Following",true,false,false,"resume",1000);Test("active audience",HelperPolicy.Audience(f,2000));Test("paused excluded",!HelperPolicy.Audience(f with {Paused=true},2000));Test("leader pause excluded",!HelperPolicy.Audience(f with {Control="pause"},2000));Test("stale status excluded",!HelperPolicy.Audience(f,16000));Test("skip audience opt in",!HelperPolicy.Audience(f,2000,true));
  Test("scene must match",!HelperPolicy.SceneMatches("1:2","1:3"));Test("unknown scene blocked",!HelperPolicy.SceneMatches("",""));
 }
}
