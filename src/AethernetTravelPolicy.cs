namespace EquinoxCompanion;
public static class AethernetTravelPolicy
{
    public static bool CanUseMenuEntry(bool locked,bool unusable,bool current)=>!locked&&!unusable&&!current;
    public static bool NameMatches(string actual,string requested)=>string.Equals(actual.Trim(),requested.Trim(),StringComparison.OrdinalIgnoreCase)||
        requested.Trim()=="Leatherworkers' Guild"&&actual.Trim()=="Leatherworkers' Guild & Shaded Bower";
}
