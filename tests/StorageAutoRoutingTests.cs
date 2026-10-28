using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class StorageAutoRoutingTests
{
    [ModuleInitializer] internal static void Run(){
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS all-retainer "+name);}
        ChestStack Empty(uint container,int slot)=>new(container,slot,0,0,false,0);
        ChestStack Item(uint container,int slot,uint id,int qty,bool hq=false)=>new(container,slot,id,qty,hq,99);
        var all=StorageAutoRouting.AllRetainers;
        var p=new StorageRoutingPlan("All",1,[new(0,all,[],[100],Amounts:new(){[100]=80})]);
        var inv=new Dictionary<ulong,ChestStack[]>{{0,[Item(0,0,100,80)]},{1,[Item(10000,0,100,90)]},{2,[Empty(10000,0)]}};
        var run=StorageRouting.Build(p,inv,_=>58,true);
        Check("fills matching partial then next retainer",run.Remaining==0&&run.Moves.Length==2&&run.Moves[0].Retainer==1&&run.Moves[0].Move.Quantity==9&&run.Moves[1].Retainer==2&&run.Moves[1].Move.Quantity==71);
        Check("does not mutate live snapshot",inv[0][0].Quantity==80&&inv[1][0].Quantity==90);
        var limited=p with{Rules=[p.Rules[0] with{Amounts=new(){[100]=5}}]};
        Check("exact amount across retainers",StorageRouting.Build(limited,inv,_=>58,true).Moves.Sum(x=>x.Move.Quantity)==5);
        var qualities=new Dictionary<ulong,ChestStack[]>(inv){{3,[Empty(10000,0)]}};qualities[0]=[Item(0,0,100,10),Item(0,1,100,10,true)];
        Check("Any budget shared across HQ NQ",StorageRouting.Build(limited,qualities,_=>58,true).Moves.Sum(x=>x.Move.Quantity)==5);
        var hq=limited with{Rules=[limited.Rules[0] with{Quality=RetrievalQuality.HqOnly}]};
        var hqRun=StorageRouting.Build(hq,qualities,_=>58,true);Check("HQ only excludes NQ bags and stacks",hqRun.Moves.Sum(x=>x.Move.Quantity)==5&&hqRun.Moves.All(x=>x.Move.Source.Hq&&x.Move.Destination.Item==0));
        var full=new Dictionary<ulong,ChestStack[]>(inv);full[2]=[Item(10000,0,200,99)];var blocked=StorageRouting.Build(p,full,_=>58,true);
        Check("reports quantity without space",blocked.Moves.Sum(x=>x.Move.Quantity)==9&&blocked.Remaining==71);
        var duplicates=p with{Rules=[new(0,all,[],[],Duplicates:true)]};var dup=StorageRouting.Build(duplicates,inv,_=>58,true);Check("duplicates stay with existing holders",dup.Moves.Length==1&&dup.Moves[0].Retainer==1&&dup.Remaining==71);
        var cat=p with{Rules=[new(0,all,[58],[])]};Check("category selection",StorageRouting.Build(cat,inv,id=>id==100?58u:0u,true).Moves.Sum(x=>x.Move.Quantity)==80);
        Check("unselected category stays in bags",StorageRouting.Build(cat,inv,_=>1,true).Moves.Length==0);
        var crystal=new Dictionary<ulong,ChestStack[]>{{0,[new(2001,0,2,20,false,9999)]},{1,[new(12001,0,2,9990,false,9999)]},{2,[new(12001,0,2,0,false,9999)]}};
        var cp=p with{Rules=[new(0,all,[],[2])]};var cr=StorageRouting.Build(cp,crystal,_=>0,true);Check("crystals split dedicated capacity",cr.Remaining==0&&cr.Moves.Length==2&&cr.Moves.Sum(x=>x.Move.Quantity)==20);
        var org=new StorageRoutingPlan("Organize",1,[new(StorageRouting.AllSources,all,[],[],AllItems:true)]);
        var stock=new Dictionary<ulong,ChestStack[]>{{0,[Empty(0,0),Empty(0,1),Empty(0,2)]},{1,[Item(10000,0,100,30),Item(10000,1,100,40),Item(10000,2,200,1),Empty(10000,3)]},{2,[Item(10000,0,100,20),Item(10000,1,200,20),Item(10000,2,200,30),Empty(10000,3)]}};
        uint Category(uint id)=>id==100?58u:80u;
        var resolved=StorageAutoRouting.Resolve(org,stock,Category);
        Check("category goes to its main existing holder",resolved.Rules.Single(x=>x.Items.Contains(100u)).To==1&&resolved.Rules.Single(x=>x.Items.Contains(200u)).To==2);
        var organized=StorageRouting.Build(org,stock,Category,includeExistingBags:false);Check("all-retainer organizer produces transfers",organized.Remaining==0&&organized.Moves.Any(x=>x.Withdraw));
        var off=org with{Rules=[org.Rules[0] with{Categories=[58],AllItems=false}]};Check("selected categories only",StorageAutoRouting.Resolve(off,stock,Category).Rules.All(x=>!x.Items.Contains(200u)));
        var direct=org with{Rules=[new(StorageRouting.AllSources,2,[],[],AllItems:true)]};Check("explicit destinations preserved",StorageAutoRouting.Resolve(direct,stock,Category).Rules[0].To==2);
        var split=new Dictionary<ulong,ChestStack[]>{{0,[Empty(0,0),Empty(0,1)]},{1,[Item(10000,0,100,99),Item(10000,1,101,99)]},{2,[Item(10000,0,102,99),Empty(10000,1)]}};
        var splitRules=StorageAutoRouting.Resolve(org,split,_=>58).Rules;
        Check("oversized category splits across retainers",splitRules.Select(x=>x.To).Distinct().Count()==2);
        Check("each item has one destination",splitRules.SelectMany(x=>x.Items).Distinct().Count()==3);
        Check("all-retainer choice survives preset export",RetainerRetrieval.Import(RetainerRetrieval.Export(new("All",[],DestinationRetainer:all)),_=>null).DestinationRetainer==all);
    }
}
