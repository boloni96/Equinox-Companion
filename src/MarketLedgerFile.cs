using System.Text.Json;
using System.Text.Json.Serialization;

namespace EquinoxCompanion;

internal static class MarketLedgerFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static MarketLedger Read(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { return ReadSnapshot(path); }
            catch (IOException e) when (attempt < 3 && (e.HResult & 0xffff) is 32 or 33)
            {
                Thread.Sleep(150 * (attempt + 1));
            }
        }
    }

    private static MarketLedger ReadSnapshot(string path)
    {
        using var input = File.OpenRead(path);
        var ledger = JsonSerializer.Deserialize<MarketLedger>(input, Options);
        if (ledger?.Retainers == null || ledger.Income == null || ledger.Prices == null ||
            ledger.Retainers.Any(r => r?.Stock == null || r.Stock.Any(s => s == null)) ||
            ledger.Income.Any(i => i == null) || ledger.Prices.Any(p => p == null) ||
            ledger.Expenses?.Any(e => e == null) == true)
            throw new InvalidDataException("Incomplete market ledger.");
        return ledger;
    }

    private static FileStream OpenLock(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        for (var attempt = 0; ; attempt++)
        {
            try { return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 6) { Thread.Sleep(100 * (attempt + 1)); }
        }
    }

    internal static (MarketLedger Ledger, string Notice) Load(string path)
    {
        using var gate = OpenLock(path);
        var missing = false;
        Exception? invalid = null;
        try { return (Read(path), ""); }
        catch (FileNotFoundException) { missing = true; }
        catch (Exception e) when (e is JsonException or InvalidDataException) { invalid = e; }

        foreach (var suffix in new[] { ".bak", ".tmp" })
        {
            var candidate = path + suffix;
            MarketLedger recovered;
            try { recovered = Read(candidate); }
            catch (FileNotFoundException) { continue; }
            catch (Exception e) when (e is JsonException or InvalidDataException) { invalid ??= e; continue; }

            var stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(candidate, stage);
                if (missing) File.Move(stage, path);
                else File.Replace(stage, path, path + ".unreadable-" + Guid.NewGuid().ToString("N"));
            }
            finally { File.Delete(stage); }
            return (recovered, $"Market records restored from {Path.GetFileName(candidate)}.");
        }
        if (invalid != null) throw new InvalidDataException("No readable market ledger or recovery copy. " + invalid.Message, invalid);
        return (MarketLedger.Empty, "");
    }

    internal static void Save(string path, MarketLedger ledger)
    {
        using var gate = OpenLock(path);
        var stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, ledger);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(stage, path, path + ".bak");
            else File.Move(stage, path);
        }
        finally { File.Delete(stage); }
    }
}
