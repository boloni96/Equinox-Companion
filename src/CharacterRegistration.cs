using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public sealed record CharacterRegistration(string PersonId,string AccountId,string PersonName="",string AccountName="",string AccountKey="",string Dc="",string Region="",string PairingScope="");
public static class CharacterRegistrationPolicy
{
    public static bool Valid(CharacterRegistration? r) => r is not null && Regex.IsMatch(r.PersonId??"","^[A-Za-z0-9_-]{1,100}$") && Regex.IsMatch(r.AccountId??"","^[A-Za-z0-9_-]{1,100}$") && new[]{r.PersonName,r.AccountName,r.Dc,r.Region}.All(s=>s is not null&&s.Length<=100&&!s.Any(char.IsControl)) && (r.AccountKey==""||Regex.IsMatch(r.AccountKey??"","^[a-f0-9]{64}$"));
    public static SharedCharacter? Find(SharedRoster roster,Actor actor)
    {
        var all=roster.People.SelectMany(p=>p.Characters).ToArray();
        var stable=all.Where(c=>c.ContentId==actor.ContentId||c.Id=="companion-character-"+actor.ContentId).ToArray();
        if(stable.Length==1)return stable[0];
        var names=all.Where(c=>string.Equals(c.Name.Trim(),actor.Name.Trim(),StringComparison.OrdinalIgnoreCase)&&string.Equals(c.World.Trim(),actor.HomeWorldName?.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
        return names.Length==1?names[0]:null;
    }
    public static (string Person,string Account)? Destination(SharedRoster roster,string key)
    {
        if(!Regex.IsMatch(key??"","^[a-f0-9]{64}$"))return null;
        var matches=roster.People.SelectMany(p=>p.Characters.Where(c=>c.GameAccountKey==key&&!string.IsNullOrWhiteSpace(c.AccountId)).Select(c=>(Person:p.Id,Account:c.AccountId))).Distinct().Take(2).ToArray();
        return matches.Length==1?matches[0]:null;
    }
}
