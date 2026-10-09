namespace EquinoxCompanion;
public static class FollowTicketPolicy
{
    public static bool Prompt(string text)=>text.Length<=160&&System.Text.RegularExpressions.Regex.IsMatch(
        System.Text.RegularExpressions.Regex.Replace(text,@"\s+"," ").Trim(),
        @"\AUse an aetheryte ticket to teleport free of charge\? \(Total: [1-9][0-9,]*\)\z",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    public static bool Pending(string request,string active,long session,long currentSession,long now,long until,
        bool armed,bool enabled,bool paused,bool loading,bool sameArea)=>
        request.Length>0&&request==active&&session==currentSession&&now<until&&armed&&enabled&&!paused&&!loading&&sameArea;
}
