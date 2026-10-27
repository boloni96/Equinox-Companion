using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class TradeItemShortcut263Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Trade click timing: "+why);}
        var item=new TradeItemShortcutTarget(10,20,30,40,0,2,100,0,99);
        var request=new TradeItemShortcutRequest(item,"",1000);
        // Dalamud raises OnMenuOpened before the game opens/replaces the addon.
        Check(!request.MenuClosed(50),"old ContextMenu teardown does not cancel an incoming click");
        Check(!request.Ready(1100,false,0),"never select in the old menu before setup");
        request.BindMenu(60);
        Check(!request.MenuClosed(50),"different old addon cannot cancel the new menu");
        Check(request.MenuClosed(60),"closing the clicked menu cancels its pending action");
        Check(request.Ready(1100,false,0),"ready once the new menu is set up");
        Check(!request.Ready(1020,false,0),"allow plugin entries to finish populating");
        Check(!request.Ready(1100,true,0),"do not act while a prior offer syncs");
        Check(request.Matches(item,2000),"slow sync preserves the exact clicked item beyond old timeout");
        Check(request.Ready(2000,false,0),"same live menu proceeds after sync clears");
        Check(!request.Ready(1100,false,1000),"rapid next click waits for action spacing");
        Check(request.Ready(1250,false,1000),"rapid next click is retained instead of discarded");
        Check(request.Expired(3501)&&!request.Ready(3501,false,0),"no delayed action after timeout");
        Check(request.Expired(999),"invalid monotonic clock cancels");
        request.BindMenu(70);
        Check(request.Menu==60,"refresh cannot rebind a click to a replacement menu");
        Check(!request.Matches(item with {ItemId=101},1200),"item changed while waiting");
        Check(!request.Matches(item with {Partner=21},1200),"partner changed while waiting");
        Check(!request.Matches(item with {Quantity=98},1200),"stack changed while waiting");

        var watch=new TradeItemOfferWatch(item,0,1000);
        Check(watch.Step(1050,true,false,false)==TradeItemOfferDecision.Wait,"selection alone is not a confirmed addition");
        // ContextMenu is no longer involved: this also models normal menu
        // teardown followed by a delayed native item/stack offer update.
        Check(watch.Step(1200,true,false,true)==TradeItemOfferDecision.Added,"gear or furnishing offer confirmed after menu closes");
        watch=new(item,0,1000);
        Check(watch.Step(1050,true,true,false)==TradeItemOfferDecision.Wait,"stack quantity stays manual");
        Check(watch.Step(9000,true,true,false)==TradeItemOfferDecision.Wait,"quantity choice may take longer than action timeout");
        Check(watch.Step(9200,true,false,false)==TradeItemOfferDecision.Wait,"quantity dismissed before offer arrives");
        Check(watch.Step(9400,true,false,true)==TradeItemOfferDecision.Added,"delayed stack offer observed");
        watch=new(item,0,1000);
        watch.Step(1050,true,true,false);
        watch.Step(2000,true,false,false);
        Check(watch.Step(3501,true,false,false)==TradeItemOfferDecision.NoOffer,"cancelled quantity is not claimed as success");
        watch=new(item,0,1000);
        Check(watch.Step(4001,true,false,false)==TradeItemOfferDecision.NoOffer,"native selection with no effect is reported");
        Check(watch.Step(1100,false,false,true)==TradeItemOfferDecision.Cancelled,"another trade cannot confirm the old offer");
        Check(watch.Step(999,true,false,true)==TradeItemOfferDecision.Cancelled,"watch rejects clock rollback");
        Check(watch.Step(301001,true,true,false)==TradeItemOfferDecision.Cancelled,"watch is bounded even if quantity stays open");
        Console.WriteLine($"Trade click timing and observed offer checks passed: {checks}.");
    }
}
