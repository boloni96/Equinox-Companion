using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class AssignmentHomes308Tests
{
 [ModuleInitializer] internal static void Run(){
    void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS assignment homes "+label);}
    ChestStack[] Full(uint item)=>Enumerable.Range(0,175).Select(i=>new ChestStack((uint)(10000+i/25),i%25,item,1,false,1)).ToArray();
    var owners=Enumerable.Range(1,10).Select(x=>(ulong)x).ToArray();
    var inventory=owners.ToDictionary(x=>x,x=>Full((uint)(100+x)));
    foreach(var style in Enum.GetValues<AssignmentRecommendationStyle>()){
        var result=AssignmentRecommendations.Build(owners,inventory,[],[],id=>1,style);
        var applied=AssignmentRecommendations.Apply([],result.Suggestions);
        var check=AssignmentCoverage.Check(owners,inventory,applied,[],id=>1);
        Check("cleared ten full retainers covered: "+style,result.Remaining.Length==0&&check.Missing.Length==0&&check.MisplacedSlots==0&&result.Suggestions.Length==10);
        Check("shared full homes require no extra slots",result.Suggestions.All(s=>s.AdditionalSlots==0&&s.MovedStacks==0));
    }
    var crystals=new Dictionary<ulong,ChestStack[]>{[1]=[..Full(100),new(12001,0,2,8000,false,9999)],[2]=[..Full(101),new(12001,0,2,8000,false,9999)]};
    var proposal=AssignmentRecommendations.Build([1,2],crystals,[],[],id=>id==2?59u:1u);
    Check("crystals above one home capacity get shared homes",proposal.Suggestions.Count(s=>s.Category==59)==2&&proposal.Remaining.Length==0);
    Check("crystals never consume normal slots",proposal.Suggestions.Where(s=>s.Category==59).All(s=>s.AdditionalSlots==0));
    crystals[1]=[..Full(100),new(12001,0,2,4000,false,9999)];crystals[2]=[..Full(101),new(12001,0,2,4000,false,9999)];
    proposal=AssignmentRecommendations.Build([1,2],crystals,[],[],id=>id==2?59u:1u);
    Check("crystals can consolidate despite full normal inventory",proposal.Suggestions.Count(s=>s.Category==59)==1&&proposal.Remaining.Length==0);
    var unknown=new Dictionary<ulong,ChestStack[]>{[1]=[new(10000,0,999,1,false,1)],[2]=[new(10000,0,998,1,false,1)]};
    proposal=AssignmentRecommendations.Build([1,2],unknown,[],[],id=>0);
    Check("unknown categories get explicit safe homes",proposal.Suggestions.All(s=>s.Item>0)&&proposal.Remaining.Length==0);
    var rows=AssignmentRecommendations.Apply([],proposal.Suggestions);
    Check("unknown item rules do not create category zero catchall",rows.All(r=>r.Categories.Length==0));
    var existing=new[]{new RetainerAssignment(2,[],[999])};
    proposal=AssignmentRecommendations.Build([1,2],unknown,existing,[],id=>1,relieveFullHomes:true);
    rows=AssignmentRecommendations.Apply(existing,proposal.Suggestions);
    Check("explicit item homes preserved",rows.Single(r=>r.Retainer==2).Items.Contains(999)&&existing[0].Categories.Length==0);
    Check("reported remaining matches applied coverage",proposal.Remaining.Length==AssignmentCoverage.Check([1,2],unknown,rows,[],id=>1).Missing.Length);
    var twice=AssignmentRecommendations.Apply(rows,proposal.Suggestions);
    Check("applying additions is idempotent",System.Text.Json.JsonSerializer.Serialize(rows)==System.Text.Json.JsonSerializer.Serialize(twice));
 }
}
