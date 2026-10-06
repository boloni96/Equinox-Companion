using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool WithinTravelInteractionRange(FollowPortalSignal s)
    {
        if(s.SourceKind=="boundary"||s.TravelKind is "teleport" or "estate" or "friendestate" or "world"||Objects.LocalPlayer is not {} self)return false;
        return Objects.Any(x=>x.BaseId==s.BaseId&&(s.SourceKind.Length==0||x.ObjectKind.ToString()==s.SourceKind)&&x.IsTargetable&&Vector3.DistanceSquared(x.Position,new(s.X,s.Y,s.Z))<1&&Math.Abs(x.Position.Y-self.Position.Y)<2&&Vector3.Distance(self.Position,x.Position)<=x.HitboxRadius+2.5f);
    }
    private unsafe bool MatchingTravelMenu(FollowPortalSignal s)
    {
        if(Objects.LocalPlayer is not {} self)return false;
        if(s.TravelKind is "estate" or "friendestate"&&s.FriendContentId.Length>0&&VisibleFollowAddon("TeleportHousingFriend"))return true;
        if(s.SourceKind!="boundary"&&!Objects.Any(x=>x.BaseId==s.BaseId&&(s.SourceKind.Length==0||x.ObjectKind.ToString()==s.SourceKind)&&Vector3.Distance(x.Position,new(s.X,s.Y,s.Z))<1&&Vector3.Distance(self.Position,x.Position)<=x.HitboxRadius+3))return false;
        if(BoundaryWardOpen(s))return true;
        var menu=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;
        var choices=TransportChoices(menu);
        if(s.TravelKind=="aethernet")return choices.Any(x=>x.Trim().TrimEnd('.')=="Aethernet")||VisibleFollowAddon("TelepotTown");
        if(s.TravelKind=="ward")return choices.Any(x=>x.StartsWith("Residential District Aethernet")||x.StartsWith("Go to specified ward"));
        if(s.Steps is not {Length:>0})return false;
        var step=s.Steps[0];
        if(!step.Confirmation)return choices.Count(x=>x==step.Text)==1;
        var yes=(AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        return yes!=null&&yes->IsVisible&&yes->PromptText!=null&&yes->PromptText->NodeText.ToString()==step.Text;
    }
    private unsafe bool VisibleFollowAddon(string name){var a=(AtkUnitBase*)GardenGui.GetAddonByName(name).Address;return a!=null&&a->IsVisible;}
}
