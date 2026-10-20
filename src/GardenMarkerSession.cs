namespace EquinoxCompanion;

// Presentation only. GardenMenu.Matches still bounds action/callback evidence.
// Bind a fresh numbered menu to the native planting window, then retain its
// identity while that same window remains open and live context still matches.
public sealed class GardenMarkerSession
{
    private GardenMenu? boundMenu;
    private nint boundAddon;
    private uint boundAddonId;
    public bool Bound => boundMenu is not null;
    public uint AddonId => boundAddonId;

    public void Reset() { boundMenu = null; boundAddon = 0; boundAddonId = 0; }

    public bool Matches(GardenMenu? menu, GardenSnapshot? current, DateTimeOffset now,
        nint addon, uint addonId, bool nativeOpen, GardenSnapshot? live = null)
    {
        if (!nativeOpen || addon == 0 || addonId == 0 || menu is null)
        { Reset(); return false; }

        if (!ReferenceEquals(menu, boundMenu) || addon != boundAddon || addonId != boundAddonId)
        {
            Reset();
            if (menu.EmptyLocation() is null || !menu.Matches(now, current)) return false;
            boundMenu = menu; boundAddon = addon; boundAddonId = addonId;
        }

        // The game may clear its hard target while this same native window stays
        // open. Its retained bed is display-only: fresh actor/address context is
        // still required and an explicitly different target never inherits it.
        // Initial binding above still needs the original, fresh numbered target.
        live ??= current;
        return now >= menu.OpenedAt && live is not null &&
            now >= live.ObservedAt && now - live.ObservedAt <= TimeSpan.FromMilliseconds(500) &&
            live.Actor == menu.Target.Actor && live.Address is not null &&
            live.Address == menu.Target.Address &&
            (live.TargetId is null || live.TargetId == menu.Target.TargetId);
    }
}
