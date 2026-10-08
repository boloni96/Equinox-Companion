using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper106Tests
{
    [ModuleInitializer] public static void Run(){
        void Check(bool ok,string label){if(!ok)throw new Exception("Helper106: "+label);}
        var npc=new HelperNpc("recorded",1026870,"Noctis",132,1,1,new(11,0,4),new(12,0,4),0);
        var now=DateTimeOffset.UnixEpoch.AddMinutes(1);
        var a=new HelperAction("a","Leader",1,"interact",0,npc,Text:"EventNpc",Scene:"68695:24",QuestId:68695);
        var confirm=a with {Id="b",Text="confirmQuestScene"};
        var talk=a with {Id="c",Kind="talk",Text="Oh, hey. What's up?",Signature="exact"};
        HelperAction[] steps=[a,confirm,talk];
        bool Match(HelperAction[] input,HelperNpc? observed,string scene="68695:24",string line="Oh, hey. What's up?",string sig="exact",DateTimeOffset? clicked=null,long started=0)=>
            HelperOpenConversationPolicy.Matches(input,observed,"EventNpc",clicked??now.AddSeconds(-2),now,started,scene,line,sig);
        Check(Match(steps,npc with {Conversation=""}),"same local NPC, scene and first line resumes");
        Check(!Match(steps,null),"no local click provenance rejected");
        Check(!Match(steps,npc with {BaseId=1026871}),"different Noctis rejected");
        Check(!Match(steps,npc with {World=2}),"different world rejected");
        Check(!Match(steps,npc with {Map=2}),"different map rejected");
        Check(!Match(steps,npc with {Position=new(30,0,4)}),"different position rejected");
        Check(!Match(steps,npc,scene:"68695:20"),"different quest scene rejected");
        Check(!Match(steps,npc,line:"Another line"),"different dialogue rejected");
        Check(!Match(steps,npc,sig:"other"),"different speaker/signature rejected");
        Check(!Match([a,confirm,talk with {Kind="choice"}],npc),"never crosses choice or skip");
        Check(!Match(steps,npc,clicked:now.AddSeconds(-61)),"old click rejected");
        Check(!Match(steps,npc,started:now.ToUnixTimeMilliseconds()),"previous session rejected");
    }
}
