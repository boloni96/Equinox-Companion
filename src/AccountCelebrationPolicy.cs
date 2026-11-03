namespace EquinoxCompanion;
internal static class AccountCelebrationPolicy
{
    internal static bool Complete(int total,int done,int skipped=0)=>total>0&&done==total&&skipped==0;
    internal static bool Transition(bool? previous,bool complete)=>previous==false&&complete;
    internal static bool EventGoalComplete(bool questComplete,IEnumerable<(bool Wanted,bool Obtained)>? rewards)
    {
        if(!questComplete||rewards is null)return false;
        var wanted=rewards.Where(r=>r.Wanted).ToArray();
        return wanted.Length>0&&wanted.All(r=>r.Obtained);
    }
    internal const double DurationSeconds=5;
    internal static float Alpha(double seconds)=>seconds<0||seconds>=DurationSeconds?0:(float)Math.Min(1,Math.Min(seconds/.5,(DurationSeconds-seconds)/.75));
}

