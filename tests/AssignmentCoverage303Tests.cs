using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class AssignmentCoverage303Tests
{
 [ModuleInitializer] internal static void Run(){
    void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS assignment audit "+label);}
    ChestStack S(uint item,int qty=1,bool hq=false,int slot=0)=>new(10000,slot,item,qty,hq,999);
    var inventory=new Dictionary<ulong,ChestStack[]>{[1]=[S(110,2),S(110,3,true,1),S(120),new(11000,0,130,1,false,1),new(12002,0,140,1,false,1)],[2]=[]};
    var result=AssignmentCoverage.Check([1,2,3],inventory,[new(99,[1],[])],[],id=>1);
    Check("ignores other characters, equipment and market listings",result.Missing.Length==2&&result.Missing.Sum(x=>x.Slots)==3);
    Check("item types merge quality but preserve quantities and slots",result.Missing.Single(x=>x.Item==110) is {Slots:2,Quantity:5});
    Check("missing inventories are counted as unknown",result.CheckedRetainers==2&&result.TotalRetainers==3);
    var plan=new RetrievalPreset("Group",[new(120,"Item",1)],PlanId:"group");
    result=AssignmentCoverage.Check([1,2],inventory,[new(1,[1],[]),new(2,[],[110],["group"])],[plan],id=>1);
    Check("explicit and plan homes override category homes",result.Missing.Length==0&&result.MisplacedSlots==3);
    result=AssignmentCoverage.Check([1,2],inventory,[new(2,[],[],["deleted"])],[plan],id=>1);
    Check("deleted plan does not hide gaps",result.Missing.Length==2);
    var recommendation=AssignmentRecommendations.Build([1,2],inventory,[],[],id=>1);
    Check("prefers existing stock without requiring moves",recommendation.Suggestions.Single().Retainer==1&&recommendation.Suggestions[0].AdditionalSlots==0);
    var full=Enumerable.Range(0,175).Select(i=>new ChestStack((uint)(10000+i/25),i%25,(uint)(1000+i),1,false,1)).ToArray();
    var tight=new Dictionary<ulong,ChestStack[]>{[1]=full,[2]=Enumerable.Range(0,174).Select(i=>new ChestStack((uint)(10000+i/25),i%25,5000,1,false,1)).ToArray()};
    recommendation=AssignmentRecommendations.Build([1,2],tight,[new(1,[2],[]),new(2,[2],[])],[],id=>id==1000?1u:2u);
    Check("does not promise space on full homes",recommendation.Suggestions.Length==1&&recommendation.Suggestions[0].Retainer==1&&recommendation.Suggestions[0].AdditionalSlots==0);
    // Large categories can retain shared homes instead of silently being omitted.
    recommendation=AssignmentRecommendations.Build([1,2],tight,[],[],id=>1);
    Check("covers split categories across full homes",recommendation.Suggestions.Length==2&&recommendation.Remaining.Length==0);
    bool refused=false;try{AssignmentRecommendations.Build([1,2,3],inventory,[],[],id=>1);}catch(InvalidOperationException){refused=true;}
    Check("recommendation requires complete inventory coverage",refused);
    var roomy=new Dictionary<ulong,ChestStack[]>{[1]=[S(110,999),S(120,999,true,1),S(130),S(131),S(132)],[2]=[]};
    var existing=new[]{new RetainerAssignment(2,[],[110]),new RetainerAssignment(1,[2],[])};
    recommendation=AssignmentRecommendations.Build([1,2],roomy,existing,[],id=>id>=130?2u:1u,AssignmentRecommendationStyle.MoreSpace);
    Check("reserves existing incoming stock before proposing additions",recommendation.Suggestions.Single().Retainer==2&&recommendation.Suggestions.Single().FreeAfter==173);
    Check("recommendations preserve input rules and inventory",existing[0].Categories.Length==0&&roomy[2].Length==0);
    var saved=AssignmentRecommendations.Copy([new(1,[1],[110],["group"])]);
    var copied=AssignmentRecommendations.Copy(saved);copied[0].Categories[0]=7;
    Check("saved setups do not share mutable selection arrays",saved[0].Categories[0]==1);
    var serialized=System.Text.Json.JsonSerializer.Serialize(new AssignmentSetup("Mine",saved,true));
    var restored=System.Text.Json.JsonSerializer.Deserialize<AssignmentSetup>(serialized)!;
    Check("saved setup round trip preserves plans and enabled state",restored.Enabled&&restored.Assignments[0].Plans!.Single()=="group");
 }
}
