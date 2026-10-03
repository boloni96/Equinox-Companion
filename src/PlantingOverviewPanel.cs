using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private string plantingOverviewSearch="";
    private readonly Dictionary<string,bool> plantingOverviewAccounts=[];
    private static Vector4 OverviewColour(IEnumerable<SharedGardenPlan> plans,DateTimeOffset now) => GardenOverview.Attention(plans,now) switch {"dead"=>Red,"risk" or "check"=>Orange,"tend"=>new(.4f,.75f,1,1),"harvest"=>Green,_=>Grey};
    private float DrawGardenIndicators(IEnumerable<SharedGardenPlan> plans,DateTimeOffset now,Vector2 at,float size)
    {
        var icons=GardenOverview.Indicators(plans,now);var draw=ImGui.GetWindowDrawList();
        foreach(var icon in icons){var color=icon is "ready" or "matched"?Green:icon=="tend"?new Vector4(.25f,.65f,1,1):icon=="unknown"?Grey:Orange;
            draw.AddRectFilled(at,at+new Vector2(size),ImGui.ColorConvertFloat4ToU32(new Vector4(color.X,color.Y,color.Z,.2f)),3);
            GardenImage("assets/icons/"+icon+".png",at+new Vector2(1),new Vector2(size-2));
            draw.AddRect(at,at+new Vector2(size),ImGui.ColorConvertFloat4ToU32(color),3);at.X+=size+3;}
        return icons.Length*(size+3);
    }
    private void DrawOverviewIndicators(IEnumerable<SharedGardenPlan> plans,DateTimeOffset now)
    {
        var list=plans.ToArray();var icons=GardenOverview.Indicators(list,now);if(icons.Length==0)return;
        ImGui.SameLine();var at=ImGui.GetCursorScreenPos();var size=ImGui.GetTextLineHeight();
        ImGui.Dummy(new Vector2(icons.Length*(size+3),size));DrawGardenIndicators(list,now,at,size);
        if(ImGui.IsItemHovered())ImGui.SetTooltip(GardenOverview.Summary(list,now));
    }
    private void DrawPlantingOverview()
    {
        ImGui.TextUnformatted("Gardening · all shared gardens");
        if(config.SharedRoster is not {} roster){ImGui.TextWrapped("Connect your shared Journal in Settings to see everyone's houses and garden timers.");return;}
        ImGui.TextWrapped("Tend suggested every 12h. Harvest countdowns are estimates; Ready and Dead require game confirmation.");
        ImGui.TextDisabled("Name bars: T = next suggested tend · H = estimated harvest. Hover for every batch; click to expand.");
        if(ImGui.SmallButton("Refresh gardens"))nextRosterRead=default;
        ImGui.SameLine();ImGui.TextDisabled($"Journal updated {roster.Updated.ToLocalTime():g}");
        ImGui.InputText("Find person / character / house",ref plantingOverviewSearch,120);
        var now=DateTimeOffset.UtcNow;
        var people=roster.People.Select(p=>p with {Characters=OrderedSharedCharacters(p)}).ToArray();
        var houses=GardenOverview.Houses(people,GardenPlanSources().Select(EffectiveGardenPlan));
        ImGui.TextWrapped($"{houses.Length} houses · "+GardenOverview.Summary(houses.SelectMany(h=>h.Batches),now));
        ImGui.Separator();
        var searching=!string.IsNullOrWhiteSpace(plantingOverviewSearch);
        bool Match(string text)=>!searching||text.Contains(plantingOverviewSearch,StringComparison.OrdinalIgnoreCase);
        int PersonRank(SharedPerson p){var i=config.TabOrder.IndexOf("person:"+p.Id);return i<0?int.MaxValue:i;}
        ImGui.TextColored(new Vector4(.4f,.75f,1,1),"Blue: tend");ImGui.SameLine();ImGui.TextColored(Green,"Green: ready / cared for");ImGui.SameLine();ImGui.TextColored(Orange,"Orange: check / risk · mixed icons show together");
        if(!ImGui.BeginTabBar("planting-persons"))return;
        foreach(var person in people.OrderBy(PersonRank))
        {
            if(!ImGui.BeginTabItem(person.Name.Replace("##","")+"###planting-person-"+person.Id))continue;
            ImGui.PushID(person.Id);
            if(ImGui.BeginChild("planting-person-scroll",Vector2.Zero))
            {
                var chars=person.Characters.Where(c=>Match(person.Name+" "+c.Name+" "+c.World+" "+c.Dc+" "+c.Account+" "+string.Join(" ",c.Houses.Select(h=>$"{h.Name} {h.OwnerName} {h.FcName} {h.District} W{h.Ward} P{h.Plot}")))).ToArray();
                var owned=houses.Where(h=>h.Person?.Id==person.Id).ToArray();
                ImGui.TextWrapped($"{chars.Length} characters · {owned.Length} houses · "+GardenOverview.Summary(owned.SelectMany(h=>h.Batches),now));
                foreach(var account in chars.GroupBy(SharedCharacterGrouping.AccountKey))
                {
                    ImGui.PushID("account-"+account.Key);
                    var key=person.Id+":"+account.Key;
                    var expanded=searching||plantingOverviewAccounts.GetValueOrDefault(key);
                    ImGui.SetNextItemOpen(expanded,ImGuiCond.Always);
                    var name=string.IsNullOrWhiteSpace(account.First().Account)?"Account":account.First().Account.Replace("##","");
                    var open=ImGui.CollapsingHeader($"{name} · {account.Count()} characters###account");
                    if(!searching&&open!=expanded)plantingOverviewAccounts[key]=open;
                    if(open)
                    {
                        ImGui.Indent();
                        foreach(var group in new[]{"Regulars","Floaters","Empty","Pending sync"})
                        {
                            var grouped=account.Where(c=>SharedCharacterGrouping.Group(c)==group).ToArray();
                            if(grouped.Length==0)continue;
                            ImGui.PushID(group);
                            ImGui.SetNextItemOpen(searching,searching?ImGuiCond.Always:ImGuiCond.Once);
                            if(ImGui.TreeNodeEx($"{group} · {grouped.Length}###group",ImGuiTreeNodeFlags.None))
                            {
                                foreach(var character in grouped)DrawOverviewCharacter(character,houses,now,searching);
                                ImGui.TreePop();
                            }
                            ImGui.PopID();
                        }
                        ImGui.Unindent();
                    }
                    ImGui.PopID();
                }
                if(chars.Length==0)ImGui.TextDisabled(searching?"No characters match this search.":"No visible characters in this profile.");
            }
            ImGui.EndChild();ImGui.PopID();ImGui.EndTabItem();
        }
        var unassigned=houses.Where(h=>h.Character is null).ToArray();
        if(unassigned.Length>0&&ImGui.BeginTabItem("Other gardens###planting-unassigned"))
        {
            if(ImGui.BeginChild("planting-unassigned-scroll",Vector2.Zero))
                foreach(var house in unassigned)
                    if(Match(string.Join(" ",house.Batches.Select(p=>$"{p.HouseName} {p.World} {p.District} W{p.Ward} P{p.Plot}"))))DrawOverviewHouse(house,now);
            ImGui.EndChild();ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawOverviewCharacter(SharedCharacter character,GardenOverviewHouse[] houses,DateTimeOffset now,bool searching)
    {
        ImGui.PushID(character.Id);
        var assigned=houses.Where(h=>h.Character?.Id==character.Id).ToArray();
        var shared=houses.Where(h=>h.Character?.Id!=character.Id&&character.Houses.Any(x=>x.Id==h.Batches[0].HouseId)).ToArray();
        var linked=assigned.Concat(shared).ToArray();
        var plans=linked.SelectMany(h=>h.Batches).ToArray();
        var position=ImGui.GetCursorScreenPos();
        var width=Math.Max(1,ImGui.GetContentRegionAvail().X);
        var nameWidth=width*.42f;
        var iconSize=ImGui.GetTextLineHeight();
        var iconWidth=GardenOverview.Indicators(plans,now).Length*(iconSize+3);
        var timersWidth=Math.Max(1,width-nameWidth-iconWidth-ImGui.GetStyle().FramePadding.X*2);
        var timers=string.Join(" | ",linked.SelectMany(h=>h.Batches.Select(b=>
            $"{(linked.Length>1?(h.House?.Type=="Free Company house"?"FC ":"Private "):"")}B{b.Batch}: {GardenOverview.HeaderSummary([b],now)}")));
        if(plans.Length==0)timers="No gardens recorded";
        else if(ImGui.CalcTextSize(timers).X>timersWidth)
            timers=$"{plans.Length} batch{(plans.Length==1?"":"es")} · "+GardenOverview.HeaderSummary(plans,now);
        ImGui.SetNextItemOpen(searching,searching?ImGuiCond.Always:ImGuiCond.Once);
        var label=FitOverviewText($"{character.Name.Replace("##","")} · {character.World}",nameWidth-ImGui.GetFrameHeight());
        var colour=OverviewColour(plans,now);
        ImGui.PushStyleColor(ImGuiCol.Header,new Vector4(colour.X,colour.Y,colour.Z,.22f));
        var expanded=ImGui.CollapsingHeader(label+"###character");
        ImGui.PopStyleColor();
        var hovered=ImGui.IsItemHovered();
        var draw=ImGui.GetWindowDrawList();
        draw.PushClipRect(new Vector2(position.X+nameWidth,position.Y),new Vector2(position.X+width-iconWidth,position.Y+ImGui.GetFrameHeight()),true);
        draw.AddText(new Vector2(position.X+nameWidth,position.Y+ImGui.GetStyle().FramePadding.Y),ImGui.ColorConvertFloat4ToU32(colour),FitOverviewText(timers,timersWidth));
        draw.PopClipRect();
        DrawGardenIndicators(plans,now,new Vector2(position.X+width-iconWidth,position.Y+ImGui.GetStyle().FramePadding.Y),iconSize);
        if(hovered)
        {
            ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*40);
            ImGui.TextUnformatted($"{character.Name} · {character.World} · {character.Account}");
            if(plans.Length==0)ImGui.TextDisabled("No gardens recorded.");
            foreach(var house in linked)
            {
                ImGui.Separator();
                var first=house.Batches[0];
                ImGui.TextWrapped(OverviewOwner(house)+$" · {first.World} · {first.District} W{first.Ward} P{first.Plot}");
                foreach(var batch in house.Batches)
                {
                    ImGui.TextWrapped($"Batch {batch.Batch}: "+GardenOverview.Summary([batch],now));
                    var last=batch.Beds.Where(b=>b.Watered is not null&&!GardenTiming.FirstTendDue(b.Planted,b.Watered,now)).MaxBy(b=>b.Watered);
                    if(last?.Watered is {} at)ImGui.TextWrapped($"Last tended: {at.ToLocalTime():g} · {(last.TendedBy.Length>0?last.TendedBy:"Unknown gardener")}");
                }
            }
            ImGui.TextDisabled("T: suggested every 12h · H: estimate; confirm maturity in game.");
            ImGui.PopTextWrapPos();ImGui.EndTooltip();
        }
        if(expanded)
        {
            ImGui.Indent();
            ImGui.TextDisabled(character.Account+" · "+SharedLocation(character));
            foreach(var house in assigned)DrawOverviewHouse(house,now);
            foreach(var house in shared)
            {
                ImGui.TextDisabled($"Shared with {house.Person?.Name} / {house.Character?.Name}");
                DrawOverviewHouse(house,now);
            }
            if(assigned.Length==0&&shared.Length==0)ImGui.TextDisabled("No house garden linked to this character yet.");
            ImGui.Unindent();
        }
        ImGui.PopID();
    }

    private static string FitOverviewText(string text,float width)
    {
        if(ImGui.CalcTextSize(text).X<=width)return text;
        const string tail="…";
        while(text.Length>0&&ImGui.CalcTextSize(text+tail).X>width)text=text[..^1];
        return text+tail;
    }

    private static string OverviewOwner(GardenOverviewHouse house) => house.House?.Type=="Free Company house"
        ? "FC: "+(string.IsNullOrWhiteSpace(house.House.FcName)?"Name not recorded":house.House.FcName)
        : "Owner: "+(string.IsNullOrWhiteSpace(house.House?.OwnerName)?"Not recorded":house.House.OwnerName);

    private void DrawOverviewHouse(GardenOverviewHouse house,DateTimeOffset now)
    {
        var first=house.Batches[0];var estate=house.House;
        var owner=OverviewOwner(house);
        ImGui.PushID(first.HouseId);
        ImGui.SetNextItemOpen(false,ImGuiCond.Once);
        ImGui.PushStyleColor(ImGuiCol.Text,OverviewColour(house.Batches,now));
        var houseOpen=ImGui.TreeNode($"{owner.Replace("##","")}###house");
        ImGui.PopStyleColor();
        DrawOverviewIndicators(house.Batches,now);
        if(houseOpen)
        {
            ImGui.TextWrapped($"{first.World} · {first.District} · W{first.Ward} P{first.Plot}");
            if(!string.IsNullOrWhiteSpace(first.HouseName))ImGui.TextUnformatted(first.HouseName);
            if(house.LinkedCharacters.Length>1)ImGui.TextWrapped("Shared with: "+string.Join(", ",house.LinkedCharacters));
            foreach(var batch in house.Batches)
            {
                ImGui.PushID(batch.Batch);
                ImGui.SetNextItemOpen(false,ImGuiCond.Once);
                ImGui.PushStyleColor(ImGuiCol.Text,OverviewColour([batch],now));
                var expanded=ImGui.TreeNode($"Batch {batch.Batch}###batch");
                ImGui.PopStyleColor();
                DrawOverviewIndicators([batch],now);
                ImGui.SameLine();
                if(ImGui.SmallButton("Website plan"))Dalamud.Utility.Util.OpenLink(GardenCareStatus.WebsiteUrl(batch.HouseId,batch.Batch));
                if(SharedGardenLocation.Match(currentAddress,[batch])==batch.HouseId)
                {
                    ImGui.SameLine();if(ImGui.SmallButton("Open planting guide")){plantingBatch=batch.Batch;previousPlantingHouse=batch.HouseId;plantingHouseId=batch.HouseId;plantingWindow.IsOpen=true;}
                }
                ImGui.PushStyleColor(ImGuiCol.Text,OverviewColour([batch],now));
                ImGui.TextWrapped(GardenOverview.Summary([batch],now));
                ImGui.PopStyleColor();
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
