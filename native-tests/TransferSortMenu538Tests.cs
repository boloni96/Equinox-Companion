using System.Runtime.CompilerServices;
using System.Text;
using EquinoxCompanion;
using FFXIVClientStructs.FFXIV.Component.GUI;
using AtkType=FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;
internal static unsafe class TransferSortMenu538Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Item trade setup: "+why);}
        var target=new TradeItemShortcutTarget(10,20,30,40,0,4,14068,0,1){ResolvedItemPointer=40,ResolvedInventory=0,ResolvedSlot=4};
        var tradeBytes=Encoding.UTF8.GetBytes("Sort\0");var discardBytes=Encoding.UTF8.GetBytes("Discard\0");var pluginBytes=Encoding.UTF8.GetBytes("Plugin action\0");
        fixed(byte* trade=tradeBytes,discard=discardBytes,plugin=pluginBytes){
            var setup=stackalloc AtkValue[14];for(var i=0;i<14;i++)setup[i]=default;
            setup[0].Type=setup[2].Type=setup[3].Type=AtkType.UInt;setup[0].UInt=3;
            for(var i=8;i<11;i++)setup[i].Type=AtkType.String;
            setup[8].String.Value=plugin;setup[9].String.Value=discard;setup[10].String.Value=trade;
            for(var i=11;i<14;i++)setup[i].Type=AtkType.Int;
            var index=TradeItemShortcutNative.DisplayedEntry(setup,14,"Sort");
            Check(index==2,"setup maps native action past plugin entries");
            Check(TradeItemShortcutNative.DisplayedEntry(null,0,"Sort")==-1,"missing retained addon values cannot verify the setup");
            var selected=new TradeItemMenuSelection(50,target,"Sort",index,1000);
            // The setup argument buffer is released after setup. No pointer is kept.
            for(var i=0;i<14;i++)setup[i]=default;
            Check(selected.Matches(50,target,"Sort",1150),"verified selection survives transient argument buffer disposal");
            Check(!selected.Matches(51,target,"Sort",1150),"replacement menu rejected");
            Check(!selected.Matches(50,target with{ItemId=37409},"Sort",1150),"next item cannot reuse old menu");
            Check(!selected.Matches(50,target with{Partner=21},"Sort",1150),"wrong partner rejected");
            Check(!selected.Matches(50,target with{Character=11},"Sort",1150),"wrong character rejected");
            Check(!selected.Matches(50,target with{TradeAddon=31},"Sort",1150),"replacement trade rejected");
            Check(!selected.Matches(50,target with{Quantity=2},"Sort",1150),"changed stack rejected");
            Check(!selected.Matches(50,target with{Flags=1},"Sort",1150),"changed quality rejected");
            Check(!selected.Matches(50,target with{ResolvedItemPointer=41},"Sort",1150),"repointed slot rejected");
            Check(!selected.Matches(50,target,"Discard",1150),"label changed rejected");
            Check(!selected.Matches(50,target,"Sort",999),"clock rollback rejected");
            Check(!selected.Matches(50,target,"Sort",9001),"stale selection expires");
            Check(!(selected with{Index=-1}).Matches(50,target,"Sort",1150),"unverified action rejected");
            Check(!(selected with{Index=32}).Matches(50,target,"Sort",1150),"out of range action rejected");
        }
        Console.WriteLine($"PASS transfer sort setup snapshot and identity checks: {checks}");
    }
}
