using EquinoxCompanion;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

static unsafe class SellerNativeTests
{
    public static void Run()
    {
        var checks=0;
        void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        AtkUnitBase addon=default;AtkComponentButton button=default;AtkComponentNode node=default;
        button.OwnerNode=&node;node.NodeId=4;node.NodeFlags=NodeFlags.Enabled;
        Check(SellerNativeInterop.ButtonEvent(&addon,&button,4,out var evt)&&evt.Param==4&&evt.Listener==(AtkEventListener*)&addon,"Compare button has native event even when registration is routed elsewhere");
        Check(!SellerNativeInterop.ButtonEvent(&addon,&button,21,out _),"Cannot confirm with comparison button");
        node.NodeFlags=0;Check(!SellerNativeInterop.ButtonEvent(&addon,&button,4,out _),"Disabled button cannot fire");node.NodeFlags=NodeFlags.Enabled;
        AtkEvent registered=new(){Param=44,State=new(){EventType=AtkEventType.ButtonClick},Listener=(AtkEventListener*)1};node.AtkEventManager.Event=&registered;
        Check(SellerNativeInterop.ButtonEvent(&addon,&button,4,out evt)&&evt.Param==44&&evt.Listener==(AtkEventListener*)&addon,"Registered click parameter preserved with component listener");
        Check(!SellerNativeInterop.ButtonEvent(null,&button,4,out _),"No addon no event");
        Check(!SellerNativeInterop.ButtonEvent(&addon,null,4,out _),"No button no event");
        InventoryItem actual=new(){ItemId=42,Quantity=99,Flags=InventoryItem.ItemFlags.HighQuality};
        InventoryItem linked=new(){IsSymbolic=true,LinkedInventoryType=(ushort)InventoryType.BlockedItems,LinkedItemSlot=0};
        var actualAddress=(nint)(&actual);var linkedAddress=(nint)(&linked);
        var resolved=SellerNativeInterop.Resolve(&linked,_=>(InventoryItem*)actualAddress);
        Check(resolved==&actual&&resolved->ItemId==42&&resolved->Quantity==99,"Blocked item resolves to original item and quantity");
        Check(SellerNativeInterop.Resolve(&linked,_=>(InventoryItem*)linkedAddress)==null,"Self-linked slot is rejected");
        Check(SellerNativeInterop.Resolve(&linked,_=>null)==null,"Missing temporary slot is rejected");
        Check(SellerNativeInterop.Resolve(&actual)==&actual,"Ordinary item preserved");
        InventoryItem second=new(){IsSymbolic=true};var secondAddress=(nint)(&second);
        Check(SellerNativeInterop.Resolve(&linked,item=>item==(InventoryItem*)linkedAddress?(InventoryItem*)secondAddress:(InventoryItem*)linkedAddress)==null,"Cyclic links bounded");
        Console.WriteLine($"Native seller checks passed: {checks}.");
    }
}
