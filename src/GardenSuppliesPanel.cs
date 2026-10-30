using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private GardenSuppliesWindow? gardenSuppliesWindow;
    private RetrievalEntry[] gardenSupplyItems=[];
    private string gardenSupplyError="",gardenSupplyStockNote="";
    private Dictionary<uint,int> gardenSupplyBags=[],gardenSupplyRetainers=[];
    private sealed class GardenSuppliesWindow(Plugin p):Window("Garden supplies###EquinoxGardenSupplies")
    {
        public override void OnOpen(){Size??=new(600,360);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(450,260),MaximumSize=new(float.MaxValue)};}
        public override void Draw()=>p.DrawGardenSupplyReview();
    }
    private void DrawGardenSuppliesWindow(){} // Independent window remains available after leaving the garden.
    private string GardenSeedItemName(string crop)
    {
        LoadGardenPictures();
        return gardenPictures!.TryGetValue(GardenCropName(crop),out var pic)?pic.GetProperty("plantingMaterialLabel").GetString()??crop+" Seeds":crop+" Seeds";
    }
    private void PrepareGardenSupplies(SharedGardenPlan plan)
    {
        var rows=new List<MarketShoppingRequest>();
        foreach(var bed in plan.Beds.Where(b=>b.Crop.Length>0&&b.Status!="confirmed")){
            rows.Add(new(GardenSeedItemName(bed.Crop),1));rows.Add(new(bed.Soil,1));
            if(bed.ReplantOrder>0&&bed.Status is not ("starter" or "replant")){
                rows.Add(new(GardenSeedItemName(bed.Crop),1));rows.Add(new(bed.StarterSoil,1));}
        }
        PrepareGardenSupplyReview(rows);
    }
    private void PasteGardenSupplies()
    {
        var parsed=MarketShopping.Parse(ImGui.GetClipboardText()??"");
        if(parsed.Errors.Length>0){PrintEquinox(string.Join("; ",parsed.Errors.Take(3)));return;}
        PrepareGardenSupplyReview(parsed.Items);
    }
    private void PrepareGardenSupplyReview(IEnumerable<MarketShoppingRequest> rows)
    {
        gardenSupplyError="";gardenSupplyItems=[];
        try{
            var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
            var names=sheet.Where(i=>!i.Name.IsEmpty).GroupBy(i=>i.Name.ToString(),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First().RowId,StringComparer.OrdinalIgnoreCase);
            var entries=new List<RetrievalEntry>();
            foreach(var row in rows){if(!names.TryGetValue(row.Name,out var id))throw new InvalidOperationException("Item name not found: "+row.Name);entries.Add(new(id,row.Name,row.Quantity,ReadyMaterial:true));}
            gardenSupplyItems=RetainerRetrieval.Combine(entries);RefreshGardenSupplyCounts();
        }catch(Exception e){gardenSupplyError=e.Message;}
        if(gardenSuppliesWindow==null){gardenSuppliesWindow=new(this);windows.AddWindow(gardenSuppliesWindow);}gardenSuppliesWindow.IsOpen=true;
    }
    private void RefreshGardenSupplyCounts()
    {
        gardenSupplyBags=[];gardenSupplyRetainers=[];
        try{
            RefreshRetrievalStock();
            foreach(var e in gardenSupplyItems){gardenSupplyBags[e.ItemId]=RetrievalHeld(e.ItemId);gardenSupplyRetainers[e.ItemId]=retrievalStock.Where(s=>s.Item==e.ItemId).Sum(s=>s.Quantity);}
            gardenSupplyStockNote=retrievalStockComplete?"Current character · bags and recorded retainers; recorded stock may have changed.":"Some retainer records are missing. Missing quantities are provisional; other characters, FC chest and saddlebags are not subtracted.";
        }catch(Exception e){gardenSupplyStockNote="Inventory unavailable: "+e.Message;}
    }
    private void DrawGardenSupplyReview()
    {
        if(ImGui.Button("Refresh counts"))RefreshGardenSupplyCounts();
        ImGui.SameLine();if(ImGui.Button("Paste Journal supplies"))PasteGardenSupplies();
        ImGui.TextWrapped(gardenSupplyError.Length>0?gardenSupplyError:gardenSupplyStockNote);
        if(ImGui.BeginTable("GardenSupplies",5,ImGuiTableFlags.RowBg|ImGuiTableFlags.ScrollY,new Vector2(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-85)))){
            foreach(var label in new[]{"Item","Need","Bags","Retainers","Missing"})ImGui.TableSetupColumn(label);ImGui.TableHeadersRow();
            foreach(var e in gardenSupplyItems){var bag=gardenSupplyBags.GetValueOrDefault(e.ItemId);var retain=gardenSupplyRetainers.GetValueOrDefault(e.ItemId);ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextUnformatted(e.Name);foreach(var n in new[]{e.Quantity,bag,retain,Math.Max(0,e.Quantity-bag-retain)}){ImGui.TableNextColumn();ImGui.TextUnformatted(n.ToString());}}
            ImGui.EndTable();}
        ImGui.BeginDisabled(gardenSupplyItems.Length==0||gardenSupplyError.Length>0);
        if(ImGui.Button("Prepare in Organizer")){
            if(craftingWithdrawalPending||retrievalRun.Running||organizeRunning||chestRunning){gardenSupplyError="Finish or stop the storage operation first.";}
            else{
                if(craftingWithdrawalPrevious==null){craftingWithdrawalPrevious=new(retrievalName,retrievalItems.ToArray(),retrievalMultiplier,retrievalIncludesSubcrafts,Mode:retrievalMode,RecipeChoices:new(retrievalRecipeOverrides),Quality:retrievalQuality,DestinationRetainer:retrievalSendTo,SendCategories:retrievalSendCategories.ToArray(),Entrust:retrievalEntrust);craftingWithdrawalPreviousEditing=retrievalEditingPreset;}
                SelectRetrievalPreset(new("Garden supplies",gardenSupplyItems,1,false,Mode:RetrievalMode.FinalItems));retrievalEditingPreset=null;retrievalSubtract=true;retrievalTransferAll=false;retrievalShowList=true;OpenRetriever();
            }
        }
        ImGui.SameLine();if(ImGui.Button("Missing → Where is cheapest")){
            RefreshGardenSupplyCounts();var missing=gardenSupplyItems.Select(e=>e with{Quantity=Math.Max(0,e.Quantity-gardenSupplyBags.GetValueOrDefault(e.ItemId)-gardenSupplyRetainers.GetValueOrDefault(e.ItemId))}).Where(e=>e.Quantity>0).ToArray();
            // Merge, never replace an unrelated saved shopping list.
            foreach(var e in missing){var old=marketShoppingEntries.FirstOrDefault(x=>x.Request.Name.Equals(e.Name,StringComparison.OrdinalIgnoreCase));if(old==null)marketShoppingEntries.Add(new(new(e.Name,e.Quantity)));else old.Request=old.Request with{Quantity=Math.Max(old.Request.Quantity,e.Quantity)};}
            SaveMarketShopping();OpenMarketSearch("");marketShoppingMode=true;ResetMarketShoppingResults();
        }
        ImGui.EndDisabled();ImGui.TextDisabled("Review quantities, then start withdrawal yourself. Buying is never automatic.");
    }
}
