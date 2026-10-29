using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EquinoxCompanion;

// Applied only while Equinox windows draw. Never change Dalamud's global theme,
// native garden markers, floating icons, or another plugin's style.
internal readonly struct EquinoxAppearance : IDisposable
{
    private readonly bool enabled;
    private static Vector4 Ink(uint rgb) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
    private static readonly (ImGuiCol Slot, Vector4 Color)[] Colors =
    [
        (ImGuiCol.Text, Ink(0xe5edf7)), (ImGuiCol.TextDisabled, Ink(0xa5b5c9)),
        (ImGuiCol.WindowBg, Ink(0x0d1724)), (ImGuiCol.ChildBg, Ink(0x0d1724)),
        (ImGuiCol.PopupBg, Ink(0x122030)), (ImGuiCol.Border, Ink(0x6589b0)),
        (ImGuiCol.TitleBg, Ink(0x101c2b)), (ImGuiCol.TitleBgActive, Ink(0x1a2c40)),
        (ImGuiCol.TitleBgCollapsed, Ink(0x101c2b)),
        (ImGuiCol.FrameBg, Ink(0x1a2c40)), (ImGuiCol.FrameBgHovered, Ink(0x253d56)), (ImGuiCol.FrameBgActive, Ink(0x1f4264)),
        (ImGuiCol.Button, Ink(0x1a2c40)), (ImGuiCol.ButtonHovered, Ink(0x253d56)), (ImGuiCol.ButtonActive, Ink(0x1f4264)),
        (ImGuiCol.Header, Ink(0x1f4264)), (ImGuiCol.HeaderHovered, Ink(0x253d56)), (ImGuiCol.HeaderActive, Ink(0x315a80)),
        (ImGuiCol.Tab, Ink(0x1a2c40)), (ImGuiCol.TabHovered, Ink(0x315a80)), (ImGuiCol.TabActive, Ink(0x1f4264)),
        (ImGuiCol.TabUnfocused, Ink(0x101c2b)), (ImGuiCol.TabUnfocusedActive, Ink(0x1a334d)),
        (ImGuiCol.CheckMark, Ink(0x91c9ff)), (ImGuiCol.SliderGrab, Ink(0x91c9ff)), (ImGuiCol.SliderGrabActive, Ink(0xb7dbff)),
        (ImGuiCol.ScrollbarBg, Ink(0x0d1724)), (ImGuiCol.ScrollbarGrab, Ink(0x2d4056)),
        (ImGuiCol.ScrollbarGrabHovered, Ink(0x46627f)), (ImGuiCol.ScrollbarGrabActive, Ink(0x6589b0)),
        (ImGuiCol.Separator, Ink(0x2d4056)), (ImGuiCol.SeparatorHovered, Ink(0x6589b0)), (ImGuiCol.SeparatorActive, Ink(0x91c9ff)),
        (ImGuiCol.ResizeGrip, Ink(0x2d4056)), (ImGuiCol.ResizeGripHovered, Ink(0x6589b0)), (ImGuiCol.ResizeGripActive, Ink(0x91c9ff)),
        (ImGuiCol.PlotHistogram, Ink(0x5994cb))
    ];
    public EquinoxAppearance(bool enabled)
    {
        this.enabled = enabled;
        // Consistent, thin light-blue outlines in both Equinox designs.
        // Scope these pushes so other plugins retain their own styling.
        ImGui.PushStyleColor(ImGuiCol.Border, Ink(0x6589b0));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabBorderSize, 1f);
        if (!enabled) return;
        foreach (var (slot, color) in Colors) ImGui.PushStyleColor(slot, color);
        var scale = ImGui.GetFontSize() / 17f;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(7, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 3) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6, 4) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(4, 3) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(4, 3) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 5 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 3 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 4 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 3 * scale);
    }
    public void Dispose()
    {
        if (enabled)
        {
            ImGui.PopStyleVar(10);
            ImGui.PopStyleColor(Colors.Length);
        }
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
    }
}
