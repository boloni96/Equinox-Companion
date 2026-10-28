using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private RetrieverWindow retrieverWindow=null!;private bool retrieverCommand;
    private string retrievalName="New list",retrievalPaste="",retrievalSearch="",retrievalJob="All crafting logs",retrievalError="";
    private RetrievalEntry[] retrievalItems=[],retrievalPlan=[];private RetrievalPreview[] retrievalPreview=[];
    private readonly HashSet<uint> retrievalSelected=[];private int retrievalMultiplier=1,retrievalQuantity=1,retrievalSource;
    private bool retrievalIncludesSubcrafts;private bool retrievalShowList, retrievalPreviewDirty=true;private bool retrievalSubtract=true;private RetrievalMode retrievalMode;
    private string retrievalLastFilter="";private RetrievalRecipe[] retrievalSearchRecipes=[];private KeyValuePair<uint,string>[] retrievalSearchItems=[];
    private sealed class RetrieverWindow(Plugin p):Window("Retainer Retriever · Equinox###EquinoxRetriever")
    {
        public override void OnOpen(){Size??=new(720,500);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(420,340),MaximumSize=new(float.MaxValue)};RespectCloseHotkey=false;AllowPinning=true;}
        public override void Draw()=>p.DrawWindowWithLauncher(p.DrawRetriever);
        public override void PostDraw()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Retainer Retriever"));
    }
    private void InitializeRetriever(){retrievalPort=new(this);retrievalRun=new(retrievalPort);retrieverWindow=new(this);windows.AddWindow(retrieverWindow);retrieverCommand=Commands.AddHandler("/eqretrieve",new CommandInfo((_,args)=>{if(args.Trim().Equals("stop",StringComparison.OrdinalIgnoreCase))retrievalRun.Stop("Stopped by command.");else OpenRetriever();}){HelpMessage="Retrieve finished items or crafting materials from your retainers. /eqretrieve stop cancels."});}
    private void DisposeRetriever(){retrievalRun.Stop("Companion unloaded.");if(retrieverCommand)Commands.RemoveHandler("/eqretrieve");}
    private void OpenRetriever(){if(!RequireOptionalTool("Retainer Retriever"))return;retrieverWindow.IsOpen=true;minimizedLaunchers.Remove("Retainer Retriever");LoadRetrievalCatalog();if(retrievalCatalog!=null)LoadRetrievalArtisan();}
    private void RetrievalTry(Action work){try{work();retrievalError="";}catch(Exception e){retrievalError=e.Message;}}
    private void AddRetrieval(RetrievalEntry[] items){retrievalItems=RetainerRetrieval.Combine(retrievalItems.Concat(items));foreach(var e in items)retrievalSelected.Add(e.ItemId);retrievalPreview=[];retrievalPreviewDirty=true;}
    private void PreviewRetrieval(bool selected=false)
    {
        if(retrievalCatalog==null)return;
        var outputs=RetainerRetrieval.Requested(retrievalItems.Where(x=>!selected||retrievalSelected.Contains(x.ItemId)),retrievalMultiplier,retrievalIncludesSubcrafts,retrievalCatalog.Recipes);
        retrievalCraftSteps=[];
        if(retrievalMode==RetrievalMode.AllMaterials){var plan=RetainerRetrieval.PlanCrafts(outputs,retrievalCatalog.Recipes);retrievalPlan=plan.Materials;retrievalCraftSteps=plan.Steps;}else retrievalPlan=outputs;
        try{RefreshRetrievalStock();}catch(Exception e){retrievalStockComplete=false;retrievalStockNotice="Inventory unavailable: "+e.Message;}
        retrievalPreviewDirty=false;
        retrievalPreview=retrievalPlan.Select(e=>new RetrievalPreview(e.ItemId,e.Name,e.Quantity,retrievalSubtract?RetrievalHeld(e.ItemId):0,retrievalStockComplete?retrievalStock.Where(x=>x.Item==e.ItemId).Sum(x=>x.Quantity):null)).ToArray();
    }
    private unsafe void StartRetrieval(bool selected){RetrievalTry(()=>{
        PreviewRetrieval(selected);if(retrievalPlan.Length==0)throw new InvalidOperationException("Choose at least one item.");
        if(!Player.IsLoaded||SellerAddon("RetainerList")==null)throw new InvalidOperationException("Open a nearby summoning bell's retainer list first.");
        if(sellerRun.Running||gilTrade.Running)throw new InvalidOperationException("Finish Seller or Gil Trader before retrieval.");
        retrievalPort.Character=Player.ContentId;retrievalPort.World=Player.CurrentWorld.RowId;
        retrievalRun.Start(ReadSellerRetainers().Select(x=>x.Id).ToArray(),retrievalPlan,retrievalSubtract,DateTimeOffset.UtcNow);
    });}
    private void DrawRetriever()
    {
        if(retrievalCatalogTask?.IsCompleted==true){try{retrievalCatalog=retrievalCatalogTask.GetAwaiter().GetResult();}catch(Exception e){retrievalError="Item catalogue: "+e.Message;}retrievalCatalogTask=null;}
        if(retrievalCatalog!=null&&!retrievalArtisanRequested)LoadRetrievalArtisan();
        if(retrievalArtisanTask?.IsCompleted==true){try{var read=retrievalArtisanTask.GetAwaiter().GetResult();retrievalArtisan=read.Lists;retrievalArtisanNotice=read.Lists.Length+" saved lists"+(read.Warnings.Length>0?" · "+string.Join("; ",read.Warnings):read.Lists.Length==0?" · Save a list in Artisan, then Refresh.":"");}catch(Exception e){retrievalArtisanNotice=e.Message;}retrievalArtisanTask=null;}
        windowStatus=retrievalRun.Running?retrievalRun.Status:retrievalError.Length>0?retrievalError:retrievalRun.Status;
        if(retrievalRun.Running){ImGui.TextUnformatted(retrievalRun.Status);if(ImGui.Button("Stop retrieval"))retrievalRun.Stop("Stopped by you.");ImGui.Separator();}
        if(retrievalCatalog==null){ImGui.TextWrapped(retrievalCatalogTask!=null?"Loading item and crafting catalogues…":"Item catalogue unavailable.");if(retrievalCatalogTask==null&&ImGui.Button("Retry"))LoadRetrievalCatalog();return;}
        ImGui.BeginDisabled(retrievalRun.Running);
        ImGui.SetNextItemWidth(Math.Max(120,ImGui.GetContentRegionAvail().X-165));ImGui.InputText("##ListName",ref retrievalName,100);ImGui.SameLine();
        if(ImGui.Button("Save preset"))RetrievalTry(()=>{if(string.IsNullOrWhiteSpace(retrievalName))throw new ArgumentException("Name this list first.");var at=config.RetrievalPresets.FindIndex(x=>x.Name==retrievalName);var p=new RetrievalPreset(retrievalName,RetainerRetrieval.Combine(retrievalItems),retrievalMultiplier,retrievalIncludesSubcrafts);if(at>=0)config.RetrievalPresets[at]=p;else if(config.RetrievalPresets.Count<25)config.RetrievalPresets.Add(p);else throw new ArgumentException("Maximum 25 presets.");SaveConfiguration();});
        ImGui.SameLine();if(ImGui.SmallButton("New")){retrievalItems=[];retrievalSelected.Clear();retrievalPreview=[];retrievalPreviewDirty=true;retrievalName="New list";retrievalMultiplier=1;retrievalIncludesSubcrafts=false;}
        if(retrievalItems.Length>0&&ImGui.SmallButton($"View current list · {retrievalItems.Length} items"))retrievalShowList=true;
        if(ImGui.BeginTabBar("RetrieverTabs")){
            if(ImGui.BeginTabItem("Browse inventory")){DrawRetrievalBrowse();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Retrieve",retrievalShowList?ImGuiTabItemFlags.SetSelected:ImGuiTabItemFlags.None)){retrievalShowList=false;DrawRetrievalList();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Add items")){ImGui.BeginChild("RetrieverSourcesScroll",new Vector2(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);DrawRetrievalSources();ImGui.EndChild();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Lists & import")){ImGui.BeginChild("RetrieverPresetsScroll",new Vector2(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);DrawRetrievalPresets();ImGui.EndChild();ImGui.EndTabItem();}
            ImGui.EndTabBar();}
        ImGui.EndDisabled();
        if(retrievalError.Length>0)ImGui.TextWrapped(retrievalError);
    }
    private void DrawRetrievalList()
    {
        var mode=(int)retrievalMode;ImGui.SetNextItemWidth(210);if(ImGui.Combo("##RetrievalMode",ref mode,"Listed items / turn-ins\0Ingredients to craft them\0")){retrievalMode=(RetrievalMode)mode;retrievalPreview=[];retrievalPreviewDirty=true;}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Final items: withdraw the exact listed items. All crafting materials: recursively expand recipes to raw/non-craftable ingredients; includes crystals. Subtract bags applies after expansion. No crafting is started.");
        ImGui.SameLine();ImGui.SetNextItemWidth(90);if(ImGui.InputInt("×",ref retrievalMultiplier)){retrievalMultiplier=Math.Clamp(retrievalMultiplier,1,999);retrievalPreview=[];retrievalPreviewDirty=true;}
        if(ImGui.CollapsingHeader("List options")){
        if(ImGui.Checkbox("List already includes subcrafts",ref retrievalIncludesSubcrafts)){retrievalPreviewDirty=true;retrievalPreview=[];}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Enable only for lists containing both desired outputs AND their intermediate recipes. Intermediate amounts used by listed recipes are removed from the output request; surplus remains. Leave off for workshop turn-ins. List names never change quantities.");
        if(ImGui.Checkbox("Subtract items in bags (NQ + HQ)",ref retrievalSubtract)){retrievalPreview=[];retrievalPreviewDirty=true;}
        }
        if(ImGui.Button("Preview all"))RetrievalTry(()=>PreviewRetrieval());ImGui.SameLine();if(ImGui.Button("Preview selected"))RetrievalTry(()=>PreviewRetrieval(true));ImGui.SameLine();if(ImGui.SmallButton("Prepare Artisan list…"))RetrievalTry(PrepareRetrievalArtisanExport);
        if(ImGui.Button("Retrieve all"))StartRetrieval(false);ImGui.SameLine();if(ImGui.Button("Retrieve selected"))StartRetrieval(true);
        DrawRetrievalArtisanExport();
        if(retrievalPreviewDirty){retrievalPreviewDirty=false;RetrievalTry(()=>PreviewRetrieval());}
        ImGui.BeginChild("RetrievalContents",new Vector2(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);
        ImGui.TextUnformatted(retrievalName+" · "+retrievalPlan.Length+" items · ×"+retrievalMultiplier);
        if(retrievalPreview.Length>0&&ImGui.BeginTable("RetrievalPreview",6,ImGuiTableFlags.RowBg|ImGuiTableFlags.Resizable|ImGuiTableFlags.BordersInnerH)){
            ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch,3);foreach(var t in new[]{"Required","Bags used","To fetch","Retainers*","Missing*"})ImGui.TableSetupColumn(t,ImGuiTableColumnFlags.WidthStretch,1);ImGui.TableHeadersRow();
            foreach(var r in retrievalPreview){ImGui.TableNextRow();ImGui.TableNextColumn();DrawRetrievalIcon(r.ItemId);ImGui.TextUnformatted(r.Name);
                if(ImGui.IsItemHovered())ImGui.SetTooltip(r.Name+"\n"+string.Join("\n",retrievalStock.Where(x=>x.Item==r.ItemId).Select(x=>x.Holder+": "+x.Quantity)));
                foreach(var t in new[]{r.Required.ToString(),r.Held.ToString(),r.Need.ToString(),r.Available?.ToString()??"Unknown",r.Missing?.ToString()??"Unknown"}){ImGui.TableNextColumn();ImGui.TextUnformatted(t);}}
            ImGui.EndTable();ImGui.TextDisabled("* Recorded stock · hover item for retainers · Preview refreshes counts");}
        if(retrievalCraftSteps.Length>0&&ImGui.CollapsingHeader("Subcrafts and outputs · craft order")){foreach(var step in retrievalCraftSteps){DrawRetrievalIcon(step.ItemId);ImGui.TextUnformatted($"{step.Name} · {step.Crafts} crafts → {step.Crafts*step.Yield} items");}ImGui.TextWrapped("These are made from the ingredients above, not additional items to withdraw.");}
        if(retrievalItems.Length==0)ImGui.TextWrapped("Choose a saved or Artisan list in Lists & import, or add items to build your own.");
        if(ImGui.CollapsingHeader("Edit requested items / selection"))
        if(ImGui.BeginTable("RetrievalItems",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.Resizable)){
            foreach(var title in new[]{"Item","Quantity","Recipe",""})ImGui.TableSetupColumn(title);ImGui.TableHeadersRow();
            foreach(var e in retrievalItems){ImGui.PushID((int)e.ItemId);ImGui.TableNextRow();ImGui.TableNextColumn();var selected=retrievalSelected.Contains(e.ItemId);if(ImGui.Checkbox("##Selected",ref selected)){if(selected)retrievalSelected.Add(e.ItemId);else retrievalSelected.Remove(e.ItemId);}ImGui.SameLine();DrawRetrievalIcon(e.ItemId);ImGui.TextUnformatted(e.Name);ImGui.TableNextColumn();var q=e.Quantity;ImGui.SetNextItemWidth(-1);if(ImGui.InputInt("##Qty",ref q)){retrievalItems=retrievalItems.Select(x=>x.ItemId==e.ItemId?x with{Quantity=Math.Clamp(q,1,RetainerRetrieval.MaxQuantity)}:x).ToArray();retrievalPreview=[];retrievalPreviewDirty=true;}
                ImGui.TableNextColumn();ImGui.TextDisabled(e.RecipeId>0?e.RecipeId.ToString():"Auto");ImGui.TableNextColumn();if(ImGui.SmallButton("×")){retrievalItems=retrievalItems.Where(x=>x.ItemId!=e.ItemId).ToArray();retrievalPreview=[];retrievalPreviewDirty=true;}ImGui.PopID();}
            ImGui.EndTable();}
        ImGui.EndChild();
    }
    private void DrawRetrievalIcon(uint item)
    {
        var size=ImGui.GetTextLineHeight()+4;
        var iconId=retrievalCatalog?.Icons.GetValueOrDefault(item)??0;
        var icon=iconId>0?Textures.GetFromGameIcon(iconId).GetWrapOrDefault():null;
        if(icon!=null)ImGui.Image(icon.Handle,new Vector2(size));else ImGui.Dummy(new Vector2(size));
        ImGui.SameLine();
    }
    private void SelectRetrievalPreset(RetrievalPreset p)
    {
        retrievalItems=p.Items.ToArray();retrievalSelected.Clear();foreach(var e in retrievalItems)retrievalSelected.Add(e.ItemId);
        retrievalName=p.Name;retrievalMultiplier=p.Multiplier;retrievalIncludesSubcrafts=p.IncludesSubcrafts;retrievalPreview=[];retrievalPreviewDirty=true;retrievalShowList=true;
    }
    private void DrawRetrievalPresetContents(RetrievalPreset p)
    {
        if(!ImGui.IsItemHovered())return;
        ImGui.BeginTooltip();ImGui.TextUnformatted(p.Name+" · ×"+p.Multiplier);
        foreach(var e in p.Items.Take(30)){DrawRetrievalIcon(e.ItemId);ImGui.TextUnformatted($"{(long)e.Quantity*p.Multiplier:N0} × {e.Name}");}
        if(p.Items.Length>30)ImGui.TextDisabled("Load to see all "+p.Items.Length+" items");ImGui.EndTooltip();
    }

    private void DrawRetrievalSources()
    {
        ImGui.Combo("Source",ref retrievalSource,"All items\0Crafting logs\0FC workshop turn-ins\0");ImGui.InputText("Search",ref retrievalSearch,100);ImGui.SetNextItemWidth(120);ImGui.InputInt("Quantity",ref retrievalQuantity);retrievalQuantity=Math.Clamp(retrievalQuantity,1,RetainerRetrieval.MaxQuantity);
        if(retrievalSource==1&&ImGui.BeginCombo("Craft",retrievalJob)){foreach(var job in new[]{"All crafting logs"}.Concat(retrievalCatalog!.Recipes.Values.SelectMany(x=>x).Select(x=>x.Job).Distinct().Order()))if(ImGui.Selectable(job,retrievalJob==job)){retrievalJob=job;}ImGui.EndCombo();}
        var key=retrievalSource+"|"+retrievalSearch+"|"+retrievalJob;
        if(key!=retrievalLastFilter){retrievalLastFilter=key;retrievalSearchItems=retrievalCatalog!.Names.Where(x=>x.Value.Contains(retrievalSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Value).Take(80).ToArray();retrievalSearchRecipes=retrievalCatalog.Recipes.Values.SelectMany(x=>x).Where(x=>(retrievalJob=="All crafting logs"||x.Job==retrievalJob)&&retrievalCatalog.Names[x.ItemId].Contains(retrievalSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>retrievalCatalog.Names[x.ItemId]).Take(80).ToArray();}
        if(retrievalSource==2){foreach(var p in retrievalCatalog!.Workshop.Where(x=>x.Name.Contains(retrievalSearch,StringComparison.OrdinalIgnoreCase)).Take(80)){if(ImGui.Selectable(p.Name))RetrievalTry(()=>AddRetrieval(p.Items.Select(x=>x with{Quantity=checked(x.Quantity*retrievalQuantity)}).ToArray()));DrawRetrievalPresetContents(p);}}
        else if(retrievalSource==1){foreach(var r in retrievalSearchRecipes){DrawRetrievalIcon(r.ItemId);if(ImGui.Selectable(retrievalCatalog!.Names[r.ItemId]+" · "+r.Job+" · yield "+r.Yield+"##"+r.Id))RetrievalTry(()=>AddRetrieval([new(r.ItemId,retrievalCatalog.Names[r.ItemId],retrievalQuantity,r.Id)]));}}
        else foreach(var e in retrievalSearchItems){DrawRetrievalIcon(e.Key);if(ImGui.Selectable(e.Value+"##"+e.Key))RetrievalTry(()=>AddRetrieval([new(e.Key,e.Value,retrievalQuantity)]));}
        ImGui.TextDisabled("Click to add · up to 80 results; narrow the search");
    }
    private void DrawRetrievalPresets()
    {
        if(ImGui.CollapsingHeader("Saved presets",ImGuiTreeNodeFlags.DefaultOpen))foreach(var p in config.RetrievalPresets.ToArray()){ImGui.PushID(p.Name);if(ImGui.SmallButton("Load"))SelectRetrievalPreset(p);ImGui.SameLine();if(ImGui.SmallButton("Add"))RetrievalTry(()=>AddRetrieval(p.Items.Select(e=>e with{Quantity=checked(e.Quantity*p.Multiplier)}).ToArray()));ImGui.SameLine();if(ImGui.SmallButton("Delete")){config.RetrievalPresets.Remove(p);SaveConfiguration();}ImGui.SameLine();ImGui.TextUnformatted(p.Name+" · "+p.Items.Length+" items");DrawRetrievalPresetContents(p);ImGui.PopID();}
        if(ImGui.CollapsingHeader("Existing Artisan lists",ImGuiTreeNodeFlags.DefaultOpen)){
            if(ImGui.Button("Refresh Artisan lists"))LoadRetrievalArtisan();if(ImGui.IsItemHovered())ImGui.SetTooltip("Reads saved Artisan lists on this computer. Save edits in Artisan before refreshing. Skipped recipes are omitted; recipe counts are converted using their output yields.");
            ImGui.TextWrapped(retrievalArtisanConnection);
            ImGui.TextWrapped(retrievalArtisanNotice);
            foreach(var name in retrievalArtisanLive.Where(n=>!retrievalArtisan.Any(p=>p.Name==n))){ImGui.TextDisabled(name+" · contents unavailable");if(ImGui.IsItemHovered())ImGui.SetTooltip("Artisan reports this list, but its saved quantities could not be read. Save it in Artisan and Refresh. See the reading message above; no partial list will be retrieved.");}
            void Group(string label,bool premade){if(!ImGui.CollapsingHeader(label,premade?ImGuiTreeNodeFlags.None:ImGuiTreeNodeFlags.DefaultOpen))return;
                ImGui.PushID(label);for(var i=0;i<retrievalArtisan.Length;i++){var p=retrievalArtisan[i];if(p.ArtisanPreset!=premade)continue;ImGui.PushID(i);if(ImGui.SmallButton("Load"))SelectRetrievalPreset(p);ImGui.SameLine();if(ImGui.SmallButton("Add"))RetrievalTry(()=>AddRetrieval(p.Items));ImGui.SameLine();ImGui.TextUnformatted(p.Name+" · "+p.Items.Length+" items");DrawRetrievalPresetContents(p);ImGui.PopID();}ImGui.PopID();}
            Group("My lists",false);Group("Artisan presets",true);}


        ImGui.InputTextMultiline("##PasteList",ref retrievalPaste,100001,new Vector2(-1,130));
        if(ImGui.Button("Paste clipboard"))retrievalPaste=ImGui.GetClipboardText();ImGui.SameLine();if(ImGui.Button("Import / add whole list"))RetrievalTry(()=>{var list=RetainerRetrieval.Import(retrievalPaste,ResolveRetrievalItem);if(retrievalItems.Length==0)SelectRetrievalPreset(list);else{if(list.IncludesSubcrafts!=retrievalIncludesSubcrafts)throw new ArgumentException("Start a new list before importing a preset with a different subcraft interpretation.");AddRetrieval(list.Items.Select(e=>e with{Quantity=checked(e.Quantity*list.Multiplier)}).ToArray());}});
        if(ImGui.Button("Export preset to clipboard"))RetrievalTry(()=>ImGui.SetClipboardText(RetainerRetrieval.Export(new(retrievalName,retrievalItems,retrievalMultiplier,retrievalIncludesSubcrafts))));ImGui.SameLine();if(ImGui.Button("Copy plain list"))ImGui.SetClipboardText(string.Join("\n",retrievalItems.Select(e=>$"{e.Quantity}x {e.Name}")));
        ImGui.TextDisabled("Paste 600x Cobalt Ingot on each line, or an exported Equinox preset.");
    }
}
