using System.Text.Json;
namespace EquinoxCompanion;
public sealed record DiagnosticExportResult(string? Path,string? Error);

// No game, UI or plugin services run on this worker. The caller supplies an
// immutable managed snapshot and thread-safe log readers. Work may finish after
// plugin unload without accessing disposed Dalamud services or blocking unload.
public static class DiagnosticExportWriter
{
    public static Task<DiagnosticExportResult> Start(string path,object snapshot,JsonSerializerOptions options,Func<string[]> errors,Func<string[]> gardenMessages)
        =>Task.Run(()=>Write(path,snapshot,options,errors,gardenMessages));
    private static DiagnosticExportResult Write(string path,object snapshot,JsonSerializerOptions options,Func<string[]> errors,Func<string[]> gardenMessages)
    {
        var temporary=path+".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var writer=new Utf8JsonWriter(stream,new JsonWriterOptions{Indented=options.WriteIndented}))
            {
                writer.WriteStartObject();
                // Stream properties without creating a second large JSON DOM/string.
                foreach(var property in snapshot.GetType().GetProperties())
                {
                    writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(property.Name)??property.Name);
                    JsonSerializer.Serialize(writer,property.GetValue(snapshot),property.PropertyType,options);
                }
                writer.WritePropertyName("errorLog");JsonSerializer.Serialize(writer,errors(),options);
                writer.WritePropertyName("recentGardenMessages");JsonSerializer.Serialize(writer,gardenMessages(),options);
                writer.WriteEndObject();writer.Flush();
            }
            File.Move(temporary,path);
            return new(path,null);
        }
        catch(Exception ex)
        {
            try{File.Delete(temporary);}catch{/* Retain the original failure. */}
            return new(null,ex.GetType().Name);
        }
    }
}
