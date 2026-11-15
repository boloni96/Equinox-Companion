namespace EquinoxCompanion;
public static class CharacterLoginWorldPolicy
{
    public static bool DisplayedListReady(uint world,uint dc,bool locked,bool menu)=>world!=0&&dc!=0&&!locked&&menu;
    // Regional World Visit retains the home-world list; cross-region lobby
    // transfers retain the visiting-world list. Unknown regions do not broaden it.
    public static uint ListDc(uint homeDc,uint currentDc,uint homeRegion=0,uint currentRegion=0)=>
        homeDc!=0&&currentDc!=0&&homeDc!=currentDc&&
        !(homeRegion is >=1 and <=4&&homeRegion==currentRegion)?currentDc:homeDc;
    public static uint ListWorld(uint home,uint current,uint homeDc,uint currentDc,uint homeRegion=0,uint currentRegion=0)=>
        current!=0&&ListDc(homeDc,currentDc,homeRegion,currentRegion)!=homeDc?current:home;
    public static bool MatchesDisplayedDc(uint displayedDc,uint homeDc,uint currentDc,uint homeRegion,uint currentRegion)=>
        displayedDc!=0&&homeDc!=0&&currentDc!=0&&displayedDc==ListDc(homeDc,currentDc,homeRegion,currentRegion);
    public static string Resolve(string home,string current,uint homeDc,uint currentDc,uint homeRegion=0,uint currentRegion=0)=>
        ListDc(homeDc,currentDc,homeRegion,currentRegion)!=homeDc&&!string.IsNullOrWhiteSpace(current)?current:home;
}
