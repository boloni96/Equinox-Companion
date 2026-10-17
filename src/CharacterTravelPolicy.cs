using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public static class CharacterTravelPolicy
{
 public static bool MatchesEstate(Address? estate,uint world,string district,int ward,int plot)
 {
  static string District(string? name)=>Regex.Replace((name??"").Trim(),"^the\\s+","",RegexOptions.IgnoreCase);
  return estate is not null&&!estate.Apartment&&!estate.Workshop&&estate.Room==0&&estate.WorldId==world&&estate.Ward==ward&&estate.Plot==plot&&world>0&&ward is >=1 and <=30&&plot is >=1 and <=60&&string.Equals(District(estate.DistrictName),District(district),StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(estate.DistrictName);
 }
 public static bool NeedsConfirmation(string currentPerson,string currentAccount,string targetPerson,string targetAccount)=>string.IsNullOrEmpty(currentPerson)||string.IsNullOrEmpty(currentAccount)||string.IsNullOrEmpty(targetPerson)||string.IsNullOrEmpty(targetAccount)||currentPerson!=targetPerson||currentAccount!=targetAccount;
 public static bool ValidTarget(string name,string world)=>Regex.IsMatch(name??"","^[A-Za-z][A-Za-z'-]{1,14} [A-Za-z][A-Za-z'-]{1,14}$")&&Regex.IsMatch(world??"","^[A-Za-z]{2,30}$");
 public static bool IsCurrent(SharedCharacter target,Actor current)=>!string.IsNullOrEmpty(target.ContentId)?target.ContentId==current.ContentId:string.Equals(target.Name,current.Name,StringComparison.OrdinalIgnoreCase)&&string.Equals(target.World,current.HomeWorldName,StringComparison.OrdinalIgnoreCase);
}
