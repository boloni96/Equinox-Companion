namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private bool OptionalToolAvailable(string name) => OptionalToolAvailability.Available(config, name);

    private bool RequireOptionalTool(string name)
    {
        if (OptionalToolAvailable(name)) return true;
        var setting = name switch { "Equinox Market" or "Equinox Seller" => "Equinox Market & Seller", "Camera" => "camera tools", "Helper" => "Equinox Helper", _ => name };
        PrintEquinox($"{name} is disabled. Enable {setting} in Settings → Optional tools.");
        return false;
    }

    private void ForgetOptionalLauncher(string name)
    {
        minimizedLaunchers.Remove(name);
        if (pendingLauncher == name) pendingLauncher = null;
        if (draggedLauncher == name) draggedLauncher = null;
    }

    private void HideDisabledOptionalWindows()
    {
        if (!config.Seller.Enabled)
        {
            equinoxMarketWindow.IsOpen = false;
            sellerWindow.IsOpen = false;
            ForgetOptionalLauncher("Equinox Market");
            ForgetOptionalLauncher("Equinox Seller");
            openMarketSettings = openMarketSelling = false;
        }
        if (!config.EnableGilTrader)
        {
            gilTraderWindow.IsOpen = false;
            ForgetOptionalLauncher("Gil Trader");
        }
        if (!config.EnableFollowThem) helperWindowOpen = followThemSelectTab = false;
        if (!config.EnableQuickLoot) quickLootSelectTab = false;
    }
}
