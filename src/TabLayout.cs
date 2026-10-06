using Dalamud.Bindings.ImGui;
using System.Numerics;

namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private void DrawOrderedTabs()
    {
        var tabs = new List<(string Id, string Label, Action Draw)>
        {
            ("housing", "My Empire###equinox-housing", DrawHousing),
        };
        foreach (var profile in config.SharedRoster?.People ?? [])
            tabs.Add(("person:" + profile.Id, profile.Name.Replace("##", "") + "###person-" + profile.Id, () => DrawSharedPerson(profile)));
        tabs.Add(("planting", "Gardening###equinox-planting", DrawPlantingOverview));
        tabs.Add(("submarines", "Submarines###equinox-submarines", DrawSubmarines));
        if(config.EnableQuickLoot)tabs.Add(("quickloot", "QuickLoot###equinox-quickloot", DrawQuickLoot));
        if(config.EnableFollowThem)tabs.Add(("followthem", "FollowThem###equinox-followthem", DrawFollowThem));
        tabs.Add(("settings", "Settings###equinox-settings", DrawSettings));
        config.TabOrder ??= [];
        // Repair the old person-at-end layout once; retain subsequent user ordering.
        var order = TabOrderPolicy.Reconcile(config.TabOrder,tabs.Select(t=>t.Id),config.TabOrderVersion<1);
        if(config.TabOrderVersion<1){config.TabOrderVersion=1;config.TabOrder=order;Pi.SavePluginConfig(config);}
        // ImGui appends late-arriving tabs regardless of submission order. Recreate the
        // bar when the roster's tab membership changes, then restore the saved order.
        var tabBarId="CompanionSections-v2-"+string.Join("|",tabs.Select(t=>t.Id));
        if (!ImGui.BeginTabBar(tabBarId, ImGuiTabBarFlags.Reorderable)) return;
        var ids = new Dictionary<uint, string>();
        foreach (var tab in tabs.OrderBy(t => order.IndexOf(t.Id)))
        {
            var category=tab.Id=="housing"||tab.Id.StartsWith("person:")?"characters":tab.Id=="planting"?"gardening":tab.Id=="submarines"?"submarines":null;
            var label=category is null?tab.Label:new string(' ',(int)Math.Ceiling((ImGui.GetFontSize()+5)/Math.Max(1,ImGui.CalcTextSize(" ").X)))+tab.Label;
            ids[ImGui.GetID(label)] = tab.Id;
            var flags=tab.Id=="settings"?ImGuiTabItemFlags.Trailing|ImGuiTabItemFlags.NoReorder:ImGuiTabItemFlags.None;
            if(tab.Id=="quickloot"&&quickLootSelectTab){flags|=ImGuiTabItemFlags.SetSelected;quickLootSelectTab=false;}
            var active=ImGui.BeginTabItem(label,flags);
            if(category is not null){var at=ImGui.GetItemRectMin();var max=ImGui.GetItemRectMax();var size=Math.Min(ImGui.GetFontSize()+2,max.Y-at.Y-2);var draw=ImGui.GetWindowDrawList();draw.PushClipRect(at,max,true);GardenImage("assets/category-icons/"+category+".png",at+new Vector2(ImGui.GetStyle().FramePadding.X,(max.Y-at.Y-size)/2),new Vector2(size));draw.PopClipRect();}
            if(!active)continue;
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
