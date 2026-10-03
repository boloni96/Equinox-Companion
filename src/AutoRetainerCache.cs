namespace EquinoxCompanion;

// Allowlisted IPC DTO: Dalamud converts the provider's return value into this
// independent copy. No AutoRetainer assembly dependency and no live object retained.
public sealed class AutoRetainerCharacter
{
    public ulong CID { get; set; }
    public string Name { get; set; } = "";
    public string World { get; set; } = "";
    public ulong FCID { get; set; }
    public int? Ceruleum { get; set; }
    public int? RepairKits { get; set; }
    public int? InventorySpace { get; set; }
    public int? NumSubSlots { get; set; }
    public AutoRetainerVessel[] OfflineSubmarineData { get; set; } = [];
    public Dictionary<string, AutoRetainerVesselDetails> AdditionalSubmarineData { get; set; } = [];
}
public sealed class AutoRetainerVessel
{
    public string Name { get; set; } = "";
    public long ReturnTime { get; set; }
}
public sealed class AutoRetainerVesselDetails
{
    public int Level { get; set; }
    public int Part1 { get; set; }
    public int Part2 { get; set; }
    public int Part3 { get; set; }
    public int Part4 { get; set; }
    public uint CurrentExp { get; set; }
    public uint NextLevelExp { get; set; }
    public byte[] Points { get; set; } = [];
}
public sealed record CachedSubmarine(string Name, int Rank, long ReturnTime, int[] PartItems, string[] PartNames, int[] Route, uint CurrentExp, uint NextLevelExp);
public sealed record CachedVoyage(string FcId, string Source, int? Ceruleum, int? RepairKits, int? Slots, CachedSubmarine[] Submarines, int? InventorySpace = null);
public sealed record SharedCachedVoyage(string CharacterId, string CharacterName, string World, string FcName, DateTimeOffset ImportedAt, CachedVoyage Data);
public static class AutoRetainerCache
{
    public static SharedCharacter? Match(AutoRetainerCharacter source, IEnumerable<SharedCharacter> characters)
    {
        if(source.CID == 0 || source.FCID == 0) return null;
        var matches=characters.Where(c=>string.Equals(c.Name.Trim(),source.Name.Trim(),StringComparison.OrdinalIgnoreCase) && string.Equals(c.World.Trim(),source.World.Trim(),StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length==1 && matches[0].FcMember!=false && matches[0].FcId==source.FCID.ToString() ? matches[0] : null;
    }
    public static CachedVoyage Copy(AutoRetainerCharacter source, Func<uint,string> itemName)
    {
        var subs=source.OfflineSubmarineData.Take(5).Select(s=>{
            source.AdditionalSubmarineData.TryGetValue(s.Name,out var d);
            int[] parts=d is null?[]:[d.Part1,d.Part2,d.Part3,d.Part4];
            return new CachedSubmarine(s.Name,d?.Level??0,s.ReturnTime,parts,parts.Select(p=>p>0?itemName((uint)p):"").ToArray(),d?.Points?.Select(p=>(int)p).ToArray()??[],d?.CurrentExp??0,d?.NextLevelExp??0);
        }).ToArray();
        return new(source.FCID.ToString(),"autoretainer",source.Ceruleum,source.RepairKits,source.NumSubSlots,subs,source.InventorySpace);
    }
    public static bool Valid(CachedVoyage? v, DateTimeOffset now)
    {
        static bool Text(string? s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=100&&!s.Any(char.IsControl);
        static bool Count(int? n)=>n is null or >=0 and <=10000000;
        return v is not null && System.Text.RegularExpressions.Regex.IsMatch(v.FcId??"",@"^[1-9]\d{0,19}$") && v.Source=="autoretainer" && Count(v.Ceruleum)&&Count(v.RepairKits)&&v.InventorySpace is null or >=0 and <=140 &&v.Slots is null or >=0 and <=4 &&
            v.Submarines is {Length:<=4} && v.Submarines.Select(s=>s?.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()==v.Submarines.Length && v.Submarines.All(s=>s is not null&&Text(s.Name)&&s.Rank is >=0 and <=200&&s.ReturnTime>=0&&s.ReturnTime<=now.AddDays(30).ToUnixTimeSeconds()&&s.PartItems is {Length:0 or 4}&&s.PartItems.All(p=>p is >=0 and <1000000)&&s.PartNames is not null&&s.PartNames.Length==s.PartItems.Length&&s.PartNames.All(p=>p is not null&&p.Length<=100&&!p.Any(char.IsControl))&&s.Route is {Length:<=5}&&s.Route.All(p=>p is >=0 and <=255));
    }
}
