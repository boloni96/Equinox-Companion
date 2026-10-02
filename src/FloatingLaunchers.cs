using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
namespace EquinoxCompanion;

[Serializable]
public sealed class FloatingLauncherOptions
{
    public bool Enabled { get; set; } = true;
    public float X { get; set; } = 24;
    public float Y { get; set; } = 180;
    public float Opacity { get; set; } = 1;
    public bool Blur { get; set; }
    public bool Locked { get; set; }
}
public sealed partial class Plugin
{
    private string? draggedLauncher;
    private FloatingLauncherOptions LauncherOptions(string name, int index)
    {
        if (!config.FloatingLaunchers.TryGetValue(name, out var options))
            config.FloatingLaunchers[name] = options = new() { Y = 180 + index * 76 };
        return options;
    }
    private void DrawFloatingLaunchers()
    {
        DrawLauncher("Companion", 0, "icon.png", Open);
        DrawLauncher("Fashion Report", 1, null, () => fashionWindow.OpenReport());
        DrawLauncher("Planting", 2, "garden-art/assets/icons/seedling.png", () => OnPlantingCommand("/planting", ""));
    }
    private void DrawLauncher(string name, int index, string? artwork, Action open)
    {
        var o = LauncherOptions(name, index);
        if (!o.Enabled) return;
        var display = ImGui.GetIO().DisplaySize;
        o.X = Math.Clamp(o.X, 0, Math.Max(0, display.X - 68));
        o.Y = Math.Clamp(o.Y, 0, Math.Max(0, display.Y - 68));
        ImGui.SetNextWindowPos(new(o.X, o.Y), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new(68, 68), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.Begin("##EquinoxLauncher" + index, flags))
        {
            var p = ImGui.GetWindowPos(); var draw = ImGui.GetWindowDrawList();
            if (o.Blur) ImGuiHelpers.PrependBlurBehind(draw, p, p + new Vector2(64), 12, 10, Vector4.Zero, Vector4.Zero, 0);
            var tint = ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, o.Opacity));
            if (artwork is not null)
            {
                var tex = Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!, artwork)).GetWrapOrDefault();
                if (tex is not null) draw.AddImage(tex.Handle, p + new Vector2(2), p + new Vector2(62), Vector2.Zero, Vector2.One, tint);
            }
            else
            {
                // A clear hanger silhouette; it stays crisp at every UI scale.
                var gold = ImGui.ColorConvertFloat4ToU32(new Vector4(1, .79f, .43f, o.Opacity));
                draw.AddCircleFilled(p + new Vector2(32), 29, ImGui.ColorConvertFloat4ToU32(new Vector4(.24f, .12f, .29f, o.Opacity)), 32);
                draw.AddBezierCubic(p + new Vector2(25, 22), p + new Vector2(25, 10), p + new Vector2(44, 12), p + new Vector2(32, 29), gold, 3);
                draw.AddLine(p + new Vector2(32, 29), p + new Vector2(12, 45), gold, 3);
                draw.AddLine(p + new Vector2(12, 45), p + new Vector2(52, 45), gold, 3);
                draw.AddLine(p + new Vector2(52, 45), p + new Vector2(32, 29), gold, 3);
            }
            ImGui.SetCursorPos(new(0, 20));
            ImGui.InvisibleButton("Open", new(64, 44));
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                draggedLauncher = name;
                if (!o.Locked) { o.X += ImGui.GetIO().MouseDelta.X; o.Y += ImGui.GetIO().MouseDelta.Y; }
            }
            if (ImGui.IsItemDeactivated())
            {
                if (draggedLauncher == name) { draggedLauncher = null; Pi.SavePluginConfig(config); }
                else if (hovered) open();
            }
            ImGui.SetCursorPos(Vector2.Zero);
            if (ImGui.InvisibleButton("Open top", new(48, 20))) open();
            ImGui.SetCursorPos(new(48, 0));
            if (ImGui.SmallButton("...")) ImGui.OpenPopup("Icon settings");
            if (hovered) ImGui.SetTooltip(name + " · Click to open · Drag to move");
            if (ImGui.BeginPopup("Icon settings")) { DrawLauncherOptions(name, o); ImGui.EndPopup(); }
        }
        ImGui.End(); ImGui.PopStyleVar();
    }
    private void DrawLauncherOptions(string name, FloatingLauncherOptions o)
    {
        ImGui.TextUnformatted(name);
        var enabled = o.Enabled; var opacity = o.Opacity; var blur = o.Blur; var locked = o.Locked;
        var changed = ImGui.Checkbox("Show floating icon", ref enabled);
        changed |= ImGui.SliderFloat("Opacity", ref opacity, .2f, 1, "%.2f");
        changed |= ImGui.Checkbox("Blur background", ref blur);
        changed |= ImGui.Checkbox("Lock position", ref locked);
        if (ImGui.Button("Reset position")) { o.X = 24; o.Y = 180 + (name == "Companion" ? 0 : name == "Fashion Report" ? 76 : 152); changed = true; }
        if (changed) { o.Enabled = enabled; o.Opacity = opacity; o.Blur = blur; o.Locked = locked; Pi.SavePluginConfig(config); }
    }
    private void DrawFloatingLauncherSettings()
    {
        ImGui.TextWrapped("Floating icons remain available when their windows are closed or minimized. Drag an icon to move it; its small button opens appearance settings.");
        var names = new[] { "Companion", "Fashion Report", "Planting" };
        for (var i = 0; i < names.Length; i++) { ImGui.PushID(i); DrawLauncherOptions(names[i], LauncherOptions(names[i], i)); ImGui.Separator(); ImGui.PopID(); }
    }
}
