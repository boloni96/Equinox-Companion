using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper112Tests
{
 [ModuleInitializer] public static void Run(){
    void Check(bool ok,string label){if(!ok)throw new Exception("Helper112: "+label);}
    var npc=new HelperNpc("conversation",1026850,"Cid",141,1,409,new(0,0,0),new(0,0,0),0);
    var first=new HelperAction("i","Leader",409,"interact",0,npc,Scene:"68694:13",QuestId:68694);
    var skip=first with {Id="s",Kind="skip",Addon="SelectString",Text="Skip cutscene?",SentAt=100};
    var talk=first with {Id="t",Kind="talk",Text="So, how fared you?",SentAt=152};
    Check(HelperCutsceneReplayPolicy.Playback([first,skip,talk],true).SequenceEqual(new[]{first,skip}),"Cid stale Talk after YES");
    Check(HelperCutsceneReplayPolicy.Playback([first,skip,talk,talk with {Id="t2"}],true).Length==2,"contiguous stale Talk");
    Check(HelperCutsceneReplayPolicy.Playback([first,skip,talk],false).Length==3,"no permission no omission");
    Check(HelperCutsceneReplayPolicy.Playback([first,skip,talk with {Scene="68694:14"}],true).Length==3,"next scene retained");
    Check(HelperCutsceneReplayPolicy.Playback([first,skip,talk with {QuestId=68695}],true).Length==3,"other quest retained");
    Check(HelperCutsceneReplayPolicy.Playback([first,skip,talk with {Npc=npc with {Conversation="other"}}],true).Length==3,"other NPC retained");
    Check(HelperCutsceneReplayPolicy.Playback([first,skip,first with {Kind="choice"},talk],true).Length==4,"choice boundary retained");
    Check(HelperCutsceneReplayPolicy.Playback([first,skip with {Text="Buy?"},talk],true).Length==3,"arbitrary confirmation retained");
 }
}
