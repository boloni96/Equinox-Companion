namespace EquinoxCompanion;
public sealed record PlantDetails(uint SeedId, string SeedName, uint SoilId, string SoilName);
public sealed record PlantingRecord(string EventId, DateTimeOffset ConfirmedAt, Actor Actor, Address Address, int Patch, int Bed, PlantDetails Plant);
public sealed record PlantIntent(string EventId, DateTimeOffset At, GardenSnapshot Target, PlantDetails Plant)
{
    public PlantingRecord? Confirm(uint logId, int?[] parameters, DateTimeOffset at, GardenSnapshot? target)
    {
        if (logId != 4015 || at < At || at - At > TimeSpan.FromSeconds(30) || target is null ||
            Target.Address is null || Target.Address.Apartment || Target.Address.Workshop || Target.Address.Room != 0 ||
            Target.TargetDetails?.DataId != 2003757 || Target.TargetId is null ||
            target.Actor != Target.Actor || target.Address != Target.Address || target.TargetId != Target.TargetId ||
            Plant.SeedId == 0 || Plant.SoilId == 0 || string.IsNullOrWhiteSpace(Plant.SeedName) || string.IsNullOrWhiteSpace(Plant.SoilName) ||
            parameters.Length < 2 || parameters[0] is not (>= 1 and <= 3) || parameters[1] is not (>= 1 and <= 8)) return null;
        return new(EventId, at, Target.Actor, Target.Address, parameters[0]!.Value, parameters[1]!.Value, Plant);
    }
}
public sealed record FreeCompanyDetails(string Id, string Name, string Tag, ushort WorldId);
public sealed record HouseDetails(string Type, string Size, string Evidence, FreeCompanyDetails? FreeCompany = null, string EstateName = "");
public sealed record PlacardDetails(string CharacterId, Address Address, string Name, string Size, byte EstateType);
public sealed record JobDetails(uint Id, string Name, int Level);
public sealed record CharacterDetails(uint JobId, string JobName, int Level, int HighestLevel, string Race, string Tribe, string Sex, JobDetails[] Jobs, FreeCompanyDetails? FreeCompany = null, int HighestBattleLevel = 0);
