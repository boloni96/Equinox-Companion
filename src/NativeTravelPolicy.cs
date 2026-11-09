namespace EquinoxCompanion;
public enum NativeTravelStage { Gateway, AetheryteMenu, WorldList, WorldConfirm, WorldQueue, Logout, CharacterMenu, VisitIntro, Destination, TransferConfirm, TransferWait, Login }
public static class NativeTravelPolicy
{
    public static bool AdoptAetheryteMenu(NativeTravelStage stage,bool aetheryte,uint id,float distance,int matchingChoices)=>
        stage==NativeTravelStage.Gateway&&aetheryte&&id is 2 or 8 or 9&&float.IsFinite(distance)&&distance is >=0 and <=20&&matchingChoices==1;
    public static bool HomeReturnSucceeded(NativeTravelStage stage,bool returning,bool committed,uint leg,uint home,string prompt)=>
        stage==NativeTravelStage.TransferWait&&returning&&committed&&home!=0&&leg==home&&
        System.Text.RegularExpressions.Regex.Replace(prompt,@"\s+"," ").Trim()=="Successfully returned to Home World.";
    public static bool HomeReturnRejected(NativeTravelStage stage,bool returning,string prompt)=>
        returning && stage is NativeTravelStage.VisitIntro or NativeTravelStage.Destination or NativeTravelStage.TransferConfirm or NativeTravelStage.TransferWait &&
        System.Text.RegularExpressions.Regex.Replace(prompt,@"\s+"," ").Trim()=="Unable to return to Home World. [1000]";
    public static string Progress(NativeTravelStage stage)=>stage switch {
        NativeTravelStage.Gateway=>"1/5",NativeTravelStage.AetheryteMenu=>"2/5",NativeTravelStage.WorldList=>"3/5",NativeTravelStage.WorldConfirm=>"4/5",NativeTravelStage.WorldQueue=>"5/5",
        NativeTravelStage.Logout=>"1/7",NativeTravelStage.CharacterMenu=>"2/7",NativeTravelStage.VisitIntro=>"3/7",NativeTravelStage.Destination=>"4/7",NativeTravelStage.TransferConfirm=>"5/7",NativeTravelStage.TransferWait=>"6/7",_=>"7/7"};
    // Home region determines eligibility even while visiting Oceania.
    // Current official rules: JP/NA/EU may visit Oceania, not each other.
    public static bool RegionReachable(uint homeRegion,uint destinationRegion)=>
        homeRegion is >=1 and <=4 && destinationRegion is >=1 and <=4 &&
        (homeRegion==destinationRegion || destinationRegion==4 && homeRegion is >=1 and <=3);
    public static bool SameDataCenter(uint sourceDc,uint destinationDc)=>sourceDc!=0&&sourceDc==destinationDc;
    public static bool ReturnFirst(uint current,uint home,uint currentDc,uint destinationDc)=>current!=home&&currentDc!=destinationDc;
    public static bool ContinueDcFromHome(bool returning,uint leg,uint home,uint destination,uint homeDc,uint destinationDc)=>returning&&leg==home&&destination!=home&&homeDc!=0&&destinationDc!=0&&homeDc!=destinationDc;
    public static bool Arrival(ulong expected,ulong actual,uint destination,uint world,bool loading)=>expected!=0&&expected==actual&&destination==world&&!loading;
    public static bool TransferPrompt(string prompt,string character,string destination,string dc)=>
        character.Length>0&&destination.Length>0&&dc.Length>0&&prompt.Contains(character,StringComparison.Ordinal)&&prompt.Contains(destination,StringComparison.Ordinal)&&prompt.Contains(dc,StringComparison.Ordinal);
    public static bool PartyBlocksTravel(int members,bool hasPartyId,bool crossWorld,bool changingDc)=>
        crossWorld?changingDc:members>1||hasPartyId;
    public static bool LeavePartyPrompt(string prompt)=>prompt.Trim() is "Leave party?" or "Leave the party?";
    public static bool WorldPrompt(string prompt,string destination)
    {
        if(destination.Length==0)return false;
        var text=System.Text.RegularExpressions.Regex.Replace(prompt,@"\s+"," ").Trim();
        var question="Travel to "+destination+"?";
        return text==question||text==question+" Please be advised you must be playing solo or in a cross-world party for your transfer to be processed.";
    }
}
