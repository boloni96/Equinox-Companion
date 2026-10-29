using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong organizeFrom,organizeTo=StorageAutoRouting.AllRetainers;private int organizePreviewPage;
    private readonly HashSet<uint> organizeCategories=[];
    private StoragePreviewRow[] organizePreview=[];
    private bool organizeRunning;private int organizeStage,organizeSourceIndex,organizeTransferred;
    private ulong[] organizeSources=[],organizeOwnerOrder=[];private RetrieverPort? organizePort;
    private DateTimeOffset organizeNext,organizeDeadline;
    private ChestMove? organizePending;
    private object? organizerLastTransfer;
    private int organizeCarry;
    private string organizeStatus="Preview or organise the selected plan. Assignments choose homes for eligible listed items.";
    private void OrganizeStage(int stage){organizeStage=stage;organizeStatus=$"{organizeTransferred} delivered · {organizeMoveIndex}/{organizeMoves.Length} transfers · "+(stage switch{0=>"Preparing",1 or 2 or 3=>$"Refreshing missing inventory {organizeSourceIndex+1}/{organizeSources.Length}",4 or 5=>"Opening retainer",6 or 7=>"Moving batch",9=>"Calculating batch space",_=>"Next retainer"});organizeDeadline=DateTimeOffset.UtcNow.AddSeconds(60);}
    private void StopOrganize(string reason){organizeRunning=false;organizePort?.Release();organizePending=null;organizeStatus=reason+(organizeCarry>0?$" · {organizeCarry} transferred items remain in your bags.":"");SaveOrganizerInventories();InvalidateOrganizePreview();retrievalRefreshPending=true;Log.Information("Retainer organizer: {Status}",organizeStatus);}
    private uint OrganizeCategory(uint item)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(item)?.ItemUICategory.RowId??0;
    private void DrawRetainerOrganizer()
    {
        ImGui.BeginDisabled(organizeRunning||retrievalRun.Running||chestRunning);
        DrawPlanCategoryRules();
        DrawRetainerAssignments();
        var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
        void Choose(string label,ref ulong id,bool all){if(ImGui.BeginCombo(label,id==StorageAutoRouting.AllRetainers?"All retainers":id==0?(all?"All other retainers":"Choose retainer"):names.GetValueOrDefault(id,id.ToString()))){if(!all&&ImGui.Selectable("All retainers · consolidate duplicates",id==StorageAutoRouting.AllRetainers)){id=StorageAutoRouting.AllRetainers;InvalidateOrganizePreview();}if(all&&ImGui.Selectable("All other retainers",id==0)){id=0;InvalidateOrganizePreview();}foreach(var n in names)if(ImGui.Selectable(n.Value+"##"+n.Key,id==n.Key)){id=n.Key;InvalidateOrganizePreview();}ImGui.EndCombo();}}
        if(ImGui.BeginTable("MovementTargets",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);Choose("##From",ref organizeFrom,true);MarketHelp("Source retainer");
            ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);Choose("##To",ref organizeTo,false);ImGui.EndTable();
        }
        if(ImGui.IsItemHovered())ImGui.SetTooltip("All retainers consolidates duplicates from last to first in game order. Enable assignments to use your chosen category homes instead. Known inventories are planned before travel; relevant slots are verified on arrival.");
        if(ImGui.BeginTable("MovementFilters",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(DrawStorageCategoryPicker("##MovementCategories",organizeCategories,"All categories"))InvalidateOrganizePreview();
            ImGui.TableNextColumn();var q=(int)retrievalQuality;ImGui.SetNextItemWidth(-1);if(ImGui.Combo("##MovementQuality",ref q,"Any quality\0NQ only\0HQ only\0")){retrievalQuality=(RetrievalQuality)q;InvalidateOrganizePreview();retrievalPreviewDirty=true;}ImGui.EndTable();
        }
        if(ImGui.SmallButton($"Movements / options · {organizeRules.Count}"))ImGui.OpenPopup("MovementOptions");
        if(ImGui.BeginPopup("MovementOptions")){
            ImGui.SetNextItemWidth(100);if(ImGui.SliderInt("Leave bag slots free",ref organizeReserve,0,10))InvalidateOrganizePreview();
            MarketHelp("Reserve free bag slots for staging. One spare may temporarily unblock a full-retainer swap.");
            ImGui.BeginDisabled(organizeTo==0);if(ImGui.SmallButton("Add movement")){organizeRules.Add(CurrentOrganizeRule());InvalidateOrganizePreview();}ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton("Clear movements")){organizeRules.Clear();InvalidateOrganizePreview();}
            ImGui.BeginChild("MovementRules",new(420,180),false);
            foreach(var rule in organizeRules.ToArray()){ImGui.PushID(organizeRules.IndexOf(rule));if(ImGui.SmallButton("×")){organizeRules.Remove(rule);InvalidateOrganizePreview();}ImGui.SameLine();ImGui.TextWrapped($"{(rule.From==StorageRouting.AllSources?"All retainers":names.GetValueOrDefault(rule.From,"Retainer"))} → {(rule.To==StorageAutoRouting.AllRetainers?"All retainers":names.GetValueOrDefault(rule.To,"Retainer"))} · {(rule.AllItems?"all items":rule.Categories.Length+" categories")}");ImGui.PopID();}
            ImGui.EndChild();ImGui.EndPopup();
        }
        if(ImGui.Button("Preview selection"))RetrievalTry(CheckOrganize);ImGui.SameLine();
        ImGui.BeginDisabled(organizeTo==0&&organizeRules.Count==0||OrganizeRules().Length==0);if(ImGui.Button("Organise"))StartOrganize();ImGui.EndDisabled();
        ImGui.EndDisabled();
        if(organizeRunning&&ImGui.Button("Stop organizer"))StopOrganize("Stopped by you; an already-sent move may still complete.");
        if(organizeRunning||organizeMoves.Length>0)ImGui.TextWrapped(organizeStatus);
        if(retrievalError.Length>0)ImGui.TextWrapped(retrievalError);
        DrawOrganizePreview();
        windowStatus=organizeRunning?organizeStatus:retrievalError.Length>0?retrievalError:organizePreviewMessage;
    }
    private StorageRoute[] OrganizeRules()=>ScopeOrganizeRules(organizeRules.Count>0?organizeRules.ToArray():[CurrentOrganizeRule()]);
    private StorageRoute CurrentOrganizeRule()=>new(organizeFrom==0?StorageRouting.AllSources:organizeFrom,organizeTo,organizeCategories.ToArray(),[],AllItems:organizeCategories.Count==0,Quality:retrievalQuality);
    private readonly List<StorageRoute> organizeRules=[];
    private StorageRoute[] organizeActiveRules=[];private int organizeReserve=1;
    private bool organizeBagsOnly;private int organizeBagTradeFilter;private readonly Dictionary<ulong,ChestStack[]> organizeInventories=[];
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
    private unsafe void StartStorageRouting(StorageRoute[] rules,bool bagsOnly,bool temporarySelection=false,int temporaryTradeFilter=0)
    {
        RetrievalTry(()=>{
            if(craftingWithdrawalPending||organizeRunning||retrievalRun.Running||chestRunning||sellerRun.Running||TraderRunning||retainerGilRunning)throw new InvalidOperationException("Finish the current inventory operation first.");
            if(!Player.IsLoaded||!RetainerToolsContext())throw new InvalidOperationException("Open the summoning bell's retainer list first.");
            var owned=ReadSellerRetainers().Select(x=>x.Id).ToArray();
            if(rules.Any(r=>r.To==0||r.To!=StorageAutoRouting.AllRetainers&&!owned.Contains(r.To)||r.From!=0&&r.From!=StorageRouting.AllSources&&!owned.Contains(r.From)))throw new InvalidOperationException("Choose this character's retainers for every movement.");
            if(config.UseRetainerAssignments.Contains(Player.ContentId)&&rules.Any(r=>r.To==StorageAutoRouting.AllRetainers)&&!StorageAssignments.HasSelections(config.RetainerAssignments.GetValueOrDefault(Player.ContentId)??[],config.RetrievalPresets))throw new InvalidOperationException("Choose retainer assignments, or turn off Use assignments for regular duplicate organisation.");
            EnsureChestMove();if(temporarySelection&&!bagsOnly)throw new InvalidOperationException("Temporary selection is only valid for Entrust.");rules=temporarySelection?PlanItemScope.Restrict(rules,rules.SelectMany(r=>r.Items),OrganizeCategory):ScopeOrganizeRules(rules);if(rules.Length==0)throw new InvalidOperationException(OrganizeScopeMessage());organizeActiveRules=rules;organizeBagsOnly=bagsOnly;organizeBagTradeFilter=temporarySelection?Math.Clamp(temporaryTradeFilter,0,2):0;
            organizeSources=owned.Where(id=>rules.Any(r=>r.To==StorageAutoRouting.AllRetainers||r.To==id||!bagsOnly&&(r.From==StorageRouting.AllSources||r.From==id))).ToArray();
            organizeOwnerOrder=organizeSources.ToArray();var recorded=RecordedOrganizerInventories(organizeSources);
            organizePort=new(this){Character=Player.ContentId,World=Player.CurrentWorld.RowId,Quality=RetrievalQuality.Any};
            organizeCarry=0;organizeTransferred=0;organizeSourceIndex=0;organizePending=null;organizeMoveIndex=0;organizeMoves=[];organizeRoutingTask=null;organizeInventories.Clear();foreach(var row in recorded)organizeInventories[row.Key]=row.Value;
            organizeSources=organizeSources.Where(id=>!recorded.ContainsKey(id)).ToArray();
            organizerVisitCache.Clear();organizerReplans.Clear();organizerEntrusted.Clear();organizerProtectedBagSlots.Clear();foreach(var slot in OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).Where(x=>x.Item>0&&x.Quantity>0))organizerProtectedBagSlots.Add((slot.Container,slot.Slot));
            organizeRunning=true;OrganizeStage(0);
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
            if(now>organizeDeadline){StopOrganize("Stopped: timed out at stage "+organizeStage+". "+organizePort.InventoryStatus);return;}
            if(organizeStage==0){if(organizePort.Prepare()){if(organizeSources.Length==0)BeginStorageCalculation();else OrganizeStage(1);}return;}
            var pause=organizePort.Pause.Check(now);if(!pause.Ready){StopOrganize(pause.Error);return;}
            var manager=InventoryManager.Instance();if(manager==null)return;
            foreach(var op in manager->PendingOperations)if(!op.IsEmpty)return;
            switch(organizeStage){
                case 1:if(organizePort.Select(organizeSources[organizeSourceIndex]))OrganizeStage(2);break;
                case 2:if(organizePort.OpenInventory(organizeSources[organizeSourceIndex])&&organizePort.Read(organizeSources[organizeSourceIndex]) is not null){RememberOrganizerInventory(organizeSources[organizeSourceIndex],OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray());if(organizeSourceIndex==organizeSources.Length-1){organizeActiveRetainer=organizeSources[organizeSourceIndex];BeginStorageCalculation();}else OrganizeStage(3);}break;
                case 3:
                    if(!organizePort.Close(organizeSources[organizeSourceIndex]))break;
                    if(++organizeSourceIndex<organizeSources.Length){OrganizeStage(1);break;}
                    BeginStorageCalculation();break;
                case 9:
                    if(organizeRoutingTask?.IsCompleted!=true)break;var plan=organizeRoutingTask.GetAwaiter().GetResult();organizeRoutingTask=null;
                    organizeMoves=plan.Moves;organizeUnmoved=plan.Remaining;
                    if(organizeMoves.Length==0){StopOrganize(plan.Remaining==0?"Nothing needs moving.":$"No available route for {plan.Remaining} items. Check bag and retainer space.");return;}
                    organizeActiveRetainer=organizeMoves[0].Retainer;OrganizeStage(RetrieverActive(organizeActiveRetainer)?5:4);break;
                case 4:if(organizePort.Select(organizeActiveRetainer))OrganizeStage(5);break;
                case 5:if(organizePort.OpenInventory(organizeActiveRetainer)&&organizePort.Read(organizeActiveRetainer) is not null)BindOrganizerVisit();break;
                case 6:
                    var action=organizeMoves[organizeMoveIndex];var move=action.Move;
                    if(!PlanItemScope.Allows(action,organizeActiveRules,organizeBagsOnly,organizerEntrusted)){StopOrganize("Stopped: movement is outside the selected plan or its remaining quantity.");return;}
                    var src=ReadChestContainer(move.Source.Container).First(s=>s.Slot==move.Source.Slot);var dst=ReadChestContainer(move.Destination.Container).First(s=>s.Slot==move.Destination.Slot);
                    if(!OrganizerSlotMatches(move.Source,src)||!OrganizerSlotMatches(move.Destination,dst)){StopOrganize("Inventory changed after the space check. Stopped before moving another item; Check again.");return;}
                    if(organizeBagsOnly&&!InventoryTradeFilterAllows(src,organizeBagTradeFilter)){StopOrganize("Stopped: item no longer matches the selected tradability filter.");return;}
                    if(CrystalStorage.IsItem(src.Item)){
                        if(!organizePort.TransferCrystals(organizeActiveRetainer,move))break;
                        organizePending=move;organizerLastTransfer=new {retainer=organizeActiveRetainer,action.Withdraw,move,command="Retainer crystal quantity",at=now};OrganizeStage(7);break;
                    }
                    organizePending=move;var submitted=chestMove!(manager,(InventoryType)src.Container,(ushort)src.Slot,(InventoryType)dst.Container,(ushort)dst.Slot,move.Quantity);organizerLastTransfer=new {retainer=organizeActiveRetainer,action.Withdraw,action.Internal,move,nativeResult=submitted,at=now};OrganizeStage(7);break;
                case 7:
                    if(!OrganizerConfirmed())break;
                    var done=organizeMoves[organizeMoveIndex];if(done.Internal){}else if(done.Withdraw)organizeCarry+=done.Move.Quantity;else{if(!organizeBagsOnly)organizeCarry-=done.Move.Quantity;organizeTransferred+=done.Move.Quantity;}
                    RememberOrganizerInventory(organizeActiveRetainer,OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray());
                    if(organizeBagsOnly&&!done.Withdraw&&!done.Internal)organizerEntrusted[done.Move.Source.Item]=organizerEntrusted.GetValueOrDefault(done.Move.Source.Item)+done.Move.Quantity;
                    ReportStorageTransfer(done.Internal?"Merged":done.Withdraw?"Withdrew":"Entrusted",done.Move.Source.Item,done.Move.Source.Hq,done.Move.Quantity,StorageRetainerName(organizeActiveRetainer));
                    organizePending=null;organizeMoveIndex++;
                    if(organizeMoveIndex<organizeMoves.Length&&organizeMoves[organizeMoveIndex].Retainer==organizeActiveRetainer)OrganizeStage(6);else OrganizeStage(8);break;
                case 8:
                    if(!organizePort.Close(organizeActiveRetainer))break;
                    if(organizeMoveIndex==organizeMoves.Length){StopOrganize(organizeUnmoved==0?$"Complete: {organizeTransferred} items delivered.":$"Partial: {organizeTransferred} items delivered · {organizeUnmoved} remain because no space was available.");return;}
                    organizeActiveRetainer=organizeMoves[organizeMoveIndex].Retainer;OrganizeStage(4);break;
            }
        }catch(Exception e){StopOrganize("Stopped: "+e.Message);}
    }
    private void BeginStorageCalculation()
    {
                    organizeInventories[StorageRouting.Bags]=OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).Where(s=>!organizeBagsOnly||InventoryTradeFilterAllows(s,organizeBagTradeFilter)).ToArray();
                    var snapshot=organizeOwnerOrder.Where(organizeInventories.ContainsKey).Append(StorageRouting.Bags).ToDictionary(id=>id,id=>organizeInventories[id].ToArray());var categories=snapshot.Values.SelectMany(x=>x).Where(x=>x.Item>0).Select(x=>x.Item).Distinct().ToDictionary(x=>x,OrganizeCategory);
                    var rules=organizeActiveRules.Select(r=>r.Amounts==null?r:r with{Amounts=r.Amounts.ToDictionary(x=>x.Key,x=>Math.Max(0,x.Value-organizerEntrusted.GetValueOrDefault(x.Key)))}).ToArray();
                    if(config.UseRetainerAssignments.Contains(Player.ContentId))rules=StorageAssignments.Apply(rules,(config.RetainerAssignments.GetValueOrDefault(Player.ContentId)??[]).ToArray(),snapshot,id=>categories.GetValueOrDefault(id),config.RetrievalPresets);
                    var routingPlan=new StorageRoutingPlan("Current movements",Player.ContentId,rules,organizeReserve);var bagsOnly=organizeBagsOnly;
                    organizeRoutingTask=Task.Run(()=>StorageRouting.Build(routingPlan,snapshot,id=>categories.GetValueOrDefault(id),bagsOnly,bagsOnly));OrganizeStage(9);
    }
    private bool OrganizerConfirmed()
    {
        var move=organizePending!;
        var src=ReadChestContainer(move.Source.Container).First(s=>s.Slot==move.Source.Slot);
        var dst=ReadChestContainer(move.Destination.Container).First(s=>s.Slot==move.Destination.Slot);
        var confirmed=ChestTransferPlanner.Confirmed(move,src,dst);
        organizerLastTransfer=new {retainer=organizeActiveRetainer,move,sourceNow=src,destinationNow=dst,confirmed,at=DateTimeOffset.UtcNow};
        return confirmed;
    }
}


