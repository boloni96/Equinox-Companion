using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool GoddessAppearance => config.Appearance?.GoddessBlue == true;
    private bool TestingInCompanion => GoddessAppearance && mainWindow is { IsOpen: true } && config.CompanionSection == "testing";
    private void SetAppearance(bool goddessBlue)
    {
        config.Appearance = AppearancePolicy.Choose(goddessBlue);
        SaveConfiguration();
    }
    private void DrawAppearanceSettings()
    {
        ImGui.TextUnformatted("Window design");
        if (ImGui.RadioButton("Goddess blue · compact menu", GoddessAppearance)) SetAppearance(true);
        if (ImGui.RadioButton("Current design · Dalamud tabs", !GoddessAppearance)) SetAppearance(false);
        SettingsHelp("Applies to Equinox windows on this installation. Pairing, window positions, minimised icons and gameplay settings are kept. Resize windows by dragging their edges. Your choice stays saved after updates.", false);
        if (ImGui.Button("Preview the new design")) welcomeWindow.IsOpen = true;
        ImGui.Separator();
        ImGui.TextUnformatted("Menu order");
        SettingsHelp("Move sections up or down here. The same order is used by the current tab layout. Settings stays at the end.");
        var tabs = CompanionTabs();
        var order = CompanionTabOrder(tabs);
        var shown = tabs.Where(t => t.Id != "settings").OrderBy(t => order.IndexOf(t.Id)).ToArray();
        for (var i = 0; i < shown.Length; i++)
        {
            var tab = shown[i]; ImGui.PushID(tab.Id);
            ImGui.BeginDisabled(i == 0);
            if (ImGui.SmallButton("Up")) MoveSection(tab.Id, shown[i - 1].Id);
            ImGui.EndDisabled(); ImGui.SameLine(); ImGui.BeginDisabled(i == shown.Length - 1);
            if (ImGui.SmallButton("Down")) MoveSection(tab.Id, shown[i + 1].Id);
            ImGui.EndDisabled(); ImGui.SameLine(); ImGui.TextUnformatted(tab.Label.Split("###")[0]); ImGui.PopID();
        }
        void MoveSection(string first, string second)
        {
            var a = order.IndexOf(first); var b = order.IndexOf(second);
            (order[a], order[b]) = (order[b], order[a]); config.TabOrder = order; SaveConfiguration();
        }
    }
    private static void SameLineIfFits(float width)
    {
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + width <= right) ImGui.SameLine();
    }
    private static float ButtonWidth(string label) => ImGui.CalcTextSize(label).X + 2 * ImGui.GetStyle().FramePadding.X;
    private void SelectCompanionSection(string id)
    {
        if (config.CompanionSection == id) return;
        config.CompanionSection = id;
        if (id == "testing") testingNext = default;
        SaveConfiguration();
    }
    private void DrawGoddessCompanion()
    {
        if (configurationError.Length > 0) ImGui.TextWrapped(configurationError);
        var iconSize = ImGui.GetFontSize() * 1.65f;
        if (ImGui.BeginTable("CompactCompanionHeader", 2, ImGuiTableFlags.SizingStretchProp))
        {
            var houses = CurrentHouseShortcuts();
            ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Shortcuts", ImGuiTableColumnFlags.WidthFixed, (houses.Length + 2) * (iconSize + ImGui.GetStyle().ItemSpacing.X));
            ImGui.TableNextColumn();
            ImGui.TextColored(new Vector4(.57f, .79f, 1, 1), "EQUINOX");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Companion " + CompanionVersion);
            ImGui.SameLine(); DrawSharedStatus();
            ImGui.TableNextColumn();
            foreach (var house in houses) { DrawHouseShortcut(house, iconSize); ImGui.SameLine(); }
            DrawHeaderShortcut(false, iconSize); ImGui.SameLine(); DrawHeaderShortcut(true, iconSize);
            ImGui.EndTable();
        }
        ImGui.Separator();
        var tabs = CompanionTabs();
        var order = CompanionTabOrder(tabs);
        if (settingsSelectTab) { SelectCompanionSection("settings"); settingsSelectTab = false; }
        if (quickLootSelectTab) { SelectCompanionSection("quickloot"); quickLootSelectTab = false; }
        if (followThemSelectTab) { SelectCompanionSection("followthem"); followThemSelectTab = false; }
        var selected = AppearancePolicy.SelectSection(config.CompanionSection, tabs.Select(t => t.Id).Append("testing"));
        var font = ImGui.GetFontSize();
        var iconsOnly = ImGui.GetContentRegionAvail().X < font * 37;
        var menuWidth = font * (iconsOnly ? 2.25f : 7.75f);
        var rowHeight = font + 6;
        ImGui.BeginChild("CompanionMenu", new Vector2(menuWidth, 0), false);
        var menuHeight = Math.Max(1, ImGui.GetContentRegionAvail().Y - rowHeight - ImGui.GetStyle().ItemSpacing.Y * 2);
        if (ImGui.BeginChild("CompanionMenuRows", new Vector2(0, menuHeight), false))
        {
            foreach (var tab in tabs.Where(t => t.Id != "settings").OrderBy(t => order.IndexOf(t.Id)))
            {
                var category = tab.Id == "planting" ? "gardening" : tab.Id == "submarines" ? "submarines" : "characters";
                MenuRow(tab.Id, tab.Label.Split("###")[0], category, () => SelectCompanionSection(tab.Id));
            }
            ImGui.Separator();
            MenuRow("adventure", "Adventure", "adventure", OpenAdventure);
            MenuRow("bids", "Housing bids", "housing-bids", OpenBidSearch);
            MenuRow("testing", "Testing", "adventure", () => SelectCompanionSection("testing"));
        }
        ImGui.EndChild();
        MenuRow("settings", "Settings", "options", OpenSettings);
        ImGui.EndChild(); ImGui.SameLine();
        if (ImGui.BeginChild("CompanionSection##" + selected, Vector2.Zero, false))
        {
            var draw = selected == "testing" ? (Action)DrawTesting : tabs.First(t => t.Id == selected).Draw;
            DrawWindowWithLauncher(draw);
        }
        ImGui.EndChild();
        void MenuRow(string id, string label, string category, Action action)
        {
            var at = ImGui.GetCursorScreenPos();
            var width = ImGui.GetContentRegionAvail().X;
            if (ImGui.Selectable("##menu-" + id, selected == id, ImGuiSelectableFlags.None, new Vector2(width, rowHeight))) action();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(label);
            var list = ImGui.GetWindowDrawList();
            list.PushClipRect(at, at + new Vector2(width, rowHeight), true);
            GardenImage("assets/category-icons/" + category + ".png", at + new Vector2(2, 1), new Vector2(rowHeight - 2));
            if (!iconsOnly) list.AddText(at + new Vector2(rowHeight + 4, 3), ImGui.GetColorU32(ImGuiCol.Text), label);
            list.PopClipRect();
        }
    }
}
