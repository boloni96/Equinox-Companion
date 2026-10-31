namespace EquinoxCompanion;

// One availability rule for both designs, commands and minimized launchers.
public static class OptionalToolAvailability
{
    public static bool Available(Configuration config, string name) => name switch
    {
        "Equinox Market" or "Equinox Seller" => config.Seller.Enabled,
        "Equinox Organizer" or "Retainer Retriever" => config.EnableRetriever,
        "Gil Trader" => config.EnableGilTrader,
        "QuickLoot" => config.EnableQuickLoot,
        "Keybound Brawler" => config.EnableKeyboundBrawler,
        "Helper" => config.EnableFollowThem,
        "Camera" => config.EnableCameraTools == true,
        "Coffer markers" => config.EnableCofferMarkers,
        _ => true // Core windows have no optional-tool switch.
    };

    public static bool InitializeCamera(Configuration config)
    {
        if (config.EnableCameraTools.HasValue) return false;
        // Existing camera users retain their saved setup; new installations opt in.
        config.EnableCameraTools = config.RememberCameraHeight || config.PreferredCameraHeight != 0 ||
            config.CameraShortcut.Key != "None";
        return true;
    }
}
