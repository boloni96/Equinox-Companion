using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public static class FriendEstateTravelPolicy
{
 // Linked houses may arrive before the optional FC identifiers. The native menu still verifies the exact address.
 public static bool CanInspectFc(bool? member,string characterFc,string houseFc)=>member!=false&&(characterFc.Length==0||houseFc.Length==0||characterFc==houseFc);
 public static bool MatchesFriend(ulong candidateId,string candidateName,uint world,ulong friendId,string friendName,uint homeWorld)=>friendId!=0&&(candidateId!=0?candidateId==friendId:world==homeWorld&&string.Equals(candidateName.Trim(),friendName.Trim(),StringComparison.OrdinalIgnoreCase));
 public static bool MatchesRow(IEnumerable<string> values,string district,int ward,int plot)
 {
  if(ward is <1 or >30||plot is <1 or >60||string.IsNullOrWhiteSpace(district))return false;
  var text=string.Join(" ",values);if(text.Length>4000)return false;
  var name=Regex.Replace(district.Trim(),@"^the\s+","",RegexOptions.IgnoreCase);
  if(!Regex.IsMatch(text,@"\b(?:The\s+)?"+Regex.Escape(name)+@"\b",RegexOptions.IgnoreCase))return false;
  static int[] Numbers(string text,string label)=>Regex.Matches(text,@"\b"+label+@"\s*(\d{1,2})\b|\b(\d{1,2})(?:st|nd|rd|th)?\s+"+label+@"\b",RegexOptions.IgnoreCase).Select(m=>int.Parse(m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value)).Distinct().ToArray();
  var wards=Numbers(text,"Ward");var plots=Numbers(text,"Plot");return wards.Length==1&&wards[0]==ward&&plots.Length==1&&plots[0]==plot;
 }
 public static bool SameSession(ulong actor,uint world,ulong currentActor,uint currentWorld,bool loaded,bool blocked)=>loaded&&!blocked&&actor!=0&&actor==currentActor&&world==currentWorld;
}
