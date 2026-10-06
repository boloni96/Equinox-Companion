using System.Security.Cryptography;
using System.Text;
namespace EquinoxCompanion;
public sealed record FollowRoomTarget(int Room,string Owner)
{
    public FollowMenuStep Step()=>new("Private chamber: "+Owner,Addon:"HousingSelectRoom",Arguments:[Room],MenuSignature:Signature(Room,Owner));
    public static string Signature(int room,string owner)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"Equinox room {room}: {owner}")));
    public static FollowRoomTarget? Read(FollowMenuStep step)
    {
        const string prefix="Private chamber: ";
        if(step.Addon!="HousingSelectRoom"||step.Confirmation||!step.Text.StartsWith(prefix,StringComparison.Ordinal)||step.Arguments is not {Length:1} args||args[0] is <1 or >512)return null;
        var owner=step.Text[prefix.Length..];
        return owner.Length is >0 and <=80&&!owner.Any(char.IsControl)&&step.MenuSignature==Signature(args[0],owner)?new(args[0],owner):null;
    }
    public static bool Confirmation(string prompt,bool ownRoom,string owner="")=>
        owner.Length>0&&(prompt==$"Enter {owner}'s room?"||prompt==$"Enter {owner}’s room?")||
        ownRoom&&prompt=="Retire to your own chambers?"||FollowPortalPolicy.IsConfirmationSupported(prompt)&&prompt.Contains("chambers",StringComparison.OrdinalIgnoreCase);
}
