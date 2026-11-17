using EquinoxCompanion;
using System.Runtime.CompilerServices;
using System.Text;
internal static class ConfigurationMemory568Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS config memory "+name);}
        var directory=Path.Combine(Path.GetTempPath(),"equinox-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"config.json");
        void Save(string text)=>AtomicConfigurationFile.Write(path,s=>s.Write(Encoding.UTF8.GetBytes(text)));
        try{
            Save("first");Check("first save succeeds",File.ReadAllText(path)=="first");
            try{AtomicConfigurationFile.Write(path,s=>{s.WriteByte(123);throw new OutOfMemoryException("simulated serialization failure");});}catch(OutOfMemoryException){}
            Check("failed serialization preserves previous save",File.ReadAllText(path)=="first");
            Check("failed temporary file removed",Directory.GetFiles(directory,"*.tmp").Length==0);
            Save("second");Check("atomic replacement keeps previous backup",File.ReadAllText(path)=="second"&&File.ReadAllText(path+".companion-backup")=="first");
            Save("third");Check("later saves rotate backup",File.ReadAllText(path+".companion-backup")=="second");
            AtomicConfigurationFile.Write(path,s=>{var block=new byte[8192];for(var i=0;i<2048;i++)s.Write(block);});
            Check("large save streams without full document buffer",new FileInfo(path).Length==16*1024*1024);
            var configuration=new Configuration{EnableWorkshop=true,WorkshopManualDelivery=true,WorkshopQueues=[new(){Character=123,World=45,House="estate",Items=[new(21792,"Bow",4)],Completed=1}],RetrievalPresets=[new("Supplies",[new(100,"Material",30)])]};
            var legacy=Newtonsoft.Json.JsonConvert.SerializeObject(configuration,Newtonsoft.Json.Formatting.Indented,new Newtonsoft.Json.JsonSerializerSettings{TypeNameHandling=Newtonsoft.Json.TypeNameHandling.Objects,TypeNameAssemblyFormatHandling=Newtonsoft.Json.TypeNameAssemblyFormatHandling.Simple});
            File.WriteAllText(path,legacy);
            var loaded=ConfigurationFile.Load(path)!;
            Check("streaming loader reads legacy Dalamud format",loaded.EnableWorkshop&&loaded.WorkshopQueues.Single().Completed==1&&loaded.RetrievalPresets.Single().Items.Single().Quantity==30);
            ConfigurationFile.Save(path,loaded);
            var legacyRead=Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(File.ReadAllText(path),new Newtonsoft.Json.JsonSerializerSettings{TypeNameHandling=Newtonsoft.Json.TypeNameHandling.Objects})!;
            Check("streamed file remains compatible with Dalamud",legacyRead.WorkshopManualDelivery&&legacyRead.WorkshopQueues.Single().Items.Single().Quantity==4);
            Check("legacy file retained as backup",File.ReadAllText(path+".companion-backup")==legacy);
            var writer=new LatestSnapshotWriter<byte[]>(_=>throw new OutOfMemoryException(),retainFailedSnapshot:false);
            try{writer.Flush(new byte[1024]);}catch(IOException){}
            var pending=typeof(LatestSnapshotWriter<byte[]>).GetField("pending",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(writer);
            Check("failed snapshot released while failure stays visible",pending==null&&writer.Failure is OutOfMemoryException);
        }finally{Directory.Delete(directory,true);}
    }
}
