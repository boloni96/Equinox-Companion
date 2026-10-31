using System.Text.Json;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record GearDutySource(uint Id,uint Type,string Name,uint[] Items);
    private string gearOrigin="",armoireOrigin="";
    private bool dutySourcesFailed;
    private IEnumerable<bool> ReadGearDutySources()
    {
        GearDutySource[] rows;
        try {
            rows=JsonSerializer.Deserialize<GearDutySource[]>(File.ReadAllText(Path.Combine(Pi.AssemblyLocation.DirectoryName!,"gear-duty-sources.json")))??[];
        } catch(Exception e) { dutySourcesFailed=true;Log.Warning(e,"Duty source catalogue unavailable");yield break; }
        foreach(var row in rows) {
            var kind=row.Type switch {2=>"Dungeon",4=>"Trial",5=>"Raid",_=>"Other duty"};
            foreach(var id in row.Items) {
                if(GearStorageItemAllowed(id))AddMarketSource(id,kind,kind+" · "+row.Name+" (Teamcraft)");
                yield return true;
            }
        }
    }
    private bool GearOriginAllowed(uint id,string origin)
    {
        if(origin.Length==0)return true;
        return marketSourcesReady&&!marketSourcesFailed&&marketSources.TryGetValue(id,out var source)&&source.Kinds.Contains(origin);
    }
    private void DrawGearOriginPicker(string scope,ref string origin)
    {
        PumpMarketSources();
        ImGui.PushID(scope);ImGui.SetNextItemWidth(240);
        if(ImGui.BeginCombo("Obtained from",origin.Length==0?"Any known or unknown source":origin)) {
            if(ImGui.Selectable("Any known or unknown source",origin.Length==0))origin="";
            foreach(var kind in new[]{"Dungeon","Trial","Raid","Other duty","Crafted","Vendor","Quest","Seasonal event","Achievement"})
                if(ImGui.Selectable(kind,origin==kind))origin=kind;
            ImGui.EndCombo();
        }
        if(!marketSourcesReady)ImGui.TextDisabled(marketSourcesFailed?"Source index unavailable; source-filtered moves are blocked.":"Loading source filters…");
        if(dutySourcesFailed)ImGui.TextWrapped("Duty source data unavailable. Dungeon/trial/raid filters cannot be used.");
        MarketHelp("An item can match several sources. Duty sources use bundled Teamcraft data; other sources use game sheets. Missing data remains unclassified, not assumed to be dungeon gear. Armoire group, source and equipment filters combine.");
        ImGui.PopID();
    }
}
