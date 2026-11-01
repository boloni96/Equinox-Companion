using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record CabinetSource(uint Item,uint Group,uint Subgroup,string GroupName,string SubgroupName,int GroupOrder,int SubgroupOrder);
    private CabinetSource[]? cabinetSources;
    private Dictionary<uint,CabinetSource>? cabinetSourceItems;
    private uint armoireSourceGroup,armoireSourceSubgroup,gearSourceGroup,gearSourceSubgroup;
    private int gearStorageKind,gearAppearance,armoireAppearance;
    private readonly Dictionary<uint,bool> cosmeticGearCache=[];
    private bool CosmeticGear(uint id)
    {
        if(cosmeticGearCache.TryGetValue(id,out var cached))return cached;
        var item=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id);
        return cosmeticGearCache[id]=item is {} r&&r.EquipSlotCategory.RowId>0&&OrganizerStoragePolicy.Cosmetic(r.LevelEquip,r.LevelItem.RowId,r.BaseParamValue.Any(v=>v>0));
    }
    private bool GearAppearanceAllowed(uint id,int filter)=>filter==0||(filter==1?CosmeticGear(id):!CosmeticGear(id));
    private void DrawGearAppearancePicker(ref int filter)
    {
        ImGui.SetNextItemWidth(240);ImGui.Combo("Gear type",ref filter,"All gear\0Glamour / cosmetic gear\0Other gear\0");
        MarketHelp("Cosmetic filter: equip level 1, item level 1, and no attribute bonuses. Independent of equipment slot or dresser outfit-set format.");
    }
    private HashSet<uint>? gearOutfitItems;
    private void LoadCabinetSources()
    {
        if(cabinetSources!=null)return;
        LoadArmoireCatalogue();
        cabinetSources=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>()
            .Where(r=>r.Item.RowId>0&&r.Category.RowId>0)
            .Select(r=>new CabinetSource(r.Item.RowId,r.Category.RowId,r.SubCategory.RowId,
                r.Category.Value.Category.Value.Text.ExtractText(),r.SubCategory.Value.Name.ExtractText(),
                (int)r.Category.Value.MenuOrder,(int)r.SubCategory.Value.MenuOrder)).ToArray();
        cabinetSourceItems=cabinetSources.GroupBy(r=>r.Item).ToDictionary(g=>g.Key,g=>g.First());
        gearOutfitItems=armoireSets!.Values.SelectMany(v=>v).Where(id=>id>0).ToHashSet();
    }
    private bool GearStorageItemAllowed(uint id)
    {
        LoadArmoireCatalogue();
        var item=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id);
        return item is {} row&&row.EquipSlotCategory.RowId>0&&(row.IsGlamorous||armoireRows!.ContainsKey(id));
    }
    private bool CabinetSourceAllowed(uint id,uint group,uint subgroup)
    {
        if(group==0&&subgroup==0)return true;
        LoadCabinetSources();
        return cabinetSourceItems!.TryGetValue(id,out var source)&&(group==0||source.Group==group)&&(subgroup==0||source.Subgroup==subgroup);
    }
    private bool ArmoireSourceAllowed(uint id)=>GearAppearanceAllowed(id,armoireAppearance)&&CabinetSourceAllowed(id,armoireSourceGroup,armoireSourceSubgroup)&&GearOriginAllowed(id,armoireOrigin);
    private bool GearSourceAllowed(uint id)
    {
        LoadCabinetSources();
        if(!GearAppearanceAllowed(id,gearAppearance)||!GearOriginAllowed(id,gearOrigin))return false;
        if(!CabinetSourceAllowed(id,gearSourceGroup,gearSourceSubgroup))return false;
        return gearStorageKind switch {
            1=>armoireRows!.ContainsKey(id),
            2=>!armoireRows!.ContainsKey(id)&&GearStorageItemAllowed(id),
            3=>gearOutfitItems!.Contains(id),
            _=>true
        };
    }
    private void DrawCabinetSourcePicker(string scope,ref uint group,ref uint subgroup)
    {
        LoadCabinetSources();
        var selectedGroup=group;var selectedSubgroup=subgroup;
        ImGui.PushID(scope);
        ImGui.SetNextItemWidth(240);
        if(ImGui.BeginCombo("Armoire group",group==0?"All groups":cabinetSources!.FirstOrDefault(s=>s.Group==selectedGroup)?.GroupName??"Unknown group")){
            if(ImGui.Selectable("All groups",group==0)){group=0;subgroup=0;}
            foreach(var row in cabinetSources!.DistinctBy(s=>s.Group).OrderBy(s=>s.GroupOrder))
                if(ImGui.Selectable(row.GroupName+"##"+row.Group,group==row.Group)){group=row.Group;subgroup=0;}
            ImGui.EndCombo();
        }
        selectedGroup=group;
        ImGui.SetNextItemWidth(240);
        if(ImGui.BeginCombo("Set / source",subgroup==0?"All sets / sources":cabinetSources!.FirstOrDefault(s=>s.Subgroup==selectedSubgroup)?.SubgroupName??"Unknown source")){
            if(ImGui.Selectable("All sets / sources",subgroup==0))subgroup=0;
            foreach(var row in cabinetSources!.Where(s=>(selectedGroup==0||s.Group==selectedGroup)&&s.Subgroup>0&&!string.IsNullOrWhiteSpace(s.SubgroupName)).DistinctBy(s=>s.Subgroup).OrderBy(s=>s.SubgroupOrder))
                if(ImGui.Selectable(row.SubgroupName+"##"+row.Subgroup,subgroup==row.Subgroup))subgroup=row.Subgroup;
            ImGui.EndCombo();
        }
        MarketHelp("Uses the current game's Armoire groups and sets, including dungeon names, jobs and events. Selecting a group or set matches only items listed in the Armoire catalogue.");
        ImGui.PopID();
    }
    private void DrawArmoireSourcePicker(){DrawGearAppearancePicker(ref armoireAppearance);DrawCabinetSourcePicker("ArmoireSources",ref armoireSourceGroup,ref armoireSourceSubgroup);DrawGearOriginPicker("ArmoireOrigin",ref armoireOrigin);}
    private void DrawGearSourcePicker(bool appearance=true)
    {
        if(appearance)DrawGearAppearancePicker(ref gearAppearance);
        ImGui.SetNextItemWidth(240);
        ImGui.Combo("Storage eligibility",ref gearStorageKind,"All supported gear\0Armoire eligible\0Dresser only\0Outfit set pieces\0");
        DrawCabinetSourcePicker("GearSources",ref gearSourceGroup,ref gearSourceSubgroup);
        DrawGearOriginPicker("GearOrigin",ref gearOrigin);
    }
}

