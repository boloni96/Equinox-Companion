using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class StorageActions544Tests
{
    [ModuleInitializer] internal static void Run(){
        var pages=StorageWithdrawalPlan.Build(new (string,uint)[]{("bag:4000",10),("bag:4001",10),("bag:4000",10),("house:123:27001",20),("bag:1000",30),("house:123:25003",40),("fc:123",50)});
        if(pages.Length!=3||pages[0].Items.Length!=1||pages[1].Items[0]!=10||pages[2].Items[0]!=20)throw new Exception("Batch withdrawal must preserve source pages, deduplicate within each page, and exclude equipped/placed/FC sources");
        if(!StorageWithdrawalPlan.Supported("bag:4100")||!StorageWithdrawalPlan.Supported("bag:3200")||StorageWithdrawalPlan.Supported("bag:0"))throw new Exception("Only supported withdrawal sources may enter the local queue");
        if(StorageWithdrawalPlan.Build(Array.Empty<(string,uint)>()).Length!=0)throw new Exception("An empty selection must not become withdraw all");
    }
}
