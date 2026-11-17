using System.Text;
using Newtonsoft.Json;
namespace EquinoxCompanion;
internal static class ConfigurationFile
{
    internal static void Save(string path,Configuration configuration)
    {
        AtomicConfigurationFile.Write(path,stream=>{
            using var text=new StreamWriter(stream,new UTF8Encoding(false),65536,leaveOpen:true);
            using var writer=new JsonTextWriter(text){CloseOutput=false,Formatting=Formatting.None};
            // Match Dalamud's config format, but without SerializeObject/StringBuilder/WriteAllText.
            var serializer=JsonSerializer.Create(new JsonSerializerSettings{
                TypeNameAssemblyFormatHandling=TypeNameAssemblyFormatHandling.Simple,
                TypeNameHandling=TypeNameHandling.Objects,
            });
            serializer.Serialize(writer,configuration);writer.Flush();text.Flush();
        });
    }
    internal static Configuration? Load(string path)
    {
        if(!File.Exists(path))return null;
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.SequentialScan);
        using var text=new StreamReader(stream,Encoding.UTF8,true,65536);
        using var reader=new JsonTextReader(text);
        // Concrete typed loading also reads existing Dalamud files and ignores obsolete
        // assembly-version metadata after a plugin update, without loading a giant string.
        return JsonSerializer.Create(new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None}).Deserialize<Configuration>(reader);
    }
}
