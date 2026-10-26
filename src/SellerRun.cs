namespace EquinoxCompanion;

public sealed record SellerRetainerRef(ulong Id,string Name);
public interface ISellerPort
{
    SellerPreparation Prepare()=>new(true);
    bool ContextMatches(ulong character,uint world);
    bool SelectRetainer(SellerRetainerRef retainer);
    bool OpenMarket(ulong retainer);
    MarketStock[]? ReadStock(ulong retainer);
    bool OpenEditor(ulong retainer,MarketStock item);
    bool EditorMatches(ulong retainer,MarketStock item);
    bool BeginComparison(uint item);
    SellerComparison? Comparison { get; }
    bool ApplyPrice(ulong retainer,MarketStock item,int price);
    bool PriceConfirmed(ulong retainer,MarketStock item,int price);
    bool CancelEditor(ulong retainer,MarketStock item);
    bool CloseRetainer(ulong retainer);
    void Review(SellerRetainerRef retainer,MarketStock item,SellerDecision decision,bool applied);
    void Release();
}
public sealed class SellerRun(ISellerPort port)
{
    public enum Phase { Idle, Prepare, Select, OpenMarket, ReadStock, NextItem, OpenEditor, Compare, ReadComparison, Commit, Close }
    public Phase State { get; private set; }
    public bool Running => State!=Phase.Idle;
    public bool Paused { get; private set; }
    public bool Apply { get; private set; }
    public string Status { get; private set; }="Open a summoning bell to begin.";
    public int RetainerIndex { get; private set; }
    public int RetainerCount => retainers.Length;
    public int ItemIndex { get; private set; }
    public int ItemCount => items.Length;
    public int Updated { get; private set; }
    public int Skipped { get; private set; }
    private SellerRetainerRef[] retainers=[];
    private MarketStock[] items=[];
    private SellerDecision? decision;
    private SellerSettings settings=new();
    private HashSet<ulong> own=[];
    private ulong character;
    private uint world;
    private DateTimeOffset deadline,next;
    public void Start(SellerRetainerRef[] selected,ulong cid,uint worldId,bool apply,SellerSettings policy,IEnumerable<ulong> ownIds,DateTimeOffset now)
    {
        if(Running || selected.Length==0 || cid==0 || worldId==0)return;
        retainers=selected.DistinctBy(x=>x.Id).Take(10).ToArray();character=cid;world=worldId;Apply=apply;
        settings=System.Text.Json.JsonSerializer.Deserialize<SellerSettings>(System.Text.Json.JsonSerializer.Serialize(policy))!;
        own=ownIds.ToHashSet();foreach(var r in retainers)own.Add(r.Id);
        RetainerIndex=0;ItemIndex=0;items=[];Updated=0;Skipped=0;Paused=false;Move(Phase.Prepare,now);
    }
    public void Pause(DateTimeOffset now)
    {
        if(!Running)return;Paused=!Paused;deadline=now.AddSeconds(State==Phase.Prepare?45:25);next=now;
        // Pausing after a confirmation does not resubmit it. All other write
        // paths revalidate the live listing and comparison after resuming.
        if(!Paused&&State==Phase.ReadComparison){
            if(ItemIndex>=items.Length||!port.CancelEditor(retainers[RetainerIndex].Id,items[ItemIndex])){Stop("Stopped: the open listing changed while paused.");return;}
            Move(Phase.OpenEditor,now);
        }
        Status=Paused?"Paused · no more prices will be submitted.":"Resuming…";
    }
    public void Stop(string reason)
    {
        State=Phase.Idle;Paused=false;Status=reason;
        // Stop does not click, confirm or dismiss an unrelated game window.
        port.Release();
    }
    private static string Describe(Phase phase)=>phase switch {
        Phase.Prepare=>"Pausing other retainer work",Phase.Select=>"Selecting retainer",Phase.OpenMarket=>"Opening selling list",Phase.ReadStock=>"Reading listings",Phase.NextItem=>"Choosing next listing",
        Phase.OpenEditor=>"Opening price editor",Phase.Compare=>"Opening comparison",Phase.ReadComparison=>"Waiting for live prices",Phase.Commit=>"Waiting for confirmed price",Phase.Close=>"Returning to retainer list",_=>"Ready"
    };
    private void Move(Phase phase,DateTimeOffset now){State=phase;deadline=now.AddSeconds(phase==Phase.Prepare?45:25);next=now.AddMilliseconds(500);Status=Describe(phase)+"…";}
    private void Advance(DateTimeOffset now){ItemIndex++;decision=null;Move(Phase.NextItem,now);}
    public void Tick(DateTimeOffset now)
    {
        if(!Running)return;
        if(!port.ContextMatches(character,world)){Stop("Stopped: character, world or game state changed.");return;}
        if(Paused||now<next)return;
        next=now.AddMilliseconds(200);
        var retainer=retainers[RetainerIndex];
        var item=ItemIndex<items.Length?items[ItemIndex]:null;
        Status=$"{Describe(State)} · Retainer {RetainerIndex+1}/{retainers.Length} · {retainer.Name} · listing {Math.Min(ItemIndex+1,items.Length)}/{items.Length}";
        if(now>=deadline)
        {
            if(State==Phase.Prepare){Stop("Stopped: AutoRetainer did not finish its current action within 45 seconds. Its previous pause state is being restored.");return;}
            if(State is Phase.OpenEditor or Phase.Compare or Phase.ReadComparison && item is not null)
            {
                port.Review(retainer,item,new(SellerStatus.Skipped,null,"Comparison/editor timed out; price kept.","None"),false);Skipped++;
                if(!port.CancelEditor(retainer.Id,item)){Stop("Stopped: a different listing is open. No window was dismissed.");return;}
                Advance(now);return;
            }
            Stop(State==Phase.Commit?"Stopped: price confirmation was not observed. Check the last listing before restarting.":$"Stopped while {Describe(State).ToLowerInvariant()}. No response from the expected game window.");return;
        }
        switch(State)
        {
            case Phase.Prepare:
                var preparation=port.Prepare();
                if(preparation.Error.Length>0){Stop(preparation.Error);break;}
                if(preparation.Ready)Move(Phase.Select,now);else Status=preparation.Status;
                break;
            case Phase.Select:
                if(port.SelectRetainer(retainer))Move(Phase.OpenMarket,now);break;
            case Phase.OpenMarket:
                if(port.OpenMarket(retainer.Id))Move(Phase.ReadStock,now);break;
            case Phase.ReadStock:
                if(port.ReadStock(retainer.Id) is {} stock){items=stock;ItemIndex=0;Move(Phase.NextItem,now);}break;
            case Phase.NextItem:
                if(item is null){Move(Phase.Close,now);break;}
                var current=port.ReadStock(retainer.Id);
                if(current is null)break;
                var live=current.FirstOrDefault(x=>x.SamePricingItem(item));
                if(live is null || live.Price!=item.Price)
                {port.Review(retainer,item,new(SellerStatus.Skipped,null,"Listing changed or sold since this run began.","None"),false);Skipped++;Advance(now);break;}
                if(settings.ExcludedItems.Contains(item.ItemId)){Skipped++;port.Review(retainer,item,new(SellerStatus.Skipped,null,"Excluded by item rule.","None"),false);Advance(now);break;}
                Move(Phase.OpenEditor,now);break;
            case Phase.OpenEditor:
                if(item is not null && port.OpenEditor(retainer.Id,item))Move(Phase.Compare,now);break;
            case Phase.Compare:
                if(item is null || !port.EditorMatches(retainer.Id,item)){Stop("Stopped: the open listing no longer matches this run.");break;}
                if(port.BeginComparison(item.ItemId))Move(Phase.ReadComparison,now);break;
            case Phase.ReadComparison:
                if(item is null || !port.EditorMatches(retainer.Id,item)){Stop("Stopped: the selected listing changed.");break;}
                var result=port.Comparison;
                if(result is null)break;
                decision=SellerPricing.Decide(item.ItemId,world,item.Hq,item.Price,result,own,settings,now,item.Materia);
                if(!Apply || !decision.CanApply)
                {port.Review(retainer,item,decision,false);if(decision.Status==SellerStatus.Skipped)Skipped++;if(!port.CancelEditor(retainer.Id,item)){Stop("Stopped: the selected listing changed.");break;}Advance(now);break;}
                if(port.ApplyPrice(retainer.Id,item,decision.Price!.Value))Move(Phase.Commit,now);
                else Stop("Stopped: price could not be safely submitted.");
                break;
            case Phase.Commit:
                if(item is not null && decision?.Price is {} price && port.PriceConfirmed(retainer.Id,item,price))
                {port.Review(retainer,item,decision,true);Updated++;Advance(now);}break;
            case Phase.Close:
                if(port.CloseRetainer(retainer.Id))
                {RetainerIndex++;if(RetainerIndex>=retainers.Length)Stop($"Finished · {Updated} updated · {Skipped} skipped.");else Move(Phase.Select,now);}break;
        }
    }
}
