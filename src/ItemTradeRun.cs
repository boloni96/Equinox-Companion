namespace EquinoxCompanion;
internal enum ItemTradeAction { None,Request,AddItem,LockOffer,Confirm }
internal enum ItemTradeStage { Stopped,Waiting,Opening,Filling,Locking,Confirming,Settling,Between,Complete }
internal sealed record ItemTradeFrame(GilTradeFrame Context,TradeBagSlot[] Bags,TradeItemAmount[] Local,TradeItemAmount[] Remote,bool CanReceive);
internal sealed class ItemTradeRun
{
    internal ItemTradeStage Stage{get;private set;}
    internal bool Running=>Stage is not (ItemTradeStage.Stopped or ItemTradeStage.Complete);
    internal bool Sending{get;private set;}
    internal ulong Character{get;private set;}
    internal uint Partner{get;private set;}
    internal int Trades{get;private set;}
    internal int Done{get;private set;}
    internal string Status{get;private set;}="Choose items from your bags.";
    internal TradeBatchSlot? NextItem{get;private set;}
    internal TradeItemAmount[] Remaining{get;private set;}=[];
    internal TradeItemAmount[] Offer{get;private set;}=[];
    private TradeBatchSlot[] batch=[];
    private Dictionary<TradeItemKey,int> before=[];
    private uint wallet;
    private int submitted;
    private bool completion,confirmed,restrictedReceive;
    private DateTimeOffset started,stageAt,offerAt;
    internal bool Start(bool send,TradeItemAmount[] items,ItemTradeFrame frame,DateTimeOffset now,bool exactReceive=false)
    {
        if(Running)return false;var f=frame.Context;
        if(f.Character==0||!f.Ready||!f.PartnerNearby||f.PartnerEntity==0){Status="Stand beside the selected player before starting.";return false;}
        if(f.TradeOpen||f.OtherDialog||f.Confirmation){Status="Close the existing trade or dialog first.";return false;}
        try{
            Remaining=ItemTradePlan.Counts(items).Select(x=>new TradeItemAmount(x.Key.Item,x.Key.Hq,x.Value)).ToArray();
            if((send||exactReceive)&&(Remaining.Length==0||Remaining.Any(x=>x.Quantity<=0)))throw new InvalidOperationException("Select items and quantities first.");
            if(send)ItemTradePlan.Next(Remaining,frame.Bags);
        }catch(Exception e){Status=e.Message;return false;}
        restrictedReceive=!send&&exactReceive;Sending=send;Character=f.Character;Partner=f.PartnerEntity;wallet=f.Wallet;before=ItemTradePlan.BagCounts(frame.Bags);
        started=now;Done=Trades=0;Offer=[];batch=[];NextItem=null;submitted=0;completion=confirmed=false;
        Move(ItemTradeStage.Waiting,now,send?"Ready to send items.":"Waiting for items from your selected player…");return true;
    }
    internal void Stop(string why){Status=why+(confirmed||Stage==ItemTradeStage.Settling?" The last trade may still settle; check your bags before restarting.":"");Stage=ItemTradeStage.Stopped;NextItem=null;}
    private ItemTradeAction Halt(string why){Stop(why);return ItemTradeAction.None;}
    private void Move(ItemTradeStage stage,DateTimeOffset now,string status){Stage=stage;stageAt=now;Status=status;}
    private bool DeltaMatches(TradeBagSlot[] bags,bool completed){var current=ItemTradePlan.BagCounts(bags);var expected=new Dictionary<TradeItemKey,int>(before);if(completed)foreach(var (key,qty) in ItemTradePlan.Counts(Offer))expected[key]=expected.GetValueOrDefault(key)+(Sending?-qty:qty);return expected.Keys.Union(current.Keys).All(k=>expected.GetValueOrDefault(k)==current.GetValueOrDefault(k));}
    internal ItemTradeAction Step(ItemTradeFrame frame,DateTimeOffset now)
    {
        if(!Running)return ItemTradeAction.None;var f=frame.Context;
        if(f.Character!=Character)return Halt("Stopped: character changed.");
        if(Stage is ItemTradeStage.Locking or ItemTradeStage.Confirming){
            var readable=f.TradeOpen&&f.TradeReady&&!f.SyncPending;
            var matches=f.TradePartner==Partner&&f.LocalGil==0&&f.RemoteGil==0&&ItemTradePlan.Same(Sending?frame.Local:frame.Remote,Offer)&&(Sending?frame.Remote:frame.Local).Length==0;
            if(f.CompletionObserved||readable&&matches&&f.LocalState==6){
                if(readable&&f.LocalState>=3&&f.RemoteState>=3&&!matches)return Halt("Stopped: completed offer changed.");
                completion=f.CompletionObserved;Move(ItemTradeStage.Settling,now,"Checking completed item transfer…");
            }
        }
        if(Stage==ItemTradeStage.Settling){
            completion|=f.CompletionObserved;
            if(f.Wallet!=wallet)return Halt("Stopped: unexpected gil change.");
            if(completion&&!f.TradeOpen&&!f.Confirmation&&DeltaMatches(frame.Bags,true)){
                Done+=Offer.Sum(x=>x.Quantity);Trades++;confirmed=completion=false;
                if(Sending||restrictedReceive){var sent=ItemTradePlan.Counts(Offer);Remaining=Remaining.Select(x=>x with{Quantity=x.Quantity-sent.GetValueOrDefault(x.Key)}).Where(x=>x.Quantity>0).ToArray();}
                before=ItemTradePlan.BagCounts(frame.Bags);Offer=[];batch=[];submitted=0;NextItem=null;
                if((Sending||restrictedReceive)&&Remaining.Length==0)Move(ItemTradeStage.Complete,now,$"{(Sending?"Sent":"Received")} {Done:N0} items · {Trades} completed trades.");
                else Move(ItemTradeStage.Between,now,$"{Done:N0} items transferred · preparing next trade…");
            }else if(now-stageAt>TimeSpan.FromSeconds(12))return Halt("Stopped: item transfer was not confirmed. Check your bags.");
            return ItemTradeAction.None;
        }
        if(!f.Ready||!f.PartnerNearby||f.PartnerEntity!=Partner)return Halt("Stopped: character, area or selected player changed.");
        if(now-started>TimeSpan.FromMinutes(30)||now-stageAt>TimeSpan.FromSeconds(60))return Halt("Stopped: no progress or session expired.");
        if(f.OtherDialog)return Halt("Stopped: another dialog needs attention.");
        if(f.Wallet!=wallet||!DeltaMatches(frame.Bags,false))return Halt("Stopped: inventory or gil changed outside the confirmed trade.");
        if(f.TradeOpen&&(!f.TradeReady||f.SyncPending))return ItemTradeAction.None;
        if(f.TradeOpen&&(f.TradePartner!=Partner||f.LocalGil!=0||f.RemoteGil!=0))return Halt("Stopped: wrong partner or gil added to an items-only trade.");
        if(f.TradeOpen&&(f.LocalState is <1 or >6||f.RemoteState is <1 or >6))return Halt("Stopped: unknown trade state.");
        if(f.TradeOpen&&(Sending?frame.Remote:frame.Local).Length>0)return Halt("Stopped: items were added on the wrong side.");
        if(Stage==ItemTradeStage.Between){if(now-stageAt<TimeSpan.FromSeconds(2))return ItemTradeAction.None;Move(ItemTradeStage.Waiting,now,"Ready for next trade.");}
        if(Stage==ItemTradeStage.Waiting){
            if(Sending){if(f.TradeOpen)return Halt("Stopped: an unexpected trade opened.");try{batch=ItemTradePlan.Next(Remaining,frame.Bags);}catch(Exception e){return Halt(e.Message);}Offer=ItemTradePlan.Amounts(batch);Move(ItemTradeStage.Opening,now,"Requesting item trade…");return ItemTradeAction.Request;}
            if(!f.TradeOpen)return ItemTradeAction.None;Move(ItemTradeStage.Filling,now,"Waiting for their item offer…");
        }
        if(Stage==ItemTradeStage.Opening){if(!f.TradeOpen)return ItemTradeAction.None;if(frame.Local.Length!=0||frame.Remote.Length!=0)return Halt("Stopped: new trade already contains items.");if(f.LocalState!=3)return ItemTradeAction.None;Move(ItemTradeStage.Filling,now,"Adding selected item quantities…");}
        if(!f.TradeOpen)return Halt("Stopped: trade was cancelled or closed.");
        if(Stage==ItemTradeStage.Filling){
            if(f.LocalState!=3)return Halt("Stopped: offer was accepted before item preparation finished.");
            if(Sending){
                var expected=ItemTradePlan.Amounts(batch.Take(submitted));
                if(!ItemTradePlan.Same(frame.Local,expected)){
                    if(submitted>0&&ItemTradePlan.Same(frame.Local,ItemTradePlan.Amounts(batch.Take(submitted-1)))&&now-offerAt<TimeSpan.FromSeconds(8))return ItemTradeAction.None;
                    return Halt("Stopped: offered items do not match the selected quantities.");
                }
                if(submitted<batch.Length){NextItem=batch[submitted++];offerAt=now;return ItemTradeAction.AddItem;}
                if(now-offerAt<TimeSpan.FromMilliseconds(800))return ItemTradeAction.None;
            }else{
                if(frame.Remote.Length==0)return ItemTradeAction.None;
                if(restrictedReceive){var wanted=ItemTradePlan.Counts(Remaining);if(ItemTradePlan.Counts(frame.Remote).Any(x=>x.Value>wanted.GetValueOrDefault(x.Key)))return Halt("Stopped: offered items exceed or differ from this delivery manifest.");}
                if(!frame.CanReceive)return Halt("Stopped: not enough room for these items.");
                if(!ItemTradePlan.Same(Offer,frame.Remote)){Offer=frame.Remote.ToArray();offerAt=now;}
                if(now-offerAt<TimeSpan.FromMilliseconds(800)||f.RemoteState<4)return ItemTradeAction.None;
            }
            Move(ItemTradeStage.Locking,now,"Waiting for both players to accept…");return ItemTradeAction.LockOffer;
        }
        if(!ItemTradePlan.Same(Sending?frame.Local:frame.Remote,Offer))return Halt("Stopped: accepted item offer changed.");
        if(Stage==ItemTradeStage.Locking){if(f.LocalState<4){if(now-stageAt>TimeSpan.FromSeconds(5))return Halt("Stopped: offer did not lock.");return ItemTradeAction.None;}Move(ItemTradeStage.Confirming,now,"Waiting for item trade confirmation…");}
        if(Stage==ItemTradeStage.Confirming){
            if(f.LocalState<4)return Halt("Stopped: offer unlocked.");
            if(f.RemoteState<4||!f.Confirmation)return ItemTradeAction.None;
            confirmed=true;Move(ItemTradeStage.Settling,now,"Confirming item transfer…");return ItemTradeAction.Confirm;
        }
        return ItemTradeAction.None;
    }
}

