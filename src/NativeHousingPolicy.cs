using System.Numerics;
using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public sealed record NativeHousingDistrict(uint Territory,uint LandSet,uint Gateway,string Name);
public static class NativeHousingPolicy
{
    public static NativeHousingDistrict? District(string name)=>name.Trim().ToLowerInvariant() switch {
        "mist"=>new(339,0,8,"Mist"),"lavender beds" or "the lavender beds"=>new(340,1,2,"The Lavender Beds"),
        "goblet" or "the goblet"=>new(341,2,9,"The Goblet"),"shirogane"=>new(641,3,111,"Shirogane"),"empyreum"=>new(979,4,70,"Empyreum"),_=>null};
    // A verified teleport can land on the street side without a plot identity.
    // Ordinary walking keeps the tighter visible placard approach; neither needs
    // the three-metre inward segment required by gardening.
    public static bool GoSeeArrival(bool correctWard,bool matchingPlot,Vector3 position,Vector3 placard,bool estateTeleport,bool visible)=>
        correctWard&&Position(position)&&Position(placard)&&(matchingPlot||
        MathF.Abs(position.Y-placard.Y)<=3&&Vector3.Distance(position,placard)<=(estateTeleport?12f:4.5f)&&(estateTeleport||visible));
    public static bool WardChoice(string expected,string actual){
        static string Clean(string text)=>Regex.Replace(text,@"\s*\([^)]*\)","").Trim().TrimEnd('.').Trim();
        return !string.IsNullOrWhiteSpace(expected)&&string.Equals(Clean(expected),Clean(actual),StringComparison.OrdinalIgnoreCase)||
            string.Equals(Clean(actual),"Go to specified ward",StringComparison.OrdinalIgnoreCase);
    }
    public static bool Address(int ward,int plot)=>ward is >=1 and <=30 && plot is >=1 and <=60;
    public static bool WardPrompt(string text,int ward,string district)
    {
        // Unknown/localized confirmations are left for manual confirmation.
        if(!text.StartsWith("Travel to ",StringComparison.Ordinal)||!text.EndsWith('?'))return false;
        var match=Regex.Match(text,@"\b(\d+)(?:st|nd|rd|th)?\s+Ward\b",RegexOptions.IgnoreCase);
        return match.Success&&int.TryParse(match.Groups[1].Value,out var number)&&number==ward&&text.Contains(district,StringComparison.OrdinalIgnoreCase);
    }
    public static bool Position(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)&&p.LengthSquared()<100000000;
}
