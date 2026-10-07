using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Helper86Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string n,bool ok){if(!ok)throw new Exception(n);Console.WriteLine("PASS 86 "+n);}
  Test("cancelled recording cannot emit orphan lines after resume",!HelperConversationPolicy.MayRecord(false,true));
  Test("paused recording cannot emit",!HelperConversationPolicy.MayRecord(true,false));
  Test("fresh recording after resume can emit",HelperConversationPolicy.MayRecord(true,true));
  Test("next NPC preserves completed setup before quiet debounce",HelperConversationPolicy.CommitAtNextNpc(true,false,false,false));
  Test("pending acceptance cannot be committed early",!HelperConversationPolicy.CommitAtNextNpc(true,false,true,false));
  Test("visible warning cannot be committed early",!HelperConversationPolicy.CommitAtNextNpc(true,false,false,true));
  Test("failed recording cannot commit",!HelperConversationPolicy.CommitAtNextNpc(true,true,false,false));
  Test("cancelled recording cannot commit",!HelperConversationPolicy.CommitAtNextNpc(false,false,false,false));
  Test("first click cannot be retried immediately",!HelperConversationPolicy.RetryTalk(1,1999));
  Test("unchanged line can get second spaced click",HelperConversationPolicy.RetryTalk(1,2000));
  Test("unchanged line can get third spaced click",HelperConversationPolicy.RetryTalk(2,2000));
  Test("no fourth click",!HelperConversationPolicy.RetryTalk(3,20000));
  Test("wrapped warning normalized",HelperConversationPolicy.NormalizePrompt("If you proceed, the following quest(s) will be\r\n rendered incomplete:") == "If you proceed, the following quest(s) will be rendered incomplete:");
  Test("different prompt remains different",HelperConversationPolicy.NormalizePrompt("Buy this item?")!="Do you wish to replay the event?");
 }
}
