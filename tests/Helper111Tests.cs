using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Helper111Tests
{
 [ModuleInitializer] public static void Run(){
    void Check(bool ok,string label){if(!ok)throw new Exception("Helper111: "+label);}
    Check(HelperConversationPolicy.PlaybackDelay("talk",0,60000)==150,"leader reading pause not replayed");
    Check(HelperConversationPolicy.PlaybackDelay("talk",0,10)==150,"dialogue pacing bounded");
    foreach(var kind in new[]{"choice","acceptQuest","completeQuest","soloDuty"})
        Check(HelperConversationPolicy.PlaybackDelay(kind,0,3000)==3000,"confirmation pacing preserved");
    Check(HelperConversationPolicy.PlaybackDelay("interact",0,60000)==0,"interaction starts immediately");
    Check(HelperConversationPolicy.PlaybackDelay("skip",0,60000)==0,"skip starts immediately");
    Check(HelperConversationPolicy.TalkAppearanceTimeoutSeconds("")==20,"wait through scene animation");
    Check(HelperConversationPolicy.TalkAppearanceTimeoutSeconds("Different dialogue")==5,"different text remains guarded");
 }
}
