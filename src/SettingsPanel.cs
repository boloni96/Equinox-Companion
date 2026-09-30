using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly List<HouseObservation> pendingHouseNotices = [];
    private void UpdateHouseNotices(DateTimeOffset now)
    {
        if (!config.NotifyHouseEntries) { pendingHouseNotices.Clear(); return; }
        foreach (var visit in pendingHouseNotices.ToArray())
        {
            if (!Player.IsLoaded || Player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture) != visit.Actor.ContentId)
            { pendingHouseNotices.Remove(visit); continue; }
            var estate = config.Discoveries.LastOrDefault(e => e.Kind == "house.discovered" && e.Actor.ContentId == visit.Actor.ContentId && e.Address?.HouseId == visit.Address.HouseId);
            // Give the estate observer a chance to identify a newly discovered house.
            if (estate is null && now - visit.ObservedAt < TimeSpan.FromSeconds(10)) continue;
            var message = HouseEntryNotice.Format(visit, estate);
            if (message is not null) Chat.Print(message);
            pendingHouseNotices.Remove(visit);
        }
    }
    private void DrawSettings()
    {
        ImGui.TextWrapped("Tracking and enabled uploads continue when the plugin window is closed.");
        var notify = config.NotifyHouseEntries;
        if (ImGui.Checkbox("Show house-entry messages in my chat",ref notify))
        { config.NotifyHouseEntries=notify;Pi.SavePluginConfig(config); }
        ImGui.TextDisabled("Only visible to you. Never sends to FC, party, tell or public chat.");
        ImGui.TextWrapped("Example: [Equinox] You entered your FC House — HAVEN28-45. Only new interior entries trigger a message; login observations and old sync records do not.");
        var background = config.RefreshSharedInBackground;
        if (ImGui.Checkbox("Refresh shared profiles while this window is closed",ref background))
        { config.RefreshSharedInBackground=background;nextRosterRead=default;Pi.SavePluginConfig(config); }
        ImGui.TextWrapped("One shared-list check per minute while open, or also in the background if enabled. Game actions still upload only when there are pending records. Both plugins can use the same pairing key.");
        ImGui.Separator();
        DrawConnection();
    }
}
