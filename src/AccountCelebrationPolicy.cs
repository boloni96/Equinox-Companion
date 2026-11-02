namespace EquinoxCompanion;
internal static class AccountCelebrationPolicy
{
    internal static bool Complete(int total,int done,int skipped=0)=>total>0&&done==total&&skipped==0;
    internal static bool Transition(bool? previous,bool complete)=>previous==false&&complete;
    internal const double DurationSeconds=5;
    internal static float Alpha(double seconds)=>seconds<0||seconds>=DurationSeconds?0:(float)Math.Min(1,Math.Min(seconds/.5,(DurationSeconds-seconds)/.75));
}
