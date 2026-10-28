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
    private readonly HashSet<uint> retrievalSendCategories=[];private ulong retrievalSendTo;
    private RetrievalQuality retrievalQuality;private bool retrievalIncludesSubcrafts;private bool retrievalPreviewSelected;private bool retrievalShowList, retrievalPreviewDirty=true;private bool retrievalSubtract=true;private RetrievalMode retrievalMode=RetrievalMode.DirectIngredients;private RetrievalPreset? retrievalCleared;
    private int retrievalSearchPage;private RetrievalPreset[] retrievalSearchWorkshop=[];
    private string retrievalLastFilter="";private RetrievalRecipe[] retrievalSearchRecipes=[];private KeyValuePair<uint,string>[] retrievalSearchItems=[];
    private sealed class RetrieverWindow(Plugin p):Window("Retainer Retriever · Equinox###EquinoxRetriever")
    {
        public override void OnOpen(){Size??=new(720,500);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(420,340),MaximumSize=new(float.MaxValue)};RespectCloseHotkey=false;AllowPinning=true;}
        public override void OnClose(){p.retrievalPickItems=false;p.retrievalPickLast=0;}
        public override void Draw()=>p.DrawWindowWithLauncher(p.DrawRetriever);
        public override void PostDraw()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Retainer Retriever"));
    }
    private void InitializeRetriever(){retrievalPort=new(this);retrievalRun=new(retrievalPort);retrieverWindow=new(this);windows.AddWindow(retrieverWindow);chestToolsWindow=new(this);windows.AddWindow(chestToolsWindow);retainerToolsWindow=new(this);windows.AddWindow(retainerToolsWindow);retrieverCommand=Commands.AddHandler("/eqretrieve",new CommandInfo((_,args)=>{if(args.Trim().Equals("stop",StringComparison.OrdinalIgnoreCase))retrievalRun.Stop("Stopped by command.");else OpenRetriever();}){HelpMessage="Retrieve finished items or crafting materials from your retainers. /eqretrieve stop cancels."});}
    private void DisposeRetriever(){StopRetainerGil("Companion unloaded.");StopChest("Companion unloaded.");StopOrganize("Companion unloaded.");retrievalRun.Stop("Companion unloaded.");if(retrieverCommand)Commands.RemoveHandler("/eqretrieve");}
    private void OpenRetriever(){if(!RequireOptionalTool("Retainer Retriever"))return;retrieverWindow.IsOpen=true;minimizedLaunchers.Remove("Retainer Retriever");retrievalRefreshPending=true;LoadRetrievalCatalog();if(retrievalCatalog!=null)LoadRetrievalArtisan();}
    private void RetrievalTry(Action work){try{work();retrievalError="";}catch(RetrievalRecipeChoiceException e){retrievalRecipeChoice=e.ItemId;retrievalError=e.Message;}catch(Exception e){retrievalError=e.Message;}}
    private void AddRetrieval(RetrievalEntry[] items){retrievalItems=RetainerRetrieval.Combine(retrievalItems.Concat(items));foreach(var e in items)retrievalSelected.Add(e.ItemId);retrievalPreview=[];retrievalPreviewDirty=true;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
    private void PreviewRetrieval(bool selected=false,bool refreshStock=true)
    {
        if(retrievalCatalog==null)throw new InvalidOperationException("Item catalogue is not ready.");
        retrievalPreviewSelected=selected;
        retrievalRecipeChoice=0;retrievalPlan=[];retrievalPreview=[];retrievalCraftSteps=[];
        var built=RetainerRetrieval.BuildPlan(retrievalItems.Where(x=>!selected||retrievalSelected.Contains(x.ItemId)),retrievalMultiplier,retrievalIncludesSubcrafts,retrievalMode,RetrievalRecipes(),retrievalCatalog.WorkshopByItem,retrievalCatalog.Outfits);
        retrievalPlan=built.Materials;retrievalCraftSteps=built.Steps;
        try{if(refreshStock)RefreshRetrievalStock();}catch(Exception e){retrievalStockComplete=false;retrievalStockNotice="Inventory unavailable: "+e.Message;}
        retrievalPreviewDirty=false;
        retrievalPreview=retrievalPlan.Select(e=>new RetrievalPreview(e.ItemId,e.Name,e.Quantity,retrievalSubtract?RetrievalHeld(e.ItemId,retrievalQuality):0,retrievalStockComplete?retrievalStock.Where(x=>x.Item==e.ItemId&&RetainerRetrieval.AllowsQuality(x.Hq,retrievalQuality)).Sum(x=>x.Quantity):null)).ToArray();
    }
    private unsafe void StartRetrieval(bool selected){RetrievalTry(()=>{
        if(organizeRunning||chestRunning||retainerGilRunning)throw new InvalidOperationException("Finish the chest operation first.");PreviewRetrieval(selected);if(retrievalPlan.Length==0)throw new InvalidOperationException("Choose at least one item.");
        if(!Player.IsLoaded||!RetainerToolsContext())throw new InvalidOperationException("Open a nearby summoning bell's retainer list first.");
        if(sellerRun.Running||gilTrade.Running)throw new InvalidOperationException("Finish Seller or Gil Trader before retrieval.");
        retrievalPort.Quality=retrievalQuality;retrievalPort.Character=Player.ContentId;retrievalPort.World=Player.CurrentWorld.RowId;
        retrievalRun.Start(ReadSellerRetainers().Select(x=>x.Id).ToArray(),retrievalPlan,retrievalSubtract,DateTimeOffset.UtcNow);
    });}
    private void DrawRetriever()
    {
        if(!retrievalRun.Running&&retrievalPreviewOwner!=Player.ContentId){retrievalPreviewOwner=Player.ContentId;retrievalRefreshPending=true;retrievalStock=[];retrievalStockComplete=false;retrievalPreview=[];retrievalPlan=[];retrievalPreviewDirty=true;}

        if(retrievalCatalogTask?.IsCompleted==true){try{retrievalCatalog=retrievalCatalogTask.GetAwaiter().GetResult();}catch(Exception e){retrievalError="Item catalogue: "+e.Message;}retrievalCatalogTask=null;}
        if(retrievalCatalog!=null&&!retrievalArtisanRequested)LoadRetrievalArtisan();
        if(retrievalArtisanTask?.IsCompleted==true){try{var read=retrievalArtisanTask.GetAwaiter().GetResult();retrievalArtisan=read.Lists;retrievalArtisanNotice=read.Lists.Length+" saved lists"+(read.Warnings.Length>0?" · "+string.Join("; ",read.Warnings):read.Lists.Length==0?" · Save a list in Artisan, then Refresh.":"");}catch(Exception e){retrievalArtisanNotice=e.Message;}retrievalArtisanTask=null;}
        windowStatus=organizeRunning?organizeStatus:chestRunning?chestStatus:retrievalRun.Running?retrievalRun.Status:retrievalError.Length>0?retrievalError:retrievalRun.Status;
        if(organizeRunning){ImGui.TextWrapped(organizeStatus);if(ImGui.SmallButton("Stop##OrganiserGlobal"))StopOrganize("Stopped by you; an already-sent move may still complete.");}
        if(retrievalRun.Running){ImGui.TextUnformatted(retrievalRun.Status);if(ImGui.Button("Stop retrieval"))retrievalRun.Stop("Stopped by you.");ImGui.Separator();}
        if(retrievalCatalog==null){ImGui.TextWrapped(retrievalCatalogTask!=null?"Loading item and crafting catalogues…":"Item catalogue unavailable.");if(retrievalCatalogTask==null&&ImGui.Button("Retry"))LoadRetrievalCatalog();return;}
        if(retrievalRefreshPending&&!retrievalRun.Running){retrievalRefreshPending=false;RetrievalTry(RefreshRetrievalStock);}
        ImGui.BeginDisabled(retrievalRun.Running||organizeRunning||chestRunning);
        DrawRetrievalPlanSelector();
        ImGui.SetNextItemWidth(Math.Max(100,ImGui.GetContentRegionAvail().X-(retrievalCleared!=null?270:180)));ImGui.InputText("##ListName",ref retrievalName,100);ImGui.SameLine();
        if(ImGui.Button("Save preset"))RetrievalTry(SaveRetrievalPreset);
        ImGui.SameLine();if(ImGui.SmallButton("Clear list")){retrievalCleared=new(retrievalName,retrievalItems.ToArray(),retrievalMultiplier,retrievalIncludesSubcrafts,Mode:retrievalMode,RecipeChoices:new(retrievalRecipeOverrides),Quality:retrievalQuality,DestinationRetainer:retrievalSendTo,SendCategories:retrievalSendCategories.ToArray());retrievalPickItems=false;retrievalPickLast=0;retrievalItems=[];retrievalPreviewSelected=false;retrievalSelected.Clear();retrievalPlan=[];retrievalPreview=[];retrievalCraftSteps=[];retrievalExportSteps=[];retrievalRecipeChoice=0;retrievalRecipeOverrides.Clear();retrievalError="";retrievalPreviewDirty=true;retrievalName="New list";retrievalEditingPreset=null;retrievalMultiplier=1;retrievalIncludesSubcrafts=false;retrievalMode=RetrievalMode.DirectIngredients;retrievalQuality=RetrievalQuality.Any;retrievalSendTo=0;retrievalSendCategories.Clear();}
        if(retrievalCleared!=null){ImGui.SameLine();if(ImGui.SmallButton("Undo clear")){SelectRetrievalPreset(retrievalCleared);retrievalCleared=null;}}

        if(retrievalItems.Length>0&&ImGui.SmallButton($"View current list · {retrievalItems.Length} items"))retrievalShowList=true;
        ImGui.EndDisabled();
        DrawRetrievalPicker();
        if(ImGui.BeginTabBar("RetrieverTabs")){
            if(ImGui.BeginTabItem("Browse inventory")){ImGui.BeginDisabled(retrievalRun.Running||organizeRunning||chestRunning);DrawRetrievalBrowse();ImGui.EndDisabled();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Retrieve",retrievalShowList?ImGuiTabItemFlags.SetSelected:ImGuiTabItemFlags.None)){retrievalShowList=false;ImGui.BeginDisabled(retrievalRun.Running||organizeRunning||chestRunning);DrawRetrievalList();ImGui.EndDisabled();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Organize retainers")){DrawRetainerOrganizer();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("FC chest")){DrawRetrievalChest();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Add items")){ImGui.BeginChild("RetrieverSourcesScroll",new Vector2(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);ImGui.BeginDisabled(retrievalRun.Running||organizeRunning||chestRunning);DrawRetrievalSources();ImGui.EndDisabled();ImGui.EndChild();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Lists & import")){ImGui.BeginChild("RetrieverPresetsScroll",new Vector2(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);ImGui.BeginDisabled(retrievalRun.Running||organizeRunning||chestRunning);DrawRetrievalPresets();ImGui.EndDisabled();ImGui.EndChild();ImGui.EndTabItem();}
            ImGui.EndTabBar();}
        
        if(retrievalError.Length>0)ImGui.TextWrapped(retrievalError);
    }
    private void DrawRetrievalList()
    {
        var modes=new[]{RetrievalMode.DirectIngredients,RetrievalMode.AllMaterials,RetrievalMode.CompletedItems};var mode=Array.IndexOf(modes,retrievalMode);if(mode<0)mode=2;
        ImGui.SetNextItemWidth(Math.Max(170,ImGui.GetContentRegionAvail().X-130));if(ImGui.Combo("##RetrievalMode",ref mode,"Recipe ingredients / workshop turn-ins\0All raw ingredients (expand subcrafts)\0Listed items themselves\0")){retrievalMode=modes[mode];retrievalPreview=[];retrievalPreviewDirty=true;}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Recipe ingredients: gear → its direct recipe materials; workshop parts → all phase turn-ins. All raw ingredients: expand subcrafts as well. Listed items themselves: retrieve exactly what you entered, e.g. 600 finished ingots. Non-craftable items remain listed; no ingredients are invented.");
        ImGui.SameLine();ImGui.SetNextItemWidth(90);if(ImGui.InputInt("×",ref retrievalMultiplier)){retrievalMultiplier=Math.Clamp(retrievalMultiplier,1,999);retrievalPreview=[];retrievalPreviewDirty=true;}
        var quality=(int)retrievalQuality;ImGui.SetNextItemWidth(180);if(ImGui.Combo("Quality",ref quality,"Any quality (NQ + HQ)\0NQ only\0HQ only\0")){retrievalQuality=(RetrievalQuality)quality;retrievalPreviewDirty=true;}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Filters bag counts, recorded retainer stock and actual withdrawals. HQ only never substitutes NQ, including items such as crystals that have no HQ version.");
        if(ImGui.CollapsingHeader("List options")){
        if(ImGui.Checkbox("List contains outputs AND their subcrafts",ref retrievalIncludesSubcrafts)){retrievalPreviewDirty=true;retrievalPreview=[];}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Enable only for lists containing both desired outputs AND their intermediate recipes. Intermediate amounts used by listed recipes are removed from the output request; surplus remains. Leave off for workshop turn-ins. List names never change quantities.");
        if(ImGui.Checkbox("Subtract matching items in bags",ref retrievalSubtract)){retrievalPreview=[];retrievalPreviewDirty=true;}
        }
        if(ImGui.Button("Preview all"))RetrievalTry(()=>PreviewRetrieval());ImGui.SameLine();if(ImGui.Button("Preview selected"))RetrievalTry(()=>PreviewRetrieval(true));ImGui.SameLine();if(ImGui.SmallButton("Prepare Artisan list…"))RetrievalTry(PrepareRetrievalArtisanExport);
        if(ImGui.Button("Retrieve all"))StartRetrieval(false);ImGui.SameLine();if(ImGui.Button("Retrieve selected"))StartRetrieval(true);
        DrawRetrievalSendControls();
        DrawRetrievalArtisanExport();
        DrawRetrievalRecipeChoice();
        if(retrievalPreviewDirty){retrievalPreviewDirty=false;RetrievalTry(()=>PreviewRetrieval(retrievalPreviewSelected,false));}
        ImGui.BeginChild("RetrievalContents",new Vector2(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-8)),false);
        if(retrievalError.Length>0)ImGui.TextWrapped(retrievalError);
        if(retrievalMode!=RetrievalMode.CompletedItems){var uncraftable=retrievalItems.Where(e=>!e.ReadyMaterial&&!retrievalCatalog!.Recipes.ContainsKey(e.ItemId)&&!retrievalCatalog.WorkshopByItem.ContainsKey(e.ItemId)&&!retrievalCatalog.Outfits.ContainsKey(e.ItemId)).Select(e=>e.Name).ToArray();if(uncraftable.Length>0)ImGui.TextWrapped("No crafting recipe found: "+string.Join(", ",uncraftable)+". These items stay unchanged. Choose Crafting logs in Add items to find craftable equipment; upgraded/exchange versions may not be craftable.");}
        ImGui.TextUnformatted(retrievalName+" · "+retrievalPlan.Length+" items · ×"+retrievalMultiplier+(retrievalPreviewSelected?" · selected only":" · all items"));
        if(retrievalPreview.Length>0&&ImGui.BeginTable("RetrievalPreview",6,ImGuiTableFlags.RowBg|ImGuiTableFlags.Resizable|ImGuiTableFlags.BordersInnerH)){
            ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch,3);foreach(var t in new[]{"Required","Bags used","To fetch","Retainers*","Missing*"})ImGui.TableSetupColumn(t,ImGuiTableColumnFlags.WidthStretch,1);ImGui.TableHeadersRow();
            foreach(var r in retrievalPreview){ImGui.TableNextRow();ImGui.TableNextColumn();DrawRetrievalIcon(r.ItemId);ImGui.TextUnformatted(r.Name);
                if(ImGui.IsItemHovered())ImGui.SetTooltip(r.Name+"\n"+string.Join("\n",retrievalStock.Where(x=>x.Item==r.ItemId&&RetainerRetrieval.AllowsQuality(x.Hq,retrievalQuality)).Select(x=>x.Holder+": "+x.Quantity)));
                foreach(var t in new[]{r.Required.ToString(),r.Held.ToString(),r.Need.ToString(),r.Available?.ToString()??(retrievalStock.Any(x=>x.Item==r.ItemId&&RetainerRetrieval.AllowsQuality(x.Hq,retrievalQuality))?retrievalStock.Where(x=>x.Item==r.ItemId&&RetainerRetrieval.AllowsQuality(x.Hq,retrievalQuality)).Sum(x=>x.Quantity)+"+":"Unknown"),r.Missing?.ToString()??"Unknown"}){ImGui.TableNextColumn();ImGui.TextUnformatted(t);}}
            ImGui.EndTable();ImGui.TextDisabled("* Recorded stock · hover item for retainers · Preview refreshes counts");}
        if(retrievalCraftSteps.Length>0&&ImGui.CollapsingHeader("Subcrafts and outputs · craft order")){foreach(var step in retrievalCraftSteps){DrawRetrievalIcon(step.ItemId);ImGui.TextUnformatted($"{step.Name} · {step.Crafts} crafts → {step.Crafts*step.Yield} items");if(retrievalCatalog!.Recipes.TryGetValue(step.ItemId,out var alternatives)&&alternatives.Length>1){ImGui.SameLine();if(ImGui.SmallButton("Recipe##"+step.ItemId))retrievalRecipeChoice=step.ItemId;}}ImGui.TextWrapped("These are made from the ingredients above, not additional items to withdraw.");}
        if(retrievalItems.Length==0)ImGui.TextWrapped("Choose a saved or Artisan list in Lists & import, or add items to build your own.");
        if(ImGui.CollapsingHeader("Edit requested items / selection"))
        if(ImGui.BeginTable("RetrievalItems",4,ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.Resizable)){
            foreach(var title in new[]{"Item","Quantity","Recipe",""})ImGui.TableSetupColumn(title);ImGui.TableHeadersRow();
            foreach(var e in retrievalItems){ImGui.PushID((int)e.ItemId);ImGui.TableNextRow();ImGui.TableNextColumn();var selected=retrievalSelected.Contains(e.ItemId);if(ImGui.Checkbox("##Selected",ref selected)){if(selected)retrievalSelected.Add(e.ItemId);else retrievalSelected.Remove(e.ItemId);if(retrievalPreviewSelected)retrievalPreviewDirty=true;}ImGui.SameLine();DrawRetrievalIcon(e.ItemId);ImGui.TextUnformatted(e.Name);ImGui.TableNextColumn();var q=e.Quantity;ImGui.SetNextItemWidth(-1);if(ImGui.InputInt("##Qty",ref q)){retrievalItems=retrievalItems.Select(x=>x.ItemId==e.ItemId?x with{Quantity=Math.Clamp(q,1,RetainerRetrieval.MaxQuantity)}:x).ToArray();retrievalPreview=[];retrievalPreviewDirty=true;}
                ImGui.TableNextColumn();if(retrievalCatalog!.Recipes.TryGetValue(e.ItemId,out var choices)&&ImGui.BeginCombo("##Recipe",e.RecipeId>0?e.RecipeId.ToString():"Auto")){foreach(var choice in choices)if(ImGui.Selectable(choice.Job+" · "+choice.Id+" · yield "+choice.Yield,e.RecipeId==choice.Id)){retrievalRecipeOverrides[e.ItemId]=choice.Id;retrievalItems=retrievalItems.Select(x=>x.ItemId==e.ItemId?x with{RecipeId=choice.Id}:x).ToArray();retrievalPreviewDirty=true;}ImGui.EndCombo();}else if(!retrievalCatalog.Recipes.ContainsKey(e.ItemId))ImGui.TextDisabled("No recipe");ImGui.TableNextColumn();if(ImGui.SmallButton("×")){retrievalSelected.Remove(e.ItemId);retrievalItems=retrievalItems.Where(x=>x.ItemId!=e.ItemId).ToArray();retrievalPreview=[];retrievalPreviewDirty=true;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}ImGui.PopID();}
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
        retrievalEditingPreset=config.RetrievalPresets.Any(x=>x.Name==p.Name)?p.Name:null;
        retrievalItems=p.Items.ToArray();retrievalSelected.Clear();foreach(var e in retrievalItems)retrievalSelected.Add(e.ItemId);
        retrievalRecipeOverrides.Clear();foreach(var pair in p.RecipeChoices??[])retrievalRecipeOverrides[pair.Key]=pair.Value;retrievalRecipeChoice=0;retrievalMode=p.Mode;retrievalQuality=p.Quality;retrievalPreviewSelected=false;retrievalPlan=[];retrievalCraftSteps=[];retrievalPickItems=false;retrievalPickLast=0;retrievalName=p.Name;retrievalSendTo=p.DestinationRetainer;retrievalSendCategories.Clear();foreach(var category in p.SendCategories??[])retrievalSendCategories.Add(category);retrievalMultiplier=p.Multiplier;retrievalIncludesSubcrafts=p.IncludesSubcrafts;retrievalPreview=[];retrievalPreviewDirty=true;retrievalShowList=true;
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
        if(key!=retrievalLastFilter){retrievalLastFilter=key;retrievalSearchPage=0;retrievalSearchWorkshop=retrievalCatalog!.Workshop.Where(x=>x.Name.Contains(retrievalSearch,StringComparison.OrdinalIgnoreCase)).ToArray();retrievalSearchItems=retrievalCatalog!.Names.Where(x=>x.Value.Contains(retrievalSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Value).ThenBy(x=>x.Key).ToArray();retrievalSearchRecipes=retrievalCatalog.Recipes.Values.SelectMany(x=>x).Where(x=>(retrievalJob=="All crafting logs"||x.Job==retrievalJob)&&retrievalCatalog.Names[x.ItemId].Contains(retrievalSearch,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>retrievalCatalog.Names[x.ItemId]).ThenBy(x=>x.Id).ToArray();}
        var total=retrievalSource==2?retrievalSearchWorkshop.Length:retrievalSource==1?retrievalSearchRecipes.Length:retrievalSearchItems.Length;
        var pages=Math.Max(1,(total+79)/80);retrievalSearchPage=Math.Clamp(retrievalSearchPage,0,pages-1);
        ImGui.BeginDisabled(retrievalSearchPage==0);if(ImGui.SmallButton("Previous"))retrievalSearchPage--;ImGui.EndDisabled();ImGui.SameLine();
        ImGui.TextUnformatted($"Page {retrievalSearchPage+1}/{pages} · {total:N0} results");ImGui.SameLine();
        ImGui.BeginDisabled(retrievalSearchPage+1>=pages);if(ImGui.SmallButton("Next"))retrievalSearchPage++;ImGui.EndDisabled();
        if(total==0){ImGui.TextDisabled("No matches in this source.");if(retrievalSource==1&&retrievalSearchWorkshop.Length>0){ImGui.TextWrapped("These are FC workshop projects, rather than ordinary crafting recipes.");if(ImGui.SmallButton("Show workshop turn-ins"))retrievalSource=2;}}
        if(retrievalSource==2){foreach(var p in retrievalSearchWorkshop.Skip(retrievalSearchPage*80).Take(80)){if(ImGui.Selectable(p.Name))RetrievalTry(()=>AddRetrieval(p.Items.Select(x=>x with{Quantity=checked(x.Quantity*retrievalQuantity)}).ToArray()));DrawRetrievalPresetContents(p);}}
        else if(retrievalSource==1){foreach(var r in retrievalSearchRecipes.Skip(retrievalSearchPage*80).Take(80)){DrawRetrievalIcon(r.ItemId);if(ImGui.Selectable(retrievalCatalog!.Names[r.ItemId]+" · "+r.Job+" · yield "+r.Yield+"##"+r.Id))RetrievalTry(()=>AddRetrieval([new(r.ItemId,retrievalCatalog.Names[r.ItemId],retrievalQuantity,r.Id)]));}}
        else foreach(var e in retrievalSearchItems.Skip(retrievalSearchPage*80).Take(80)){DrawRetrievalIcon(e.Key);if(ImGui.Selectable(e.Value+(retrievalCatalog!.Recipes.ContainsKey(e.Key)?" · craftable":retrievalCatalog.WorkshopByItem.ContainsKey(e.Key)?" · workshop":retrievalCatalog.Outfits.ContainsKey(e.Key)?" · outfit pieces":" · no crafting recipe")+"##"+e.Key))RetrievalTry(()=>AddRetrieval([new(e.Key,e.Value,retrievalQuantity)]));}
        ImGui.TextDisabled("Click an item to add it · Search covers the full catalogue");
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
        if(ImGui.Button("Export preset to clipboard"))RetrievalTry(()=>ImGui.SetClipboardText(RetainerRetrieval.Export(new(retrievalName,retrievalItems,retrievalMultiplier,retrievalIncludesSubcrafts,Mode:retrievalMode,RecipeChoices:new(retrievalRecipeOverrides),Quality:retrievalQuality,DestinationRetainer:retrievalSendTo,SendCategories:retrievalSendCategories.ToArray()))));ImGui.SameLine();if(ImGui.Button("Copy plain list"))ImGui.SetClipboardText(string.Join("\n",retrievalItems.Select(e=>$"{e.Quantity}x {e.Name}")));
        ImGui.TextDisabled("Paste 600x Cobalt Ingot on each line, or an exported Equinox preset.");
    }
}
