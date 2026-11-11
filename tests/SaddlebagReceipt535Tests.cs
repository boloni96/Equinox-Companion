using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class SaddlebagReceipt535Tests
{
    [ModuleInitializer] internal static void Run(){
        var now=DateTimeOffset.UtcNow;
        var receipt=new SaddlebagDepositReceipt(198,1,198);
        if(receipt.Observe(0,199,now)!=SaddlebagDepositReceipt.Result.Waiting)throw new Exception("Immediate client changes are not a receipt");
        if(receipt.Observe(0,199,now.AddMilliseconds(250))!=SaddlebagDepositReceipt.Result.Settled)throw new Exception("Verified native transfer must not wait a fixed two seconds");
        if(receipt.Observe(198,1,now.AddMilliseconds(750))!=SaddlebagDepositReceipt.Result.Reverted)throw new Exception("Rolled-back duplicates must stop instead of being sent again");
        receipt=new(100,990,100);
        if(receipt.Observe(91,999,now)!=SaddlebagDepositReceipt.Result.Waiting||receipt.Observe(91,999,now.AddMilliseconds(250))!=SaddlebagDepositReceipt.Result.Settled||receipt.Quantity!=9)throw new Exception("Partial native deposits must settle and retain the correct quantity");
        // Withdrawal uses saddlebag as source and inventory as destination.
        receipt=new(5,9,2);
        if(receipt.Observe(3,11,now)!=SaddlebagDepositReceipt.Result.Waiting||receipt.Observe(3,11,now.AddMilliseconds(250))!=SaddlebagDepositReceipt.Result.Settled||receipt.Quantity!=2)throw new Exception("Withdrawal must verify both sides without a fixed delay");
        receipt=new(5,9,2);
        if(receipt.Observe(2,12,now)!=SaddlebagDepositReceipt.Result.Waiting)throw new Exception("Unrequested quantity must not confirm");
        receipt=new(100,20,100);
        if(receipt.Observe(0,20,now)!=SaddlebagDepositReceipt.Result.Waiting)throw new Exception("Inventory-only loss is not a deposit confirmation");
        if(receipt.Observe(100,120,now)!=SaddlebagDepositReceipt.Result.Waiting)throw new Exception("Saddlebag-only growth is not a deposit confirmation");
    }
}
