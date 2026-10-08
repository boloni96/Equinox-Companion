using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper102Tests
{
 [ModuleInitializer] public static void Run(){
 void Check(bool ok,string label){if(!ok)throw new Exception("Helper102: "+label);}
 var npc=new HelperNpc("conversation",1026850,"Cid",130,1,1,new(0,0,0),new(0,0,0),0);
 var interact=new HelperAction("i","Leader",1,"interact",1,npc,Scene:"68694:13",QuestId:68694);
 var confirm=interact with {Id="c",Text="confirmQuestScene"};
 var talk=interact with {Id="t",Kind="talk",Text="Earlier line",Addon="Talk"};
 var skip=interact with {Id="s",Kind="skip",Text="Skip cutscene?",Addon="SelectString"};
 var batch=new[]{interact,confirm,talk,skip};
 Check(HelperCutsceneReplayPolicy.Playback(batch,true).Select(x=>x.Id).SequenceEqual(new[]{"i","c","s"}),"Cid skip bypasses stale talk, preserves interaction and scene");
 Check(HelperCutsceneReplayPolicy.Playback(batch,false).Length==4,"permission required");
 Check(HelperCutsceneReplayPolicy.Playback([talk,skip with {Scene="68694:14"}],true).Length==2,"different scene not bypassed");
 Check(HelperCutsceneReplayPolicy.Playback([talk,skip with {Npc=npc with {Conversation="other"}}],true).Length==2,"different conversation not bypassed");
 Check(HelperCutsceneReplayPolicy.Playback([talk,talk with {Kind="choice"},skip],true).Length==3,"choices not bypassed");
 Check(HelperCutsceneReplayPolicy.Playback([talk,talk with {Id="t2"},skip],true).Length==1,"consecutive same-scene talk superseded");
 Check(HelperCutsceneReplayPolicy.Playback([talk,skip,talk with {Id="post",Scene="68694:14"}],true).Length==2,"post-cutscene dialogue retained");
 Check(HelperCutsceneReplayPolicy.QuietMilliseconds([interact,confirm])==15000,"destination waits through observed delayed scene");
 Check(HelperCutsceneReplayPolicy.QuietMilliseconds(batch)==2000,"finished dialogue normal debounce");
 Check(HelperCutsceneReplayPolicy.QuietMilliseconds([interact,interact with {Kind="completeQuest"}])==15000,"allow delayed quest completion Talk");
 var later=talk with {Id="later",Text="Actual line",Signature="actual"};
 Check(HelperCutsceneReplayPolicy.MatchingLaterTalk([talk,later],"68694:13","Actual line","actual")==1,"already advanced matches exact later line");
 Check(HelperCutsceneReplayPolicy.MatchingLaterTalk([talk,talk with {Kind="choice"},later],"68694:13","Actual line","actual")==-1,"never skip choices to align Talk");
 Check(HelperCutsceneReplayPolicy.MatchingLaterTalk([talk,later,later],"68694:13","Actual line","actual")==-1,"ambiguous repeated line rejected");
 Check(HelperCutsceneReplayPolicy.MatchingLaterTalk([talk,later],"68694:14","Actual line","actual")==-1,"other scene rejected");
 Check(!HelperCutsceneReplayPolicy.BlockingAddons.Contains("Talk"),"cutscene text permits Escape");
 Check(HelperCutsceneReplayPolicy.BlockingAddons.Contains("SelectYesno")&&HelperCutsceneReplayPolicy.BlockingAddons.Contains("CutSceneSelectString"),"unrelated confirmations and choices block Escape");
 }
}
