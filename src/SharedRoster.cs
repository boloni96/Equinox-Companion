namespace EquinoxCompanion;
public sealed record SharedRoster(long Revision, DateTimeOffset Updated, SharedPerson[] People);
public sealed record SharedPerson(string Id, string Name, SharedCharacter[] Characters);
public sealed record SharedCharacter(string Id, string Name, string World, string Dc, string Region, string Account, SharedHouse[] Houses);
public sealed record SharedHouse(string Id, string GameHouseId, string Type, string Name, string World, string District, int Ward, int Plot, string Size, string OwnerName, string FcName, string FcTag, DateTimeOffset? LastEntry, bool Paused);
public sealed record RosterResult(SharedRoster? Roster, string Status, bool NotModified = false, bool Unauthorized = false);
