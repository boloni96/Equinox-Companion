using System.Text.Json;
namespace EquinoxCompanion;

// Local research evidence only. Never included in CompanionSync or shared roster payloads.
public sealed class GardenMessageJournal(string directory)
{
    private readonly object gate = new();
    public string FilePath { get; } = Path.Combine(directory,"diagnostics","garden-messages.jsonl");
    public string? WriteFailure { get; private set; }
    public void Record(Diagnostic item)
    {
        if(item.Kind is not ("garden.chatObservation" or "garden.logTextObservation" or "garden.menuObservation"))return;
        lock(gate)try
        {
            var line=JsonSerializer.Serialize(new {version=typeof(GardenMessageJournal).Assembly.GetName().Version?.ToString(),item.ObservedAt,item.Kind,item.Data});
            if(System.Text.Encoding.UTF8.GetByteCount(line)>16384)return;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if(File.Exists(FilePath)&&new FileInfo(FilePath).Length>=256000)File.Move(FilePath,FilePath+".previous",true);
            File.AppendAllText(FilePath,line+Environment.NewLine);WriteFailure=null;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){WriteFailure="Garden message history unavailable ("+e.GetType().Name+").";}
    }
    public string[] Snapshot()
    {
        lock(gate)try{return new[]{FilePath+".previous",FilePath}.Where(File.Exists).SelectMany(File.ReadLines).TakeLast(400).ToArray();}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){WriteFailure="Garden message history unavailable ("+e.GetType().Name+").";return [];}
    }
}
