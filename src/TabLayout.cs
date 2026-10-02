using Dalamud.Bindings.ImGui;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private void DrawOrderedTabs()
    {
        var tabs = new List<(string Id, string Label, Action Draw)>
        {
            ("tests", "Tests###equinox-tests", DrawTests),
            ("housing", "Characters & housing###equinox-housing", DrawHousing),
        };
        foreach (var profile in config.SharedRoster?.People ?? [])
            tabs.Add(("person:" + profile.Id, profile.Name.Replace("##", "") + "###person-" + profile.Id, () => DrawSharedPerson(profile)));
        tabs.Add(("gardens", "Garden plans###equinox-gardens", DrawGardenPlans));
        tabs.Add(("submarines", "Submarines###equinox-submarines", DrawSubmarines));
        tabs.Add(("settings", "Settings###equinox-settings", DrawSettings));
        config.TabOrder ??= [];
        // Retain absent people so a temporarily unavailable roster cannot erase their positions.
        var order = config.TabOrder.Distinct().ToList();
        foreach (var tab in tabs.Where(t => !order.Contains(t.Id)))
        {
            var settings = tab.Id.StartsWith("person:") && order.Contains("submarines") ? order.IndexOf("submarines") : order.IndexOf("settings");
            if (settings >= 0 && tab.Id != "settings") order.Insert(settings, tab.Id);
            else order.Add(tab.Id);
        }
        if (!ImGui.BeginTabBar("CompanionSections", ImGuiTabBarFlags.Reorderable)) return;
        var ids = new Dictionary<uint, string>();
        foreach (var tab in tabs.OrderBy(t => order.IndexOf(t.Id)))
        {
            ids[ImGui.GetID(tab.Label)] = tab.Id;
            if (!ImGui.BeginTabItem(tab.Label, requestedTab == tab.Id ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None)) continue;
            if (requestedTab == tab.Id) requestedTab = null;
            tab.Draw();
            ImGui.EndTabItem();
        }
        // ImGui reordering is transient: persist its actual visual order in plugin config.
        var bar = ImGui.GetCurrentContext().CurrentTabBar;
        var visible = new List<string>();
        for (var i = 0; i < bar.Tabs.Size; i++)
            if (ids.TryGetValue(bar.Tabs[i].ID, out var id)) visible.Add(id);
        if (visible.Count == tabs.Count && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var index = 0;
            var persisted = order.Select(id => visible.Contains(id) ? visible[index++] : id).ToList();
            if (!config.TabOrder.SequenceEqual(persisted))
            {
                config.TabOrder = persisted;
                Pi.SavePluginConfig(config);
            }
        }
        ImGui.EndTabBar();
    }
}
