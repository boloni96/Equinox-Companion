using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class AssignmentRelief304Tests
{
 [ModuleInitializer] internal static void Run(){
    void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS assignment capacity "+label);}
    var full=Enumerable.Range(0,175).Select(i=>new ChestStack((uint)(10000+i/25),i%25,500,1,false,1)).ToArray();
    var inventory=new Dictionary<ulong,ChestStack[]>{[1]=[new(10000,0,100,1,false,1)],[2]=full,[3]=[]};
    var rows=new[]{new RetainerAssignment(2,[1,2],[])};
    uint Category(uint id)=>id==100?1u:2u;
    var gaps=AssignmentRecommendations.Build([1,2,3],inventory,rows,[],Category);
    Check("missing-only preserves assigned category roles",gaps.Suggestions.Length==0&&gaps.ExistingUnplacedSlots==1);
    var relief=AssignmentRecommendations.Build([1,2,3],inventory,rows,[],Category,AssignmentRecommendationStyle.MoreSpace,true);
    Check("full category home gets an optional additional home",relief.Suggestions.Single() is {Category:1,CapacityRelief:true}&&relief.Suggestions.Single().Retainer!=2);
    Check("existing homes are preserved in proposal input",rows[0].Retainer==2&&rows[0].Categories.SequenceEqual(new uint[]{1,2}));
    var partial=full.ToArray();partial[0]=new(10000,0,100,700,false,999);
    var split=new Dictionary<ulong,ChestStack[]>{[1]=[new(10000,0,100,500,false,999)],[2]=partial,[3]=partial.ToArray()};
    var splitHomes=new[]{new RetainerAssignment(2,[1,2],[]),new RetainerAssignment(3,[1,2],[])};
    var fits=AssignmentRecommendations.Build([1,2,3],split,splitHomes,[],Category,AssignmentRecommendationStyle.MoreSpace,true);
    Check("combines partial-stack capacity across assigned homes",fits.ExistingUnplacedSlots==0&&fits.Suggestions.Length==0);
    var explicitHome=new[]{new RetainerAssignment(2,[2],[100])};
    relief=AssignmentRecommendations.Build([1,2,3],inventory,explicitHome,[],Category,AssignmentRecommendationStyle.MoreSpace,true);
    Check("capacity relief cannot override explicit item homes",relief.Suggestions.Length==0&&relief.ExistingUnplacedSlots==1);
 }
}
