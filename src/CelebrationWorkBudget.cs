using System.Diagnostics;
namespace EquinoxCompanion;
internal static class CelebrationWorkBudget
{
    // Native calls stay on the game thread. Never block waiting for background work.
    internal static bool Advance(IEnumerator<bool> work,int maxSteps=2048,double milliseconds=1)
    {
        var started=Stopwatch.GetTimestamp();
        for(var i=0;i<maxSteps;i++){
            if(!work.MoveNext())return false;
            if(Stopwatch.GetElapsedTime(started).TotalMilliseconds>=milliseconds)break;
        }
        return true;
    }
}
