using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace EquinoxCompanion;

// PlayerState clears its row references during logout/login. Missing world data
// pauses live following; it must never replace or invalidate the saved adventure.
public static class AdventureWorldName
{
    public static string Read(bool characterReady, RowRef<World> reference)
    {
        if (!characterReady || reference.RowId == 0 || reference.ValueNullable is not {} world || !world.IsPublic)
            return "";
        return world.Name.ToString();
    }
}
