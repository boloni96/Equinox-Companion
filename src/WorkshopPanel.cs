using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string workshopSearch="";private int workshopAddQuantity=1;
    private PreparationPlan? workshopPreparation,workshopArtisanPreview;
    private RetrievalEntry[] workshopPreparationOutputs=[];
    private string workshopArtisanNotice="";
    private Dictionary<uint,string> workshopMaterialSources=[];
    private Dictionary<(uint Item,int Set),Queue<bool>> workshopPreparedQualities=[];
    private RetrievalEntry[] workshopNqWithdraw=[],workshopHqWithdraw=[];
    private string workshopPreparationNotice="";
    private WorkshopQueueSave WorkshopSaved()
    {
        var q=config.WorkshopQueues.FirstOrDefault(q=>q.Character==Player.ContentId);
        if(q!=null)return q;
        q=new(){Character=Player.ContentId};config.WorkshopQueues.Add(q);return q;
    }
    private void AddWorkshopProjects(RetrievalEntry[] entries)
    {
        if(!Player.IsLoaded)throw new InvalidOperationException("Log in to create this character’s queue.");
        var q=WorkshopSaved();
        if(entries.Any(e=>retrievalCatalog==null||!retrievalCatalog.Projects.ContainsKey(e.ItemId)))throw new InvalidOperationException("Queue requires finished workshop projects, not their ingredient list. Add projects below.");
        // Preserve completed prefix and ordering when appending more copies.
        if(q.Items.Sum(e=>e.Quantity)+entries.Sum(e=>e.Quantity)>999)throw new InvalidOperationException("Queue is limited to 999 projects.");
        q.Items=q.Items.Concat(entries).ToArray();SaveConfiguration();workshopPreparation=null;workshopPreparationNotice="";workshopNqWithdraw=[];workshopHqWithdraw=[];workshopStatus=$"Added {entries.Sum(e=>e.Quantity)} projects to the queue.";
    }
    private void PrepareWorkshopMaterials(int maxProjects=int.MaxValue,bool currentPhaseOnly=false)
    {
        workshopArtisanPreview=null;workshopArtisanNotice="";workshopPreparationOutputs=[];workshopNqWithdraw=[];workshopHqWithdraw=[];
        try{
            if(retrievalCatalog==null)return;RefreshRetrievalStock();var q=WorkshopSaved();var live=ReadWorkshopPhase();
            var parts=q.Items.SelectMany(e=>Enumerable.Repeat(e with{Quantity=1},e.Quantity)).Skip(q.Completed).ToArray();
            if(q.PendingCollection!=0&&parts.Length>0)parts=parts.Skip(1).ToArray();
            parts=parts.Take(Math.Max(1,maxProjects)).ToArray();
            var supplies=new List<WorkshopSupply>();
            for(var i=0;i<parts.Length;i++){
                if(!retrievalCatalog.Projects.TryGetValue(parts[i].ItemId,out var project))throw new InvalidOperationException("Queued project is no longer available.");
                var same=i==0&&live?.ItemId==project.ItemId;
                if(same){supplies.AddRange(live!.Rows);if(!currentPhaseOnly)supplies.AddRange(project.Phases.Skip(live.Phase+1).SelectMany(p=>p));}
                else supplies.AddRange(project.Phases.SelectMany(p=>p));
            }
            if(parts.Length==0&&live!=null)supplies.AddRange(live.Rows.Where(r=>workshopSelection.Contains(r.ItemId)));
            var bags=new Dictionary<uint,int>();var ret=new Dictionary<uint,int>();var outputs=new List<RetrievalEntry>();var nqWithdraw=new List<RetrievalEntry>();var hqWithdraw=new List<RetrievalEntry>();
            workshopPreparedQualities=[];
            var notes=new List<string>();
            var recordedChest=WorkshopRecordedChest(0,requireReady:true);
            workshopMaterialSources=retrievalStock.Select(s=>(s.Item,Text:$"Retainer {s.Holder}: {s.Quantity} {(s.Hq?"HQ":"NQ")} "))
                .Concat(recordedChest.Select(s=>(s.Item,Text:$"FC chest {(s.Page==22001?"crystals":"tab "+(s.Page-19999))}: {s.Quantity} {(s.Hq?"HQ":"NQ")} ")))
                .GroupBy(s=>s.Item).ToDictionary(g=>g.Key,g=>string.Join("\n",g.Select(s=>s.Text)));

            foreach(var group in supplies.GroupBy(r=>r.ItemId)){
                var id=group.Key;var name=group.First().Name;
                var nq=RetrievalHeld(id,RetrievalQuality.NqOnly);var hq=RetrievalHeld(id,RetrievalQuality.HqOnly);
                var fcStock=recordedChest.Where(s=>s.Item==id);
                var rn=retrievalStock.Where(s=>s.Item==id&&!s.Hq).Sum(s=>s.Quantity)+fcStock.Where(s=>!s.Hq).Sum(s=>s.Quantity);var rh=retrievalStock.Where(s=>s.Item==id&&s.Hq).Sum(s=>s.Quantity)+fcStock.Where(s=>s.Hq).Sum(s=>s.Quantity);
                var allocation=WorkshopSetAllocator.Allocate(group,nq,hq,rn,rh,workshopWithdrawQuality);
                foreach(var choices in allocation.Choices.GroupBy(c=>c.SetSize))workshopPreparedQualities[(id,choices.Key)]=new(choices.Select(c=>c.Hq));
                var required=allocation.Required;var usedN=allocation.WithdrawNq;var usedH=allocation.WithdrawHq;var missing=allocation.Missing;
                bags[id]=allocation.Bags;ret[id]=usedN+usedH;
                if(required>0)outputs.Add(new(id,name,required));
                if(usedN>0)nqWithdraw.Add(new(id,name,usedN));if(usedH>0)hqWithdraw.Add(new(id,name,usedH));
                if(missing>0)notes.Add($"{name}: prepare {missing} {(workshopWithdrawQuality==2?"HQ":"NQ")} to complete the planned sets (HQ remnants are kept separate).");
            }
            // Reserve quality-safe turn-in allocations, but keep surplus stock
            // available when a turn-in is also an ingredient of another craft.
            var allBags=RetrievalBagTotals();var allRet=retrievalStock.GroupBy(s=>s.Item).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Quantity));
            foreach(var group in recordedChest.GroupBy(s=>s.Item))allRet[group.Key]=checked(allRet.GetValueOrDefault(group.Key)+group.Sum(s=>s.Quantity));
            var reserved=bags.ToDictionary(e=>e.Key,e=>new PreparationStockAllocation(e.Value,ret.GetValueOrDefault(e.Key)));
            workshopPreparation=CraftingPreparation.Build(outputs,RetrievalRecipes(),allBags,allRet,retrievalStockComplete,reserved);
            workshopPreparationOutputs=outputs.ToArray();
            workshopNqWithdraw=nqWithdraw.ToArray();workshopHqWithdraw=hqWithdraw.ToArray();
            workshopPreparationNotice=notes.Count>0?string.Join('\n',notes):"Complete NQ/HQ sets are available in recorded stock.";
            if(!retrievalStockComplete)workshopPreparationNotice+="\nRetainer records are incomplete; refresh before crafting to avoid extras.";
        }catch(Exception e){workshopPreparation=null;workshopPreparationNotice=e.Message;}
    }
    private int workshopView,workshopWithdrawQuality,workshopMaterialView;
    private bool workshopExportMissing=true;
    private bool workshopArtisanSkipsStock=true;
    private bool workshopClearRequested,workshopOptionsRequested;
    private bool WorkshopUiBusy=>workshopCombinedStage!=0||workshopRunning||retrievalRun.Running||craftingWithdrawalPending||organizeRunning||chestRunning||gearRouting||armoireRunning||workshopWithdrawalPending;
    private void ClearWorkshopQueue(WorkshopQueueSave q)
    {
        WorkshopPolicy.Clear(q,0,"");
        workshopPreparation=null;workshopNqWithdraw=[];workshopHqWithdraw=[];workshopPreparationNotice="";
        workshopSelection.Clear();workshopActive=null;workshopBefore=null;workshopSupply=null;
        workshopStatus="Queue cleared. The in-game project is unchanged.";SaveConfiguration();
    }
    private void DrawWorkshop()
    {
        if(!Player.IsLoaded){ImGui.TextWrapped("Log in to view this character's queue.");return;}
        var q=WorkshopSaved();var total=q.Items.Sum(e=>e.Quantity);
        ImGui.TextUnformatted($"{q.Completed}/{total} collected · {Math.Max(0,total-q.Completed)} remaining");
        var status=workshopStatus.Length>85?workshopStatus[..82]+"…":workshopStatus;
        ImGui.TextUnformatted(status);if(ImGui.IsItemHovered())ImGui.SetTooltip(workshopStatus);
        // Fixed controls stay outside the scrolling content and confirmation is rendered outside disabled scopes.
        if(ImGui.BeginTable("WorkshopToolbar",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextColumn();
            if(workshopCombinedStage!=0){if(ImGui.Button("Stop withdraw + turn in",new Vector2(-1,0)))StopWorkshop("Stopped by you. Queue preserved.");}
            else if(workshopWithdrawalPending){if(ImGui.Button("Stop withdrawal",new Vector2(-1,0)))CancelWorkshopWithdrawal();}
            else if(workshopRunning){if(ImGui.Button("Stop workshop",new Vector2(-1,0)))StopWorkshop("Stopped by you. Queue preserved.");}
            else {ImGui.BeginDisabled(WorkshopUiBusy||!config.EnableWorkshop||total<=q.Completed&&q.PendingDeposit==0);if(ImGui.Button("Start / resume",new Vector2(-1,0)))StartWorkshop(true);ImGui.EndDisabled();}
            ImGui.TableNextColumn();ImGui.BeginDisabled(WorkshopUiBusy||total==0&&q.PendingCollection==0&&q.ReadyToCollect==0);if(ImGui.Button("Clear queue…",new Vector2(-1,0)))workshopClearRequested=true;ImGui.EndDisabled();
            ImGui.TableNextColumn();if(ImGui.Button("Options…",new Vector2(-1,0)))workshopOptionsRequested=true;
            ImGui.TableNextColumn();ImGui.BeginDisabled(WorkshopUiBusy);if(ImGui.Button("Refresh materials",new Vector2(-1,0))){PrepareWorkshopMaterials();workshopView=2;}ImGui.EndDisabled();
            ImGui.EndTable();
        }
        ImGui.BeginDisabled(WorkshopUiBusy);
        var combined=config.WorkshopWithdrawAndDeliver;
        if(ImGui.Checkbox("Withdraw + turn in",ref combined)){config.WorkshopWithdrawAndDeliver=combined;SaveConfiguration();}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Review the whole remaining queue and withdraw its turn-ins from recorded retainer / FC chest sources before delivering. Smaller batches only when bag capacity requires it. Uses the Materials quality filter. Home world required.");
        ImGui.EndDisabled();
        if(workshopOptionsRequested){ImGui.OpenPopup("Workshop options");workshopOptionsRequested=false;}
        if(ImGui.BeginPopup("Workshop options")){
            var enabled=config.EnableWorkshop;if(ImGui.Checkbox("Enable workshop automation",ref enabled)){config.EnableWorkshop=enabled;SaveConfiguration();if(!enabled)StopWorkshop("Workshop tool disabled.");}
            ImGui.BeginDisabled(WorkshopUiBusy);
            var deposit=config.WorkshopDepositParts;if(ImGui.Checkbox("Entrust finished parts to FC chest",ref deposit)){config.WorkshopDepositParts=deposit;SaveConfiguration();}
            var tab=Math.Clamp(config.WorkshopChestTab,1,5)-1;
            if(ImGui.Combo("Finished parts tab",ref tab,"Tab 1\0Tab 2\0Tab 3\0Tab 4\0Tab 5\0")){config.WorkshopChestTab=tab+1;SaveConfiguration();}
            ImGui.TextWrapped("Materials use their recorded locations in this FC chest or your retainers; the route chooses an available source. Finished parts go only to the selected FC chest tab; full or restricted tabs stop the queue. Leftover materials stay in bags.");
            ImGui.EndDisabled();
            var manual=config.WorkshopManualDelivery;
            if(ImGui.Checkbox("Complete turn-ins I click in game",ref manual)){config.WorkshopManualDelivery=manual;if(manual)config.EnableWorkshop=true;SaveConfiguration();if(!manual){if(workshopRunning&&workshopSingleDelivery)StopWorkshop("Manual assistance disabled.");else if(!workshopRunning)PauseWorkshop(false);}}
            ImGui.TextWrapped("Click a material in the game to deliver one full NQ or HQ set, including its confirmations. No queue required.");
            ImGui.EndPopup();
        }
        if(!config.EnableWorkshop)ImGui.TextDisabled("Automation off · enable in Options");
        if(workshopClearRequested){ImGui.OpenPopup("Clear workshop queue");workshopClearRequested=false;}
        if(ImGui.BeginPopupModal("Clear workshop queue",ImGuiWindowFlags.AlwaysAutoResize)){
            ImGui.TextUnformatted("Clear this character's saved queue and progress?");
            ImGui.TextUnformatted("The current in-game project will remain active.");
            ImGui.BeginDisabled(WorkshopUiBusy);
            if(ImGui.Button("Clear queue")){ClearWorkshopQueue(q);ImGui.CloseCurrentPopup();}
            ImGui.EndDisabled();ImGui.SameLine();if(ImGui.Button("Cancel"))ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        ImGui.SetNextItemWidth(-1);ImGui.Combo("##WorkshopView",ref workshopView,"Project queue\0Current delivery\0Materials / Artisan\0");
        ImGui.BeginChild("WorkshopContent",new Vector2(0,Math.Max(60,ImGui.GetContentRegionAvail().Y)),false);
        ImGui.BeginDisabled(WorkshopUiBusy);
        if(workshopView==0)DrawWorkshopQueue(q);
        else if(workshopView==1)DrawWorkshopDelivery();
        else DrawWorkshopMaterials();
        ImGui.EndDisabled();ImGui.EndChild();
    }
    private void DrawWorkshopQueue(WorkshopQueueSave q)
    {
        ImGui.SetNextItemWidth(100);ImGui.InputInt("Copies",ref workshopAddQuantity);workshopAddQuantity=Math.Clamp(workshopAddQuantity,1,99);
        if(ImGui.BeginTable("WorkshopAddActions",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextColumn();if(ImGui.Button("Add current list",new Vector2(-1,0)))RetrievalTry(()=>AddWorkshopProjects(RetainerRetrieval.Requested(retrievalItems,retrievalMultiplier,false,RetrievalRecipes())));
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Add the Organizer list's finished projects, using that list's quantities and multiplier.");
            ImGui.TableNextColumn();if(ImGui.Button("Add Shark set",new Vector2(-1,0)))RetrievalTry(()=>AddWorkshopProjects(retrievalCatalog!.Projects.Values.Where(p=>p.Name.StartsWith("Shark-class ",StringComparison.OrdinalIgnoreCase)).Select(p=>new RetrievalEntry(p.ItemId,p.Name,workshopAddQuantity)).ToArray()));
            if(ImGui.IsItemHovered())ImGui.SetTooltip($"Add {workshopAddQuantity} complete submarine(s): {workshopAddQuantity*4} parts. Copies also applies to search results.");
            ImGui.EndTable();
        }
        ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##WorkshopSearch","Search FC projects…",ref workshopSearch,100);
        if(workshopSearch.Length>0&&retrievalCatalog!=null){
            ImGui.BeginChild("WorkshopSearchResults",new Vector2(0,100),true);
            foreach(var project in retrievalCatalog.Projects.Values.Where(p=>p.Name.Contains(workshopSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p.Name).Take(100))if(ImGui.Selectable(project.Name+"##"+project.Sequence))RetrievalTry(()=>AddWorkshopProjects([new(project.ItemId,project.Name,workshopAddQuantity)]));
            ImGui.EndChild();
        }
        if(q.Items.Length==0){ImGui.TextDisabled("Queue empty · add a set, list, or search result.");return;}
        if(ImGui.BeginTable("WorkshopQueueRows",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerV)){
            ImGui.TableSetupColumn("Project",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("Done",ImGuiTableColumnFlags.WidthFixed,50);ImGui.TableSetupColumn("Left",ImGuiTableColumnFlags.WidthFixed,70);ImGui.TableSetupColumn("##Remove",ImGuiTableColumnFlags.WidthFixed,28);ImGui.TableHeadersRow();
            var previous=0;var edited=-1;var requested=0;
            for(var i=0;i<q.Items.Length;i++){
                var entry=q.Items[i];var done=Math.Clamp(q.Completed-previous,0,entry.Quantity);previous+=entry.Quantity;
                var left=entry.Quantity-done;var minimum=WorkshopPolicy.MinimumRemaining(q,i);
                ImGui.PushID(i);ImGui.TableNextRow();ImGui.TableNextColumn();DrawOrganizerItemName(entry.Name);if(ImGui.IsItemHovered())ImGui.SetTooltip("Click to copy item name");
                ImGui.TableNextColumn();ImGui.TextUnformatted(done.ToString());ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
                if(ImGui.InputInt("##Remaining",ref left,0,0,"%d",ImGuiInputTextFlags.EnterReturnsTrue)){edited=i;requested=Math.Max(minimum,left);}
                if(ImGui.IsItemHovered())ImGui.SetTooltip("Remaining copies · type a quantity and press Enter. Completed copies are kept.");
                ImGui.TableNextColumn();ImGui.BeginDisabled(entry.Quantity==done||minimum>0);
                if(ImGui.SmallButton("X")){edited=i;requested=0;}
                if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(minimum>0?"Collect the pending part first.":"Remove remaining copies of this part. Completed copies are kept.");
                ImGui.EndDisabled();ImGui.PopID();
            }
            ImGui.EndTable();
            if(edited>=0)RetrievalTry(()=>{
                WorkshopPolicy.EditRemaining(q,edited,requested);SaveConfiguration();
                workshopPreparation=null;workshopNqWithdraw=[];workshopHqWithdraw=[];workshopPreparationNotice="";
                workshopStatus="Queue updated. The in-game project is unchanged.";
            });
        }
    }
    private void DrawWorkshopDelivery()
    {
        var live=ReadWorkshopPhase();
        // Keep the action visible even before the game opens Material Delivery.
        ImGui.BeginDisabled(!config.EnableWorkshop||live==null||workshopSelection.Count==0);
        if(ImGui.Button("Deliver selected",new Vector2(-1,0)))StartWorkshop(false);ImGui.EndDisabled();
        if(live==null){ImGui.TextWrapped("Open Material Delivery at the fabrication station to select materials here.");return;}
        ImGui.TextUnformatted($"Phase {live.Phase+1}/{live.TotalPhases}");
        if(ImGui.SmallButton("Select all"))workshopSelection=live.Rows.Where(r=>r.RemainingSets>0).Select(r=>r.ItemId).ToHashSet();ImGui.SameLine();if(ImGui.SmallButton("Clear selection"))workshopSelection.Clear();
        foreach(var row in live.Rows){
            var selected=workshopSelection.Contains(row.ItemId);ImGui.BeginDisabled(row.RemainingSets==0);
            if(ImGui.Checkbox($"{row.Name} · {row.SetsDelivered}/{row.SetsRequired}##Workshop{row.ItemId}",ref selected)){if(selected)workshopSelection.Add(row.ItemId);else workshopSelection.Remove(row.ItemId);}ImGui.EndDisabled();
            if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)){
                var nq=RetrievalHeld(row.ItemId,RetrievalQuality.NqOnly);var hq=RetrievalHeld(row.ItemId,RetrievalQuality.HqOnly);var supplement=WorkshopPolicy.Supplements(nq,hq,row.SetSize,row.RemainingSets);
                ImGui.SetTooltip($"{row.SetSize} per set · bags: {nq} NQ + {hq} HQ\n{WorkshopPolicy.CompleteSets(nq,hq,row.SetSize)} full sets available\nAdd {supplement.Nq} NQ OR {supplement.Hq} HQ for this row.");
            }
        }
    }
    private void CopyWorkshopArtisanList()
    {
        PrepareWorkshopMaterials();
        if(workshopPreparation is not {} current||retrievalCatalog==null)throw new InvalidOperationException(workshopPreparationNotice);
        var export=CraftingPreparation.ArtisanPlan(current,workshopPreparationOutputs,RetrievalRecipes(),workshopExportMissing);
        var skipStock=!workshopExportMissing||workshopArtisanSkipsStock;
        if(workshopExportMissing&&skipStock)
            export=export with{Steps=CraftingPreparation.ArtisanTargets(current)};
        var text=RetainerRetrieval.ExportArtisan(workshopExportMissing?"Workshop missing materials":"Workshop full requirements",export.Steps,skipIfEnough:skipStock);
        ImGui.SetClipboardText(text);workshopArtisanPreview=export;
        workshopArtisanNotice=$"Copied {export.Steps.Length} recipes · {export.Steps.Sum(s=>(long)s.Crafts):N0} crafts. "+
            (skipStock?"Inventory targets copied. Withdraw allocated turn-ins and ingredients before crafting. Use Companion for net shopping quantities.":"Exact craft counts copied for Artisan with stock skipping off.");
    }
    private PreparationRow[]? workshopSortSource;
    private PreparationRow[] workshopSortedRows=[];
    private int workshopSortColumn=-1;
    private bool workshopSortDescending;

    private unsafe PreparationRow[] SortedWorkshopRows(PreparationRow[] rows)
    {
        var specs=ImGui.TableGetSortSpecs();
        if(specs.SpecsCount==0)return rows;
        var column=specs.Specs.ColumnIndex;
        var descending=specs.Specs.SortDirection==ImGuiSortDirection.Descending;
        if(ReferenceEquals(rows,workshopSortSource)&&column==workshopSortColumn&&descending==workshopSortDescending)
            return workshopSortedRows;

        var names=StringComparer.OrdinalIgnoreCase;
        if(column==0){
            workshopSortedRows=(descending?rows.OrderByDescending(r=>r.Name,names):rows.OrderBy(r=>r.Name,names)).ToArray();
        }else{
            // A named ingredient shortage sorts between a numeric shortage and a covered row.
            (int,int) Quantity(PreparationRow r)=>column switch{
                1=>(0,r.Required),2=>(0,r.Bags),3=>(0,r.Retainers),4=>(0,r.CraftQuantity),
                _=>(r.Missing>0?2:r.MissingIngredients.Length>0?1:0,r.Missing)};
            var sorted=descending?rows.OrderByDescending(Quantity):rows.OrderBy(Quantity);
            workshopSortedRows=sorted.ThenBy(r=>r.Name,names).ToArray();
        }
        workshopSortSource=rows;workshopSortColumn=column;workshopSortDescending=descending;
        return workshopSortedRows;
    }

    private void DrawWorkshopMaterials()
    {
        if(workshopChestRefreshDue!=default&&DateTimeOffset.UtcNow>=workshopChestRefreshDue&&!WorkshopUiBusy){
            workshopChestRefreshDue=default;PrepareWorkshopMaterials();
        }
        var p=workshopPreparation;
        if(ImGui.Checkbox("Export missing only",ref workshopExportMissing)){workshopArtisanPreview=null;workshopArtisanNotice="";}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Checked: only recipes still needed after recorded stock. Unchecked: every recipe in the remaining queue, including subcrafts and items already owned. Raw/non-craftable materials stay in the material list.");
        if(workshopExportMissing){
            if(ImGui.Checkbox("Artisan skips owned stock",ref workshopArtisanSkipsStock)){workshopArtisanPreview=null;workshopArtisanNotice="";}
            if(ImGui.IsItemHovered())ImGui.SetTooltip("Match your Artisan list settings here. With both Skip Crafting Unnecessary Materials and Skip Up To List Amount enabled, export inventory targets for the remaining recipes. Fully stocked recipes are excluded.");
        }
        ImGui.SetNextItemWidth(-1);if(ImGui.Combo("##WorkshopWithdrawQuality",ref workshopWithdrawQuality,"Quality: Both (NQ + HQ)\0Quality: NQ only\0Quality: HQ only\0")){PrepareWorkshopMaterials();p=workshopPreparation;}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Filter the NQ/HQ turn-ins allocated by the materials review. Both withdraws each quality separately, preserving complete sets.");
        ImGui.BeginDisabled(p==null);
        if(ImGui.Button("Withdraw all",new Vector2(-1,0)))RetrievalTry(()=>BeginWorkshopPreparationWithdrawal(true,true));
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Withdraw allocated turn-ins and ingredients together in one pass through the retainers and FC chest.");
        if(ImGui.BeginTable("WorkshopMaterialActions",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextColumn();if(ImGui.Button("Withdraw turn-ins",new Vector2(-1,0)))RetrievalTry(()=>BeginWorkshopPreparationWithdrawal(true,false));
            ImGui.TableNextColumn();if(ImGui.Button("Withdraw ingredients",new Vector2(-1,0)))RetrievalTry(()=>BeginWorkshopPreparationWithdrawal(false,true));
            ImGui.TableNextColumn();ImGui.BeginDisabled(p==null||workshopExportMissing&&p.Steps.Length==0);if(ImGui.Button("Copy Artisan list",new Vector2(-1,0)))RetrievalTry(CopyWorkshopArtisanList);ImGui.EndDisabled();
            if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Copies every recipe in the chosen export mode, ingredients before outputs. Artisan can replace import settings with its defaults; verify stock skipping after import.");
            ImGui.TableNextColumn();ImGui.BeginDisabled(!Pi.InstalledPlugins.Any(x=>x.InternalName=="Artisan"&&x.IsLoaded));if(ImGui.Button("Open Artisan",new Vector2(-1,0)))Commands.ProcessCommand("/artisan lists");ImGui.EndDisabled();
            ImGui.TableNextColumn();if(ImGui.Button("Copy missing raw",new Vector2(-1,0)))ImGui.SetClipboardText(string.Join('\n',p!.Missing.Select(e=>$"{e.Quantity} x {e.Name}")));
            ImGui.TableNextColumn();ImGui.BeginDisabled(p==null||p.Missing.Length==0);
            if(ImGui.Button("Where Is Cheap",new Vector2(-1,0)))RetrievalTry(()=>{PrepareWorkshopMaterials();if(workshopPreparation is {} current)OpenMissingMaterialShopping(current);});
            ImGui.EndDisabled();if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip("Send the whole queue’s missing raw materials and quantities to Where Is Cheap. Updates matching items; keeps other shopping entries.");
            ImGui.EndTable();
        }
        ImGui.EndDisabled();
        if(p==null){ImGui.TextWrapped(workshopPreparationNotice.Length>0?workshopPreparationNotice:"Click Refresh materials above to calculate the remaining requirements.");return;}
        if(workshopArtisanNotice.Length>0)ImGui.TextWrapped(workshopArtisanNotice);
        if(workshopArtisanPreview is {} exported&&ImGui.CollapsingHeader($"Copied Artisan recipes ({exported.Steps.Length})")){
            ImGui.TextWrapped("Counts are crafts, not output items. Artisan applies its default settings on import. Avoid auto-adjusting subcraft quantities in an exact missing-only list; it can replace this calculation.");
            foreach(var step in exported.Steps){DrawOrganizerItemName(step.Name);ImGui.SameLine();ImGui.TextUnformatted($"· {step.Crafts:N0} crafts → {(long)step.Crafts*step.Yield:N0} items");}
            ImGui.TextWrapped($"{exported.Missing.Length} raw/non-craftable ingredient types are not recipes. Use Materials for current shortages.");
        }
        ImGui.TextUnformatted($"{p.Steps.Length} recipes · {p.Missing.Length} raw shortage{(p.Missing.Length==1?"":"s")}");
        if(ImGui.IsItemHovered())ImGui.SetTooltip(workshopPreparationNotice+"\nIngredient names in the Missing column identify shortages shared across the queue.");
        if(ImGui.CollapsingHeader("Details / quality notes"))ImGui.TextWrapped(workshopPreparationNotice);
        ImGui.SetNextItemWidth(-1);ImGui.Combo("##WorkshopMaterialView",ref workshopMaterialView,"All materials (turn-ins + ingredients)\0Turn-ins only\0Ingredients only (subcrafts + raw)\0");
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Filters this table only. An item needed both as a turn-in and an ingredient appears in both views; its totals stay shared across the queue.");
        if(ImGui.BeginTable("WorkshopMaterialRows",6,ImGuiTableFlags.Sortable|ImGuiTableFlags.SortTristate|ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerV|ImGuiTableFlags.ScrollY|ImGuiTableFlags.ScrollX,new Vector2(0,Math.Max(100,ImGui.GetContentRegionAvail().Y)),560)){
            ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthFixed,170);
            foreach(var name in new[]{"Need","Bags","Stored","Craft","Missing"})ImGui.TableSetupColumn(name,ImGuiTableColumnFlags.WidthStretch|ImGuiTableColumnFlags.PreferSortDescending);
            ImGui.TableSetupScrollFreeze(1,1);ImGui.TableHeadersRow();
            foreach(var r in SortedWorkshopRows(p.Rows).Where(r=>r.MatchesView(workshopMaterialView))){ImGui.TableNextRow();OrganizerMaterialRow(r.Coverage);ImGui.TableNextColumn();DrawOrganizerItemName(r.Name);if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(OrganizerMaterialDetails(r)+"\n\nRecorded storage:\n"+workshopMaterialSources.GetValueOrDefault(r.ItemId,"No recorded storage location"));ImGui.TableNextColumn();ImGui.TextUnformatted(r.Required.ToString("N0"));ImGui.TableNextColumn();ImGui.TextUnformatted(r.Bags.ToString("N0"));ImGui.TableNextColumn();ImGui.TextUnformatted(r.Retainers.ToString("N0"));ImGui.TableNextColumn();ImGui.TextUnformatted(r.CraftQuantity.ToString("N0"));ImGui.TableNextColumn();DrawOrganizerMissing(r);}
            ImGui.EndTable();
        }
    }
}
