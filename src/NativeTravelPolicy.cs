namespace EquinoxCompanion;
public enum NativeTravelStage { Gateway, AetheryteMenu, WorldList, WorldConfirm, WorldQueue, Logout, CharacterMenu, VisitIntro, Destination, TransferConfirm, TransferWait, Login }
public static class NativeTravelPolicy
{
    public static string Progress(NativeTravelStage stage)=>stage switch {
        NativeTravelStage.Gateway=>"1/5",NativeTravelStage.AetheryteMenu=>"2/5",NativeTravelStage.WorldList=>"3/5",NativeTravelStage.WorldConfirm=>"4/5",NativeTravelStage.WorldQueue=>"5/5",
        NativeTravelStage.Logout=>"1/7",NativeTravelStage.CharacterMenu=>"2/7",NativeTravelStage.VisitIntro=>"3/7",NativeTravelStage.Destination=>"4/7",NativeTravelStage.TransferConfirm=>"5/7",NativeTravelStage.TransferWait=>"6/7",_=>"7/7"};
    public static bool ReturnFirst(uint current,uint home,uint currentDc,uint destinationDc)=>current!=home&&currentDc!=destinationDc;
    public static bool Arrival(ulong expected,ulong actual,uint destination,uint world,bool loading)=>expected!=0&&expected==actual&&destination==world&&!loading;
    public static bool TransferPrompt(string prompt,string character,string destination,string dc)=>
        character.Length>0&&destination.Length>0&&dc.Length>0&&prompt.Contains(character,StringComparison.Ordinal)&&prompt.Contains(destination,StringComparison.Ordinal)&&prompt.Contains(dc,StringComparison.Ordinal);
    public static bool WorldPrompt(string prompt,string destination)=>destination.Length>0&&prompt.Trim()=="Travel to "+destination+"?";
}
