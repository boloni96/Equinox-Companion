using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class SaddlebagReceipt535Tests
{
    [ModuleInitializer] internal static void Run(){
        var now=DateTimeOffset.UtcNow;
        var receipt=new SaddlebagDepositReceipt(198,1,198);
        if(receipt.Observe(0,199,now)!=SaddlebagDepositReceipt.Result.Waiting)throw new Exception("Immediate client changes are not a receipt");
        if(receipt.Observe(0,199,now.AddMilliseconds(250))!=SaddlebagDepositReceipt.Result.Waiting)throw new Exception("The previous 250 ms confirmation was too early");
        if(receipt.Observe(198,1,now.AddMilliseconds(750))!=SaddlebagDepositReceipt.Result.Reverted)throw new Exception("Rolled-back duplicates must stop instead of being sent again");
        receipt=new(100,990,100);
        if(receipt.Observe(91,999,now)!=SaddlebagDepositReceipt.Result.Waiting||receipt.Observe(91,999,now.AddSeconds(2))!=SaddlebagDepositReceipt.Result.Settled||receipt.Quantity!=9)throw new Exception("Partial native deposits must settle and retain the correct quantity");
        receipt=new(100,20,100);
        if(receipt.Observe(0,20,now)!=SaddlebagDepositReceipt.Result.Waiting)throw new Exception("Inventory-only loss is not a deposit confirmation");
        if(receipt.Observe(100,120,now)!=SaddlebagDepositReceipt.Result.Waiting)throw new Exception("Saddlebag-only growth is not a deposit confirmation");
    }
}
