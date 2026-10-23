using System.Runtime.CompilerServices;
using System.Text.Json;
using EquinoxCompanion;
internal static class Appearance239Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var checks=0;
        void Check(bool ok,string name){checks++;if(!ok)throw new Exception("Appearance: "+name);}
        var migrated=JsonSerializer.Deserialize<Configuration>("{\"WelcomeVersion\":\"0.5.1.238\"}")!;
        Check(!migrated.Appearance.GoddessBlue&&AppearancePolicy.NeedsChoice(migrated.Appearance),"old config retains current style until choice");
        Check(AppearancePolicy.NeedsChoice(new Configuration().Appearance),"new install gets design choice");
        migrated.Appearance=AppearancePolicy.Choose(true);migrated.CompanionSection="person:goddess";
        var reloaded=JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(migrated.Snapshot()))!;
        Check(reloaded.Appearance.GoddessBlue&&!AppearancePolicy.NeedsChoice(reloaded.Appearance),"new design survives save, snapshot and reload");
        Check(reloaded.CompanionSection=="person:goddess","menu section survives reload");
        Check(AppearancePolicy.Dismiss(reloaded.Appearance).GoddessBlue,"closing preview keeps new choice");
        Check(!AppearancePolicy.Dismiss(new()).GoddessBlue&&!AppearancePolicy.NeedsChoice(AppearancePolicy.Dismiss(new())),"closing first preview keeps current and prevents repeated prompt");
        Check(!AppearancePolicy.Choose(false).GoddessBlue&&!AppearancePolicy.NeedsChoice(AppearancePolicy.Choose(false)),"explicit current choice remembered");
        Check(AppearancePolicy.SelectSection("person:goddess",["housing","person:goddess","settings"])=="person:goddess","paired person retained");
        Check(AppearancePolicy.SelectSection("person:removed",["housing","settings"])=="housing","removed person has safe fallback");
        foreach(var font in new[]{13f,17f,25.5f,34f})foreach(var width in new[]{280f,340f,490f,740f,1100f})
        {
            var actions=font*12;var gap=font*.35f;
            var layout=CompactLayout.Navigation(width,actions,gap,font);
            Check(layout.SelectorWidth>0&&layout.SelectorWidth<=width,"selector stays within window");
            Check(layout.SelectorWidth<=font*10,"large window does not stretch selector");
            Check(layout.WrapActions||layout.SelectorWidth+actions+gap<=width+.01f,"cancel controls fit or wrap at font scaling");
        }
        var done=new AdventureTask("shared","Tend","Garden",true,false);
        var waiting=new AdventureTask("event","Event","Event",false,false);
        var stop=new AdventureStop("World","","",0,[done,waiting]);
        var board=new AdventureBoard("board","generation","","multi","Our adventure",[stop,stop with {Tasks=[done]}],0,1);
        var progress=AdventureProgress.Count(board);
        Check(progress.Done==1&&progress.Total==2,"shared task counted once across stops");
        Check(AdventureProgress.Count(board with {Index=2})==progress,"moving past stops does not fabricate completion");
        Check(AdventureProgress.Count(board with {Stops=[stop,stop with {Tasks=[done with {Done=false}]}]}).Done==0,"conflicting duplicate confirmation is conservative");
        Check(AdventureProgress.Count(board with {Stops=[]}).Total==0,"empty board is not complete activity");
        Check(AdventureProgress.Count(board with {Stops=[stop with {Tasks=[done,waiting with {Done=true}]}]}).Done==2,"confirmation updates total");
        Console.WriteLine($"PASS {checks} appearance and compact Adventure checks");
    }
}
