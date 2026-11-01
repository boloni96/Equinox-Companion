using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private Dictionary<uint,string> protectedGearSets=[];
    private bool protectedGearSetsReady;
    private unsafe void RefreshGearSetProtection()
    {
        protectedGearSetsReady=false;protectedGearSets.Clear();
        if(!config.ProtectOrganizerGearSets)return;
        var module=RaptureGearsetModule.Instance();
        if(!Player.IsLoaded||module==null)return;
        var sets=new List<SavedGearSet>();
        foreach(var entry in module->Entries){
            if((entry.Flags&RaptureGearsetModule.GearsetFlag.Exists)==0)continue;
            var name=entry.NameString;
            if(string.IsNullOrWhiteSpace(name))name="Gear set "+(entry.Id+1);
            sets.Add(new(name,true,entry.Items.ToArray().Select(i=>i.ItemId).ToArray()));
        }
        protectedGearSets=GearSetProtectionPolicy.Build(sets);protectedGearSetsReady=true;
    }
    private StorageRoute[] ProtectGearSetRoutes(StorageRoute[] rules,IReadOnlyDictionary<ulong,ChestStack[]> inventories)
    {
        if(!config.ProtectOrganizerGearSets)return rules;
        var allowed=inventories.Values.SelectMany(x=>x).Where(s=>s.Item>0&&!OrganizerGearProtected(s.Item)).Select(s=>s.Item).Distinct().ToArray();
        return rules.SelectMany(r=>r.Duplicates
            ?new[]{RetrievalQuality.NqOnly,RetrievalQuality.HqOnly}.Where(q=>RetainerRetrieval.AllowsQuality(q==RetrievalQuality.HqOnly,r.Quality)).Select(q=>r with{
                Items=allowed.Where(id=>r.AllItems||r.Items.Contains(id)||r.Categories.Contains(OrganizeCategory(id))||
                    inventories.GetValueOrDefault(r.To,[]).Any(s=>s.Item==id&&s.Quantity>0&&s.Hq==(q==RetrievalQuality.HqOnly))).ToArray(),
                Categories=[],AllItems=false,Duplicates=false,Quality=q})
            :new[]{r with{Items=allowed.Where(id=>r.AllItems||r.Items.Contains(id)||r.Categories.Contains(OrganizeCategory(id))).ToArray(),Categories=[],AllItems=false}})
            .Where(r=>r.Items.Length>0).ToArray();
    }
    private bool OrganizerGearProtected(uint item)=>GearSetProtectionPolicy.Protected(config.ProtectOrganizerGearSets,protectedGearSetsReady,item,protectedGearSets);
    private string OrganizerGearProtectionLabel(uint item)=>protectedGearSetsReady
        ?"Used by gear set: "+protectedGearSets.GetValueOrDefault(item%1000000,"")
        :"Gear-set data unavailable";
    private void DrawGearSetProtectionOption()
    {
        var enabled=config.ProtectOrganizerGearSets;
        if(ImGui.Checkbox("Protect gear used in gear sets",ref enabled)){
            config.ProtectOrganizerGearSets=enabled;SaveConfiguration();InvalidateOrganizePreview();
        }
        RefreshGearSetProtection();
        foreach(var id in myInventorySelected.Keys.Where(OrganizerGearProtected).ToArray())myInventorySelected.Remove(id);
        browseSelected.RemoveWhere(OrganizerGearProtected);
        MarketHelp("Protects items referenced by any saved Gear Set List entry, not only your current set. Protected items cannot be selected or Shift-hovered and are skipped before storage transfers. Matching item IDs are protected across NQ/HQ and duplicate copies. Glamour appearances alone do not count as gear-set equipment.");
    }
}
