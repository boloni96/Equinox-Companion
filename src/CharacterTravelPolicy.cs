using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public static class CharacterTravelPolicy
{
 public static bool NeedsConfirmation(string currentPerson,string currentAccount,string targetPerson,string targetAccount)=>string.IsNullOrEmpty(currentPerson)||string.IsNullOrEmpty(currentAccount)||string.IsNullOrEmpty(targetPerson)||string.IsNullOrEmpty(targetAccount)||currentPerson!=targetPerson||currentAccount!=targetAccount;
 public static bool ValidTarget(string name,string world)=>Regex.IsMatch(name??"","^[A-Za-z][A-Za-z'-]{1,14} [A-Za-z][A-Za-z'-]{1,14}$")&&Regex.IsMatch(world??"","^[A-Za-z]{2,30}$");
 public static bool IsCurrent(SharedCharacter target,Actor current)=>!string.IsNullOrEmpty(target.ContentId)?target.ContentId==current.ContentId:string.Equals(target.Name,current.Name,StringComparison.OrdinalIgnoreCase)&&string.Equals(target.World,current.HomeWorldName,StringComparison.OrdinalIgnoreCase);
}
