using EquinoxCompanion;
using System.Numerics;
using System.Runtime.CompilerServices;
internal static class Helper80Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string n,bool ok){if(!ok)throw new Exception(n);Console.WriteLine("PASS 80 "+n);}
  var p=new HelperPermission();Test("disabled by default",!p.Active&&!p.Allows("talk"));
  p.Start(true,false);Test("session quest permitted",p.Allows("talk"));Test("skip separate opt in",!p.Allows("skip"));
  p.Control("pause");Test("leader pause blocks actions",p.Paused&&!p.Allows("talk"));p.Control("resume");Test("leader resume restores actions",p.Allows("choice"));
  p.Stop();p.Control("resume");Test("leader cannot restart stopped follower",!p.Active);
  p.Start(true,true);p.Control("stop");p.Control("resume");Test("leader stop revokes until local start",!p.Active&&!p.Skip);
  p.Start(false,true);Test("skip requires quest permission",!p.Skip);p.Start(true,true);Test("fresh session skip",p.Allows("skip"));
  Test("text match reordered",HelperPolicy.Match(["Two","One"],"One")==1);Test("duplicate response blocked",HelperPolicy.Match(["One","One"],"One")==-1);Test("missing response blocked",HelperPolicy.Match(["Two"],"One")==-1);
  Test("right side north",Vector3.Distance(HelperPolicy.Right(Vector3.Zero,0),new(.9f,0,0))<.001f);Test("right side east",Vector3.Distance(HelperPolicy.Right(Vector3.Zero,MathF.PI/2),new(0,0,-.9f))<.001f);
  var npc=new HelperNpc("a",1,"NPC",1,1,1,new(0,0,0),new(1,0,0),0);var a=new HelperAction("id","Leader",1,"talk",1000,npc);
  Test("current action fresh",HelperPolicy.Fresh(a,2000,1000));Test("past session rejected",!HelperPolicy.Fresh(a,2000,1001));Test("expired action rejected",!HelperPolicy.Fresh(a,121000,1000));
  var f=new HelperFollower("id","Follower",1,"Following",true,false,false,"resume",1000);Test("active audience",HelperPolicy.Audience(f,2000));Test("paused excluded",!HelperPolicy.Audience(f with {Paused=true},2000));Test("leader pause excluded",!HelperPolicy.Audience(f with {Control="pause"},2000));Test("stale status excluded",!HelperPolicy.Audience(f,16000));Test("skip audience opt in",!HelperPolicy.Audience(f,2000,true));
  p.Start(true,true);p.SetQuestPause(true);Test("quest pause keeps follow session active",p.Active&&!p.Paused&&!p.Allows("talk"));
  p.Control("pause");p.SetQuestPause(false);Test("quest resume cannot undo whole pause",p.Paused&&!p.Allows("talk"));
  p.SetQuestPause(true);p.Control("resume");Test("follow resume preserves quest pause",p.QuestPaused&&!p.Paused&&!p.Allows("acceptQuest"));
  p.SetQuestPause(false);Test("quest resume permits acceptance",p.Allows("acceptQuest"));
  p.Stop();p.SetQuestPause(false);p.Control("resume");Test("quest controls cannot restart stopped session",!p.Active);
  Test("quest paused excluded from audience",!HelperPolicy.Audience(f with {QuestPaused=true},2000));
  p.Start(false,false);Test("FATE sync requires Quest Helper",!p.Allows("fateSync"));p.Start(true,false);Test("FATE sync permitted",p.Allows("fateSync"));p.SetQuestPause(true);Test("quest pause blocks FATE",!p.Allows("fateSync")&&!p.Paused);p.SetQuestPause(false);p.Control("pause");Test("whole pause blocks FATE",!p.Allows("fateSync"));p.Stop();Test("stop blocks FATE",!p.Allows("fateSync"));
  var dialogue=new HelperDialogueCapture();Test("first dialogue observed without advancing",dialogue.Observe("First","one","1")==null);
  Test("repeat refresh does not duplicate",dialogue.Observe("First","one","1")==null);
  Test("next line emits preceding line",dialogue.Observe("Second","two","1")?.Text=="First");
  Test("closing talk before menu flushes last line",dialogue.Finish()?.Text=="Second");Test("finalize after flush cannot duplicate",dialogue.Finish()==null);
  dialogue.Observe("Old","old","1");dialogue.Reset();Test("pause resets dialogue capture",dialogue.Finish()==null);
  Test("leader role retained for stale status",HelperPolicy.HasLeaderRole([f with {Updated=0}]));
  Test("paused followers still reserve leader role",HelperPolicy.HasLeaderRole([f with {Control="pause"}]));
  Test("ended followers release leader role",!HelperPolicy.HasLeaderRole([f with {Control="stop"}]));
  Test("no followers releases leader role",!HelperPolicy.HasLeaderRole([]));
  Test("quest short ID normalized",HelperPolicy.QuestRowId(42)==65578);Test("quest row ID retained",HelperPolicy.QuestRowId(65578)==65578);Test("unknown quest remains unknown",HelperPolicy.QuestRowId(0)==0);
  Test("scene must match",!HelperPolicy.SceneMatches("1:2","1:3"));Test("unknown scene blocked",!HelperPolicy.SceneMatches("",""));
 }
}
