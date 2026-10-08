namespace EquinoxCompanion;
// TextAdvance's public IPC contract; all unrelated automation is explicitly disabled.
public sealed class HelperTextAdvanceOptions
{
    public bool? EnableQuestAccept=false,EnableQuestComplete=false,EnableRewardPick=false,EnableRequestHandin=false;
    public bool? EnableCutsceneEsc,EnableCutsceneSkipConfirm;
    public bool? EnableTalkSkip=false,EnableRequestFill=false,EnableAutoInteract=false;
    public HelperTextAdvanceOptions(bool confirm){EnableCutsceneEsc=!confirm;EnableCutsceneSkipConfirm=confirm;}
}
public static class HelperTextAdvancePolicy
{
    public static bool Keep(bool permitted,bool active,bool cutscene,string expected,string actual,DateTimeOffset now,DateTimeOffset until)=>
        permitted&&active&&cutscene&&expected.Length>0&&expected==actual&&now<until;
}
