namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Task<(string Key,string Text)[]>? lotteryNoticeTask;
    private ulong lotteryNoticeCharacter;
    private string lotteryNoticePairing="";
    private SharedRoster? lotteryNoticeRoster;
    private long lotteryNoticeRevision;
    private IEnumerable<SyncEvent> LotteryRecords()
    {
        var reads=DiscoveryReads();
        return reads.Kind("housing.lottery").Concat(reads.Kind("housing.lottery-result")).Concat(reads.Kind("house.discovered"));
    }
    private void QueueLotteryNotices(ulong id,DateTimeOffset now)
    {
        if(lotteryNoticeTask!=null)return;
        var records=LotteryRecords().ToArray();var roster=config.SharedRoster;
        lotteryNoticeCharacter=id;lotteryNoticePairing=config.PairingKey;lotteryNoticeRoster=roster;lotteryNoticeRevision=discoveryReads.Revision;
        lotteryNoticeTask=Task.Run(()=>HousingLotteryNotices.Messages(HousingLotteryNotices.WithShared(records,roster),id.ToString(),now).ToArray());
    }
    private void CompleteLotteryNotices(ulong id,DateTimeOffset now)
    {
        if(lotteryNoticeTask?.IsCompleted!=true)return;
        var task=lotteryNoticeTask;lotteryNoticeTask=null;
        if(task.IsFaulted){errorJournal.Record("lottery-reminders","Reminder calculation deferred; tracking continues.",exceptionType:task.Exception?.GetBaseException().GetType().Name);return;}
        if(!task.IsCompletedSuccessfully||id==0||!config.NotifyHousingLottery||!LotteryLoaded)return;
        if(id!=lotteryNoticeCharacter||lotteryNoticePairing!=config.PairingKey||!ReferenceEquals(lotteryNoticeRoster,config.SharedRoster)||lotteryNoticeRevision!=DiscoveryReads().Revision){nextLotteryReminderCheck=default;return;}
        foreach(var notice in task.Result)if(lotteryBidNotices.Due(id,notice.Key,now))PrintEquinox(notice.Text);
    }
}
