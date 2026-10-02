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
public sealed record FreeCompanyDetails(string Id, string Name, string Tag, ushort WorldId, string MasterName = "", CompanyProfileDetails? Profile = null);
public sealed record HouseDetails(string Type, string Size, string Evidence, FreeCompanyDetails? FreeCompany = null, string EstateName = "", string OwnerName = "");
public sealed record PlacardDetails(string CharacterId, Address Address, string Name, string Size, byte EstateType, string OwnerName = "", string FcTag = "", DateTimeOffset At = default);
public sealed record JobDetails(uint Id, string Name, int Level);
public sealed record CharacterDetails(uint JobId, string JobName, int Level, int HighestLevel, string Race, string Tribe, string Sex, JobDetails[] Jobs, FreeCompanyDetails? FreeCompany = null, int HighestBattleLevel = 0, string AccountKey = "", bool? Msq15Complete = null, bool? FcMember = null, string Nameday = "", string Guardian = "", string CityState = "", string GrandCompany = "");

public sealed record CompanyProfileDetails(int Rank, int ActiveMembers, string Source, string HomeWorld,
    string? FormedAt = null, string? Slogan = null, string? GrandCompany = null, string? Recruitment = null,
    string? Active = null, string? Focus = null, string? Seeking = null, string? EstateName = null);
public static class CompanyProfileIdentity
{
    public static string Flags(int bits, string[] labels) => bits == 0 ? "Not specified" : string.Join(", ", labels.Where((_,i)=>(bits & (1<<i))!=0));
    // Native RequestId is signed, while FC/Lodestone IDs use the entire unsigned 64-bit value.
    public static string Id(long requestId) => unchecked((ulong)requestId).ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static bool MatchesPlacard(FreeCompanyDetails company, PlacardDetails sign) =>
        company.WorldId == sign.Address.WorldId &&
        string.Equals(company.Name.Trim(), sign.OwnerName.Trim(), StringComparison.OrdinalIgnoreCase) &&
        (string.IsNullOrWhiteSpace(company.Profile?.EstateName) || string.IsNullOrWhiteSpace(sign.Name) ||
         string.Equals(company.Profile.EstateName.Trim(), sign.Name.Trim(), StringComparison.OrdinalIgnoreCase));
}
