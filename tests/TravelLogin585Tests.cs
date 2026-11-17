using EquinoxCompanion;
using System.Runtime.CompilerServices;

internal static class TravelLogin585Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var gate = new TravelLoginListGate();
        var start = DateTimeOffset.UnixEpoch;
        bool Ready(int row, int ms) => gate.Ready(row, start.AddMilliseconds(ms));
        if (Ready(4,0) || Ready(-1,500) || Ready(4,1000) || Ready(4,2000) ||
            Ready(3,3000) || Ready(3,4000) || !Ready(3,5000) || Ready(3,7000))
            throw new Exception("Login must wait for a continuous stable destination row.");
        if (!TravelLoginListGate.IsNamedLoginPrompt("Logging in with Meowt Nolobi.\n\nYour character is visiting.", "Meowt Nolobi") ||
            TravelLoginListGate.IsNamedLoginPrompt("Cancel login for Meowt Nolobi?", "Meowt Nolobi") ||
            TravelLoginListGate.IsNamedLoginPrompt("Logging in with Another Character.", "Meowt Nolobi"))
            throw new Exception("Only the requested character login prompt can be dismissed.");
        Console.WriteLine("PASS travel585: settling, row changes and stale login prompt identity");
    }
}
