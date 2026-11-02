using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace EquinoxCompanion;

// A data reader, not a Questionable IPC client. Preserve unknown fields for adapters.
public sealed class IndependentQuestBook
{
    public sealed record Route(string Key,string Path,JsonElement Definition);
    private readonly Dictionary<string,Route> byPath=new(StringComparer.Ordinal);
    public IReadOnlyCollection<Route> Routes=>byPath.Values;
    public string Revision { get; private set; }="";
    public static IndependentQuestBook Load(Stream stream)
    {
        using var zip=new ZipArchive(stream,ZipArchiveMode.Read,true);
        if(zip.Entries.Select(e=>e.FullName).Distinct(StringComparer.Ordinal).Count()!=zip.Entries.Count)
            throw new InvalidDataException("Duplicate pack entries");
        using var manifest=JsonDocument.Parse(Read(zip,"manifest.json",16*1024*1024));
        var root=manifest.RootElement;
        if(root.GetProperty("format").GetInt32()!=1)throw new InvalidDataException("Unsupported quest pack");
        var book=new IndependentQuestBook{Revision=root.GetProperty("revision").GetString()??""};
        foreach(var row in root.GetProperty("routes").EnumerateArray())
        {
            var path=row.GetProperty("path").GetString()??throw new InvalidDataException("Route path missing");
            if(!path.StartsWith("QuestPaths/",StringComparison.Ordinal)||path.Split('/').Contains(".."))
                throw new InvalidDataException("Invalid route path");
            var bytes=Read(zip,path,4*1024*1024);
            if(!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),row.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Route integrity mismatch: "+path);
            var offset=bytes.AsSpan().StartsWith(new byte[]{0xef,0xbb,0xbf})?3:0;
            using var definition=JsonDocument.Parse(bytes.AsMemory(offset));
            if(definition.RootElement.GetProperty("QuestSequence").ValueKind!=JsonValueKind.Array)
                throw new InvalidDataException("Invalid quest sequences");
            book.byPath.Add(path,new(row.GetProperty("key").GetString()??"",path,definition.RootElement.Clone()));
        }
        if(book.byPath.Count!=root.GetProperty("routeCount").GetInt32())throw new InvalidDataException("Incomplete quest pack");
        return book;
    }
    public Route[] Find(string key)=>byPath.Values.Where(r=>r.Key==key).ToArray();
    // Unknown interactions must be handled explicitly; data presence never enables execution.
    public static string[] MissingHandlers(Route route,IReadOnlySet<string> handlers)=>
        route.Definition.GetProperty("QuestSequence").EnumerateArray()
            .SelectMany(s=>s.TryGetProperty("Steps",out var steps)?steps.EnumerateArray().ToArray():[])
            .Select(s=>s.TryGetProperty("InteractionType",out var kind)?kind.GetString()??"<missing>":"<missing>")
            .Where(k=>!handlers.Contains(k)).Distinct().OrderBy(k=>k,StringComparer.Ordinal).ToArray();
    private static byte[] Read(ZipArchive zip,string name,long limit)
    {
        var entry=zip.GetEntry(name)??throw new InvalidDataException("Missing pack entry: "+name);
        if(entry.Length>limit)throw new InvalidDataException("Pack entry too large");
        using var input=entry.Open();using var output=new MemoryStream();input.CopyTo(output);
        if(output.Length>limit)throw new InvalidDataException("Pack entry exceeds limit");
        return output.ToArray();
    }
}
