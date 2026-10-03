using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static string VoyageTimer(long seconds)
    {
        if(seconds<=0)return "No voyage recorded";
        var remaining=DateTimeOffset.FromUnixTimeSeconds(seconds)-DateTimeOffset.UtcNow;
        return remaining<=TimeSpan.Zero?"Return due — confirm in workshop":$"Returns in {(int)remaining.TotalHours}h {remaining.Minutes}m";
    }
    private void DrawSubmarines()
    {
        ImGui.TextWrapped("Open the FC workshop voyage panel to refresh timers. Character and account order follows your Persons tabs.");
        ImGui.TextWrapped(config.SyncAutoRetainer?autoRetainerStatus:"Background submarine sync is off. Enable it in Settings > Tracking.");
        var observations=new List<SharedVoyage>(config.SharedRoster?.Voyages??[]);
        foreach(var e in config.Discoveries.Where(e=>e.Voyage is not null))
        {
            var fc=config.Discoveries.LastOrDefault(x=>x.Character?.FreeCompany?.Id==e.Voyage!.FcId)?.Character?.FreeCompany;
            observations.Add(new(e.Voyage!.FcId,fc?.Name??"Free Company",e.At,e.Voyage.Submarines));
        }
        var direct=observations.GroupBy(v=>v.FcId).ToDictionary(g=>g.Key,g=>g.MaxBy(v=>v.At)!);
        var caches=CachedSubmarineRecords().ToDictionary(c=>c.CharacterId);
        var represented=new HashSet<string>();var shown=0;
        var people=config.SharedRoster?.People??[];
        int PersonRank(SharedPerson p){var i=config.TabOrder.IndexOf("person:"+p.Id);return i<0?int.MaxValue:i;}
        foreach(var c in people.SelectMany(p=>p.Characters).Where(c=>c.FcMember!=false&&direct.ContainsKey(c.FcId)))represented.Add(c.FcId);
        if(!ImGui.BeginTabBar("submarine-persons"))return;
        foreach(var person in people.OrderBy(PersonRank))
        {
            var characters=OrderedSharedCharacters(person);
            var accounts=characters.GroupBy(SharedCharacterGrouping.AccountKey).ToArray();
            var hasData=characters.Any(c=>c.FcMember!=false&&direct.ContainsKey(c.FcId)||caches.ContainsKey(c.Id));
            if(!ImGui.BeginTabItem(person.Name.Replace("##","")+"###submarine-person-"+person.Id))continue;
            ImGui.PushID(person.Id);
            ImGui.BeginChild("submarine-person-scroll",System.Numerics.Vector2.Zero);
            if(!hasData)ImGui.TextWrapped("No submarines observed for this person yet.");
            foreach(var account in accounts)
            {
                var rows=account.Where(c=>c.FcMember!=false&&direct.ContainsKey(c.FcId)||caches.ContainsKey(c.Id)).OrderBy(c=>Array.IndexOf(new[]{"Regulars","Floaters","Empty","Pending sync"},SharedCharacterGrouping.Group(c))).ToArray();
                if(rows.Length==0)continue;
                foreach(var c in rows)if(c.FcMember!=false&&direct.ContainsKey(c.FcId))represented.Add(c.FcId);
                shown+=rows.Length;
                ImGui.SetNextItemOpen(false,ImGuiCond.Once);
                if(!ImGui.TreeNode($"{account.First().Account} · {rows.Length} characters###account-{account.Key}"))continue;
                foreach(var group in rows.GroupBy(SharedCharacterGrouping.Group))
                {
                ImGui.PushID(group.Key);ImGui.SetNextItemOpen(false,ImGuiCond.Once);
                if(ImGui.TreeNode($"{group.Key} · {group.Count()}###group")){
                foreach(var c in group)
                {
                    ImGui.PushID(c.Id);
                    ImGui.SetNextItemOpen(false,ImGuiCond.Once);
                    if(ImGui.TreeNode($"{c.Name} · {c.World}###character"))
                    {
                        caches.TryGetValue(c.Id,out var cache);direct.TryGetValue(c.FcId,out var observed);if(c.FcMember==false)observed=null;
                        ImGui.TextWrapped((observed?.FcName??cache?.FcName??"Free Company")+" · FC fleet");
                        if(cache is not null)
                        {
                            var v=cache.Data;
                            ImGui.TextWrapped($"Carried supplies: {v.Ceruleum?.ToString()??"unknown"} ceruleum tanks · {v.RepairKits?.ToString()??"unknown"} repair kits · {v.Slots?.ToString()??"unknown"} submarine slots");
                            ImGui.TextDisabled($"Cache imported {cache.ImportedAt.LocalDateTime:g}; game observation time unknown.");
                        }
                        if(observed is not null)ImGui.TextDisabled($"Workshop observed {observed.At.LocalDateTime:g}");
                        var names=(observed?.Submarines.Select(s=>s.Name)??[]).Concat(cache?.Data.Submarines.Select(s=>s.Name)??[]).Distinct(StringComparer.OrdinalIgnoreCase);
                        foreach(var name in names)
                        {
                            var live=observed?.Submarines.FirstOrDefault(s=>s.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
                            var saved=cache?.Data.Submarines.FirstOrDefault(s=>s.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
                            var rank=live?.Rank??saved?.Rank??0;var returns=live?.ReturnTime??saved?.ReturnTime??0;
                            if(!ImGui.TreeNode($"{name} · rank {rank} · {VoyageTimer(returns)}###sub-{name}"))continue;
                            if(live is not null)
                            {
                                ImGui.TextUnformatted("Workshop timer: "+VoyageTimer(live.ReturnTime));
                                ImGui.TextUnformatted("Route sector IDs: "+string.Join(", ",live.Route.Where(x=>x>0)));
                                ImGui.TextUnformatted("Workshop part IDs: "+string.Join(", ",live.Parts));
                            }
                            if(saved is not null)
                            {
                                if(live is not null)ImGui.TextUnformatted("Cached timer: "+VoyageTimer(saved.ReturnTime));
                                ImGui.TextUnformatted($"Cached EXP: {saved.CurrentExp:N0} / {saved.NextLevelExp:N0}");
                                foreach(var part in saved.PartItems.Select((id,i)=>i<saved.PartNames.Length&&saved.PartNames[i].Length>0?saved.PartNames[i]:$"Item #{id}"))ImGui.TextUnformatted(part);
                                ImGui.TextUnformatted("Cached route sector IDs: "+string.Join(", ",saved.Route.Where(x=>x>0)));
                            }
                            ImGui.TreePop();
                        }
                        ImGui.TreePop();
                    }
                    ImGui.PopID();
                }
                ImGui.TreePop();}
                ImGui.PopID();
                }
                ImGui.TreePop();
            }
            ImGui.EndChild();ImGui.PopID();ImGui.EndTabItem();
        }
        if(direct.Values.Any(v=>!represented.Contains(v.FcId))&&ImGui.BeginTabItem("Other fleets###submarine-unassigned")){
        foreach(var v in direct.Values.Where(v=>!represented.Contains(v.FcId)))
        {
            shown++;
            if(!ImGui.TreeNode($"{v.FcName} · character assignment pending###unassigned-{v.FcId}"))continue;
            ImGui.TextDisabled($"Workshop observed {v.At.LocalDateTime:g}");
            foreach(var sub in v.Submarines)if(ImGui.TreeNode($"{sub.Name} · {VoyageTimer(sub.ReturnTime)}###unassigned-sub-{sub.Slot}")){ImGui.TextUnformatted($"Rank {sub.Rank} · Parts: {string.Join(", ",sub.Parts)}");ImGui.TextUnformatted("Route: "+string.Join(", ",sub.Route));ImGui.TreePop();}
            ImGui.TreePop();
        }
        ImGui.EndTabItem();}
        ImGui.EndTabBar();
    }
}
