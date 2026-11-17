using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string workshopSearch="";private int workshopAddQuantity=1;
    private PreparationPlan? workshopPreparation;
    private Dictionary<uint,string> workshopMaterialSources=[];
    private Dictionary<(uint Item,int Set),Queue<bool>> workshopPreparedQualities=[];
    private RetrievalEntry[] workshopNqWithdraw=[],workshopHqWithdraw=[];
    private string workshopPreparationNotice="";
    private WorkshopQueueSave WorkshopSaved()
    {
        var q=config.WorkshopQueues.FirstOrDefault(q=>q.Character==Player.ContentId);
        if(q!=null)return q;
        q=new(){Character=Player.ContentId,World=Player.CurrentWorld.RowId,House=WorkshopHouse()};config.WorkshopQueues.Add(q);return q;
    }
    private void AddWorkshopProjects(RetrievalEntry[] entries)
    {
        if(!Player.IsLoaded||WorkshopHouse().Length==0)throw new InvalidOperationException("Enter the company workshop to create its queue.");
        var q=WorkshopSaved();
        if(q.House.Length==0){q.House=WorkshopHouse();q.World=Player.CurrentWorld.RowId;}
        if(q.House!=WorkshopHouse()||q.World!=Player.CurrentWorld.RowId)throw new InvalidOperationException("This queue belongs to another workshop. Finish or clear it before creating a different one.");
        if(entries.Any(e=>retrievalCatalog==null||!retrievalCatalog.Projects.ContainsKey(e.ItemId)))throw new InvalidOperationException("Queue requires finished workshop projects, not their ingredient list. Add projects below.");
        // Preserve completed prefix and ordering when appending more copies.
        if(q.Items.Sum(e=>e.Quantity)+entries.Sum(e=>e.Quantity)>999)throw new InvalidOperationException("Queue is limited to 999 projects.");
        q.Items=q.Items.Concat(entries).ToArray();SaveConfiguration();workshopPreparation=null;workshopPreparationNotice="";workshopNqWithdraw=[];workshopHqWithdraw=[];workshopStatus=$"Added {entries.Sum(e=>e.Quantity)} projects to the queue.";
    }
    private void PrepareWorkshopMaterials(int maxProjects=int.MaxValue,bool currentPhaseOnly=false)
    {
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
            var recordedChest=WorkshopRecordedChest(0);
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
            // Keep all other raw/subcraft stock available; override turn-in allocations with whole-set accounting.
            var allBags=RetrievalBagTotals();var allRet=retrievalStock.GroupBy(s=>s.Item).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Quantity));
            foreach(var group in recordedChest.GroupBy(s=>s.Item))allRet[group.Key]=checked(allRet.GetValueOrDefault(group.Key)+group.Sum(s=>s.Quantity));
            foreach(var (id,count) in bags)allBags[id]=count;foreach(var (id,count) in ret)allRet[id]=count;
            workshopPreparation=CraftingPreparation.Build(outputs,RetrievalRecipes(),allBags,allRet,retrievalStockComplete);
            workshopNqWithdraw=nqWithdraw.ToArray();workshopHqWithdraw=hqWithdraw.ToArray();
            workshopPreparationNotice=notes.Count>0?string.Join('\n',notes):"Complete NQ/HQ sets are available in recorded stock.";
            if(!retrievalStockComplete)workshopPreparationNotice+="\nRetainer records are incomplete; refresh before crafting to avoid extras.";
        }catch(Exception e){workshopPreparation=null;workshopPreparationNotice=e.Message;}
    }
    private int workshopView,workshopWithdrawQuality;
    private bool workshopExportMissing=true;
    private bool workshopClearRequested,workshopOptionsRequested;
    private bool WorkshopUiBusy=>workshopCombinedStage!=0||workshopRunning||retrievalRun.Running||craftingWithdrawalPending||organizeRunning||chestRunning||gearRouting||armoireRunning||workshopWithdrawalPending;
    private void ClearWorkshopQueue(WorkshopQueueSave q)
    {
        WorkshopPolicy.Clear(q,Player.CurrentWorld.RowId,WorkshopHouse());
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
        if(ImGui.BeginTable("WorkshopQueueRows",3,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerV)){
            ImGui.TableSetupColumn("Project",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("Done",ImGuiTableColumnFlags.WidthFixed,50);ImGui.TableSetupColumn("Left",ImGuiTableColumnFlags.WidthFixed,50);ImGui.TableHeadersRow();
            var previous=0;
            foreach(var entry in q.Items){var done=Math.Clamp(q.Completed-previous,0,entry.Quantity);previous+=entry.Quantity;ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextUnformatted(entry.Name);ImGui.TableNextColumn();ImGui.TextUnformatted(done.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted((entry.Quantity-done).ToString());}
            ImGui.EndTable();
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
    private void DrawWorkshopMaterials()
    {
        var p=workshopPreparation;
        ImGui.Checkbox("Export missing only (Artisan skip off)",ref workshopExportMissing);
        ImGui.SetNextItemWidth(-1);ImGui.Combo("##WorkshopWithdrawQuality",ref workshopWithdrawQuality,"Quality: Both (NQ + HQ)\0Quality: NQ only\0Quality: HQ only\0");
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Filter the NQ/HQ turn-ins allocated by the materials review. Both withdraws each quality separately, preserving complete sets.");
        ImGui.BeginDisabled(p==null);
        if(ImGui.BeginTable("WorkshopMaterialActions",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextColumn();if(ImGui.Button("Withdraw turn-ins",new Vector2(-1,0)))RetrievalTry(()=>{
                PrepareWorkshopMaterials();
                BeginWorkshopPreparationWithdrawal(workshopWithdrawQuality==2?[]:workshopNqWithdraw,RetrievalQuality.NqOnly,workshopWithdrawQuality==1?[]:workshopHqWithdraw);
            });
            ImGui.TableNextColumn();if(ImGui.Button("Withdraw ingredients",new Vector2(-1,0)))RetrievalTry(()=>BeginWorkshopPreparationWithdrawal(p!.Withdraw.Where(e=>!workshopNqWithdraw.Any(x=>x.ItemId==e.ItemId)&&!workshopHqWithdraw.Any(x=>x.ItemId==e.ItemId)).ToArray()));
            ImGui.TableNextColumn();ImGui.BeginDisabled(p==null||p.Steps.Length==0);if(ImGui.Button("Copy Artisan list",new Vector2(-1,0)))ImGui.SetClipboardText(RetainerRetrieval.ExportArtisan(workshopExportMissing?"Workshop missing materials":"Workshop required materials",workshopExportMissing?p!.Steps:CraftingPreparation.ArtisanTargets(p!),skipIfEnough:!workshopExportMissing));ImGui.EndDisabled();if(ImGui.IsItemHovered())ImGui.SetTooltip(workshopExportMissing?"Exports only Craft quantities. Artisan skip-if-enough is OFF; do not subtract owned stock again.":"Exports required quantities. Artisan skip-if-enough is ON so Artisan subtracts stock.");
            ImGui.TableNextColumn();ImGui.BeginDisabled(!Pi.InstalledPlugins.Any(x=>x.InternalName=="Artisan"&&x.IsLoaded));if(ImGui.Button("Open Artisan",new Vector2(-1,0)))Commands.ProcessCommand("/artisan lists");ImGui.EndDisabled();
            ImGui.TableNextColumn();if(ImGui.Button("Copy missing raw",new Vector2(-1,0)))ImGui.SetClipboardText(string.Join('\n',p!.Missing.Select(e=>$"{e.Quantity} x {e.Name}")));
            ImGui.EndTable();
        }
        ImGui.EndDisabled();
        if(p==null){ImGui.TextWrapped(workshopPreparationNotice.Length>0?workshopPreparationNotice:"Click Refresh materials above to calculate the remaining requirements.");return;}
        ImGui.TextUnformatted($"{p.Steps.Length} recipes · {p.Missing.Length} raw shortages");
        if(ImGui.IsItemHovered())ImGui.SetTooltip(workshopPreparationNotice);
        if(ImGui.CollapsingHeader("Details / quality notes"))ImGui.TextWrapped(workshopPreparationNotice);
        if(ImGui.BeginTable("WorkshopMaterialRows",6,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerV)){
            foreach(var name in new[]{"Item","Need","Bags","Retainers / FC","Craft","Missing"})ImGui.TableSetupColumn(name);ImGui.TableHeadersRow();
            foreach(var r in p.Rows){ImGui.TableNextRow();OrganizerMaterialRow(r.Coverage);ImGui.TableNextColumn();ImGui.TextUnformatted(r.Name);if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip($"Bags allocated: {r.Bags} · Storage allocated: {r.Retainers}\nUsed for: "+string.Join(", ",r.UsedFor)+"\n"+workshopMaterialSources.GetValueOrDefault(r.ItemId,"No recorded storage location"));ImGui.TableNextColumn();ImGui.TextUnformatted(r.Required.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(r.Bags.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(r.Retainers.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(r.CraftQuantity.ToString());ImGui.TableNextColumn();ImGui.TextUnformatted(r.Missing>0&&!r.Complete?$"≤ {r.Missing} · verify":r.Missing.ToString());}
            ImGui.EndTable();
        }
    }
}
