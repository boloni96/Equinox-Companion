using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private string plantingOverviewSearch="";
    private void DrawPlantingOverview()
    {
        ImGui.TextUnformatted("Planting · all shared gardens");
        if(config.SharedRoster is not {} roster){ImGui.TextWrapped("Connect your shared Journal in Settings to see everyone's houses and garden timers.");return;}
        ImGui.TextWrapped("Tend suggested every 12h. Harvest countdowns are estimates; Ready and Dead require game confirmation.");
        if(ImGui.SmallButton("Refresh gardens"))nextRosterRead=default;
        ImGui.SameLine();ImGui.TextDisabled($"Journal updated {roster.Updated.ToLocalTime():g}");
        ImGui.InputText("Find person / character / house",ref plantingOverviewSearch,120);
        var now=DateTimeOffset.UtcNow;
        var people=roster.People.Select(p=>p with {Characters=OrderedSharedCharacters(p)}).ToArray();
        var houses=GardenOverview.Houses(people,GardenPlanSources().Select(EffectiveGardenPlan));
        ImGui.TextWrapped($"{houses.Length} houses · "+GardenOverview.Summary(houses.SelectMany(h=>h.Batches),now));
        ImGui.Separator();
        if(!ImGui.BeginChild("planting-overview-scroll",Vector2.Zero)){ImGui.EndChild();return;}
        var searching=!string.IsNullOrWhiteSpace(plantingOverviewSearch);
        bool Match(string text)=>!searching||text.Contains(plantingOverviewSearch,StringComparison.OrdinalIgnoreCase);
        int PersonRank(SharedPerson p){var i=config.TabOrder.IndexOf("person:"+p.Id);return i<0?int.MaxValue:i;}
        foreach(var person in people.OrderBy(PersonRank))
        {
            var chars=person.Characters.Where(c=>Match(person.Name+" "+c.Name+" "+c.World+" "+c.Dc+" "+c.Account+" "+string.Join(" ",c.Houses.Select(h=>$"{h.Name} {h.OwnerName} {h.FcName} {h.District} W{h.Ward} P{h.Plot}")))).ToArray();
            if(chars.Length==0)continue;
            ImGui.PushID(person.Id);
            if(searching)ImGui.SetNextItemOpen(true,ImGuiCond.Always);
            var owned=houses.Where(h=>h.Person?.Id==person.Id).ToArray();
            if(ImGui.CollapsingHeader($"{person.Name.Replace("##","")} · {chars.Length} characters · {owned.Length} houses###person",ImGuiTreeNodeFlags.DefaultOpen))
            foreach(var character in chars)
            {
                ImGui.PushID(character.Id);
                var assigned=houses.Where(h=>h.Character?.Id==character.Id).ToArray();
                var shared=houses.Where(h=>h.Character?.Id!=character.Id&&character.Houses.Any(x=>x.Id==h.Batches[0].HouseId)).ToArray();
                var summary=assigned.Length>0?GardenOverview.Summary(assigned.SelectMany(h=>h.Batches),now):shared.Length>0?"Shared · "+GardenOverview.Summary(shared.SelectMany(h=>h.Batches),now):"No gardens recorded";
                if(searching)ImGui.SetNextItemOpen(true,ImGuiCond.Always);
                var expanded=ImGui.TreeNode($"{character.Name.Replace("##","")} · {character.World}###character");
                ImGui.TextWrapped(summary);
                if(expanded)
                {
                    ImGui.TextDisabled(character.Account+" · "+SharedLocation(character));
                    foreach(var house in assigned)DrawOverviewHouse(house,now);
                    foreach(var house in shared){ImGui.TextWrapped($"Shared: {house.House?.Name} · timers under {house.Person?.Name} / {house.Character?.Name}");}
                    if(assigned.Length==0&&shared.Length==0)ImGui.TextDisabled("No house garden linked to this character yet.");
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            ImGui.PopID();
        }
        foreach(var house in houses.Where(h=>h.Character is null))
            if(Match(string.Join(" ",house.Batches.Select(p=>$"{p.HouseName} {p.World} {p.District} W{p.Ward} P{p.Plot}"))))DrawOverviewHouse(house,now);
        ImGui.EndChild();
    }

    private void DrawOverviewHouse(GardenOverviewHouse house,DateTimeOffset now)
    {
        var first=house.Batches[0];var estate=house.House;
        var owner=estate?.Type=="Free Company house"?"FC: "+(string.IsNullOrWhiteSpace(estate.FcName)?"Name not recorded":estate.FcName):"Owner: "+(string.IsNullOrWhiteSpace(estate?.OwnerName)?"Not recorded":estate.OwnerName);
        ImGui.PushID(first.HouseId);
        if(ImGui.TreeNode($"{owner.Replace("##","")}###house"))
        {
            ImGui.TextWrapped($"{first.World} · {first.District} · W{first.Ward} P{first.Plot}");
            if(!string.IsNullOrWhiteSpace(first.HouseName))ImGui.TextUnformatted(first.HouseName);
            if(house.LinkedCharacters.Length>1)ImGui.TextWrapped("Shared with: "+string.Join(", ",house.LinkedCharacters));
            foreach(var batch in house.Batches)
            {
                ImGui.PushID(batch.Batch);
                var expanded=ImGui.TreeNode($"Batch {batch.Batch}###batch");
                ImGui.SameLine();
                if(ImGui.SmallButton("Website plan"))Dalamud.Utility.Util.OpenLink(GardenCareStatus.WebsiteUrl(batch.HouseId,batch.Batch));
                if(SharedGardenLocation.Match(currentAddress,[batch])==batch.HouseId)
                {
                    ImGui.SameLine();if(ImGui.SmallButton("Open planting guide")){plantingBatch=batch.Batch;previousPlantingHouse=batch.HouseId;plantingHouseId=batch.HouseId;plantingWindow.IsOpen=true;}
                }
                ImGui.TextWrapped(GardenOverview.Summary([batch],now));
                if(expanded)
                {
                    if(ImGui.BeginTable("beds",4,ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.RowBg|ImGuiTableFlags.Resizable))
                    {
                        ImGui.TableSetupColumn("Bed / crop");ImGui.TableSetupColumn("Status");ImGui.TableSetupColumn("Tending");ImGui.TableSetupColumn("Harvest estimate");ImGui.TableHeadersRow();
                        foreach(var bed in batch.Beds.OrderBy(b=>b.Bed))
                        {
                            var row=GardenOverview.Bed(batch,bed,now);
                            var color=row.State=="dead"?Red:row.State=="ready"?Green:row.State is "wilted" or "wilt-estimated" or "at-risk" or "dead-estimated"?Orange:row.TendDue?new Vector4(.4f,.75f,1,1):Grey;
                            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextWrapped($"{bed.Bed} · {row.Crop}");
                            if(ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                if(bed.Crop.Length>0)ImGui.TextUnformatted("Plan: "+bed.Crop);
                                if(bed.ActualSoil.Length>0)ImGui.TextUnformatted("Soil: "+bed.ActualSoil);
                                if(bed.Planted is {} planted)ImGui.TextUnformatted($"Planted: {planted.ToLocalTime():g}");
                                if(bed.Watered is {} watered&&!GardenTiming.FirstTendDue(bed.Planted,bed.Watered,now))ImGui.TextUnformatted($"Last tended: {watered.ToLocalTime():g} · {(bed.TendedBy.Length>0?bed.TendedBy:"Unknown gardener")}");
                                if(bed.ObservedAt is {} seen)ImGui.TextUnformatted($"Last observed: {seen.ToLocalTime():g}");
                                ImGui.EndTooltip();
                            }
                            ImGui.TableNextColumn();ImGui.PushStyleColor(ImGuiCol.Text,color);ImGui.TextWrapped(row.Status);ImGui.PopStyleColor();
                            ImGui.TableNextColumn();ImGui.TextWrapped(row.Care);
                            if(ImGui.IsItemHovered()&&row.NextTend is {} tend)ImGui.SetTooltip($"Suggested tend: {tend.ToLocalTime():g}");
                            ImGui.TableNextColumn();ImGui.TextWrapped(row.Harvest);
                            if(ImGui.IsItemHovered()&&row.HarvestAt is {} harvest)ImGui.SetTooltip($"Estimated harvest: {harvest.ToLocalTime():g}");
                        }
                        ImGui.EndTable();
                    }
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }
            ImGui.TreePop();
        }
        ImGui.PopID();
    }
}
