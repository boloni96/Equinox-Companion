using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class StorageRefresh311Tests
{
 [ModuleInitializer] internal static void Run(){
    void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS storage refresh "+label);}
    var now=DateTimeOffset.UtcNow;
    var slots=Enumerable.Range(0,175).Select(i=>new ChestStack((uint)(10000+i/25),i%25,0,0,false,0))
        .Concat(Enumerable.Range(0,18).Select(i=>new ChestStack(12001,i,(uint)(i+2),0,false,9999))).ToArray();
    slots[0]=new(10000,0,100,1,false,1);
    var gate=new RetainerCaptureStability();
    Check("unverified mismatched initial inventory is rejected",!gate.Ready(1,2,slots,2,now));
    Check("first valid read waits",!gate.Ready(1,2,slots,1,now));
    Check("initial inventory becomes verified",gate.Ready(1,2,slots,1,now.AddSeconds(1)));
    slots[0]=new(10000,0,0,0,false,0);
    Check("withdrawal waits for stability despite stale summary",!gate.Ready(1,2,slots,1,now.AddSeconds(2)));
    Check("stable empty post-transfer inventory accepted with stale summary",gate.Ready(1,2,slots,1,now.AddSeconds(3)));
    Check("other retainer cannot reuse verified session",!gate.Ready(1,3,slots,1,now.AddSeconds(4)));
    gate.ConfirmTransfer(1,3);
    Check("confirmed transfer still waits for stable snapshot",!gate.Ready(1,3,slots,1,now.AddSeconds(5)));
    Check("confirmed transfer can refresh after stale opening summary",gate.Ready(1,3,slots,1,now.AddSeconds(6)));
    Check("incomplete container remains rejected after confirmation",!gate.Ready(1,3,slots[..^1],1,now.AddSeconds(7)));
    Check("incomplete read revokes trusted session",!gate.Ready(1,3,slots,1,now.AddSeconds(8)));
    gate.ConfirmTransfer(1,3);
    Check("character switch revokes transfer trust",!gate.Ready(2,3,slots,1,now.AddSeconds(9)));
    var plan=new[]{new RetrievalEntry(100,"Gear",1),new RetrievalEntry(101,"Missing",1)};
    var stock=new[]{new WithdrawalStock(3,100,1,false)};
    var order=WithdrawalRetainerOrder.Build([1,2,3,4],stock,plan,_=>0,false,RetrievalQuality.Any,true);
    Check("complete records visit only known holders even with missing items",order.Retainers.SequenceEqual(new ulong[]{3})&&order.KnownHolders==1);
    Check("known empty stock does not summon retainers",WithdrawalRetainerOrder.Build([1,2,3],[],plan,_=>0,false,RetrievalQuality.Any,true).Retainers.Length==0);
    Check("unknown inventories retain fallback path",WithdrawalRetainerOrder.Build([1,2,3,4],stock,plan,_=>0,false,RetrievalQuality.Any,false).Retainers.SequenceEqual(new ulong[]{3,1,2,4}));
 }
}
