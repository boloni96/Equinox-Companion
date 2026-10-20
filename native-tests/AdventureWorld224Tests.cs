using System.Runtime.CompilerServices;
using EquinoxCompanion;
using Lumina.Excel;
using Lumina.Excel.Sheets;

internal static class AdventureWorld224Tests
{
    [ModuleInitializer]
    internal static void Run()
    {
        RowRef<World> cleared = default;
        var previousFailure = false;
        try { _ = cleared.Value; }
        catch (InvalidOperationException) { previousFailure = true; }
        if (!previousFailure) throw new Exception("The fixture must reproduce the reported cleared-world exception.");

        // Exercise Lumina's actual row references, including a retained numeric
        // world ID with no backing sheet during the character transition.
        foreach (var reference in new[] { cleared, new RowRef<World>(null, 34), new RowRef<World>(null, 65535) })
        {
            if (AdventureWorldName.Read(false, reference) != "")
                throw new Exception("Logged-out/loading characters must not report a current world.");
            if (AdventureWorldName.Read(true, reference) != "")
                throw new Exception("An unavailable world must pause following even if the character loaded first.");
        }
        Console.WriteLine("Adventure cleared-world regression checks passed: 7.");
    }
}
