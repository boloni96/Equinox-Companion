namespace EquinoxCompanion;
public sealed record AccountEventEvidence(bool QuestComplete,uint[] Items);
internal static class AccountCelebrationPolicy
{
    internal static string CompletionMessage(string account,string activity)=>activity=="Keybound Brawler + reward list"
        ?$"{account}: Keybound Brawler event quest complete for all participating characters. Reward shopping complete — all wishlist rewards obtained; nothing left to buy. Good job!"
        :$"{account}: {activity} complete for all participating characters. Good job!";
    internal static bool Complete(int total,int done,int skipped=0)=>total>0&&done==total&&skipped==0;
    internal static bool Transition(bool? previous,bool complete)=>previous==false&&complete;
    internal static bool EventGoalComplete(bool questComplete,IEnumerable<(bool Wanted,bool Obtained)>? rewards)
    {
        if(!questComplete||rewards is null)return false;
        var wanted=rewards.Where(r=>r.Wanted).ToArray();
        return wanted.Length>0&&wanted.All(r=>r.Obtained);
    }
    internal static bool RewardComplete(bool journalObtained,bool verifiedOwned)=>journalObtained||verifiedOwned;
    internal static double VisibleDelta(double delta,bool focused,bool loaded,bool textureReady)=>focused&&loaded&&textureReady?Math.Clamp(delta,0,.1):0;
    internal const double DurationSeconds=5;
    internal static float Alpha(double seconds)=>seconds<0||seconds>=DurationSeconds?0:(float)Math.Min(1,Math.Min(seconds/.5,(DurationSeconds-seconds)/.75));
}

