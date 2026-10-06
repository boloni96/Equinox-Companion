namespace EquinoxCompanion;
public static class FollowTransportPolicy
{
    public static bool Choice(string text)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>300||text.Any(char.IsControl))return false;
        var s=text.Trim();
        if(new[]{"purchase","buy ","sell ","discard","delete","abandon","exchange","repair","reward","coffer","treasure"}.Any(x=>s.Contains(x,StringComparison.OrdinalIgnoreCase)))return false;
        return new[]{"Teleport to ","Travel to ","Go to ","Return to ","Journey to ","Take the ferry", "Board the ","Enter the ","Enter Eureka", "Enter Zadnor", "Enter Gangos", "Move to ","Proceed to ","Residential District Aethernet", "Specify a ward", "Select a ward", "Leave residential district"}.Any(x=>s.StartsWith(x,StringComparison.OrdinalIgnoreCase))
            || new[]{"Gangos","The Firmament","The Bozjan Southern Front","Zadnor","The Doman Enclave"}.Contains(s.TrimEnd('.'),StringComparer.OrdinalIgnoreCase);
    }
    public static bool StepSupported(string text,bool confirmation)=>confirmation?Choice(text)||FollowPortalPolicy.IsConfirmationSupported(text):Choice(text);
    public static bool EstateChoice(string text)=>Choice(text)||text.Length<=300&&!text.Any(char.IsControl)&&new[]{"Private Estate", "Free Company Estate", "Shared Estate", "Apartment"}.Any(x=>text.StartsWith(x,StringComparison.Ordinal));
    public static bool RoomStep(FollowMenuStep s)=>!s.Confirmation&&s.Addon=="HousingSelectRoom"&&s.Arguments is {Length:>0 and <=4}&&s.MenuSignature.Length==64&&s.MenuSignature.All(Uri.IsHexDigit);
    public static bool Valid(FollowPortalSignal s) => s.TravelKind=="door"?s.SourceKind=="EventObj"&&s.BaseId>0&&s.Steps is {Length:<=8} doorSteps&&doorSteps.All(x=>x!=null&&(RoomStep(x)||StepSupported(x.Text,x.Confirmation))):(s.TravelKind=="friendestate"?s.SourceKind=="FriendEstate"&&ulong.TryParse(s.FriendContentId,out var id)&&id>0:(s.BaseId>0&&s.SourceKind is "Aetheryte" or "EventNpc" or "EventObj"||s.SourceKind=="boundary"&&s.Steps is {Length:>0} b&&b[0].Text.Trim().TrimEnd('.')=="Leave residential district"))&&s.Steps is {Length:>0 and <=8} steps&&steps.All(x=>x!=null&&(s.TravelKind=="friendestate"?EstateChoice(x.Text):(RoomStep(x)||StepSupported(x.Text,x.Confirmation))));
    public static bool Affordable(string text,int limit)
    {
        // Prices are accepted only when explicitly expressed in gil. No other currencies.
        if(text.Contains(" MGP",StringComparison.OrdinalIgnoreCase)||text.Contains(" seals",StringComparison.OrdinalIgnoreCase)||text.Contains("token",StringComparison.OrdinalIgnoreCase))return false;
        if(!text.Contains("gil",StringComparison.OrdinalIgnoreCase))return true;
        var match=System.Text.RegularExpressions.Regex.Match(text,@"\b([0-9][0-9,]*)\s+gil\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success&&int.TryParse(match.Groups[1].Value.Replace(",",""),out var fee)&&fee<=Math.Max(0,limit);
    }
}
