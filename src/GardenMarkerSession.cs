namespace EquinoxCompanion;

// Presentation only. GardenMenu.Matches still bounds action/callback evidence.
// Bind a fresh numbered menu to the native planting window, then retain its
// identity while that same window remains open and live context still matches.
public sealed class GardenMarkerSession
{
    private GardenMenu? boundMenu;
    private nint boundAddon;
    private uint boundAddonId;

    public void Reset() { boundMenu = null; boundAddon = 0; boundAddonId = 0; }

    public bool Matches(GardenMenu? menu, GardenSnapshot? current, DateTimeOffset now,
        nint addon, uint addonId, bool nativeOpen)
    {
        if (!nativeOpen || addon == 0 || addonId == 0 || menu is null)
        { Reset(); return false; }

        if (!ReferenceEquals(menu, boundMenu) || addon != boundAddon || addonId != boundAddonId)
        {
            Reset();
            if (menu.EmptyLocation() is null || !menu.Matches(now, current)) return false;
            boundMenu = menu; boundAddon = addon; boundAddonId = addonId;
        }

        // Waiting in a picker does not expire the display. Lost/stale context,
        // changing character/house/target, and a closed window still hide it.
        return now >= menu.OpenedAt && current is not null &&
            now >= current.ObservedAt && now - current.ObservedAt <= TimeSpan.FromSeconds(2) &&
            current.Actor == menu.Target.Actor && current.Address is not null &&
            current.Address == menu.Target.Address && current.TargetId == menu.Target.TargetId;
    }
}
