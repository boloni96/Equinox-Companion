using System.Text.Json;
using EquinoxCompanion;
internal static class DiagnosticExportTests
{
    internal static async Task Run()
    {
        var folder=Path.Combine(Path.GetTempPath(),"equinox-export-"+Guid.NewGuid().ToString("N"));
        try
        {
            var rows=new List<string>{"before"};var snapshot=new{schemaVersion=7,observedDetails=rows.ToArray()};
            var path=Path.Combine(folder,"test.json");
            var task=DiagnosticExportWriter.Start(path,snapshot,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase},()=>["error"],()=>["garden"]);
            rows.Clear();rows.Add("after");
            var result=await task;
            if(result.Path!=path||result.Error is not null)throw new Exception("Diagnostic export failed");
            using var json=JsonDocument.Parse(await File.ReadAllTextAsync(path));
            if(json.RootElement.GetProperty("observedDetails")[0].GetString()!="before"||json.RootElement.GetProperty("errorLog")[0].GetString()!="error"||json.RootElement.GetProperty("recentGardenMessages")[0].GetString()!="garden"||File.Exists(path+".tmp"))throw new Exception("Export snapshot or atomic finalization failed");
            var failed=await DiagnosticExportWriter.Start(Path.Combine(path,"blocked.json"),snapshot,new(),()=>[],()=>[]);
            if(failed.Path is not null||failed.Error is null)throw new Exception("Export failure was reported as success");
            Console.WriteLine("PASS diagnostic export: copied snapshot, background log readers, complete file publication, failure result");
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
