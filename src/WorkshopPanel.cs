using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string workshopSearch="";private int workshopAddQuantity=1;
    private PreparationPlan? workshopPreparation;
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
        q.Items=q.Items.Concat(entries).ToArray();SaveConfiguration();workshopPreparation=null;
    }
    private void PrepareWorkshopMaterials()
    {
        try{
            if(retrievalCatalog==null)return;RefreshRetrievalStock();var q=WorkshopSaved();var live=ReadWorkshopPhase();
            var parts=q.Items.SelectMany(e=>Enumerable.Repeat(e with{Quantity=1},e.Quantity)).Skip(q.Completed).ToArray();
            if(q.PendingCollection!=0&&parts.Length>0)parts=parts.Skip(1).ToArray();
            var supplies=new List<WorkshopSupply>();
            for(var i=0;i<parts.Length;i++){
                if(!retrievalCatalog.Projects.TryGetValue(parts[i].ItemId,out var project))throw new InvalidOperationException("Queued project is no longer available.");
                var same=i==0&&live?.ItemId==project.ItemId;
                if(same){supplies.AddRange(live!.Rows);supplies.AddRange(project.Phases.Skip(live.Phase+1).SelectMany(p=>p));}
                else supplies.AddRange(project.Phases.SelectMany(p=>p));
            }
            if(parts.Length==0&&live!=null)supplies.AddRange(live.Rows.Where(r=>workshopSelection.Contains(r.ItemId)));
            var bags=new Dictionary<uint,int>();var ret=new Dictionary<uint,int>();var outputs=new List<RetrievalEntry>();var nqWithdraw=new List<RetrievalEntry>();var hqWithdraw=new List<RetrievalEntry>();
            var notes=new List<string>();
            foreach(var group in supplies.GroupBy(r=>r.ItemId)){
                var id=group.Key;var name=group.First().Name;
                var nq=RetrievalHeld(id,RetrievalQuality.NqOnly);var hq=RetrievalHeld(id,RetrievalQuality.HqOnly);
                var rn=retrievalStock.Where(s=>s.Item==id&&!s.Hq).Sum(s=>s.Quantity);var rh=retrievalStock.Where(s=>s.Item==id&&s.Hq).Sum(s=>s.Quantity);
                var initialN=nq;var initialH=hq;var usedN=0;var usedH=0;var missing=0;var required=0;
                foreach(var row in group.Where(r=>r.RemainingSets>0)){
                    for(var set=0;set<row.RemainingSets;set++){
                        required=checked(required+row.SetSize);
                        // Prefer a complete bag stack, then complete NQ/HQ sets including retainers.
                        var quality=WorkshopPolicy.ChooseHq(nq,hq,row.SetSize)??WorkshopPolicy.ChooseHq(nq+rn,hq+rh,row.SetSize);
                        if(quality==true){var fromBag=Math.Min(hq,row.SetSize);hq-=fromBag;var fromRet=row.SetSize-fromBag;rh-=fromRet;usedH+=fromRet;}
                        else {var fromBag=Math.Min(nq,row.SetSize);nq-=fromBag;var fromRet=Math.Min(rn,row.SetSize-fromBag);rn-=fromRet;usedN+=fromRet;missing+=row.SetSize-fromBag-fromRet;}
                    }
                }
                bags[id]=initialN-nq+initialH-hq;ret[id]=usedN+usedH;
                if(required>0)outputs.Add(new(id,name,required));
                if(usedN>0)nqWithdraw.Add(new(id,name,usedN));if(usedH>0)hqWithdraw.Add(new(id,name,usedH));
                if(missing>0)notes.Add($"{name}: prepare {missing} NQ to complete the planned sets (HQ remnants are kept separate).");
            }
            // Keep all other raw/subcraft stock available; override turn-in allocations with whole-set accounting.
            var allBags=RetrievalBagTotals();var allRet=retrievalStock.GroupBy(s=>s.Item).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.Quantity));
            foreach(var (id,count) in bags)allBags[id]=count;foreach(var (id,count) in ret)allRet[id]=count;
            workshopPreparation=CraftingPreparation.Build(outputs,RetrievalRecipes(),allBags,allRet,retrievalStockComplete);
            workshopNqWithdraw=nqWithdraw.ToArray();workshopHqWithdraw=hqWithdraw.ToArray();
            workshopPreparationNotice=notes.Count>0?string.Join('\n',notes):"Complete NQ/HQ sets are available in recorded stock.";
            if(!retrievalStockComplete)workshopPreparationNotice+="\nRetainer records are incomplete; refresh before crafting to avoid extras.";
        }catch(Exception e){workshopPreparation=null;workshopPreparationNotice=e.Message;}
    }
    private void DrawWorkshop()
    {
        var enabled=config.EnableWorkshop;
        if(ImGui.Checkbox("Enable automatic FC workshop delivery",ref enabled)){config.EnableWorkshop=enabled;SaveConfiguration();if(!enabled)StopWorkshop("Workshop tool disabled.");}
        ImGui.TextWrapped(workshopStatus);
        if(workshopRunning){if(ImGui.Button("Stop workshop"))StopWorkshop("Stopped by you. Queue saved; an already-confirmed hand-in may still finish.");}
        if(!Player.IsLoaded){ImGui.TextWrapped("Log in to view this character's queue.");return;}
        var q=WorkshopSaved();
        ImGui.TextWrapped($"Queue: {q.Completed} / {q.Items.Sum(e=>e.Quantity)} projects collected. Resume is always manual.");
        ImGui.BeginDisabled(workshopRunning);
        if(ImGui.Button("Add projects from current list"))RetrievalTry(()=>AddWorkshopProjects(RetainerRetrieval.Requested(retrievalItems,retrievalMultiplier,false,RetrievalRecipes())));
        ImGui.SetNextItemWidth(100);ImGui.InputInt("Quantity per project",ref workshopAddQuantity);workshopAddQuantity=Math.Clamp(workshopAddQuantity,1,99);
        if(ImGui.Button("Add complete Shark submarine"))RetrievalTry(()=>AddWorkshopProjects(retrievalCatalog!.Projects.Values.Where(p=>p.Name.StartsWith("Shark-class ",StringComparison.OrdinalIgnoreCase)).Select(p=>new RetrievalEntry(p.ItemId,p.Name,workshopAddQuantity)).ToArray()));
        ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##WorkshopSearch","Search all FC projects",ref workshopSearch,100);
        if(workshopSearch.Length>0&&retrievalCatalog!=null){
            ImGui.BeginChild("WorkshopSearch",new Vector2(0,120),true);
            foreach(var project in retrievalCatalog.Projects.Values.Where(p=>p.Name.Contains(workshopSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p.Name).Take(100))if(ImGui.Selectable(project.Name+"##"+project.Sequence))RetrievalTry(()=>AddWorkshopProjects([new(project.ItemId,project.Name,workshopAddQuantity)]));
            ImGui.EndChild();
        }
        if(ImGui.CollapsingHeader("Queued projects")){
            var index=0;foreach(var e in q.Items){ImGui.TextUnformatted($"{e.Quantity} × {e.Name}");index+=e.Quantity;}
            if(ImGui.Button("Clear queue…"))ImGui.OpenPopup("Clear workshop queue");
            if(ImGui.BeginPopup("Clear workshop queue")){ImGui.TextWrapped("Clear saved progress and remaining projects? This does not discontinue the in-game project.");if(ImGui.Button("Clear saved queue")){q.Items=[];q.Completed=0;q.PendingCollection=0;q.ReadyToCollect=0;q.World=Player.CurrentWorld.RowId;q.House=WorkshopHouse();SaveConfiguration();workshopPreparation=null;ImGui.CloseCurrentPopup();}ImGui.EndPopup();}
        }
        ImGui.BeginDisabled(!enabled);if(ImGui.Button("Start / resume project queue"))StartWorkshop(true);ImGui.EndDisabled();
        var live=ReadWorkshopPhase();
        if(live!=null){
            ImGui.Separator();ImGui.TextUnformatted($"Current project · phase {live.Phase+1}/{live.TotalPhases}");
            if(ImGui.SmallButton("Select all materials"))workshopSelection=live.Rows.Where(r=>r.RemainingSets>0).Select(r=>r.ItemId).ToHashSet();ImGui.SameLine();if(ImGui.SmallButton("Clear selection"))workshopSelection.Clear();
            foreach(var row in live.Rows){
                var selected=workshopSelection.Contains(row.ItemId);if(ImGui.Checkbox(row.Name+"##Workshop"+row.ItemId,ref selected)){if(selected)workshopSelection.Add(row.ItemId);else workshopSelection.Remove(row.ItemId);}
                var nq=RetrievalHeld(row.ItemId,RetrievalQuality.NqOnly);var hq=RetrievalHeld(row.ItemId,RetrievalQuality.HqOnly);var supplement=WorkshopPolicy.Supplements(nq,hq,row.SetSize,row.RemainingSets);
                ImGui.TextDisabled($"{row.SetsDelivered}/{row.SetsRequired} sets · {row.SetSize}/set · bags {nq} NQ + {hq} HQ · {WorkshopPolicy.CompleteSets(nq,hq,row.SetSize)} full sets");
                if(supplement.Nq>0)ImGui.TextWrapped($"For this row: add {supplement.Nq} NQ OR {supplement.Hq} HQ.");
            }
            ImGui.BeginDisabled(!enabled||workshopSelection.Count==0);if(ImGui.Button("Deliver selected materials"))StartWorkshop(false);ImGui.EndDisabled();
        }else ImGui.TextDisabled("Open Material Delivery to choose individual turn-ins.");
        if(ImGui.Button("Review remaining materials"))PrepareWorkshopMaterials();
        if(workshopPreparation is {} p){
            ImGui.TextWrapped(workshopPreparationNotice);
            if(ImGui.Button("Withdraw NQ turn-ins"))RetrievalTry(()=>WithdrawPreparation(workshopNqWithdraw,RetrievalQuality.NqOnly));ImGui.SameLine();if(ImGui.Button("Withdraw HQ turn-ins"))RetrievalTry(()=>WithdrawPreparation(workshopHqWithdraw,RetrievalQuality.HqOnly));
            if(ImGui.Button("Withdraw crafting ingredients"))RetrievalTry(()=>WithdrawPreparation(p.Withdraw.Where(e=>!workshopNqWithdraw.Any(x=>x.ItemId==e.ItemId)&&!workshopHqWithdraw.Any(x=>x.ItemId==e.ItemId)).ToArray()));
            ImGui.BeginDisabled(p.Steps.Length==0);if(ImGui.Button("Copy missing crafts for Artisan"))ImGui.SetClipboardText(RetainerRetrieval.ExportArtisan("Workshop remaining materials",p.Steps));ImGui.SameLine();if(ImGui.Button("Open Artisan lists"))Commands.ProcessCommand("/artisan lists");ImGui.EndDisabled();
            ImGui.TextWrapped("Import the copied list in Artisan. NQ is sufficient for every planned shortfall; HQ is also usable only in full sets. Review materials again after crafting.");
            if(ImGui.Button("Copy missing raw ingredients"))ImGui.SetClipboardText(string.Join('\n',p.Missing.Select(e=>$"{e.Quantity} x {e.Name}")));
            if(ImGui.CollapsingHeader("Remaining materials / subcrafts"))foreach(var r in p.Rows){ImGui.TextUnformatted($"{r.Name}: {r.Required} needed · {r.Bags} bags · {r.Retainers} retainers · craft {r.CraftQuantity} · missing {r.Missing}" );if(ImGui.IsItemHovered())ImGui.SetTooltip("Used for: "+string.Join(", ",r.UsedFor));}
        }else if(workshopPreparationNotice.Length>0)ImGui.TextWrapped(workshopPreparationNotice);
        ImGui.EndDisabled();
    }
}
