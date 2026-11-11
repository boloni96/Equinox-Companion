using System.Globalization;
using System.Text;
namespace EquinoxCompanion;
public sealed record TeleportName(uint Id,string Name,string Map,string EnglishName,string EnglishMap,uint Cost);
public static class TeleportNameSearch
{
    public static uint CityAlias(string input)=>Normalize(input) switch{"u" or "ul"=>9u,"g" or "gr" or "gri"=>2u,"l" or "li"=>8u,_=>0u};
    public static string Query(string text)=>text.Trim().Trim('*','"','\'',' ').Trim();
    public static string Normalize(string text)
    {
        var result=new StringBuilder();var space=false;
        foreach(var c in text.Normalize(NormalizationForm.FormD)){
            if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;
            if(char.IsLetterOrDigit(c)){if(space&&result.Length>0)result.Append(' ');result.Append(char.ToLowerInvariant(c));space=false;}
            else space=true;
        }
        return result.ToString();
    }
    public static TeleportName[] Matches(IEnumerable<TeleportName> destinations,string input)
    {
        var query=Query(input);var all=destinations.DistinctBy(d=>d.Id).ToArray();
        if(query.StartsWith('#'))return uint.TryParse(query[1..],out var id)?all.Where(d=>d.Id==id).ToArray():[];
        var city=CityAlias(query);if(city!=0)return all.Where(d=>d.Id==city).ToArray();
        query=Normalize(query);if(query.Length==0)return [];
        string[] Names(TeleportName d)=>new[]{d.Name,d.EnglishName,d.Id==62?"Golden Saucer":""}.Where(n=>n.Length>0).Select(Normalize).ToArray();
        var exact=all.Where(d=>Names(d).Contains(query)).ToArray();
        if(exact.Length>0)return exact;
        var matches=all.Where(d=>Names(d).Any(n=>n.Contains(query,StringComparison.Ordinal))||Normalize(d.Map).Contains(query,StringComparison.Ordinal)||Normalize(d.EnglishMap).Contains(query,StringComparison.Ordinal));
        return matches.OrderBy(d=>d.Name,StringComparer.OrdinalIgnoreCase).ThenBy(d=>d.Id).ToArray();
    }
}
