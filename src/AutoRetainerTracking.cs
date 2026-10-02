using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset nextAutoRetainerRead;
    private Queue<ulong> autoRetainerCharacters = new();
    private int autoRetainerMatched;
    private string autoRetainerStatus = "Waiting for AutoRetainer and the paired character list.";
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
                if(autoRetainerCharacters.Count==0){autoRetainerStatus="AutoRetainer has no registered characters.";nextAutoRetainerRead=now.AddMinutes(1);}
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
                        KeepDiscovery(new(Guid.NewGuid().ToString("N"),"submarines.cached",now,actor,null,CachedVoyage:cache));
                        autoRetainerMatched++;
                    }
                }
            }
            autoRetainerStatus=$"AutoRetainer: {autoRetainerMatched} matched character cache(s); {autoRetainerCharacters.Count} remaining.";
            if(autoRetainerCharacters.Count==0){nextAutoRetainerRead=now.AddMinutes(1);autoRetainerStatus=$"AutoRetainer: {autoRetainerMatched} matched character cache(s). Last scan {now.LocalDateTime:t}.";}
        }
        catch(Exception ex)
        {
            autoRetainerCharacters.Clear();nextAutoRetainerRead=now.AddMinutes(1);
            autoRetainerStatus="AutoRetainer cache unavailable; enable/update AutoRetainer. Direct workshop tracking still works.";
            Log.Debug(ex,"Optional AutoRetainer cache read deferred");
        }
    }
    private void DrawAutoRetainerSubmarines()
    {
        ImGui.Separator();ImGui.TextUnformatted("AutoRetainer cached submarines & supplies");
        ImGui.TextWrapped(config.SyncAutoRetainer?autoRetainerStatus:"AutoRetainer import is off. Enable it in Settings > Tracking.");
        var roster=config.SharedRoster;
        var chars=roster?.People.SelectMany(p=>p.Characters).ToArray()??[];
        var records=new List<SharedCachedVoyage>(roster?.CachedVoyages??[]);
        foreach(var e in config.Discoveries.Where(e=>e.CachedVoyage is not null))
        {
            var matches=chars.Where(c=>c.Name==e.Actor.Name&&c.World==e.Actor.HomeWorldName).Take(2).ToArray();
            var c=matches.Length==1?matches[0]:null;
            if(c is null||c.FcMember==false||c.FcId!=e.CachedVoyage!.FcId)continue;
            var fc=config.Discoveries.LastOrDefault(e=>e.Character?.FreeCompany?.Id==c.FcId)?.Character?.FreeCompany?.Name??c.FcId;
            records.Add(new(c.Id,c.Name,c.World,fc,e.At,e.CachedVoyage!));
        }
        foreach(var group in records.Where(r=>chars.Any(c=>c.Id==r.CharacterId&&c.FcMember!=false&&c.FcId==r.Data.FcId)).GroupBy(r=>r.CharacterId))
        {
            var r=group.MaxBy(r=>r.ImportedAt)!;var v=r.Data;
            ImGui.Separator();ImGui.TextUnformatted($"{r.CharacterName}@{r.World} · {r.FcName}");
            ImGui.TextWrapped($"Carried supplies: {v.Ceruleum?.ToString()??"unknown"} ceruleum tanks · {v.RepairKits?.ToString()??"unknown"} repair kits · {v.Slots?.ToString()??"unknown"} submarine slots");
            ImGui.TextWrapped($"Cache imported {r.ImportedAt.LocalDateTime:g}; last game observation unknown.");
            foreach(var s in v.Submarines)
            {
                var left=DateTimeOffset.FromUnixTimeSeconds(s.ReturnTime)-DateTimeOffset.UtcNow;
                var timer=s.ReturnTime==0?"No voyage recorded":left<=TimeSpan.Zero?"Return due — confirm in game":$"Returns in {(int)left.TotalHours}h {left.Minutes}m";
                ImGui.TextUnformatted($"{s.Name} · {(s.Rank>0?$"rank {s.Rank}":"rank unknown")} · {timer}");
                if(ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();ImGui.TextUnformatted($"EXP {s.CurrentExp:N0} / {s.NextLevelExp:N0}");
                    foreach(var p in s.PartItems.Select((id,i)=>s.PartNames[i].Length>0?s.PartNames[i]:id>0?$"Item #{id}":"Unknown component"))ImGui.TextUnformatted(p);
                    ImGui.TextUnformatted("Route sector IDs: "+string.Join(", ",s.Route.Where(id=>id>0)));ImGui.EndTooltip();
                }
            }
        }
    }
}
