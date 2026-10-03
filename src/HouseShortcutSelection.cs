namespace EquinoxCompanion;
public static class HouseShortcutSelection
{
    public static bool Checked(SharedHouse house,DateTimeOffset now) => house.LastEntry is {} at&&at<=now&&now-at<TimeSpan.FromDays(7);
    public static SharedHouse[] For(Actor? actor,IEnumerable<SharedPerson> people)
    {
        if(actor is null||string.IsNullOrWhiteSpace(actor.HomeWorldName))return [];
        var matches=people.SelectMany(p=>p.Characters).Where(c=>string.Equals(c.Name.Trim(),actor.Name.Trim(),StringComparison.OrdinalIgnoreCase)&&string.Equals(c.World.Trim(),actor.HomeWorldName.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
        if(matches.Length!=1)return [];var c=matches[0];
        var houses=c.Houses.Where(h=>string.Equals(h.World,c.World,StringComparison.OrdinalIgnoreCase));
        var own=houses.Where(h=>h.Type=="Private house"&&string.Equals(h.OwnerName.Trim(),c.Name.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
        var fc=houses.Where(h=>h.Type=="Free Company house"&&c.FcMember!=false&&c.FcId.Length>0&&h.FcId==c.FcId).ToArray();
        return (own.Length==1?own:[]).Concat(fc.Length==1?fc:[]).ToArray();
    }
}
