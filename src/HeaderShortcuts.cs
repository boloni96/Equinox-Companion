using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private void DrawHeaderShortcut(bool fashion, float size)
    {
        var name = fashion ? "Fashion Report" : "Planting";
        var at = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton("Open " + name + "##header", new Vector2(size));
        var draw = ImGui.GetWindowDrawList();
        var hover = ImGui.IsItemHovered();
        draw.AddRectFilled(at, at + new Vector2(size), hover ? 0xff534735 : 0x99312720, 5);
        if (fashion)
        {
            var scale = size / 64;
            var gold = 0xff6ec9ffu;
            draw.AddBezierCubic(at + new Vector2(25,22)*scale, at + new Vector2(25,10)*scale,
                at + new Vector2(44,12)*scale, at + new Vector2(32,29)*scale, gold, 2);
            draw.AddLine(at + new Vector2(32,29)*scale, at + new Vector2(12,45)*scale, gold, 2);
            draw.AddLine(at + new Vector2(12,45)*scale, at + new Vector2(52,45)*scale, gold, 2);
            draw.AddLine(at + new Vector2(52,45)*scale, at + new Vector2(32,29)*scale, gold, 2);
        }
        else
        {
            var texture = Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!, "garden-art/assets/icons/gardening-tools.png")).GetWrapOrDefault();
            if (texture is not null) draw.AddImage(texture.Handle, at + new Vector2(3), at + new Vector2(size-3));
            else draw.AddText(at + new Vector2(4), 0xffffffff, "P");
        }
        if (hover) ImGui.SetTooltip(fashion ? "Open Fashion Report · /fashionr" : "Open planting guide · /planting\nRequires an identified paired estate, just like the command.");
        if (!clicked) return;
        if (fashion) OnFashionCommand("/fashionr", "");
        else OnPlantingCommand("/planting", "");
        if (fashion || plantingWindow.IsOpen)
        {
            minimizedLaunchers.Remove(name);
            if (pendingLauncher == name) pendingLauncher = null;
        }
    }
}
