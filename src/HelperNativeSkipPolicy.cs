namespace EquinoxCompanion;
public static class HelperNativeSkipPolicy
{
    // FFXIV InputId.ESC = 3. Never synthesize general Confirm, Cancel or movement.
    public static bool Escape(bool permitted,bool active,bool cutscene,string expected,string actual,int id,DateTimeOffset now,DateTimeOffset until)=>
        id==3&&permitted&&active&&cutscene&&expected.Length>0&&expected==actual&&now<until;
}
