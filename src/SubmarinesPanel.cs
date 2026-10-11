using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static bool SubmarineBar(string label,int level)
    {
        var c=level switch{3=>new System.Numerics.Vector4(.95f,.48f,.12f,1),2=>new System.Numerics.Vector4(.85f,.72f,.12f,1),1=>new System.Numerics.Vector4(.18f,.65f,.35f,1),_=>new System.Numerics.Vector4(.35f,.38f,.42f,1)};
        ImGui.PushStyleColor(ImGuiCol.Header,c*new System.Numerics.Vector4(.45f,.45f,.45f,1));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered,c*new System.Numerics.Vector4(.7f,.7f,.7f,1));ImGui.PushStyleColor(ImGuiCol.HeaderActive,c);
        var open=ImGui.TreeNodeEx(label,ImGuiTreeNodeFlags.Framed|ImGuiTreeNodeFlags.SpanAvailWidth);ImGui.PopStyleColor(3);return open;
    }
    private static string VoyageTimer(long seconds)
    {
        if(seconds<=0)return "No voyage recorded";
        var remaining=DateTimeOffset.FromUnixTimeSeconds(seconds)-DateTimeOffset.UtcNow;
        return remaining<=TimeSpan.Zero?"Voyage Complete":$"Returns in {(int)remaining.TotalHours}h {remaining.Minutes}m";
    }
    private readonly UiProjectionCache<SubmarineDisplayIndex> submarineDisplayCache=new(0);
    private readonly UiProjectionCache<Dictionary<string,FleetAttention>> submarineAttentionCache=new(0);
    private readonly UiProjectionCache<IGrouping<string,SharedCharacter>[]> submarineAccountCache=new();
    private double submarineDrawLastMs,submarineDrawPeakMs,submarineIndexLastMs;
    private long submarineIndexBuilds;
    private SubmarineDisplayIndex BuildSubmarineDisplay()
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        var result=SubmarineDisplayIndex.Build(config.SharedRoster,config.Discoveries);
        submarineIndexLastMs=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;submarineIndexBuilds++;
        return result;
    }
    private void DrawSubmarines()
    {
        var started=System.Diagnostics.Stopwatch.GetTimestamp();
        try{DrawSubmarinesContents();}
        finally{submarineDrawLastMs=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;submarineDrawPeakMs=Math.Max(submarineDrawPeakMs,submarineDrawLastMs);}
    }
    private void DrawSubmarinesContents()
    {
        ImGui.TextWrapped("Open the FC workshop voyage panel to refresh timers. Character and account order follows your Persons tabs.");
        ImGui.TextWrapped(config.SyncAutoRetainer?autoRetainerStatus:"Background submarine sync is off. Enable it in Settings > Tracking.");
        if(ImGui.TreeNode("Colour rules / reserve reminders")){
            ImGui.TextWrapped("Green: voyaging or Voyage Complete. Yellow: dispatch or a confirmed unlocked empty slot. Orange: repairs or low reserves for a recorded fleet. Grey: no fleet evidence; stock alone never creates maintenance warnings. Counts are last recorded values.");
            DrawCommittedInteger("Ceruleum reserve",config.SubmarineFuelReserve,1,100000,v=>config.SubmarineFuelReserve=v);
            DrawCommittedInteger("Repair-kit reserve",config.SubmarineRepairReserve,1,100000,v=>config.SubmarineRepairReserve=v);
            DrawCommittedInteger("Free inventory slots reserve",config.SubmarineSpaceReserve,1,140,v=>config.SubmarineSpaceReserve=v);
            ImGui.TreePop();
        }
        var index=submarineDisplayCache.Get(config.SharedRoster,
            $"{config.Discoveries.Count}:{config.Discoveries.LastOrDefault()?.Id}",Environment.TickCount64,BuildSubmarineDisplay);
        var (direct,caches,supplies)=index;
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        FleetAttention Attention(SharedCharacter c){
            caches.TryGetValue(c.Id,out var cache);if(cache?.Data.FcId!=c.FcId||c.FcMember==false)cache=null;
            direct.TryGetValue(c.FcId,out var fleet);if(c.FcMember==false)fleet=null;
            supplies.TryGetValue(c.Id,out var supplied);
            return SubmarineAttention.Evaluate(fleet?.Submarines.Select(x=>x.ReturnTime).ToArray()??cache?.Data.Submarines.Select(x=>x.ReturnTime).ToArray()??[],fleet?.Submarines.Select(x=>x.NeedsRepair).ToArray()??[],SubmarineSupplyStatus.Select(c.FcId,supplied,cache),fleet?.Slots,now,config.SubmarineFuelReserve,config.SubmarineRepairReserve,config.SubmarineSpaceReserve);
        }
        var attention=submarineAttentionCache.Get(index,$"{now}:{config.SubmarineFuelReserve}:{config.SubmarineRepairReserve}:{config.SubmarineSpaceReserve}",Environment.TickCount64,
            ()=>(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).GroupBy(c=>c.Id).ToDictionary(g=>g.Key,g=>Attention(g.First())));
        if(ImGui.SmallButton("Refresh submarine data")){autoRetainerForceRead=true;autoRetainerCharacters.Clear();nextAutoRetainerRead=default;nextRosterRead=default;}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Read available cached records and refresh the paired Journal. Offline characters retain their last recorded values.");
        var represented=new HashSet<string>();var shown=0;
        var people=config.SharedRoster?.People??[];
        int PersonRank(SharedPerson p){var i=config.TabOrder.IndexOf("person:"+p.Id);return i<0?int.MaxValue:i;}
        foreach(var c in people.SelectMany(p=>p.Characters).Where(c=>c.FcMember!=false&&direct.ContainsKey(c.FcId)))represented.Add(c.FcId);
        if(!ImGui.BeginTabBar("submarine-persons"))return;
        foreach(var person in people.OrderBy(PersonRank))
        {
            if(!ImGui.BeginTabItem(person.Name.Replace("##","")+"###submarine-person-"+person.Id))continue;
            var accounts=submarineAccountCache.Get(index,$"{person.Id}:{Player.ContentId}:{config.HouseCharactersFirst}",Environment.TickCount64,
                ()=>OrderedSharedCharacters(person).Where(c=>c.FcMember!=false&&direct.ContainsKey(c.FcId)||caches.ContainsKey(c.Id)||supplies.ContainsKey(c.Id)).GroupBy(SharedCharacterGrouping.AccountKey).ToArray());
            var hasData=accounts.Length>0;
            ImGui.PushID(person.Id);
            ImGui.BeginChild("submarine-person-scroll",System.Numerics.Vector2.Zero);
            if(!hasData)ImGui.TextWrapped("No submarine or supply records for this person yet.");
            foreach(var account in accounts)
            {
                var rows=account.Where(c=>c.FcMember!=false&&direct.ContainsKey(c.FcId)||caches.ContainsKey(c.Id)||supplies.ContainsKey(c.Id)).OrderBy(c=>Array.IndexOf(new[]{"Regulars","Floaters","Empty","Pending sync"},SharedCharacterGrouping.Group(c))).ToArray();
                if(rows.Length==0)continue;
                foreach(var c in rows)if(c.FcMember!=false&&direct.ContainsKey(c.FcId))represented.Add(c.FcId);
                shown+=rows.Length;
                ImGui.SetNextItemOpen(false,ImGuiCond.Once);
                if(!SubmarineBar($"{account.First().Account} · {rows.Length} characters###account-{account.Key}",SubmarineAttention.Combine(rows.Select(c=>attention[c.Id].Level))))continue;
                foreach(var group in rows.GroupBy(SharedCharacterGrouping.Group))
                {
                ImGui.PushID(group.Key);ImGui.SetNextItemOpen(false,ImGuiCond.Once);
                if(SubmarineBar($"{group.Key} · {group.Count()}###group",SubmarineAttention.Combine(group.Select(c=>attention[c.Id].Level)))){
                foreach(var c in group)
                {
                    ImGui.PushID(c.Id);
                    ImGui.SetNextItemOpen(false,ImGuiCond.Once);
                    caches.TryGetValue(c.Id,out var cache);if(cache?.Data.FcId!=c.FcId||c.FcMember==false)cache=null;direct.TryGetValue(c.FcId,out var observed);if(c.FcMember==false)observed=null;
                    supplies.TryGetValue(c.Id,out var supplyRecord);var supply=SubmarineSupplyStatus.Select(c.FcId,supplyRecord,cache);
                    var returnTimes=(observed?.Submarines.Select(s=>s.ReturnTime)??cache?.Data.Submarines.Select(s=>s.ReturnTime)??[]).Where(t=>t>0).ToArray();
                    var timer=returnTimes.Length>0?" · "+VoyageTimer(returnTimes.Min()):"";
                    var status=attention[c.Id];
                    var expanded=SubmarineBar($"{c.Name} · {c.World}{timer} · {status.Text}###character",status.Level);
                    if(ImGui.IsItemHovered())ImGui.SetTooltip(SubmarineSupplyStatus.Summary(supply)+(supply is null?"":"\n"+SubmarineSupplyStatus.Freshness(supply,DateTimeOffset.UtcNow)));
                    if(expanded)
                    {
                        ImGui.TextWrapped(status.Text);
                        ImGui.TextWrapped(SubmarineSupplyStatus.Summary(supply));
                        if(supply is not null)ImGui.TextWrapped(SubmarineSupplyStatus.Freshness(supply,DateTimeOffset.UtcNow));
                        var fcIconSize=ImGui.GetFontSize()*1.5f;
                        GardenImage("assets/category-icons/free-company.png",ImGui.GetCursorScreenPos(),new System.Numerics.Vector2(fcIconSize));
                        ImGui.Dummy(new System.Numerics.Vector2(fcIconSize));ImGui.SameLine();
                        ImGui.TextWrapped((observed?.FcName??cache?.FcName??"Free Company")+" · FC fleet");
                        var knownSlots=observed?.Slots;
                        var registered=observed?.Submarines.Length??cache?.Data.Submarines.Length??0;
                        if(knownSlots is {} capacity && capacity>registered)ImGui.TextWrapped($"A new submarine can be registered! ({registered}/{capacity})");
                        for(var slot=registered;slot<4;slot++)ImGui.TextDisabled($"Submarine {slot+1}: "+(knownSlots is null?"unlock status not recorded":slot<knownSlots?"unlocked · ready to register":"locked · explore "+new[]{"workshop","J · Unidentified Derelict","O · The Wreckage of Discovery I","T · Purgatory"}[slot]));
                        if(cache is not null)
                        {
                            var v=cache.Data;
                            ImGui.TextWrapped($"Submarine capacity: {v.Slots?.ToString()??"unknown"} slots");
                            if(supply?.Observed==true)ImGui.TextWrapped($"Cached supplies: {v.Ceruleum?.ToString()??"?"} tanks · {v.RepairKits?.ToString()??"?"} repairs · {v.InventorySpace?.ToString()??"?"} free inventory slots");
                            ImGui.TextDisabled($"Cache imported {cache.ImportedAt.LocalDateTime:g}; game observation time unknown.");
                        }
                        if(observed is not null)ImGui.TextDisabled($"Workshop observed {observed.At.LocalDateTime:g}");
                        var names=(observed?.Submarines.Select(s=>s.Name)??cache?.Data.Submarines.Select(s=>s.Name)??[]).Distinct(StringComparer.OrdinalIgnoreCase);
                        foreach(var name in names)
                        {
                            var live=observed?.Submarines.FirstOrDefault(s=>s.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
                            var saved=cache?.Data.Submarines.FirstOrDefault(s=>s.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
                            var rank=live?.Rank??saved?.Rank??0;var returns=live?.ReturnTime??saved?.ReturnTime??0;
                            var subStatus=SubmarineAttention.Evaluate([returns],[live?.NeedsRepair],supply,null,now,config.SubmarineFuelReserve,config.SubmarineRepairReserve,config.SubmarineSpaceReserve);
                            if(!SubmarineBar($"{name} · rank {rank} · {VoyageTimer(returns)} · {subStatus.Text}###sub-{name}",subStatus.Level))continue;
                            ImGui.TextWrapped(live?.NeedsRepair is {} repair?(repair?"Observed repair needed":"No broken parts at last workshop observation"):"Part condition not recorded — open workshop to refresh");
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
            if(!SubmarineBar($"{v.FcName} · character assignment pending###unassigned-{v.FcId}",SubmarineAttention.Evaluate(v.Submarines.Select(x=>x.ReturnTime).ToArray(),v.Submarines.Select(x=>x.NeedsRepair).ToArray(),null,null,now).Level))continue;
            ImGui.TextDisabled($"Workshop observed {v.At.LocalDateTime:g}");
            foreach(var sub in v.Submarines)if(SubmarineBar($"{sub.Name} · {VoyageTimer(sub.ReturnTime)}###unassigned-sub-{sub.Slot}",SubmarineAttention.Evaluate([sub.ReturnTime],[sub.NeedsRepair],null,null,now).Level)){ImGui.TextUnformatted($"Rank {sub.Rank} · Parts: {string.Join(", ",sub.Parts)}");ImGui.TextUnformatted("Route: "+string.Join(", ",sub.Route));ImGui.TreePop();}
            ImGui.TreePop();
        }
        ImGui.EndTabItem();}
        ImGui.EndTabBar();
    }
}
