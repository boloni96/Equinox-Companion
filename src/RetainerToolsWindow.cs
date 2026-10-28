using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private RetainerToolsWindow retainerToolsWindow=null!;
    private bool retainerToolsWasOpen;private string? retrievalEditingPreset;
    private sealed class RetainerToolsWindow(Plugin p):Window("Retainer tools · Equinox###EquinoxRetainerTools",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing)
    {
        public override void Draw()=>p.DrawRetainerTools();
        public override void OnClose(){p.retrievalPickItems=false;}
        public override unsafe void PreDraw(){var a=SellerAddon("RetainerList");if(a!=null){Position=new Vector2(Math.Max(0,(int)a->X),Math.Max(0,a->Y+a->GetScaledHeight(true)+4));PositionCondition=ImGuiCond.Always;}else Position=null;RespectCloseHotkey=false;AllowPinning=false;PushRetainerToolbarStyle();}
        public override void PostDraw(){ImGui.PopStyleVar(2);ImGui.PopStyleColor(7);}
    }
    private static unsafe bool RetainerToolsContext(){var rm=RetainerManager.Instance();return SellerAddon("RetainerList")!=null||(rm!=null&&RetrieverActive(rm->LastSelectedRetainerId));}
    private void SaveRetrievalPreset()
    {
        if(string.IsNullOrWhiteSpace(retrievalName))throw new ArgumentException("Name this plan first.");
        var at=config.RetrievalPresets.FindIndex(x=>x.Name==(retrievalEditingPreset??retrievalName));
        var p=new RetrievalPreset(retrievalName,RetainerRetrieval.Combine(retrievalItems),retrievalMultiplier,retrievalIncludesSubcrafts,Mode:retrievalMode,RecipeChoices:new(retrievalRecipeOverrides),Quality:retrievalQuality,DestinationRetainer:retrievalSendTo,SendCategories:retrievalSendCategories.ToArray());
        if(config.RetrievalPresets.Any(x=>x.Name==p.Name)&&at!=config.RetrievalPresets.FindIndex(x=>x.Name==p.Name))throw new ArgumentException("Another plan already has this name.");
        if(at>=0)config.RetrievalPresets[at]=p;else if(config.RetrievalPresets.Count<25)config.RetrievalPresets.Add(p);else throw new ArgumentException("Maximum 25 presets.");
        retrievalEditingPreset=p.Name;SaveConfiguration();
    }
    private void DrawRetrievalPlanSelector(bool compact=false)
    {
        ImGui.SetNextItemWidth(compact?200:220);
        if(ImGui.BeginCombo(compact?"##ToolbarPlan":"Plan",retrievalEditingPreset??retrievalName)){
            foreach(var plan in config.RetrievalPresets.ToArray())if(ImGui.Selectable(plan.Name,retrievalEditingPreset==plan.Name)){if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);SelectRetrievalPreset(config.RetrievalPresets.First(x=>x.Name==plan.Name));}
            ImGui.EndCombo();
        }
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Select the saved plan you want to edit or run. Shift/Alt changes are saved to that selected plan. HQ/NQ and crafting modes remain part of the plan.");
    }
    private unsafe void TickRetainerTools(DateTimeOffset now)
    {
        var open=config.EnableRetriever&&Player.IsLoaded&&Conditions[ConditionFlag.OccupiedSummoningBell];
        if(open&&retrievalPreviewOwner!=Player.ContentId){retrievalPreviewOwner=Player.ContentId;retrievalStock=[];retrievalStockComplete=false;retrievalPreview=[];retrievalPlan=[];retrievalPreviewDirty=true;retrievalRefreshPending=true;}
        if(open&&!retainerToolsWasOpen){retainerToolsWindow.IsOpen=true;retrievalRefreshPending=true;LoadRetrievalCatalog();}
        if(!open){retainerToolsWindow.IsOpen=false;if(!retrieverWindow.IsOpen)retrievalPickItems=false;}
        retainerToolsWasOpen=open;
        if(retrievalCatalogTask?.IsCompleted==true){try{retrievalCatalog=retrievalCatalogTask.GetAwaiter().GetResult();}catch(Exception e){retrievalError=e.Message;}retrievalCatalogTask=null;retrievalPreviewDirty=true;}
        if(open&&retrievalRefreshPending&&!organizeRunning&&!retrievalRun.Running&&!retainerGilRunning){retrievalRefreshPending=false;RetrievalTry(RefreshRetrievalStock);}
    }
    private bool retainerToolsMaterials=true;
    private unsafe void DrawRetainerTools()
    {
        var busy=organizeRunning||retrievalRun.Running||chestRunning||retainerGilRunning;
        ImGui.BeginDisabled(busy);DrawRetrievalPlanSelector(true);ImGui.SameLine();
        if(ImGui.SmallButton("Edit")){OpenRetriever();retrievalShowList=true;}
        MarketHelp("Edit the requested outputs, quantities and recipes in Equinox Organizer.");
        ImGui.EndDisabled();ImGui.SameLine();
        if(ImGui.SmallButton("...##RetainerOptions"))ImGui.OpenPopup("Retainer plan options");
        MarketHelp("Crafting mode, HQ/NQ, entrust destination and inventory item picking.");ImGui.SameLine();
        if(ImGui.SmallButton("×##HideRetainerTools")){retainerToolsWindow.IsOpen=false;retrievalPickItems=false;}
        MarketHelp("Hide this toolbar until the next summoning bell visit.");
        ImGui.BeginDisabled(busy||retrievalCatalog==null);
        if(ImGui.SmallButton("Withdraw"))StartRetrieval(false);MarketHelp("Withdraw the calculated materials below, respecting the plan's quality and bag subtraction settings.");ImGui.SameLine();
        if(ImGui.SmallButton("Entrust"))SendRetrievalList(false);MarketHelp("Entrust the calculated quantities from your bags to the plan's destination. Entrust categories, if selected, are additional.");ImGui.SameLine();
        if(ImGui.SmallButton("Gil"))StartRetainerGil();MarketHelp("Withdraw available gil from the open retainer, or all retainers from the retainer list. Checks your gil cap.");
        ImGui.EndDisabled();ImGui.SameLine();
        if(ImGui.SmallButton(retainerToolsMaterials?"Hide materials":"Materials"))retainerToolsMaterials=!retainerToolsMaterials;
        if(busy){ImGui.SameLine();if(ImGui.SmallButton("Stop")){StopRetainerGil("Stopped by you.");if(organizeRunning)StopOrganize("Stopped by you.");if(retrievalRun.Running)retrievalRun.Stop("Stopped by you.");}}
        if(ImGui.BeginPopup("Retainer plan options")){
            ImGui.BeginDisabled(busy);
            var changed=false;
            var modes=new[]{RetrievalMode.DirectIngredients,RetrievalMode.AllMaterials,RetrievalMode.CompletedItems,RetrievalMode.FinalItems};
            var mode=Array.IndexOf(modes,retrievalMode);ImGui.SetNextItemWidth(300);
            if(ImGui.Combo("Mode",ref mode,"Recipe ingredients / workshop turn-ins\0All raw ingredients (expand subcrafts)\0Listed items themselves\0Workshop turn-ins / other items as listed\0")){retrievalMode=modes[mode];changed=true;}
            MarketHelp("Workshop turn-ins are the finished ingots, rivets and lumber for all phases. Raw ingredients also expand their recipes. Listed items themselves keeps the submarine parts.");
            var quality=(int)retrievalQuality;ImGui.SetNextItemWidth(160);
            if(ImGui.Combo("Quality",ref quality,"Any quality\0NQ only\0HQ only\0")){retrievalQuality=(RetrievalQuality)quality;changed=true;}
            ImGui.SetNextItemWidth(90);if(ImGui.InputInt("Multiplier",ref retrievalMultiplier)){retrievalMultiplier=Math.Clamp(retrievalMultiplier,1,999);changed=true;}
            if(ImGui.Checkbox("Subtract matching items in bags",ref retrievalSubtract))changed=true;
            var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
            ImGui.SetNextItemWidth(200);
            if(ImGui.BeginCombo("Entrust to",retrievalSendTo==StorageAutoRouting.AllRetainers?"All retainers":names.GetValueOrDefault(retrievalSendTo,"Choose destination"))){
                if(ImGui.Selectable("All retainers",retrievalSendTo==StorageAutoRouting.AllRetainers)){retrievalSendTo=StorageAutoRouting.AllRetainers;changed=true;}
                foreach(var row in names)if(ImGui.Selectable(row.Value+"##target"+row.Key,retrievalSendTo==row.Key)){retrievalSendTo=row.Key;changed=true;}
                ImGui.EndCombo();
            }
            if(changed){retrievalPreviewDirty=true;if(retrievalEditingPreset!=null)RetrievalTry(SaveRetrievalPreset);}
            DrawRetrievalPicker();ImGui.EndDisabled();
            if(ImGui.Button("Refresh quantities")){retrievalRefreshPending=true;retrievalPreviewDirty=true;}
            if(ImGui.Button("Open Organizer")){OpenRetriever();retrievalShowList=true;ImGui.CloseCurrentPopup();}
            ImGui.EndPopup();
        }
        // Picking must keep working when the options popup is closed.
        if(retrievalPickItems){TickRetrievalPicker();ImGui.TextDisabled("Picking · Shift add / Alt remove");}
        if(!busy&&!retrieverWindow.IsOpen&&retrievalCatalog!=null&&(retrievalPreviewDirty||retrievalPreviewSelected)){
            retrievalPreviewDirty=false;RetrievalTry(()=>PreviewRetrieval(false,false));
        }
        var status=retainerGilRunning?retainerGilStatus:organizeRunning?organizeStatus:retrievalRun.Running?retrievalRun.Status:retrievalError.Length>0?retrievalError:retainerGilStatus;
        if(status.Length>0){ImGui.SameLine();ImGui.TextDisabled(busy?"…":"i");MarketHelp(status);}
        if(!retainerToolsMaterials)return;
        if(retrieverWindow.IsOpen){ImGui.TextDisabled("Preview and edit in Organizer");return;}
        if(retrievalCatalog==null){ImGui.TextDisabled("Loading item catalogue…");return;}
        if(retrievalError.Length>0){ImGui.TextWrapped(retrievalError);if(retrievalRecipeChoice!=0&&ImGui.SmallButton("Choose recipe")){OpenRetriever();retrievalShowList=true;}return;}
        ImGui.Separator();
        var label=retrievalMode switch{RetrievalMode.CompletedItems=>"Listed items",RetrievalMode.AllMaterials=>"Raw ingredients",RetrievalMode.FinalItems=>"Turn-ins / listed items",_=>"Ingredients / turn-ins"};
        ImGui.TextDisabled($"{label} · {retrievalPreview.Length} · {(retrievalQuality==RetrievalQuality.Any?"NQ + HQ":retrievalQuality==RetrievalQuality.HqOnly?"HQ only":"NQ only")}");
        MarketHelp("Total = required by this plan. Bags = counted towards withdrawal. Withdraw = still needed. Hover an item for recorded retainer stock and missing quantities. Entrust uses Total, limited to what you hold.");
        if(retrievalPreview.Length==0){ImGui.TextDisabled("Choose a plan or add items in Edit.");return;}
        var height=Math.Min(8,retrievalPreview.Length)*(ImGui.GetTextLineHeight()+7)+ImGui.GetFrameHeight()+8;
        if(ImGui.BeginTable("ToolbarMaterials",4,ImGuiTableFlags.ScrollY|ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH,new Vector2(430,height))){
            ImGui.TableSetupColumn("Item",ImGuiTableColumnFlags.WidthStretch);
            foreach(var title in new[]{"Total","Bags","Withdraw"})ImGui.TableSetupColumn(title,ImGuiTableColumnFlags.WidthFixed,55);
            ImGui.TableSetupScrollFreeze(0,1);ImGui.TableHeadersRow();
            foreach(var row in retrievalPreview){
                ImGui.TableNextRow();ImGui.TableNextColumn();DrawRetrievalIcon(row.ItemId);ImGui.TextUnformatted(row.Name);
                MarketHelp(row.Name+"\nRecorded retainer stock: "+(row.Available?.ToString()??"Unknown")+"\nMissing after withdrawal: "+(row.Missing?.ToString()??"Unknown")+"\n"+string.Join("\n",retrievalStock.Where(x=>x.Item==row.ItemId&&RetainerRetrieval.AllowsQuality(x.Hq,retrievalQuality)).Select(x=>x.Holder+": "+x.Quantity)));
                foreach(var n in new[]{row.Required,row.Held,row.Need}){ImGui.TableNextColumn();ImGui.TextUnformatted(n.ToString());}
            }
            ImGui.EndTable();
        }
    }
}
