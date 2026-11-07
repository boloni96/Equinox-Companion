namespace EquinoxCompanion;
public static class AethernetTravelPolicy
{
    // Compare walking legs plus a modest allowance for menu/loading overhead.
    // Distances estimate effort; the chosen walk still requires native geometry.
    public static bool PreferWalking(System.Numerics.Vector3 start,System.Numerics.Vector3 plot,
        System.Numerics.Vector3 source,System.Numerics.Vector3 destination){
        var direct=System.Numerics.Vector3.Distance(start,plot);
        var via=System.Numerics.Vector3.Distance(start,source)+System.Numerics.Vector3.Distance(destination,plot);
        return direct<=via+30;
    }
    public static bool CanUseMenuEntry(bool locked,bool unusable,bool current)=>!locked&&!unusable&&!current;
    public static bool NameMatches(string actual,string requested)=>string.Equals(actual.Trim(),requested.Trim(),StringComparison.OrdinalIgnoreCase)||
        requested.Trim()=="Leatherworkers' Guild"&&actual.Trim()=="Leatherworkers' Guild & Shaded Bower";
}
