using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Workshop579Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS bulk workshop: "+name);}
        var batches=WorkshopBulkPolicy.Allocate([new(1,"A",30),new(2,"B",20)],[],[new(1,40,false,false),new(2,20,false,false)],false);
        Check("all retainer materials share a batch",batches.Length==1&&batches[0].Items.Length==2&&!batches[0].Chest);
        batches=WorkshopBulkPolicy.Allocate([new(1,"A",30)],[new(1,"A",10)],[new(1,20,false,false),new(1,10,false,true),new(1,10,true,false)],false);
        Check("split sources preserve exact NQ demand",batches.Where(b=>b.Quality==RetrievalQuality.NqOnly).Sum(b=>b.Items.Sum(e=>e.Quantity))==30);
        Check("HQ remains a separate batch",batches.Single(b=>b.Quality==RetrievalQuality.HqOnly).Items.Single().Quantity==10);
        Check("chest receives only its share",batches.Single(b=>b.Chest).Items.Single().Quantity==10);
        Check("partial stacks use existing room",WorkshopBulkPolicy.NewSlots(20,30,99)==0);
        Check("overflow requests only new slots",WorkshopBulkPolicy.NewSlots(130,30,99)==2);
        var row=new WorkshopSupply(1,"A",10,3);
        var plan=WorkshopSetAllocator.Allocate([row],20,10,0,0,0);
        Check("20 NQ + 10 HQ supply three complete sets",plan.Required==30&&plan.Bags==30&&plan.Missing==0);
        plan=WorkshopSetAllocator.Allocate([row with{SetsRequired=1}],8,2,0,0,0);
        Check("8 NQ + 2 HQ never count as a full set",plan.Bags==8&&plan.Missing==2);
        plan=WorkshopSetAllocator.Allocate([row],0,0,20,10,0);
        Check("whole queue allocates both qualities",plan.WithdrawNq==20&&plan.WithdrawHq==10&&plan.Missing==0);
        plan=WorkshopSetAllocator.Allocate([row with{SetsDelivered=2}],0,0,30,0,0);
        Check("already delivered sets excluded",plan.Required==10&&plan.WithdrawNq==10);
        plan=WorkshopSetAllocator.Allocate([row],30,0,0,30,2);
        Check("HQ filter does not use NQ bags",plan.Bags==0&&plan.WithdrawHq==30);
        var failed=false;try{WorkshopBulkPolicy.Allocate([new(1,"A",10)],[],[new(1,9,false,false)],false);}catch(InvalidOperationException){failed=true;}
        Check("changed stock blocks before delivery",failed);
    }
}
