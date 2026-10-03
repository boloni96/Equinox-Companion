using System.Text.RegularExpressions;
namespace EquinoxCompanion;

// Read the item parameter from the installed game's template, not a guessed numeric slot.
public sealed record HarvestReceipt(uint LogId, int ItemParameter)
{
    public static uint? FromChat(string text, string sender, IEnumerable<uint> itemIds)
    {
        // Original system/loot text and an actual item link, not text typed by another player.
        text=text.Trim();
        if(!string.IsNullOrWhiteSpace(sender)||text.Length>512||!text.StartsWith("You obtain ",StringComparison.Ordinal)||!text.EndsWith('.')||text.Contains('\n')||text.Contains('\r'))return null;
        var ids=itemIds.Select(id=>id%1_000_000).Where(id=>id>0).Distinct().ToArray();
        return ids.Length==1?ids[0]:null;
    }

    public static HarvestReceipt? FromTemplate(uint logId, string template)
    {
        if (logId is not (750 or 751) || !template.StartsWith("You obtain ", StringComparison.Ordinal)) return null;
        var slots = Regex.Matches(template, @"(?:ennoun\(Item,[^,]+,|sheet\(Item,)lnum(\d+)(?:[,\)])")
            .Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToArray();
        return slots.Length == 1 && slots[0] is >= 1 and <= 16 ? new(logId, slots[0] - 1) : null;
    }
    public uint? Item(int?[] parameters) => ItemParameter < parameters.Length && parameters[ItemParameter] is > 0 and var id
        ? (uint)id % 1_000_000 : null;
}

// An inspected crop + the player's submitted harvest + its received item establish success.
// Neither opening a ready bed nor receiving an item alone clears any bed.
public sealed record HarvestIntent(string EventId, DateTimeOffset At, GardenSnapshot Target, int Patch, int Bed, uint CropItemId)
{
    private static bool Same(GardenSnapshot a, GardenSnapshot b) =>
        a.Actor == b.Actor && a.Address is not null && a.Address == b.Address && a.TargetId is not null && a.TargetId == b.TargetId;

    public static HarvestIntent? From(GardenMenu menu, string? option, DateTimeOffset at, IEnumerable<CropChat> history, Func<string, uint> itemId)
    {
        if (option != "Harvest Crop" || menu.ReadyLocation() is not {} location || at < menu.OpenedAt || at - menu.OpenedAt > TimeSpan.FromMinutes(1)) return null;
        var chats = history.Where(c => Same(c.Target, menu.Target) && c.At <= at && Math.Abs((c.At - menu.OpenedAt).TotalSeconds) <= 2).ToArray();
        var crops = new HashSet<uint>();
        foreach (var chat in chats.Where(c => c.Text.TrimEnd().EndsWith(CropChatMatcher.ReadyText, StringComparison.Ordinal)))
        {
            var name = CropChatMatcher.Parse(chat.Text, chat.Sender);
            var id = name is not null ? itemId(name) : 0;
            if (id == 0 && name is null)
            {
                var names = chats.Where(c => c.At <= chat.At && chat.At - c.At <= TimeSpan.FromMilliseconds(750))
                    .Select(c => c.Text.Trim()).Where(n => n.Length is > 0 and <= 100 && !n.Any(char.IsControl))
                    .Select(itemId).Where(n => n > 0).Distinct().ToArray();
                if (names.Length == 1) id = names[0];
            }
            if (id > 0) crops.Add(id);
        }
        return crops.Count == 1 ? new(Guid.NewGuid().ToString("N"), at, menu.Target, location.Patch, location.Bed, crops.Single()) : null;
    }

    public TendingRecord? Confirm(uint logId, uint? receivedItem, DateTimeOffset at, GardenSnapshot? target)
    {
        if (logId is not (750 or 751) || receivedItem != CropItemId || CropItemId == 0 || at < At || at - At > TimeSpan.FromSeconds(10) || target is null || !Same(Target, target)) return null;
        return new(EventId, At, at, Target.Actor, Target.Address!, Patch, Bed, "garden.empty");
    }

    public TendingRecord? ConfirmChat(uint? receivedItem, DateTimeOffset at, GardenSnapshot? current)
    {
        if(receivedItem!=CropItemId||CropItemId==0||at<At||at-At>TimeSpan.FromSeconds(10)||current is null||
            at<current.ObservedAt||at-current.ObservedAt>TimeSpan.FromMilliseconds(500)||
            current.Actor!=Target.Actor||current.Address is null||current.Address!=Target.Address||
            current.TargetId is not null&&current.TargetId!=Target.TargetId)return null;
        // Harvest animation can drop the target. The submitted numbered bed stays authoritative.
        return new(EventId,At,at,Target.Actor,Target.Address!,Patch,Bed,"garden.empty");
    }
}
