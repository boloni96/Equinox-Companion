using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset nextAutoRetainerRead;
    private Queue<ulong> autoRetainerCharacters = new();
    private int autoRetainerMatched;
    private bool autoRetainerForceRead=true;
    private string autoRetainerStatus = "Waiting for submarine data and the paired character list.";
    private void UpdateAutoRetainer(DateTimeOffset now)
    {
        if(!config.SyncAutoRetainer || !config.SyncEnabled || config.SharedRoster is null || now<nextAutoRetainerRead) return;
        nextAutoRetainerRead=now.AddMilliseconds(100);
        try
        {
            if(autoRetainerCharacters.Count==0)
            {
                var ids=Pi.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs").InvokeFunc();
                autoRetainerCharacters=new(ids.Where(id=>id!=0).Distinct().Take(500));
                autoRetainerMatched=0;
                if(autoRetainerCharacters.Count==0){autoRetainerStatus="No saved submarine characters are available yet.";nextAutoRetainerRead=now.AddMinutes(1);}
                return;
            }
            var cid=autoRetainerCharacters.Dequeue();
            var source=Pi.GetIpcSubscriber<ulong,AutoRetainerCharacter>("AutoRetainer.GetOfflineCharacterData").InvokeFunc(cid);
            if(source is not null && source.CID==cid && AutoRetainerCache.Match(source,config.SharedRoster.People.SelectMany(p=>p.Characters)) is {} match)
            {
                var world=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().FirstOrDefault(w=>string.Equals(w.Name.ToString(),match.World,StringComparison.OrdinalIgnoreCase)).RowId;
                if(world>0)
                {
                    string Item(uint id)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(id)?.Name.ToString()??"";
                    var cache=AutoRetainerCache.Copy(source,Item);
                    if(AutoRetainerCache.Valid(cache,now))
                    {
                        var actor=new Actor(cid.ToString(),match.Name,world,world,match.World,match.World);
                        KeepDiscovery(new(Guid.NewGuid().ToString("N"),"submarines.cached",now,actor,null,CachedVoyage:cache),autoRetainerForceRead);
                        autoRetainerMatched++;
                    }
                }
            }
            autoRetainerStatus=$"Submarine data: {autoRetainerMatched} matched character record(s); {autoRetainerCharacters.Count} remaining.";
            if(autoRetainerCharacters.Count==0){autoRetainerForceRead=false;nextAutoRetainerRead=now.AddMinutes(1);autoRetainerStatus=$"Submarine data: {autoRetainerMatched} matched character record(s). Last scan {now.LocalDateTime:t}.";}
        }
        catch(Exception ex)
        {
            nextAutoRetainerRead=autoRetainerCharacters.Count>0?now.AddMilliseconds(100):now.AddMinutes(1);
            autoRetainerStatus=autoRetainerCharacters.Count>0?"One cached character could not be read; continuing with the remaining characters.":"Background submarine data is unavailable. Open the workshop voyage panel to refresh direct observations.";
            Log.Debug(ex,"Optional AutoRetainer cache read deferred");
        }
    }
    private List<SharedCachedVoyage> CachedSubmarineRecords()
    {
        var roster=config.SharedRoster;
        var chars=roster?.People.SelectMany(p=>p.Characters).ToArray()??[];
        var records=new List<SharedCachedVoyage>(roster?.CachedVoyages??[]);
        foreach(var e in config.Discoveries.Where(e=>e.CachedVoyage is not null))
        {
            var matches=chars.Where(c=>string.Equals(c.Name,e.Actor.Name,StringComparison.OrdinalIgnoreCase)&&string.Equals(c.World,e.Actor.HomeWorldName,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
            var c=matches.Length==1?matches[0]:null;
            if(c is null||c.FcMember==false||c.FcId!=e.CachedVoyage!.FcId)continue;
            var fc=config.Discoveries.LastOrDefault(e=>e.Character?.FreeCompany?.Id==c.FcId)?.Character?.FreeCompany?.Name??c.FcId;
            records.Add(new(c.Id,c.Name,c.World,fc,e.At,e.CachedVoyage!));
        }
        return records.Where(r=>chars.Any(c=>c.Id==r.CharacterId&&c.FcMember!=false&&c.FcId==r.Data.FcId)).GroupBy(r=>r.CharacterId).Select(g=>g.MaxBy(r=>r.ImportedAt)!).ToList();
    }
}
