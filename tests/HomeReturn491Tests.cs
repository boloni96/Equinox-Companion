using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class HomeReturn491Tests
{
    [ModuleInitializer] internal static void Run(){
        foreach(var stage in new[]{NativeTravelStage.VisitIntro,NativeTravelStage.TransferConfirm,NativeTravelStage.TransferWait})
            if(!NativeTravelPolicy.HomeReturnRejected(stage,true,"Unable to return to Home World.\n[1000]"))throw new Exception("Home rejection must be recognized before and after submission");
        if(NativeTravelPolicy.HomeReturnRejected(NativeTravelStage.Login,true,"Unable to return to Home World. [1000]")||
           NativeTravelPolicy.HomeReturnRejected(NativeTravelStage.TransferWait,false,"Unable to return to Home World. [1000]")||
           NativeTravelPolicy.HomeReturnRejected(NativeTravelStage.TransferWait,true,"Arrived safely on Raiden"))throw new Exception("Unrelated dialog classified as home rejection");
        Console.WriteLine("491 explicit home-return rejection stage guards passed.");
    }
}
