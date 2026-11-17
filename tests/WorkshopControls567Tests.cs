using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class WorkshopControls567Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop controls "+name);}
        var q=new WorkshopQueueSave{Character=12,World=1,House="old",Items=[new(1,"Bow",4)],Completed=2,ReadyToCollect=1,PendingCollection=1,CollectionBefore=6};
        WorkshopPolicy.Clear(q,2,"new");
        Check("clear resets queue and every receipt counter",q.Items.Length==0&&q.Completed==0&&q.ReadyToCollect==0&&q.PendingCollection==0&&q.CollectionBefore==0);
        Check("clear preserves character and rebinds current workshop",q.Character==12&&q.World==2&&q.House=="new");
        Check("cleared queue has no next project",WorkshopPolicy.Next(q)==null);
        var draft=new WorkshopQueueSave{Character=12,Items=[new(1,"Bow",4),new(2,"Hull",4)]};
        WorkshopPolicy.EditRemaining(draft,0,2);
        Check("draft editable outside workshop",draft.House==""&&draft.World==0&&draft.Items[0].Quantity==2);
        WorkshopPolicy.EditRemaining(draft,1,0);
        Check("remove only selected final part",draft.Items.Length==1&&draft.Items[0].ItemId==1);
        WorkshopPolicy.BindForRun(draft,3,"estate");
        Check("draft binds on first run",draft.World==3&&draft.House=="estate");
        var rejected=false;try{WorkshopPolicy.BindForRun(draft,4,"other");}catch(InvalidOperationException){rejected=true;}
        Check("started queue cannot change workshop",rejected&&draft.World==3&&draft.House=="estate");
        draft.Completed=1;draft.ReadyToCollect=1;
        rejected=false;try{WorkshopPolicy.EditRemaining(draft,0,0);}catch(InvalidOperationException){rejected=true;}
        Check("pending collection cannot be removed",rejected&&draft.Items[0].Quantity==2);
        draft.ReadyToCollect=0;draft.PendingDeposit=1;WorkshopPolicy.EditRemaining(draft,0,0);
        Check("editing preserves completed counts and pending deposit",draft.Items[0].Quantity==1&&draft.Completed==1&&draft.PendingDeposit==1);
        WorkshopPolicy.Clear(draft,0,"");
        Check("clear returns to unbound draft",draft.House==""&&draft.World==0&&draft.PendingDeposit==0);
        var progress=new WorkshopQueueSave{Completed=5,Items=[new(1,"Bow",4),new(2,"Hull",4)]};
        WorkshopPolicy.EditRemaining(progress,0,2);
        Check("adding to completed row preserves later receipts",progress.Completed==5&&progress.Items[0].Quantity==4&&progress.Items[1].Quantity==4&&progress.Items[2].Quantity==2);
        RetrievalEntry[] nq=[new(1,"Lumber",20)],hq=[new(1,"Lumber",10)];
        var both=WorkshopPolicy.WithdrawalBatches(nq,hq,0);
        Check("Both keeps two distinct quality batches",both.Length==2&&both[0].Quality==RetrievalQuality.NqOnly&&both[0].Items[0].Quantity==20&&both[1].Quality==RetrievalQuality.HqOnly&&both[1].Items[0].Quantity==10);
        Check("NQ filter excludes HQ",WorkshopPolicy.WithdrawalBatches(nq,hq,1).Single().Quality==RetrievalQuality.NqOnly);
        Check("HQ filter excludes NQ",WorkshopPolicy.WithdrawalBatches(nq,hq,2).Single().Quality==RetrievalQuality.HqOnly);
        Check("empty quality skipped",WorkshopPolicy.WithdrawalBatches([],hq,0).Length==1&&WorkshopPolicy.WithdrawalBatches([],hq,1).Length==0);
    }
}
