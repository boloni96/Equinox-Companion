using System.Text.Json;
namespace EquinoxCompanion;

public sealed class ErrorJournal
{
    private readonly object gate = new();
    private readonly Dictionary<string, DateTimeOffset> recent = [];
    public string FilePath { get; }
    public string? WriteFailure { get; private set; }
    public ErrorJournal(string directory) => FilePath = Path.Combine(directory, "errors", "equinox-errors.jsonl");
    // Callers supply fixed diagnostic descriptions, never raw exceptions, requests or credentials.
    public void Record(string area, string reason, string? eventId = null, string? kind = null, string? exceptionType = null)
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            var identity = area + reason + eventId + exceptionType;
            if (recent.TryGetValue(identity, out var last) && now - last < TimeSpan.FromMinutes(5)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= 1_000_000)
                    File.Move(FilePath, FilePath + ".previous", true);
                var entry = new { at = now, version = typeof(ErrorJournal).Assembly.GetName().Version?.ToString(), area, reason, eventId, kind, exceptionType };
                File.AppendAllText(FilePath, JsonSerializer.Serialize(entry) + Environment.NewLine);
                if (recent.Count >= 2000) recent.Clear();
                recent[identity] = now;
                WriteFailure = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { WriteFailure = "Could not write the error log (" + ex.GetType().Name + ")."; }
        }
    }
    public string[] Snapshot()
    {
        lock (gate)
        {
            try
            {
                return new[] { FilePath + ".previous", FilePath }.Where(File.Exists)
                    .SelectMany(File.ReadLines).TakeLast(2000).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { WriteFailure = "Could not read the error log (" + ex.GetType().Name + ")."; return []; }
        }
    }
}
