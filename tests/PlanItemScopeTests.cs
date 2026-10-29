using System.Runtime.CompilerServices;
using System.Text.Json;
using EquinoxCompanion;

internal static class PlanItemScopeTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Check(bool ok,string message){if(!ok)throw new Exception("Plan scope: "+message);}
        static uint Category(uint id)=>id==200?20u:10u;
        RetrievalEntry[] listed=[new(100,"Ore",7),new(200,"Housing",9),new(2,"Crystal",12),new(300,"Protected",1)];
        var filtered=PlanItemScope.Filter(listed,true,[10u],Category);
        Check(filtered.Select(e=>e.ItemId).SequenceEqual(new uint[]{100,2,300}),"category filters saved list");
        Check(PlanItemScope.Filter(listed,true,[],Category).Length==0,"empty enabled filter denies all");
        Check(PlanItemScope.Filter(listed,false,[],Category).Length==4,"disabled filter preserves list");
        Check(PlanItemScope.Filter([],true,[10u],Category).Length==0,"category alone cannot create items");
        var saved=new RetrievalPreset("Scope",listed,SendCategories:[],FilterPlanCategories:true);
        var restored=JsonSerializer.Deserialize<RetrievalPreset>(JsonSerializer.Serialize(saved))!;
        Check(restored.FilterPlanCategories==true&&restored.Items.Length==4,"saved empty filter retains hidden items");
        ChestStack[] bags=[new(0,0,100,20,false,999),new(0,1,100,10,true,999),new(0,2,200,30,false,999),new(0,3,999,40,false,999),new(0,4,300,8,false,999),new(2001,0,2,100,false,9999)];
        var keep=new EntrustSelection(new(){{100,25},{2,95}},new(),[300]);
        var amounts=keep.Amounts(filtered.Select(e=>e.ItemId),bags,RetrievalQuality.Any,Category,[]);
        Check(amounts.Count==2&&amounts[100]==5&&amounts[2]==5,"Keep shared across quality; protected and unlisted denied");
        StorageRoute[] initial=[new(0,StorageAutoRouting.AllRetainers,[],amounts.Keys.ToArray(),Amounts:amounts)];
        var rules=PlanItemScope.Restrict(initial,filtered.Select(e=>e.ItemId),Category);
        var inventories=new Dictionary<ulong,ChestStack[]>{{0,bags},{1,[new(10000,0,0,0,false,999),new(12001,0,2,9999,false,9999)]},{2,[new(10000,0,0,0,false,999),new(12001,0,2,0,false,9999)]}};
        RetainerAssignment[] homes=[new(1,[10,20],[]),new(2,[10,20],[999,200])];
        var assigned=StorageAssignments.Apply(rules,homes,inventories,Category);
        Check(assigned.All(r=>r.Items.All(id=>id==100||id==2)),"broad assignments cannot add bag items");
        var result=StorageRouting.Build(new("Scope",1,assigned,0),inventories,Category,true,true);
        Check(result.Moves.Length>0&&result.Moves.All(m=>m.Move.Source.Item==100||m.Move.Source.Item==2),"only listed permitted items routed");
        Check(result.Moves.Where(m=>m.Move.Source.Item==100).Sum(m=>m.Move.Quantity)==5,"HQ/NQ combined quantity limit");
        Check(result.Moves.Any(m=>m.Move.Source.Item==2&&m.Retainer==2),"crystals use available assigned home");
        var move=new RoutedMove(2,new(bags[0],new(10000,0,0,0,false,999),5),false);
        Check(PlanItemScope.Allows(move,rules,true,new Dictionary<uint,int>()),"valid assigned move allowed");
        Check(!PlanItemScope.Allows(move,rules,true,new Dictionary<uint,int>{{100,1}}),"confirmed budget cannot be reused");
        Check(!PlanItemScope.Allows(move with{Move=move.Move with{Source=bags[3]}},rules,true,new Dictionary<uint,int>()),"unlisted final move blocked");
        Check(!PlanItemScope.Allows(move with{Move=move.Move with{Source=bags[2]}},rules,true,new Dictionary<uint,int>()),"excluded Housing final move blocked");
        Check(!PlanItemScope.Allows(move with{Withdraw=true},rules,true,new Dictionary<uint,int>()),"entrust cannot withdraw");
        Check(!PlanItemScope.Allows(move with{Internal=true},rules,true,new Dictionary<uint,int>()),"entrust cannot merge unrelated storage");
        var nq=rules.Select(r=>r with{Quality=RetrievalQuality.NqOnly}).ToArray();
        Check(!PlanItemScope.Allows(move with{Move=move.Move with{Source=bags[1]}},nq,true,new Dictionary<uint,int>()),"quality checked at final submit");
        var organize=PlanItemScope.Restrict([new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[],AllItems:true)],filtered.Select(e=>e.ItemId),Category);
        Check(organize.All(r=>!r.AllItems&&r.Categories.Length==0&&!r.Items.Contains(200u)&&!r.Items.Contains(999u)),"Organise all bounded by selected plan");
        var retainerStock=new Dictionary<ulong,ChestStack[]>{
            {0,[new(0,0,999,10,false,999),new(0,1,100,12,false,999),new(0,2,0,0,false,999),new(0,3,0,0,false,999)]},
            {1,[new(10000,0,100,10,false,999),new(10000,1,200,10,false,999),new(10000,2,999,10,false,999)]},
            {2,[new(10000,0,100,10,false,999),new(10000,1,0,0,false,999),new(10000,2,0,0,false,999)]}};
        var organizeAssigned=StorageAssignments.Apply(organize,[new(2,[10,20],[])],retainerStock,Category);
        var organized=StorageRouting.Build(new("Organize plan",1,organizeAssigned,0),retainerStock,Category,includeExistingBags:false);
        Check(organized.Moves.Length>0&&organized.Moves.All(m=>m.Move.Source.Item==100),"organizer assignments only move eligible plan items");
        Check(organized.Moves.All(m=>m.Move.Source.Container!=0||m.Move.Source.Slot>=2),"existing bag items never become organizer cargo");
        Check(PlanItemScope.Restrict(initial,[],Category).Length==0,"empty plan has no routes");
        StorageRoute[] broad=[new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[],[],AllItems:true)];
        var noPlan=PlanItemScope.FromRetainers(broad,retainerStock,Category);
        Check(noPlan.Single().Items.Order().SequenceEqual(new uint[]{100,200,999}),"no plan includes all recorded retainer items");
        var categoryOnly=PlanItemScope.FromRetainers([new(StorageRouting.AllSources,StorageAutoRouting.AllRetainers,[20],[])],retainerStock,Category);
        Check(categoryOnly.Single().Items.SequenceEqual(new uint[]{200}),"no plan category filter narrows all retainer items");
        var onlyBags=new Dictionary<ulong,ChestStack[]>{{0,[new(0,0,555,10,false,999)]},{1,[new(10000,0,100,10,false,999)]}};
        Check(!PlanItemScope.FromRetainers(broad,onlyBags,Category).Single().Items.Contains(555u),"bag-only items never enter no-plan scope");
        var allAssigned=StorageAssignments.Apply(noPlan,[new(2,[10,20],[])],retainerStock,Category);
        var allOrganized=StorageRouting.Build(new("No plan",1,allAssigned,0),retainerStock,Category,includeExistingBags:false);
        Check(allOrganized.Moves.Any(m=>m.Move.Source.Item==200)&&allOrganized.Moves.Any(m=>m.Move.Source.Item==999),"no plan assignments organize items beyond a saved list");
        Check(allOrganized.Moves.All(m=>m.Move.Source.Container!=0||m.Move.Source.Slot>=2),"no plan leaves existing bag items untouched");
        var onlyHousing=StorageAssignments.Apply(categoryOnly,[new(2,[10,20],[])],retainerStock,Category);
        Check(StorageRouting.Build(new("Filtered no plan",1,onlyHousing,0),retainerStock,Category,includeExistingBags:false).Moves.All(m=>m.Move.Source.Item==200),"no plan movement filters survive assignments");
        amounts[100]=999;
        Check(rules[0].Amounts![100]==5,"run freezes quantity snapshot");
        Console.WriteLine("PASS plan item scope: filters, assignment routing, Keep, crystals and final transfer guard");
    }
}
