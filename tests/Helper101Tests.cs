using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper101Tests
{
 [ModuleInitializer] public static void Run(){
 void Check(bool ok,string label){if(!ok)throw new Exception("Helper101: "+label);}
 Check(HelperCutscenePolicy.Menu("Skip cutscene?",["Yes","No"]),"skip prompt");
 Check(!HelperCutscenePolicy.Menu("Buy this item?",["Yes","No"]),"purchase rejected");
 Check(!HelperCutscenePolicy.Menu("Skip cutscene?",["No","Yes"]),"order guarded");
 Check(!HelperCutscenePolicy.Menu("Skip cutscene?",["Yes"]),"incomplete menu");
 const string first="Greetings, adventurer. I'm Kipih Jakkya, reporter for The Raven, Gridania's leading tabloid. I'm hoping that I can rely on your kind assistance.";
 const string returning="Greetings, adventurer! It's been a while. ...Hm? Do not tell me you've forgotten about Kipih Jakkya, reporter for The Raven?";
 Check(HelperQuestTextPolicy.Canonical("68694:1","Kipih Jakkya",first)==HelperQuestTextPolicy.Canonical("68694:1","Kipih Jakkya",returning),"known greeting variants");
 Check(HelperQuestTextPolicy.Canonical("68694:2","Kipih Jakkya",first)==first,"different scene unchanged");
 Check(HelperQuestTextPolicy.Canonical("68694:1","Other",first)==first,"different speaker unchanged");
 Check(HelperQuestTextPolicy.Canonical("68694:1","Kipih Jakkya","Unknown")=="Unknown","unknown dialogue unchanged");
 Check(!new FollowThemSettings().VerifiedCutsceneSkip,"default off");
 }
}
