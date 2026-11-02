namespace EquinoxCompanion;
internal static class EventCutsceneRetry
{
    public static bool Ready(int attempts,DateTimeOffset now,DateTimeOffset last)=>attempts<3&&(last==default||now-last>=TimeSpan.FromSeconds(2));
}
