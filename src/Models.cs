namespace EquinoxCompanion;

public sealed record Actor(string ContentId, string Name, uint HomeWorldId, uint CurrentWorldId);
public sealed record Address(string HouseId, ushort WorldId, ushort TerritoryTypeId,
    int Ward, int Plot, int Room, bool Apartment, bool Workshop);
public sealed record HouseObservation(string EventId, DateTimeOffset ObservedAt,
    string Kind, Actor Actor, Address Address);
public sealed record GardenSnapshot(DateTimeOffset ObservedAt, Actor Actor, Address? Address,
    string? TargetId, string? TargetName, uint? HousingObjectId, short? FurnitureIndex,
    bool PlantingMenuOpen, uint[] SelectedItemIds, TargetDetails? TargetDetails = null);
public sealed record TargetDetails(uint DataId, uint EntityId, string Kind, float X, float Y, float Z, uint? EventArgument = null, ushort? TimelineState = null);
public sealed record Diagnostic(DateTimeOffset ObservedAt, string Kind, object Data);
