namespace EquinoxCompanion;

public sealed record CropChat(DateTimeOffset At, GardenSnapshot Target, string Text, string Sender = "");
public sealed record CropDetails(string CropName, bool Ready = true, string Evidence = "garden-menu-and-system-message");
public sealed record CropObservation(DateTimeOffset At, GardenSnapshot Target, int Patch, int Bed, CropDetails Crop);

// Copied system messages only. Never infer a planting, watering, or harvest action.
public sealed class CropChatMatcher
{
    public const string ReadyText = "This crop is ready to be harvested.";
    private readonly List<GardenMenu> menus = [];
    private readonly List<CropChat> chats = [];
    public void Clear() { menus.Clear(); chats.Clear(); }
    public void Add(GardenMenu menu) { if (menu.ReadyLocation() is not null) { menus.Add(menu); if (menus.Count > 64) menus.RemoveAt(0); } }
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
    public List<CropObservation> Drain(DateTimeOffset now, Func<string, bool> knownItem)
    {
        var result = new List<CropObservation>();
        foreach (var chat in chats.ToArray())
        {
            // The game can emit text immediately before setting up the menu.
            if (now - chat.At < TimeSpan.FromMilliseconds(350)) continue;
            if (!chat.Text.TrimEnd().EndsWith(ReadyText, StringComparison.Ordinal)) continue;
            var name = Parse(chat.Text, chat.Sender);
            if (name is null)
            {
                var names = chats.Where(x => x.At <= chat.At && chat.At - x.At <= TimeSpan.FromMilliseconds(750) && Same(x.Target, chat.Target))
                    .Select(x => x.Text.Trim()).Where(x => x.Length is > 0 and <= 100 && !x.Any(char.IsControl) && knownItem(x)).Distinct().ToArray();
                if (names.Length == 1) name = names[0];
            }
            if (name is null || !knownItem(name)) continue;
            var candidates = menus.Where(m => Math.Abs((m.OpenedAt - chat.At).TotalSeconds) <= 2 && Same(m.Target, chat.Target))
                .Select(m => (Menu: m, Location: m.ReadyLocation()!.Value)).ToArray();
            var locations = candidates.Select(x => x.Location).Distinct().ToArray();
            if (locations.Length != 1) continue;
            var nearest = candidates.MinBy(x => Math.Abs((x.Menu.OpenedAt - chat.At).TotalMilliseconds));
            result.Add(new(chat.At, nearest.Menu.Target, nearest.Location.Patch, nearest.Location.Bed, new(name)));
            chats.Remove(chat);
        }
        chats.RemoveAll(x => now - x.At > TimeSpan.FromSeconds(3));
        menus.RemoveAll(x => now - x.OpenedAt > TimeSpan.FromSeconds(4));
        return result;
    }
}
