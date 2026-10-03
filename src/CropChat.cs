namespace EquinoxCompanion;

public sealed record CropChat(DateTimeOffset At, GardenSnapshot Target, string Text, string Sender = "");
public sealed record CropDetails(string CropName, bool Ready = true, string Evidence = "garden-menu-and-system-message", string? Status = null);
public sealed record CropObservation(DateTimeOffset At, GardenSnapshot Target, int Patch, int Bed, CropDetails Crop);

// Copied system messages only. Never infer a planting, watering, or harvest action.
public sealed class CropChatMatcher
{
    public const string ReadyText = "This crop is ready to be harvested.";
    private readonly List<GardenMenu> menus = [];
    private readonly List<CropChat> chats = [];
    public void Clear() { menus.Clear(); chats.Clear(); }
    public void Add(GardenMenu menu) { if (menu.NumberedLocation() is not null) { menus.Add(menu); if (menus.Count > 64) menus.RemoveAt(0); } }
    public void Add(CropChat chat) { chats.Add(chat); if (chats.Count > 64) chats.RemoveAt(0); }
    private static bool Same(GardenSnapshot a, GardenSnapshot b) => a.Actor == b.Actor && a.Address is not null && a.Address == b.Address && a.TargetId is not null && a.TargetId == b.TargetId;
    public static string? Parse(string text, string sender = "")
    {
        text = text.Replace("\r", "").Trim();
        if (!text.EndsWith(ReadyText, StringComparison.Ordinal)) return null;
        var name = text[..^ReadyText.Length].Trim();
        if (name.Length == 0) name = sender.Trim();
        return name.Length is > 0 and <= 100 && !name.Any(char.IsControl) ? name : null;
    }
    // English garden inspection text, verified in CmnDefHousingGardeningPlant_00151.
    // A standalone status may be paired with a separate item-name message below.
    public static CropDetails? ParseStatus(string text,string sender="")
    {
        text=text.Replace("\r","").Trim();
        foreach(var pair in new[]{(Text:"This crop is doing well.",State:"growing"),(Text:"This crop has seen better days...",State:"wilting"),(Text:"This crop is beyond hope.",State:"dead")})
        {
            if(text!=pair.Text&&!text.EndsWith("\n"+pair.Text,StringComparison.Ordinal))continue;
            var name=text[..^pair.Text.Length].Trim();if(name.Length==0)name=sender.Trim();
            if(name.Length>100||name.Any(char.IsControl))return null;
            return new(name,false,Status:pair.State);
        }
        return null;
    }
    private static (int Patch,int Bed)? StatusLocation(GardenMenu m,string? status)=>status switch
    {
        null=>m.ReadyLocation(),
        "dead"=>m.DeadLocation(),
        _=>m.Options.Contains("Tend Crop")&&!m.Options.Contains("Harvest Crop")&&!m.Options.Contains("Plant Seeds")?m.NumberedLocation():null
    };
    public List<CropObservation> Drain(DateTimeOffset now, Func<string, bool> knownItem, bool retainUnmapped = false)
    {
        var result = new List<CropObservation>();
        foreach (var chat in chats.ToArray())
        {
            // The game can emit text immediately before setting up the menu.
            if (now - chat.At < TimeSpan.FromMilliseconds(350)) continue;
            var status=ParseStatus(chat.Text,chat.Sender);
            if (status is null&&!chat.Text.TrimEnd().EndsWith(ReadyText, StringComparison.Ordinal)) continue;
            var name = status?.CropName??Parse(chat.Text, chat.Sender);
            if (string.IsNullOrEmpty(name))
            {
                var names = chats.Where(x => x.At <= chat.At && chat.At - x.At <= TimeSpan.FromMilliseconds(750) && Same(x.Target, chat.Target))
                    .Select(x => x.Text.Trim()).Where(x => x.Length is > 0 and <= 100 && !x.Any(char.IsControl) && knownItem(x)).Distinct().ToArray();
                if (names.Length == 1) name = names[0];
            }
            if (string.IsNullOrEmpty(name) || !knownItem(name)) continue;
            var candidates = menus.Where(m => StatusLocation(m,status?.Status) is not null&&Math.Abs((m.OpenedAt - chat.At).TotalSeconds) <= 2 && Same(m.Target, chat.Target))
                .Select(m => (Menu: m, Location: StatusLocation(m,status?.Status)!.Value)).ToArray();
            var locations = candidates.Select(x => x.Location).Distinct().ToArray();
            if (locations.Length != 1)
            {
                if (retainUnmapped && locations.Length == 0 && !menus.Any(m=>Math.Abs((m.OpenedAt-chat.At).TotalSeconds)<=2&&Same(m.Target,chat.Target)) && now-chat.At >= TimeSpan.FromSeconds(2) && chat.Target.TargetDetails?.EventArgument is not null)
                { result.Add(new(chat.At,chat.Target,0,0,new(name,status is null,"garden-system-message-awaiting-calibration",status?.Status))); chats.Remove(chat); }
                continue;
            }
            var nearest = candidates.MinBy(x => Math.Abs((x.Menu.OpenedAt - chat.At).TotalMilliseconds));
            result.Add(new(chat.At, nearest.Menu.Target, nearest.Location.Patch, nearest.Location.Bed, status is null?new(name):status with {CropName=name}));
            chats.Remove(chat);
        }
        chats.RemoveAll(x => now - x.At > TimeSpan.FromSeconds(3));
        menus.RemoveAll(x => now - x.OpenedAt > TimeSpan.FromSeconds(4));
        return result;
    }
}
