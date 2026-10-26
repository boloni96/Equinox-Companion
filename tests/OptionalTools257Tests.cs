using EquinoxCompanion;
using System.Text.Json;

internal static class OptionalTools257Tests
{
    public static void Run()
    {
        var count = 0;
        void Check(bool ok, string message) { count++; if (!ok) throw new Exception(message); }
        Configuration Reload(Configuration c) => JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(c))!;
        var fresh = new Configuration();
        Check(OptionalToolAvailability.InitializeCamera(fresh), "New installation initializes camera availability");
        foreach (var tool in new[] { "Equinox Market", "Equinox Seller", "Gil Trader", "QuickLoot", "Helper", "Camera", "Coffer markers" })
            Check(!OptionalToolAvailability.Available(fresh, tool), "Fresh installation opts out of " + tool);
        foreach (var core in new[] { "Companion", "Adventure", "Planting", "Fashion Report", "Housing bids", "Do I already have it?", "Where is it cheapest?", "Testing", "Settings" })
            Check(OptionalToolAvailability.Available(fresh, core), "Optional switches do not hide " + core);

        var configured = new Configuration { Seller = new() { Enabled = true, OpenAtBell = true, ManualAllowOtherQuality = true, UndercutGil = 17 }, EnableGilTrader = true };
        configured = Reload(configured);
        Check(OptionalToolAvailability.Available(configured, "Equinox Market") && OptionalToolAvailability.Available(configured, "Equinox Seller"), "Saved Market opt-in enables both windows");
        Check(OptionalToolAvailability.Available(configured, "Gil Trader"), "Saved Trader opt-in persists");
        configured.Seller.Enabled = false;
        Check(!OptionalToolAvailability.Available(configured, "Equinox Seller") && configured.Seller.OpenAtBell, "Bell preference alone cannot make disabled Seller available");
        Check(!OptionalToolAvailability.Available(configured, "Equinox Market") && OptionalToolAvailability.Available(configured, "Gil Trader"), "Disabling Market is independent of Trader");
        configured = Reload(configured);
        Check(!configured.Seller.Enabled && configured.Seller.ManualAllowOtherQuality && configured.Seller.UndercutGil == 17, "Disabled Market survives restart with manual and pricing preferences kept");

        foreach (var legacy in new[] {
            new Configuration { RememberCameraHeight = true },
            new Configuration { PreferredCameraHeight = -.702f },
            new Configuration { CameraShortcut = new() { Key = "F10", Ctrl = true } }
        })
        {
            var before = Reload(legacy);
            Check(OptionalToolAvailability.InitializeCamera(legacy) && OptionalToolAvailability.Available(legacy, "Camera"), "Existing camera setup remains available after update");
            Check(before.RememberCameraHeight == legacy.RememberCameraHeight && before.PreferredCameraHeight == legacy.PreferredCameraHeight && before.CameraShortcut.Label == legacy.CameraShortcut.Label, "Migration does not alter camera activation, value or keybind");
            legacy.EnableCameraTools = false;
            var reloaded = Reload(legacy);
            Check(!OptionalToolAvailability.InitializeCamera(reloaded) && !OptionalToolAvailability.Available(reloaded, "Camera"), "Explicit opt-out survives restart despite retained camera preferences");
            Check(reloaded.PreferredCameraHeight == before.PreferredCameraHeight && reloaded.CameraShortcut.Label == before.CameraShortcut.Label, "Disabled camera retains saved offset and shortcut");
            reloaded.EnableCameraTools = true;
            Check(OptionalToolAvailability.Available(reloaded, "Camera"), "Camera can be made available again without new setup");
        }
        Check(!OptionalToolAvailability.InitializeCamera(Reload(fresh)), "Fresh opt-out is initialized only once");
        Console.WriteLine($"Optional tool availability and upgrade checks passed: {count}.");
    }
}
