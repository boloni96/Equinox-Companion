using System.Text.RegularExpressions;
namespace EquinoxCompanion;
public sealed record CharacterRegistration(string PersonId,string AccountId,string PersonName="",string AccountName="",string AccountKey="",string Dc="",string Region="",string PairingScope="");
public static class CharacterRegistrationPolicy
{
    public static bool Valid(CharacterRegistration? r) => r is not null && Regex.IsMatch(r.PersonId??"","^[A-Za-z0-9_-]{1,100}$") && Regex.IsMatch(r.AccountId??"","^[A-Za-z0-9_-]{1,100}$") && new[]{r.PersonName,r.AccountName,r.Dc,r.Region}.All(s=>s is not null&&s.Length<=100&&!s.Any(char.IsControl)) && (r.AccountKey==""||Regex.IsMatch(r.AccountKey??"","^[a-f0-9]{64}$"));
    public static SharedCharacter? Find(SharedRoster roster,Actor actor)
    {
        SharedCharacter? stable=null,named=null;var stableCount=0;var nameCount=0;
        var generatedId="companion-character-"+actor.ContentId;
        var name=actor.Name.Trim();var world=actor.HomeWorldName?.Trim();
        foreach(var person in roster.People)foreach(var c in person.Characters){
            if(c.ContentId==actor.ContentId||c.Id==generatedId){stable=c;stableCount++;}
            if(string.Equals(c.Name.Trim(),name,StringComparison.OrdinalIgnoreCase)&&string.Equals(c.World.Trim(),world,StringComparison.OrdinalIgnoreCase)){named=c;nameCount++;}
        }
        return stableCount==1?stable:nameCount==1?named:null;
    }
    public static (string Person,string Account)? Destination(SharedRoster roster,string key)
    {
        if(!Regex.IsMatch(key??"","^[a-f0-9]{64}$"))return null;
        var matches=roster.People.SelectMany(p=>p.Characters.Where(c=>c.GameAccountKey==key&&!string.IsNullOrWhiteSpace(c.AccountId)).Select(c=>(Person:p.Id,Account:c.AccountId))).Distinct().Take(2).ToArray();
        return matches.Length==1?matches[0]:null;
    }
}
