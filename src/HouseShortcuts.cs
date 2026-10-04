using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private SharedHouse[] CurrentHouseShortcuts()=>HouseShortcutSelection.For(Player.IsLoaded?ReadActor():null,config.SharedRoster?.People??[]);
    private void DrawHouseShortcut(SharedHouse house,float size)
    {
        var now=DateTimeOffset.UtcNow;var fc=house.Type=="Free Company house";var at=ImGui.GetCursorScreenPos();
        var clicked=ImGui.InvisibleButton((fc?"FC house":"Private house")+"###house-shortcut-"+house.Id,new Vector2(size));var hovered=ImGui.IsItemHovered();
        var band=house.Paused?HousingBand.Unknown:HousingStatus.Band(house.LastEntry,now);var draw=ImGui.GetWindowDrawList();
        draw.AddRectFilled(at,at+new Vector2(size),hovered?0xff534735:0x99312720,5);
        GardenImage("assets/category-icons/"+(fc?"fc-house":"private-house")+".png",at+new Vector2(2),new Vector2(size-4));
        if(HouseShortcutSelection.Checked(house,now))DrawFashionCheck(at,size);
        draw.AddRect(at,at+new Vector2(size),ImGui.ColorConvertFloat4ToU32(BandColour(band)),5,ImDrawFlags.None,2);
        if(hovered){ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*28);
            ImGui.TextUnformatted(fc?"FC House · "+house.FcName:"Private House · "+house.OwnerName);
            ImGui.TextWrapped(house.Name+" · "+house.World+" · "+house.District+$" W{house.Ward} P{house.Plot}");
            ImGui.TextColored(BandColour(band),BandLabel(band));
            ImGui.TextWrapped(HouseShortcutSelection.Checked(house,now)?"Checked · qualifying interior visit within the last 7 days":"Unchecked · no qualifying interior visit in the last 7 days");
            ImGui.TextWrapped(house.LastEntry is {} last?$"Last qualifying entry: {last.ToLocalTime():g}\n45-day estimate: {last.AddDays(45).ToLocalTime():g}":"No qualifying entry recorded.");
            ImGui.TextWrapped(house.Paused?"Demolition marked suspended in Journal.":fc?"A linked FC member must enter the interior. Touching the placard does not reset the timer.":"The private owner must enter the interior. Touching the placard does not reset the timer.");
            ImGui.TextDisabled("Click to open this house in the Journal.");ImGui.PopTextWrapPos();ImGui.EndTooltip();}
        if(clicked)Dalamud.Utility.Util.OpenLink("https://equinoxjournal.pages.dev/#house="+Uri.EscapeDataString(house.Id));
    }
}
