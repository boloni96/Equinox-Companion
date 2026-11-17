namespace EquinoxCompanion;

// Serialize into a sibling file. Never replace the last good save until serialization
// and the durable disk flush have both completed successfully.
internal static class AtomicConfigurationFile
{
    internal static void Write(string path,Action<Stream> serialize)
    {
        var directory=Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary=Path.Combine(directory,Path.GetFileName(path)+".companion-"+Guid.NewGuid().ToString("N")+".tmp");
        try{
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.SequentialScan)){
                serialize(stream);stream.Flush(true);
            }
            if(File.Exists(path))File.Replace(temporary,path,path+".companion-backup");
            else File.Move(temporary,path);
        }
        finally{if(File.Exists(temporary))try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
