using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private bool CurrentFashionComplete() => Player.IsLoaded && FashionCompletion.IsComplete(ReadActor(),config.Discoveries,config.SharedRoster?.People??[],DateTimeOffset.UtcNow);
    private string CurrentFashionTooltip() => FashionCompletion.Tooltip(Player.IsLoaded?ReadActor():null,config.Discoveries,config.SharedRoster?.People??[],DateTimeOffset.UtcNow);
    private static void DrawFashionCheck(Vector2 at,float size,float opacity=1)
    {
        var scale=size/64;var draw=ImGui.GetWindowDrawList();
        var dark=ImGui.ColorConvertFloat4ToU32(new Vector4(.02f,.12f,.02f,opacity));
        var green=ImGui.ColorConvertFloat4ToU32(new Vector4(.25f,1,.15f,opacity));
        var a=at+new Vector2(15,34)*scale;var b=at+new Vector2(28,47)*scale;var c=at+new Vector2(51,17)*scale;
        draw.AddLine(a,b,dark,8*scale);draw.AddLine(b,c,dark,8*scale);
        draw.AddLine(a,b,green,5*scale);draw.AddLine(b,c,green,5*scale);
    }
    private static void DrawFashionBorder(Vector2 at,float size,float opacity=1) =>
        ImGui.GetWindowDrawList().AddRect(at+new Vector2(1),at+new Vector2(size-1),ImGui.ColorConvertFloat4ToU32(new Vector4(.25f,1,.15f,opacity)),5,ImDrawFlags.None,2);
    private void DrawHeaderShortcut(bool fashion, float size)
    {
        var name = fashion ? "Fashion Report" : "Planting";
        var at = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton("Open " + name + "##header", new Vector2(size));
        var draw = ImGui.GetWindowDrawList();
        var hover = ImGui.IsItemHovered();
        var complete=fashion&&CurrentFashionComplete();
        draw.AddRectFilled(at, at + new Vector2(size), hover ? 0xff534735 : 0x99312720, 5);
        if (fashion)
        {
            var texture = Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!, "garden-art/assets/category-icons/fashion-report.png")).GetWrapOrDefault();
            if (texture is not null) draw.AddImage(texture.Handle, at + new Vector2(3), at + new Vector2(size-3));
            else draw.AddText(at + new Vector2(4), 0xffffffff, "F");
            if(complete){DrawFashionCheck(at,size);DrawFashionBorder(at,size);}
        }
        else
        {
            var texture = Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!, "garden-art/assets/category-icons/gardening.png")).GetWrapOrDefault();
            if (texture is not null) draw.AddImage(texture.Handle, at + new Vector2(3), at + new Vector2(size-3));
            else draw.AddText(at + new Vector2(4), 0xffffffff, "P");
        }
        if (hover) ImGui.SetTooltip(fashion ? "Open Fashion Report · /fashionr\n"+CurrentFashionTooltip() : "Open Gardening guide · /gardening\nRequires an identified paired estate, just like the command.");
        if (!clicked) return;
        if (fashion) OnFashionCommand("/fashionr", "");
        else OnPlantingCommand("/gardening", "");
        if (fashion || plantingWindow.IsOpen)
        {
            minimizedLaunchers.Remove(name);
            if (pendingLauncher == name) pendingLauncher = null;
        }
    }
}
