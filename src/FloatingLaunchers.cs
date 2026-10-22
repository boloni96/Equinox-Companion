using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private static bool escapePopupWasOpen,escapeWasDown,escapeRequested;
    private static int escapeMinimizedFrame = -1;
    private string? draggedLauncher;
    private readonly HashSet<string> minimizedLaunchers = [];
    private string? pendingLauncher;
    private double launcherClickAt;
    // PostDraw runs after ImGui.End, including collapsed windows. Query this
    // window explicitly: IsWindowCollapsed() here would read the parent window.
    private static void HandleNativeCollapse(Window window, Action? minimize)
    {
        if (!window.IsOpen || minimize is null) return;
        var native = ImGuiP.FindWindowByName(window.WindowName);
        var escape=window.IsFocused && escapeRequested &&
            escapeMinimizedFrame!=ImGui.GetFrameCount() && !escapePopupWasOpen &&
            !ImGui.IsPopupOpen("",ImGuiPopupFlags.AnyPopupId|ImGuiPopupFlags.AnyPopupLevel);
        if((native.IsNull || !native.Collapsed) && !escape)return;
        if(escape){escapeMinimizedFrame=ImGui.GetFrameCount();Keys[Dalamud.Game.ClientState.Keys.VirtualKey.ESCAPE]=false;}
        ImGui.SetWindowCollapsed(window.WindowName, false, ImGuiCond.Always);
        minimize();
    }
    private void MinimizeLauncher(string name)
    {
        minimizedLaunchers.Add(name);
        if(name == "Companion") { mainWindow.IsOpen=false; visible=false; }
        else if(name == "Where is it cheapest?") marketWindow.IsOpen=false;
        else if(name == "Do I already have it?") itemSearchWindow.IsOpen=false;
        else if(name == "Fashion Report") fashionWindow.IsOpen=false;
        else if(name=="Planting")plantingWindow.IsOpen=false;
        else if(name=="Testing")testingWindow.IsOpen=false;
        else if(name=="Adventure")adventureWindow.IsOpen=false;
        else if(name=="Housing bids")bidWindow.IsOpen=false;
        else if(name=="Welcome")welcomeWindow.IsOpen=false;
        else if(name=="New Character"){registrationDeferred=true;registrationWindow.IsOpen=false;}
    }
    private void LauncherClick(string name)
    {
        var now=ImGui.GetTime();
        if(pendingLauncher==name && now-launcherClickAt<=ImGui.GetIO().MouseDoubleClickTime)
        { pendingLauncher=null; minimizedLaunchers.Remove(name); }
        else { pendingLauncher=name; launcherClickAt=now; }
    }
    private FloatingLauncherOptions LauncherOptions(string name, int index)
    {
        if (!config.FloatingLaunchers.TryGetValue(name, out var options))
            config.FloatingLaunchers[name] = options = new() { Y = 180 + index * 76 };
        return options;
    }
    private void DrawFloatingLaunchers()
    {
        if(mainWindow.IsOpen) minimizedLaunchers.Remove("Companion");
        if(fashionWindow.IsOpen) minimizedLaunchers.Remove("Fashion Report");
        if(plantingWindow.IsOpen) minimizedLaunchers.Remove("Planting");
        if(itemSearchWindow.IsOpen)minimizedLaunchers.Remove("Do I already have it?");
        if(marketWindow.IsOpen)minimizedLaunchers.Remove("Where is it cheapest?");
        if(testingWindow.IsOpen)minimizedLaunchers.Remove("Testing");
        DrawLauncher("Testing",9,"garden-art/assets/category-icons/adventure.png");
        if(adventureWindow.IsOpen)minimizedLaunchers.Remove("Adventure");
        if(bidWindow.IsOpen)minimizedLaunchers.Remove("Housing bids");
        DrawLauncher("Adventure",7,"garden-art/assets/category-icons/adventure.png");
        DrawLauncher("Housing bids",8,"garden-art/assets/category-icons/housing-bids.png");
        DrawLauncher("Where is it cheapest?",6,"garden-art/assets/category-icons/market-search.png");
        DrawLauncher("Do I already have it?",5,"garden-art/assets/category-icons/item-search.png");
        DrawLauncher("Companion", 0, "icon.png");
        DrawLauncher("Fashion Report", 1, "garden-art/assets/category-icons/fashion-report.png");
        DrawLauncher("Planting", 2, "garden-art/assets/category-icons/gardening.png");
        if(welcomeWindow.IsOpen)minimizedLaunchers.Remove("Welcome");
        if(registrationWindow.IsOpen || !Player.IsLoaded || registrationSnapshot==null)minimizedLaunchers.Remove("New Character");
        DrawLauncher("Welcome",3,"icon.png");
        DrawLauncher("New Character",4,"icon.png");
        if(pendingLauncher is {} pending && ImGui.GetTime()-launcherClickAt>ImGui.GetIO().MouseDoubleClickTime && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            pendingLauncher=null;
            if(minimizedLaunchers.Contains(pending))
            {
                if(pending=="Where is it cheapest?")marketWindow.IsOpen=true;
                else if(pending=="Do I already have it?")itemSearchWindow.IsOpen=true;
                else if(pending=="Testing")OpenTesting();
                else if(pending=="Adventure")OpenAdventure();
                else if(pending=="Housing bids")bidWindow.IsOpen=true;
                else if(pending=="Companion") Open();
                else if(pending=="Fashion Report") fashionWindow.OpenReport();
                else if(pending=="Planting")OnPlantingCommand("/gardening", "");
                else if(pending=="Welcome")welcomeWindow.IsOpen=true;
                else if(pending=="New Character")registrationWindow.IsOpen=true;
                if(pending!="Planting" || plantingWindow.IsOpen) minimizedLaunchers.Remove(pending);
            }
        }
    }
    private void DrawLauncher(string name, int index, string? artwork)
    {
        var o = LauncherOptions(name, index);
        if (!minimizedLaunchers.Contains(name)) return;
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
            else if(name=="Where is it cheapest?")DrawMarketSearchIcon(p,new Vector2(64),o.Opacity);
            else if(name=="Do I already have it?")DrawItemSearchIcon(p,new Vector2(64),o.Opacity);
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
            var fashionComplete=name=="Fashion Report"&&CurrentFashionComplete();
            if(fashionComplete){DrawFashionCheck(p,64,o.Opacity);}
            ImGui.SetCursorPos(new(0, 20));
            ImGui.InvisibleButton("Open", new(64, 44));
            var hovered = ImGui.IsItemHovered();
            if(hovered && pendingLauncher==name && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            { pendingLauncher=null; minimizedLaunchers.Remove(name); }
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                draggedLauncher = name;
                if (!o.Locked) { o.X += ImGui.GetIO().MouseDelta.X; o.Y += ImGui.GetIO().MouseDelta.Y; }
            }
            if (ImGui.IsItemDeactivated())
            {
                if (draggedLauncher == name) { draggedLauncher = null; SaveConfiguration(); }
                else if (hovered && minimizedLaunchers.Contains(name)) LauncherClick(name);
            }
            ImGui.SetCursorPos(Vector2.Zero);
            if (ImGui.InvisibleButton("Open top", new(48, 20))) LauncherClick(name);
            ImGui.SetCursorPos(new(48, 0));
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha,ImGui.GetStyle().Alpha*.75f);
            if (ImGui.SmallButton("...")) ImGui.OpenPopup("Icon settings");
            ImGui.PopStyleVar();
            if (hovered) ImGui.SetTooltip((name=="Planting"?"Gardening":name) + " · Click to open · Double-click to close · Drag to move"+(name=="Fashion Report"?"\n"+CurrentFashionTooltip():""));
            if (ImGui.BeginPopup("Icon settings")) { DrawLauncherOptions(name, o); ImGui.EndPopup(); }
        }
        ImGui.End(); ImGui.PopStyleVar();
    }
    private void DrawLauncherOptions(string name, FloatingLauncherOptions o)
    {
        ImGui.TextUnformatted(name=="Planting"?"Gardening":name);
        var opacity = o.Opacity; var blur = o.Blur; var locked = o.Locked;
        var changed = ImGui.SliderFloat("Opacity", ref opacity, .2f, 1, "%.2f");
        changed |= ImGui.Checkbox("Blur background", ref blur);
        changed |= ImGui.Checkbox("Lock position", ref locked);
        if (ImGui.Button("Reset position")) { o.X = 24; o.Y = 180 + (name == "Companion" ? 0 : name == "Fashion Report" ? 76 : name=="Planting"?152:name=="Welcome"?228:name=="New Character"?304:name=="Where is it cheapest?"?456:name=="Adventure"?532:name=="Housing bids"?608:name=="Testing"?684:380); changed = true; }
        if (changed) { o.Opacity = opacity; o.Blur = blur; o.Locked = locked; SaveConfiguration(); }
    }
    private void DrawFloatingLauncherSettings()
    {
        SettingsHelp("Use the native title-bar collapse triangle or double-click the title bar to minimize to an icon. Single-click reopens its window; double-click closes both. Esc minimizes the focused window to its icon. X closes without an icon. Commands and keybinds reopen windows. Drag icons to move them.",false);
        var names = new[] { "Companion", "Fashion Report", "Planting", "Welcome", "New Character", "Do I already have it?", "Where is it cheapest?", "Adventure", "Housing bids", "Testing" };
        for (var i = 0; i < names.Length; i++) { ImGui.PushID(i); if(ImGui.CollapsingHeader(names[i]=="Planting"?"Gardening":names[i]))DrawLauncherOptions(names[i], LauncherOptions(names[i], i)); ImGui.PopID(); }
    }
}

