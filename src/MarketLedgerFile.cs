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

    private static (MarketLedger Ledger, byte[]? Repaired) Read(string path)
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

    private static (MarketLedger Ledger, byte[]? Repaired) ReadSnapshot(string path)
    {
        using var input = File.OpenRead(path);
        try { return (Parse(input), null); }
        catch (JsonException)
        {
            input.Position = 0;
            var bytes = new byte[checked((int)input.Length)];
            input.ReadExactly(bytes);
            var start = Array.FindIndex(bytes, b => b != 0);
            if (start < 0) throw new EmptyLedgerException();
            var end = Array.FindLastIndex(bytes, b => b != 0);
            if (start == 0 && end == bytes.Length - 1) throw;
            var repaired = bytes[start..(end + 1)];
            using var trimmed = new MemoryStream(repaired);
            return (Parse(trimmed), repaired);
        }
    }

    private sealed class EmptyLedgerException() : IOException("File is empty or contains only zero bytes.") { }

    private static MarketLedger Parse(Stream input)
    {
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

    internal static (MarketLedger Ledger, string Notice) Load(string path, bool allowFresh = false)
    {
        using var gate = OpenLock(path);
        var directory = Path.GetDirectoryName(path)!;
        var file = Path.GetFileName(path);
        var candidates = new[] { path, path + ".bak", path + ".tmp" }.Concat(
            Directory.EnumerateFiles(directory, file + ".*.tmp")
                .Concat(Directory.EnumerateFiles(directory, file + ".unreadable-*"))
                .OrderByDescending(File.GetLastWriteTimeUtc));
        var unreadable = new List<string>();
        Exception? invalid = null;
        foreach (var candidate in candidates)
        {
            (MarketLedger Ledger, byte[]? Repaired) snapshot;
            try { snapshot = Read(candidate); }
            catch (FileNotFoundException) { continue; }
            catch (EmptyLedgerException) { unreadable.Add(candidate); continue; }
            catch (Exception e) when (e is JsonException or InvalidDataException)
            {
                unreadable.Add(candidate);
                invalid ??= e;
                continue;
            }
            if (candidate == path && snapshot.Repaired == null) return (snapshot.Ledger, "");
            Install(path, stage =>
            {
                if (snapshot.Repaired is {} repaired) File.WriteAllBytes(stage, repaired);
                else File.Copy(candidate, stage);
            });
            return (snapshot.Ledger, $"Market records recovered from {Path.GetFileName(candidate)}.");
        }
        if (invalid != null && !allowFresh)
            throw new InvalidDataException("No complete local market ledger found. " + invalid.Message, invalid);
        if (unreadable.Count == 0) return (MarketLedger.Empty, "");
        // A later normal save rotates the .bak file, so preserve that copy too.
        if (unreadable.Contains(path + ".bak"))
            File.Copy(path + ".bak", path + ".unreadable-" + Guid.NewGuid().ToString("N"));
        Install(path, stage => File.WriteAllText(stage, JsonSerializer.Serialize(MarketLedger.Empty)));
        return (MarketLedger.Empty, "Local market tracking restarted. Previous files preserved.");
    }

    private static void Install(string path, Action<string> prepare)
    {
        var stage = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            prepare(stage);
            if (File.Exists(path)) File.Replace(stage, path, path + ".unreadable-" + Guid.NewGuid().ToString("N"));
            else File.Move(stage, path);
        }
        finally { File.Delete(stage); }
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
