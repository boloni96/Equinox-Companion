using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool organizeSelectionMode;
    private StorageRoute[] VisibleOrganizeRules()=>organizeSelectionMode?OrganizeRules():NoOrganizerPlan?WholeOrganizerRules():ScopeOrganizeRules(WholeOrganizerRules());
    private ulong organizeFrom,organizeTo=StorageAutoRouting.AllRetainers;private int organizePreviewPage;
    private readonly HashSet<uint> organizeCategories=[];
    private StoragePreviewRow[] organizePreview=[];
    private bool organizeRunning;private int organizeStage,organizeSourceIndex,organizeTransferred;
    private ulong[] organizeSources=[],organizeOwnerOrder=[];private RetrieverPort? organizePort;
    private DateTimeOffset organizeNext,organizeDeadline;
    private ChestMove? organizePending;
    private object? organizerLastTransfer,organizerSubmission;
    private DateTimeOffset organizerSubmittedAt;
    private readonly Queue<object> organizerTransferIssues=[];
    private int organizerDeferred;
    private readonly HashSet<(uint Item,bool Hq)> organizerDeferredItems=[];

    private int organizeCarry;
    private string organizeStatus="Preview or organise the selected plan. Assignments choose homes for eligible listed items.";
    private void OrganizeStage(int stage){organizeStage=stage;organizeStatus=$"{organizeTransferred} delivered · {organizeMoveIndex}/{organizeMoves.Length} transfers · "+(stage switch{0=>"Preparing",1 or 2 or 3=>$"Refreshing missing inventory {organizeSourceIndex+1}/{organizeSources.Length}",4 or 5=>"Opening retainer",6 or 7=>"Moving batch",9=>"Calculating batch space",10 or 11 or 12 or 13 or 14=>$"Sorting retainer {organizeSortIndex+1}/{organizeSortRetainers.Length}",_=>"Next retainer"});organizeDeadline=DateTimeOffset.UtcNow.AddSeconds(60);}
    private void StopOrganize(string reason){ClearOrganizerSortMenu();organizeRunning=false;organizePort?.Release();organizePending=null;organizeStatus=reason+(organizerUnassigned>0?$" · {organizerUnassigned} item types without assigned destinations.":"")+(organizerDeferred>0?$" · {organizerDeferred} item types deferred after unconfirmed moves.":"")+(organizeCarry>0?$" · {organizeCarry} transferred items remain in your bags.":"");SaveOrganizerInventories();InvalidateOrganizePreview();retrievalRefreshPending=true;Log.Information("Retainer organizer: {Status}",organizeStatus);}
    private uint OrganizeCategory(uint item)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(item) is {} row?GearCategorySelection.Category(row.ItemUICategory.RowId,row.LevelEquip,row.EquipSlotCategory.RowId>0):0;
    private void DrawRetainerOrganizer()
    {
        ImGui.BeginDisabled(gearRouting||armoireRunning||organizeRunning||retrievalRun.Running||chestRunning);
        if(NoOrganizerPlan)ImGui.TextDisabled("No plan · all retainer items, narrowed by movement filters");else DrawPlanCategoryRules();
        var scope=organizeSelectionMode?1:0;ImGui.SetNextItemWidth(220);
        if(ImGui.Combo("Scope",ref scope,"All retainers\0Selected movement\0")){organizeSelectionMode=scope==1;InvalidateOrganizePreview();}
        MarketHelp("All retainers organizes eligible stock across this character's retainers. Selected movement uses the source/destination below, or your custom movements. Plans and filters always limit eligible items; assignments choose their allowed homes.");
        DrawRetainerAssignments();
        var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
        void Choose(string label,ref ulong id,bool all){if(ImGui.BeginCombo(label,id==StorageAutoRouting.AllRetainers?"All retainers":id==0?(all?"All other retainers":"Choose retainer"):names.GetValueOrDefault(id,id.ToString()))){if(!all&&ImGui.Selectable("All retainers · consolidate duplicates",id==StorageAutoRouting.AllRetainers)){id=StorageAutoRouting.AllRetainers;InvalidateOrganizePreview();}if(all&&ImGui.Selectable("All other retainers",id==0)){id=0;InvalidateOrganizePreview();}foreach(var n in names)if(ImGui.Selectable(n.Value+"##"+n.Key,id==n.Key)){id=n.Key;InvalidateOrganizePreview();}ImGui.EndCombo();}}
        if(organizeSelectionMode&&ImGui.BeginTable("MovementTargets",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);Choose("##From",ref organizeFrom,true);MarketHelp("Source retainer");
            ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);Choose("##To",ref organizeTo,false);MarketHelp("Destination retainer. All retainers uses assignments when enabled, otherwise duplicate consolidation.");ImGui.EndTable();
        }
        if(ImGui.BeginTable("MovementFilters",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(DrawStorageCategoryPicker("##MovementCategories",organizeCategories,"All categories"))InvalidateOrganizePreview();
            ImGui.TableNextColumn();var q=(int)retrievalQuality;ImGui.SetNextItemWidth(-1);if(ImGui.Combo("##MovementQuality",ref q,"Any quality\0NQ only\0HQ only\0")){retrievalQuality=(RetrievalQuality)q;InvalidateOrganizePreview();retrievalPreviewDirty=true;}ImGui.EndTable();
        }
        if(ImGui.Button("Preview"))RetrievalTry(CheckOrganize);MarketHelp("Preview this scope without summoning retainers or moving items.");
        ImGui.SameLine();ImGui.BeginDisabled(VisibleOrganizeRules().Length==0||organizeSelectionMode&&organizeTo==0&&organizeRules.Count==0);
        if(ImGui.Button("Organize"))StartStorageRouting(VisibleOrganizeRules(),false);ImGui.EndDisabled();
        ImGui.SameLine();if(ImGui.SmallButton(config.SortItemsAfterOrganizing?"Options · sort on":"Options…"))ImGui.OpenPopup("MovementOptions");
        if(ImGui.BeginPopup("MovementOptions")){
            var sortAfter=config.SortItemsAfterOrganizing;
            if(ImGui.Checkbox("Sort items after transfers",ref sortAfter)){config.SortItemsAfterOrganizing=sortAfter;SaveConfiguration();}
            MarketHelp("After each retainer’s final confirmed transfer, use its normal item-menu Sort action with your game settings. Applies to Entrust and Withdraw too.");
            ImGui.SetNextItemWidth(100);if(ImGui.SliderInt("Leave bag slots free",ref organizeReserve,0,10))InvalidateOrganizePreview();
            MarketHelp("Reserve free bag slots for staging. One spare may temporarily unblock a full-retainer swap.");
            if(organizeSelectionMode){
                ImGui.Separator();ImGui.TextDisabled("Custom movements");
                ImGui.BeginDisabled(organizeTo==0);if(ImGui.SmallButton("Add movement")){organizeRules.Add(CurrentOrganizeRule());InvalidateOrganizePreview();}ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton("Clear movements")){organizeRules.Clear();InvalidateOrganizePreview();}
                ImGui.BeginChild("MovementRules",new(360,140),false);
                foreach(var rule in organizeRules.ToArray()){ImGui.PushID(organizeRules.IndexOf(rule));if(ImGui.SmallButton("×")){organizeRules.Remove(rule);InvalidateOrganizePreview();}ImGui.SameLine();ImGui.TextWrapped($"{(rule.From==StorageRouting.AllSources?"All retainers":names.GetValueOrDefault(rule.From,"Retainer"))} → {(rule.To==StorageAutoRouting.AllRetainers?"All retainers":names.GetValueOrDefault(rule.To,"Retainer"))} · {(rule.AllItems?"all items":rule.Categories.Length+" categories")}");ImGui.PopID();}
                ImGui.EndChild();
            }
            ImGui.EndPopup();
        }
        ImGui.EndDisabled();
        if(organizeRunning&&ImGui.Button("Stop organizer"))StopOrganize("Stopped by you; an already-sent move may still complete.");
        if(organizeRunning||organizeMoves.Length>0)ImGui.TextWrapped(organizeStatus);
        if(retrievalError.Length>0)ImGui.TextWrapped(retrievalError);
        DrawOrganizePreview();
        windowStatus=organizeRunning?organizeStatus:retrievalError.Length>0?retrievalError:organizePreviewMessage;
    }
    private StorageRoute[] OrganizeRules(){var rules=organizeRules.Count>0?organizeRules.ToArray():[CurrentOrganizeRule()];return NoOrganizerPlan?rules:ScopeOrganizeRules(rules);}
    private StorageRoute CurrentOrganizeRule()=>new(organizeFrom==0?StorageRouting.AllSources:organizeFrom,organizeTo,organizeCategories.ToArray(),[],AllItems:organizeCategories.Count==0,Quality:retrievalQuality);
    private readonly List<StorageRoute> organizeRules=[];
    private StorageRoute[] organizeActiveRules=[];private int organizeReserve=1;
    private bool organizeBagsOnly,organizeNoPlan;private int organizeBagTradeFilter;private readonly Dictionary<ulong,ChestStack[]> organizeInventories=[];
    private Task<RoutingResult>? organizeRoutingTask;private RoutedMove[] organizeMoves=[];private int organizeMoveIndex,organizeUnmoved;
    private ulong organizeActiveRetainer;
    private unsafe void StartOrganize()=>StartStorageRouting(OrganizeRules(),false);
    private unsafe void SendRetrievalList(bool duplicates)
    {
        RetrievalTry(()=>{
            if(retrievalSendTo==0)throw new InvalidOperationException("Choose a destination retainer for this plan first.");
            if(duplicates)throw new InvalidOperationException("Use Entrust plan to move only the filtered plan items and preserve quantities and Keep amounts.");
            var amounts=retrievalEntrust!=null?retrievalEntrust.Amounts(FilteredPlanItems().Select(x=>x.ItemId),OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)),retrievalQuality,OrganizeCategory,Array.Empty<uint>()):PlanEntrustEntries().ToDictionary(e=>e.ItemId,e=>e.Quantity);
            if(amounts.Count==0)throw new InvalidOperationException("No listed items match this plan’s filters, Keep amounts and protection.");
            StartStorageRouting([new(StorageRouting.Bags,retrievalSendTo,[],amounts.Keys.ToArray(),Quality:retrievalQuality,Amounts:amounts)],true);
        });
    }
    private bool organizerHasRun;
    private int organizerUnassigned;
    private unsafe void StartStorageRouting(StorageRoute[] rules,bool bagsOnly,bool temporarySelection=false,int temporaryTradeFilter=0)
    {
        RetrievalTry(()=>{
            if(gearRouting||armoireRunning||craftingWithdrawalPending||organizeRunning||retrievalRun.Running||chestRunning||sellerRun.Running||TraderRunning||retainerGilRunning)throw new InvalidOperationException("Finish the current inventory operation first.");
            if(!Player.IsLoaded)throw new InvalidOperationException("Log in before transferring items.");
            if(!RetainerToolsContext()){
                var saved=rules.Select(r=>r with{Items=r.Items.ToArray(),Categories=r.Categories.ToArray(),Amounts=r.Amounts==null?null:new(r.Amounts)}).ToArray();
                QueueCraftingWithdrawal(false);craftingWithdrawalPrepared=true;
                pendingStorageAtBell=()=>StartStorageRouting(saved,bagsOnly,temporarySelection,temporaryTradeFilter);
                return;
            }
            var owned=ReadSellerRetainers().Select(x=>x.Id).ToArray();
            if(rules.Any(r=>r.To==0||r.To!=StorageAutoRouting.AllRetainers&&!owned.Contains(r.To)||r.From!=0&&r.From!=StorageRouting.AllSources&&!owned.Contains(r.From)))throw new InvalidOperationException("Choose this character's retainers for every movement.");
            if(config.UseRetainerAssignments.Contains(Player.ContentId)&&rules.Any(r=>r.To==StorageAutoRouting.AllRetainers)&&!StorageAssignments.HasSelections(config.RetainerAssignments.GetValueOrDefault(Player.ContentId)??[],config.RetrievalPresets))throw new InvalidOperationException("This character has no retainer assignments. Choose a destination retainer, create assignments, or turn off Use assignments.");
            EnsureChestMove();if(temporarySelection&&!bagsOnly)throw new InvalidOperationException("Temporary selection is only valid for Entrust.");organizeNoPlan=!bagsOnly&&NoOrganizerPlan;rules=temporarySelection?PlanItemScope.Restrict(rules,rules.SelectMany(r=>r.Items),OrganizeCategory):organizeNoPlan?rules:ScopeOrganizeRules(rules);if(rules.Length==0)throw new InvalidOperationException(OrganizeScopeMessage());organizeActiveRules=rules;organizeBagsOnly=bagsOnly;organizeBagTradeFilter=temporarySelection?Math.Clamp(temporaryTradeFilter,0,4):0;
            organizeSources=owned.Where(id=>rules.Any(r=>r.To==StorageAutoRouting.AllRetainers||r.To==id||!bagsOnly&&(r.From==StorageRouting.AllSources||r.From==id))).ToArray();
            organizeSortEnabled=false;organizeSortRetainers=owned.ToArray();organizeSortItem=null;organizeSortMenu=null;if(organizeSortEnabled)PrepareOrganizerSort();
            organizeOwnerOrder=organizeSources.ToArray();var recorded=RecordedOrganizerInventories(organizeSources);
            organizePort=storageBellHandoff?retrievalPort:new(this);organizePort.Character=Player.ContentId;organizePort.World=Player.CurrentWorld.RowId;organizePort.Quality=RetrievalQuality.Any;
            organizerTransferIssues.Clear();organizerDeferredItems.Clear();organizerDeferred=0;organizerSubmission=null;organizerLastTransfer=null;organizeCarry=0;organizeTransferred=0;organizeSourceIndex=0;organizePending=null;organizeMoveIndex=0;organizeMoves=[];organizeRoutingTask=null;organizeInventories.Clear();foreach(var row in recorded)organizeInventories[row.Key]=row.Value;
            organizeSources=organizeSources.Where(id=>!recorded.ContainsKey(id)).ToArray();
            organizerVisitCache.Clear();organizerReplans.Clear();organizerEntrusted.Clear();organizerProtectedBagSlots.Clear();foreach(var slot in OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).Where(x=>x.Item>0&&x.Quantity>0))organizerProtectedBagSlots.Add((slot.Container,slot.Slot));
            organizerHasRun=true;organizerUnassigned=0;organizeRunning=true;OrganizeStage(0);
        });
    }
    private unsafe ChestStack[] OrganizerInventory(bool retainer,bool crystals)=>
        (crystals?new uint[]{retainer?12001u:2001u}:retainer?Enumerable.Range(10000,7).Select(x=>(uint)x).ToArray():new uint[]{0,1,2,3}).SelectMany(ReadChestContainer).Select(s=>s with{CanDeposit=true}).ToArray();
    private static bool OrganizerSlotMatches(ChestStack expected,ChestStack actual)=>expected.Item==actual.Item&&expected.Quantity==actual.Quantity&&(expected.Item==0||expected.Hq==actual.Hq);
    private unsafe void TickOrganize(DateTimeOffset now)
    {
        if(organizePort?.Pause.RestorePending==true&&!organizeRunning)organizePort.Pause.Release(now);
        if(!organizeRunning||now<organizeNext)return;organizeNext=now.AddMilliseconds(250);
        try{
            if(organizePort==null||!organizePort.ContextMatches||retainerGilRunning||chestRunning||retrievalRun.Running){StopOrganize("Stopped: character, bell or active task changed.");return;}
            if(now>organizeDeadline){StopOrganize(organizeStage==7&&organizePending is {} waiting?$"Stopped: item {waiting.Source.Item} transfer confirmation timed out; the last move is unconfirmed.":"Stopped: timed out at stage "+organizeStage+". "+organizePort.InventoryStatus);return;}
            if(organizeStage==0){if(organizePort.Prepare()){if(organizeSources.Length==0)BeginStorageCalculation();else OrganizeStage(1);}return;}
            var pause=organizePort.Pause.Check(now);if(!pause.Ready){StopOrganize(pause.Error);return;}
            var manager=InventoryManager.Instance();if(manager==null)return;
            foreach(var op in manager->PendingOperations)if(!op.IsEmpty)return;
            if(organizeStage is >=10 and <=14){TickOrganizerSort(now);return;}
            switch(organizeStage){
                case 1:if(organizePort.Select(organizeSources[organizeSourceIndex]))OrganizeStage(2);break;
                case 2:if(organizePort.OpenInventory(organizeSources[organizeSourceIndex])&&organizePort.Read(organizeSources[organizeSourceIndex]) is not null){RememberOrganizerInventory(organizeSources[organizeSourceIndex],OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray());if(organizeSourceIndex==organizeSources.Length-1){organizeActiveRetainer=organizeSources[organizeSourceIndex];BeginStorageCalculation();}else OrganizeStage(3);}break;
                case 3:
                    if(!organizePort.Close(organizeSources[organizeSourceIndex]))break;
                    if(++organizeSourceIndex<organizeSources.Length){OrganizeStage(1);break;}
                    BeginStorageCalculation();break;
                case 9:
                    if(organizeRoutingTask?.IsCompleted!=true)break;var plan=organizeRoutingTask.GetAwaiter().GetResult();organizeRoutingTask=null;
                    organizeMoves=plan.Moves.Where(x=>!organizerDeferredItems.Contains((x.Move.Source.Item,x.Move.Source.Hq))).ToArray();organizeUnmoved=plan.Remaining;
                    if(organizeMoves.Length==0){FinishOrganize(organizerUnassigned>0?$"No further eligible transfers; {organizerUnassigned} selected item types have no assigned retainer. Assign a home or choose an explicit destination.":plan.Remaining==0?"Nothing needs moving: check current stock, Keep amounts, quality and item protections.":$"No available route for {plan.Remaining} items. Check bag and retainer space.");return;}
                    organizeActiveRetainer=organizeMoves[0].Retainer;OrganizeStage(RetrieverActive(organizeActiveRetainer)?5:4);break;
                case 4:if(organizePort.Select(organizeActiveRetainer))OrganizeStage(5);break;
                case 5:if(organizePort.OpenInventory(organizeActiveRetainer)&&organizePort.Read(organizeActiveRetainer) is not null)BindOrganizerVisit();break;
                case 6:
                    if(!RetrieverActive(organizeActiveRetainer)){StopOrganize("Stopped: selected retainer changed.");return;}
                    if(organizePort.Read(organizeActiveRetainer)==null)break;
                    var action=organizeMoves[organizeMoveIndex];var move=action.Move;
                    if(!PlanItemScope.Allows(action,organizeActiveRules,organizeBagsOnly,organizerEntrusted)){StopOrganize("Stopped: movement is outside the selected plan or its remaining quantity.");return;}
                    RefreshGearSetProtection();
                    if(OrganizerGearProtected(move.Source.Item)){
                        RememberOrganizerInventory(organizeActiveRetainer,OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray());
                        BeginStorageCalculation();break;
                    }
                    var src=ReadChestContainer(move.Source.Container).First(s=>s.Slot==move.Source.Slot);var dst=ReadChestContainer(move.Destination.Container).First(s=>s.Slot==move.Destination.Slot);
                    if(!OrganizerSlotMatches(move.Source,src)||!OrganizerSlotMatches(move.Destination,dst)){StopOrganize("Inventory changed after the space check. Stopped before moving another item; Check again.");return;}
                    if(organizeBagsOnly&&!InventoryTradeFilterAllows(src,organizeBagTradeFilter)){StopOrganize("Stopped: item no longer matches the selected tradability filter.");return;}
                    if(CrystalStorage.IsItem(src.Item)){
                        if(!organizePort.TransferCrystals(organizeActiveRetainer,move))break;
                        organizePending=move;organizerSubmittedAt=now;organizerLastTransfer=organizerSubmission=new {retainer=organizeActiveRetainer,action.Withdraw,move,command="Retainer crystal quantity",at=now};OrganizeStage(7);break;
                    }
                    organizePending=move;var submitted=chestMove!(manager,(InventoryType)src.Container,(ushort)src.Slot,(InventoryType)dst.Container,(ushort)dst.Slot,move.Quantity);organizerSubmittedAt=now;organizerLastTransfer=organizerSubmission=new {retainer=organizeActiveRetainer,action.Withdraw,action.Internal,move,nativeResult=submitted,at=now};OrganizeStage(7);break;
                case 7:
                    if(!RetrieverActive(organizeActiveRetainer)){StopOrganize("Stopped: selected retainer changed while a transfer was unconfirmed.");return;}
                    if(organizePort.Read(organizeActiveRetainer)==null)break;
                    if(!OrganizerConfirmed())break;
                    organizePort.MarkForSort(organizeActiveRetainer);
                    var done=organizeMoves[organizeMoveIndex];if(done.Internal){}else if(done.Withdraw)organizeCarry+=done.Move.Quantity;else{if(!organizeBagsOnly)organizeCarry-=done.Move.Quantity;organizeTransferred+=done.Move.Quantity;}
                    RememberOrganizerInventory(organizeActiveRetainer,OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray());
                    if(organizeBagsOnly&&!done.Withdraw&&!done.Internal)organizerEntrusted[done.Move.Source.Item]=organizerEntrusted.GetValueOrDefault(done.Move.Source.Item)+done.Move.Quantity;
                    ReportStorageTransfer(done.Internal?"Merged":done.Withdraw?"Withdrew":"Entrusted",done.Move.Source.Item,done.Move.Source.Hq,done.Move.Quantity,StorageRetainerName(organizeActiveRetainer));
                    organizePending=null;organizeMoveIndex++;
                    if(organizeMoveIndex<organizeMoves.Length&&organizeMoves[organizeMoveIndex].Retainer==organizeActiveRetainer)OrganizeStage(6);else OrganizeStage(8);break;
                case 8:
                    organizePort.SortAllowed=!organizeMoves.Skip(organizeMoveIndex).Any(m=>m.Retainer==organizeActiveRetainer);
                    if(!organizePort.Close(organizeActiveRetainer))break;
                    if(organizeMoveIndex==organizeMoves.Length){FinishOrganize(organizeUnmoved==0?$"Complete: {organizeTransferred} items delivered.":$"Partial: {organizeTransferred} items delivered · {organizeUnmoved} remain because no space was available.");return;}
                    organizeActiveRetainer=organizeMoves[organizeMoveIndex].Retainer;OrganizeStage(4);break;
            }
        }catch(Exception e){StopOrganize("Stopped: "+e.Message);}
    }
    private void BeginStorageCalculation()
    {
                    organizeInventories[StorageRouting.Bags]=OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).Where(s=>!organizeBagsOnly||InventoryTradeFilterAllows(s,organizeBagTradeFilter)).ToArray();
                    var snapshot=organizeOwnerOrder.Where(organizeInventories.ContainsKey).Append(StorageRouting.Bags).ToDictionary(id=>id,id=>organizeInventories[id].ToArray());var categories=snapshot.Values.SelectMany(x=>x).Where(x=>x.Item>0).Select(x=>x.Item).Distinct().ToDictionary(x=>x,OrganizeCategory);
                    if(organizeNoPlan)organizeActiveRules=PlanItemScope.FromRetainers(organizeActiveRules,snapshot,OrganizeCategory);
                    var rules=organizeActiveRules.Select(r=>r.Amounts==null?r:r with{Amounts=r.Amounts.ToDictionary(x=>x.Key,x=>Math.Max(0,x.Value-organizerEntrusted.GetValueOrDefault(x.Key)))}).ToArray();
                    var beforeAssignments=rules;
                    if(config.UseRetainerAssignments.Contains(Player.ContentId))rules=StorageAssignments.Apply(rules,(config.RetainerAssignments.GetValueOrDefault(Player.ContentId)??[]).ToArray(),snapshot,id=>categories.GetValueOrDefault(id),config.RetrievalPresets);
                    organizerUnassigned=organizeBagsOnly?beforeAssignments.Where(r=>r.To==StorageAutoRouting.AllRetainers).SelectMany(r=>r.Items).Distinct().Count(id=>snapshot[StorageRouting.Bags].Any(s=>s.Item==id&&s.Quantity>0)&&!rules.Any(r=>r.Items.Contains(id))):0;
                    RefreshGearSetProtection();
                    rules=ProtectGearSetRoutes(rules,snapshot);
                    var routingPlan=new StorageRoutingPlan("Current movements",Player.ContentId,rules,organizeReserve);var bagsOnly=organizeBagsOnly;
                    organizeRoutingTask=Task.Run(()=>StorageRouting.Build(routingPlan,snapshot,id=>categories.GetValueOrDefault(id),bagsOnly,bagsOnly));OrganizeStage(9);
    }
    private bool OrganizerConfirmed()
    {
        var move=organizePending!;
        var src=ReadChestContainer(move.Source.Container).First(s=>s.Slot==move.Source.Slot);
        var dst=ReadChestContainer(move.Destination.Container).First(s=>s.Slot==move.Destination.Slot);
        var now=DateTimeOffset.UtcNow;
        var state=OrganizerTransferRecovery.Check(move,src,dst,now-organizerSubmittedAt);
        var confirmed=state==OrganizerTransferState.Confirmed;
        organizerLastTransfer=new {retainer=organizeActiveRetainer,submission=organizerSubmission,move,sourceNow=src,destinationNow=dst,confirmed,state=state.ToString(),recentIssues=organizerTransferIssues.ToArray(),at=now};
        if(state==OrganizerTransferState.Changed){
            StopOrganize($"Stopped: item {move.Source.Item} changed without the expected transfer result. No retry sent.");return false;
        }
        if(state==OrganizerTransferState.Unchanged){
            organizerTransferIssues.Enqueue(new {submission=organizerSubmission,sourceNow=src,destinationNow=dst,state=state.ToString(),at=now});while(organizerTransferIssues.Count>32)organizerTransferIssues.Dequeue();
            organizerDeferredItems.Add((move.Source.Item,move.Source.Hq));organizerDeferred=organizerDeferredItems.Count;
            FollowChatNotice($"ORGANIZER — Item {move.Source.Item} did not move; deferred it and its remaining transfers. Continuing other items.");
            organizeMoves=OrganizerTransferRecovery.DeferItem(organizeMoves,organizeMoveIndex);
            organizePending=null;
            RememberOrganizerInventory(organizeActiveRetainer,OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray());
            if(organizeMoveIndex<organizeMoves.Length&&organizeMoves[organizeMoveIndex].Retainer==organizeActiveRetainer)BindOrganizerVisit();
            else OrganizeStage(8);
            return false;
        }
        if(confirmed){retainerCaptureStability.ConfirmTransfer(Player.ContentId,organizeActiveRetainer);}
        return confirmed;
    }
}



