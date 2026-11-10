namespace EquinoxCompanion;
public static class WorldDestinationPolicy
{
    public static bool Available(uint dc,uint region,string name)=>dc!=0&&region is >=1 and <=4&&!string.IsNullOrWhiteSpace(name);
}
