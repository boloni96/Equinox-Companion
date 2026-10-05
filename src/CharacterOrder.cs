using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private sealed record OrderCharacter(string Id, string Label, bool HasHouse);
    private HashSet<string> LocalHousingActors() => config.Discoveries
        .Where(d => d.Kind == "house.discovered" && d.House?.Evidence == "owned-estate-id" && d.Address is not null &&
            (d.House.Type is "Private house" or "Free Company house"))
        .Select(d => d.Actor.ContentId).ToHashSet();

    private Actor[] LocalCharacters() => config.Discoveries.Select(d => (d.At, d.Actor))
        .Concat(config.Houses.Select(h => (At: h.ObservedAt, h.Actor)))
        .Concat(config.Tending.Select(t => (At: t.ConfirmedAt, t.Actor)))
        .Concat(config.Planting.Select(p => (At: p.ConfirmedAt, p.Actor)))
        .Where(x => SyncValidation.ActorReady(x.Actor)).GroupBy(x => x.Actor.ContentId)
        .Select(g => g.OrderByDescending(x => x.At).First().Actor)
        .OrderBy(a => a.HomeWorldName).ThenBy(a => a.Name).ToArray();

    private T[] OrderCharacters<T>(string scope, IEnumerable<T> source, Func<T, string> id, Func<T, bool> hasHouse, bool includeHidden = false)
    {
        config.CharacterOrders.TryGetValue(scope, out var saved);
        int Rank(T item) { var index = saved?.IndexOf(id(item)) ?? -1; return index < 0 ? int.MaxValue : index; }
        return source.Where(c => includeHidden || !IsCharacterHidden(scope, id(c))).OrderBy(c => config.HouseCharactersFirst && !hasHouse(c) ? 1 : 0).ThenBy(Rank).ToArray();
    }
    private bool IsCharacterHidden(string scope, string id) => config.HiddenCharacters.TryGetValue(scope, out var hidden) && hidden.Contains(id);
    private Actor[] OrderedLocalCharacters(bool includeHidden = false)
    {
        var owners = LocalHousingActors();
        var ordered = OrderCharacters("local", LocalCharacters(), a => a.ContentId, a => owners.Contains(a.ContentId), includeHidden);
        if (includeHidden || !Player.IsLoaded) return ordered;
        var currentId = Player.ContentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ordered.OrderBy(a => a.ContentId == currentId ? 0 : 1).ToArray();
    }
    private bool HasSharedHouse(SharedCharacter c) => c.Houses.Any(h => CountsForCharacter(c, h));
    private SharedCharacter[] OrderedSharedCharacters(SharedPerson person, bool includeHidden = false)
    {
        var ordered = OrderCharacters("shared-" + person.Id, person.Characters, c => c.Id, HasSharedHouse, includeHidden);
        if (includeHidden || !Player.IsLoaded) return ordered;
        var actor = ReadActor();
        var matches = ordered.Where(c => string.Equals(c.Name.Trim(), actor.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(c.World.Trim(), actor.HomeWorldName, StringComparison.OrdinalIgnoreCase)).ToArray();
        return matches.Length == 1 ? ordered.OrderBy(c => c.Id == matches[0].Id ? 0 : 1).ToArray() : ordered;
    }
    private static string SharedLocation(SharedCharacter c) =>
        string.Join(" · ", new[] { c.World, c.Dc, c.Region }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private void DrawCharacterOrderSettings()
    {
        ImGui.TextUnformatted("Character order & hidden characters");
        var ownersFirst = config.HouseCharactersFirst;
        if (ImGui.Checkbox("Keep characters without houses at the bottom", ref ownersFirst))
        { config.HouseCharactersFirst = ownersFirst; Pi.SavePluginConfig(config); }
        ImGui.TextWrapped("Use Up / Down to arrange each list. With houses first enabled, moves stay within the house / no-house groups. Disable it to place anyone anywhere. Order is saved on this installation. The logged-in character temporarily appears first in the character tabs; your order here stays unchanged.");
        var owners = LocalHousingActors();
        DrawOrderList("local", "My Empire", OrderedLocalCharacters(true)
            .Select(a => new OrderCharacter(a.ContentId, a.Name + " · " + HomeLocation(a), owners.Contains(a.ContentId))).ToArray());
        foreach (var person in config.SharedRoster?.People ?? [])
            DrawOrderList("shared-" + person.Id, person.Name, OrderedSharedCharacters(person, true)
                .Select(c => new OrderCharacter(c.Id, c.Name + " · " + SharedLocation(c), HasSharedHouse(c))).ToArray());
    }
    private void DrawOrderList(string scope, string title, OrderCharacter[] characters)
    {
        ImGui.PushID(scope);
        if (ImGui.TreeNode(title + "###order-list"))
        {
            if (ImGui.SmallButton("Reset order"))
            { config.CharacterOrders.Remove(scope); Pi.SavePluginConfig(config); }
            var hiddenCharacters = characters.Where(c => IsCharacterHidden(scope, c.Id)).ToArray();
            characters = characters.Where(c => !IsCharacterHidden(scope, c.Id)).ToArray();
            if (characters.Length == 0) ImGui.TextDisabled("No visible characters. Restore hidden characters below.");
            for (var i = 0; i < characters.Length; i++)
            {
                var c = characters[i];
                ImGui.PushID(c.Id);
                bool CanMove(int target) => target >= 0 && target < characters.Length &&
                    (!config.HouseCharactersFirst || characters[target].HasHouse == c.HasHouse);
                var move = 0;
                ImGui.BeginDisabled(!CanMove(i - 1));
                if (ImGui.SmallButton("Up")) move = -1;
                ImGui.EndDisabled(); ImGui.SameLine();
                ImGui.BeginDisabled(!CanMove(i + 1));
                if (ImGui.SmallButton("Down")) move = 1;
                ImGui.EndDisabled(); ImGui.SameLine();
                var hide = ImGui.SmallButton("Hide");
                ImGui.SameLine();
                ImGui.TextUnformatted(c.Label + (c.HasHouse ? "" : " · No house recorded"));
                ImGui.PopID();
                if (hide)
                {
                    if (!config.HiddenCharacters.TryGetValue(scope, out var hidden)) config.HiddenCharacters[scope] = hidden = [];
                    hidden.Add(c.Id); Pi.SavePluginConfig(config); break;
                }
                if (move != 0)
                {
                    (characters[i], characters[i + move]) = (characters[i + move], characters[i]);
                    config.CharacterOrders[scope] = characters.Select(x => x.Id).ToList();
                    Pi.SavePluginConfig(config);
                    break;
                }
            }
            if (ImGui.TreeNode($"Hidden characters ({hiddenCharacters.Length})###hidden"))
            {
                ImGui.TextWrapped("Hidden only from this plugin list. House records and website data are kept.");
                foreach (var c in hiddenCharacters)
                {
                    ImGui.PushID("hidden-" + c.Id);
                    if (ImGui.SmallButton("Restore"))
                    { config.HiddenCharacters[scope].Remove(c.Id); Pi.SavePluginConfig(config); }
                    ImGui.SameLine(); ImGui.TextUnformatted(c.Label);
                    ImGui.PopID();
                }
                ImGui.TreePop();
            }
            ImGui.TreePop();
        }
        ImGui.PopID();
    }
}
