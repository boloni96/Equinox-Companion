using EquinoxCompanion;

static class ComparisonCaptureTests
{
    public static void Run(Action<string,bool> check,DateTimeOffset now)
    {
        var capture=new SellerComparisonCapture();
        SellerReceivedOffer Row(ulong id,uint item=10,long price=100)=>new(id,item,new(id+100,price,false,1));
        capture.Begin(10,20,false,now);
        check("cached native rows are not fresh comparison evidence",!capture.Ready(10,20,2));
        capture.Response(10,2,0,now.AddMilliseconds(50));
        check("response alone cannot approve stale native buffer",!capture.Ready(10,20,2));
        capture.ReceiveOffers(1,[Row(1)],now.AddMilliseconds(100));
        check("partial comparison waits for all listings",!capture.Ready(10,20,2));
        capture.ReceiveOffers(1,[Row(1)],now.AddMilliseconds(110));
        check("duplicate network row is not another listing",capture.ReceivedCount==1);
        check("wrong request chunk cannot complete comparison",!capture.ReceiveOffers(2,[Row(2)],now.AddMilliseconds(120))&&!capture.Ready(10,20,2));
        capture.ReceiveOffers(1,[Row(2)],now.AddMilliseconds(130));
        check("fresh complete response works independently of persistent native waiting flag",capture.Ready(10,20,2));
        check("changed item/world/count is rejected",!capture.Ready(11,20,2)&&!capture.Ready(10,21,2)&&!capture.Ready(10,20,1));
        check("native offer must match freshly received price",capture.Contains(1,Row(1).Offer)&&!capture.Contains(1,Row(1,price:999).Offer));
        capture.HistoryRequested=true;
        capture.ReceiveHistory(10,[new(100,false,1,now)],now.AddMilliseconds(150));
        capture.Clear();
        check("opening next editor clears history even with auto-compare disabled",!capture.HistoryRequested&&!capture.HistoryReceived&&capture.History.Length==0&&capture.Item==0);
        capture.Begin(11,20,true,now.AddSeconds(1));
        check("next item can request its own history",!capture.HistoryRequested&&!capture.HistoryReceived&&capture.ReceivedCount==0&&capture.Expected==-1);
        check("previous item history is rejected",!capture.ReceiveHistory(10,[new(500,false,1,now)],now.AddSeconds(2)));
        check("queued old history is rejected even for same item",!capture.ReceiveHistory(11,[new(500,true,1,now)],now));
        capture.Response(11,0,0,now.AddSeconds(2));
        check("confirmed empty results remain valid without listing packets",capture.Ready(11,20,0));
        capture.HistoryRequested=true;
        capture.ReceiveHistory(11,[new(200,true,1,now)],now.AddSeconds(2));
        check("second item gets its own sales",capture.HistoryReceived&&capture.History[0].Price==200);
        capture.Begin(11,20,true,now.AddSeconds(3));
        check("same item next stack still requires a fresh request",!capture.Ready(11,20,0)&&!capture.HistoryRequested&&!capture.HistoryReceived);
        capture.Response(11,0,1879048195,now.AddSeconds(4));
        check("busy response is not an empty market",!capture.Ready(11,20,0));
        capture.Begin(10,20,false,now);
        check("old packet rejected",!capture.ReceiveOffers(1,[Row(1)],now.AddSeconds(-1)));
        check("wrong-item packet rejected",!capture.ReceiveOffers(1,[Row(1,11)],now));
        capture.ReceiveOffers(3,[Row(1),Row(2)],now.AddMilliseconds(20));
        capture.Response(10,2,0,now.AddMilliseconds(50));
        check("listing event before response still completes correctly",capture.Ready(10,20,2));
        capture.ReceiveOffers(3,[Row(1,price:200)],now.AddMilliseconds(60));
        check("conflicting duplicate fails closed",!capture.Ready(10,20,2));
        capture.Begin(10,20,false,now);
        capture.Response(10,101,0,now.AddMilliseconds(1));
        check("unsupported count cannot be truncated into success",!capture.Ready(10,20,100));
        capture.Begin(10,20,false,now);
        capture.Response(10,100,0,now.AddMilliseconds(1));
        for(var i=0;i<10;i++)capture.ReceiveOffers(4,Enumerable.Range(i*10+1,10).Select(x=>Row((ulong)x)).ToArray(),now.AddMilliseconds(i+2));
        check("all ten listing chunks captured",capture.Ready(10,20,100));
    }
}
