namespace EquinoxCompanion;

public sealed record SellerReceivedOffer(ulong Listing,uint Item,SellerOffer Offer);

// A search flag in InfoProxyItemSearch remains set after results arrive on
// some game builds. Completion comes from this request's response and rows,
// never from that flag or from a previous native listing buffer.
public sealed class SellerComparisonCapture
{
    public uint Item { get; private set; }
    public uint World { get; private set; }
    public bool Hq { get; private set; }
    public string? Materia { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset RespondedAt { get; private set; }
    public int Expected { get; private set; }=-1;
    public int Error { get; private set; }
    public int? PacketRequest { get; private set; }
    public bool HistoryRequested { get; set; }
    public bool HistoryReceived { get; private set; }
    public SellerSale[] History { get; private set; }=[];
    public int ReceivedCount=>offers.Count;
    private readonly Dictionary<ulong,SellerOffer> offers=[];
    private bool conflictingRows;

    public void Clear()
    {
        Item=World=0;Hq=false;Materia=null;RequestedAt=RespondedAt=default;Expected=-1;Error=0;
        PacketRequest=null;HistoryRequested=HistoryReceived=false;History=[];offers.Clear();conflictingRows=false;
    }
    public void Begin(uint item,uint world,bool hq,DateTimeOffset at,string? materia="")
    {Clear();Item=item;World=world;Hq=hq;Materia=materia;RequestedAt=at;}
    public bool Response(uint item,int count,int error,DateTimeOffset at)
    {
        if(Item==0||item!=Item||at<RequestedAt)return false;
        Expected=count is >=0 and <=100?count:-1;Error=error;RespondedAt=at;return true;
    }
    public bool ReceiveOffers(int request,SellerReceivedOffer[] rows,DateTimeOffset at)
    {
        if(Item==0||at<RequestedAt||rows.Length==0||rows.Any(x=>x.Item!=Item||x.Listing==0||x.Offer.Price<=0||x.Offer.Quantity==0))return false;
        if(PacketRequest is {} previous&&previous!=request)return false;
        PacketRequest=request;
        foreach(var row in rows){
            if(offers.TryGetValue(row.Listing,out var old)&&old!=row.Offer)conflictingRows=true;
            if(offers.Count<100||offers.ContainsKey(row.Listing))offers[row.Listing]=row.Offer;
            else conflictingRows=true;
        }
        return true;
    }
    public bool ReceiveHistory(uint item,SellerSale[] sales,DateTimeOffset at)
    {
        if(Item==0||item!=Item||at<RequestedAt)return false;
        History=sales;HistoryReceived=true;return true;
    }
    public bool Ready(uint item,uint world,int nativeCount)=>Item!=0&&item==Item&&world==World&&
        RespondedAt>=RequestedAt&&Expected>=0&&Error==0&&!conflictingRows&&nativeCount==Expected&&offers.Count==Expected;
    public bool Contains(ulong listing,SellerOffer offer)=>offers.TryGetValue(listing,out var received)&&received==offer;
}
