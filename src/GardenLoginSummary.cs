namespace EquinoxCompanion;

public sealed record GardenPersonNotice(string PersonId, string PersonName, string Kind);

public static class GardenLoginSummary
{
    public static string? Kind(SharedGardenCareBed bed, DateTimeOffset now)
    {
        if (bed.Empty || bed.Ready) return null;
        var due = GardenCareStatus.Due(bed, now);
        if (due is "dead" or "wilt") return "check";
        var death = bed.DeathAt;
        var risk = death is {} end && end <= now.AddHours(4) && !(end <= bed.GrowingObservedAt) &&
            (end > now || GardenTiming.DeathRisk(false, death, bed.HarvestAt, now));
        var wilting = bed.Watered is {} water && bed.WiltHours is > 0 && water.AddHours(bed.WiltHours.Value) <= now &&
            !(water.AddHours(bed.WiltHours.Value) <= bed.GrowingObservedAt) &&
            !GardenTiming.MaturityEstimateDue(bed.HarvestAt, bed.GrowingObservedAt, now);
        if (risk || wilting) return "check";
        return due == "tend" ? "tend" : null;
    }

    public static SharedPerson? Person(SharedPerson[] people, SharedGardenCare garden)
    {
        var links = people.SelectMany(p => p.Characters.SelectMany(c => c.Houses
            .Where(h => h.Id == garden.HouseId && string.Equals(c.World, h.World, StringComparison.OrdinalIgnoreCase))
            .Select(h => (Person: p, Character: c, House: h)))).ToArray();
        int Rank((SharedPerson Person, SharedCharacter Character, SharedHouse House) link) =>
            !string.IsNullOrWhiteSpace(link.House.OwnerName) && string.Equals(link.Character.Name.Trim(), link.House.OwnerName.Trim(), StringComparison.OrdinalIgnoreCase) ? 0 :
            link.House.Type == "Free Company house" && link.House.FcId.Length > 0 && link.Character.FcId == link.House.FcId && link.Character.FcMember != false ? (link.Character.FcMaster ? 1 : 2) : 3;
        if (links.Length > 0) return links.OrderBy(Rank).ThenBy(l => l.Person.Id).ThenBy(l => l.Character.Id).First().Person;
        return people.FirstOrDefault(p => p.Characters.Any(c => garden.CharacterIds.Contains(c.Id)));
    }

    public static (string Text, ushort Color)[] Messages(IEnumerable<GardenPersonNotice> notices)
    {
        var rows = notices.ToArray();
        var messages = new List<(string, ushort)>();
        foreach (var kind in new[] { "tend", "check" })
        {
            var names = rows.Where(n => n.Kind == kind).DistinctBy(n => n.PersonId)
                .Select(n => string.Concat(n.PersonName.Where(c => !char.IsControl(c))).Trim()).Where(n => n.Length > 0).ToArray();
            if (names.Length == 0) continue;
            var people = names.Length == 1 ? names[0] : string.Join(", ", names[..^1]) + " and " + names[^1];
            messages.Add(($"{people} {(names.Length == 1 ? "needs" : "need")} to {kind} their Gardens.", kind == "tend" ? (ushort)37 : (ushort)32));
        }
        return messages.ToArray();
    }
}
