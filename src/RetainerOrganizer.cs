using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong organizeFrom,organizeTo;private int organizePreviewPage;
    private readonly HashSet<uint> organizeCategories=[];
    private RetrieverStock[] organizePreview=[];
    private bool organizeRunning;private int organizeStage,organizeSourceIndex,organizeTransferred;
    private ulong[] organizeSources=[];private RetrieverPort? organizePort;
    private DateTimeOffset organizeNext,organizeDeadline;
    private ChestMove? organizePending;
    private int organizeCarry;
    private string organizeStatus="Choose source, destination and categories, then Check.";
    private void OrganizeStage(int stage){organizeStage=stage;organizeStatus=$"{organizeTransferred} delivered · {organizeMoveIndex}/{organizeMoves.Length} transfers · "+(stage switch{0=>"Preparing",1 or 2 or 3=>"Checking retainer space",4 or 5=>"Opening retainer",6 or 7=>"Moving batch",9=>"Calculating batch space",_=>"Next retainer"});organizeDeadline=DateTimeOffset.UtcNow.AddSeconds(60);}
    private void StopOrganize(string reason){organizeRunning=false;organizePort?.Release();organizePending=null;organizeStatus=reason+(organizeCarry>0?$" · {organizeCarry} transferred items remain in your bags.":"");retrievalRefreshPending=true;Log.Information("Retainer organizer: {Status}",organizeStatus);}
    private uint OrganizeCategory(uint item)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(item)?.ItemUICategory.RowId??0;
    private void CheckOrganize()
    {
        RefreshRetrievalStock();
        var rules=OrganizeRules();
        organizePreview=retrievalStock.Where(x=>rules.Any(r=>r.To!=x.Retainer&&(r.From==StorageRouting.AllSources||r.From==x.Retainer)&&(r.AllItems||r.Categories.Contains(OrganizeCategory(x.Item)))&&RetainerRetrieval.AllowsQuality(x.Hq,r.Quality))).ToArray();
        organizePreview=organizePreview.OrderBy(x=>x.Name).ThenBy(x=>x.Holder).ToArray();organizePreviewPage=0;
        var free=OrganizerInventory(false,false).Count(x=>x.Item==0);
        organizeStatus=$"{free} free bag slots · {organizePreview.Length} item/quality groups · recorded inventory; quantities and space are rechecked in game.";
    }
    private void DrawRetainerOrganizer()
    {
        ImGui.BeginDisabled(organizeRunning||retrievalRun.Running||chestRunning);
        var names=retrievalKnownRetainers.GetValueOrDefault(Player.ContentId)??new Dictionary<ulong,string>();
        void Choose(string label,ref ulong id,bool all){if(ImGui.BeginCombo(label,id==0?(all?"All other retainers":"Choose retainer"):names.GetValueOrDefault(id,id.ToString()))){if(all&&ImGui.Selectable("All other retainers",id==0)){id=0;organizePreview=[];}foreach(var n in names)if(ImGui.Selectable(n.Value+"##"+n.Key,id==n.Key)){id=n.Key;organizePreview=[];}ImGui.EndCombo();}}
        Choose("From",ref organizeFrom,true);Choose("To",ref organizeTo,false);
        if(ImGui.SmallButton("All items")){organizeCategories.Clear();organizePreview=[];}ImGui.SameLine();
        if(ImGui.SmallButton("Materia")){organizeCategories.Clear();organizeCategories.Add(58);organizePreview=[];}ImGui.SameLine();
        if(ImGui.SmallButton("Housing")){organizeCategories.Clear();foreach(var id in Enumerable.Range(64,17).Select(x=>(uint)x).Concat(new uint[]{56,57,94,95}))organizeCategories.Add(id);organizePreview=[];}ImGui.SameLine();
        if(ImGui.SmallButton("Gardening")){organizeCategories.Clear();organizeCategories.Add(82);organizePreview=[];}
        if(ImGui.BeginCombo("Categories",organizeCategories.Count==0?"All item categories":organizeCategories.Count+" selected")){
            foreach(var cat in DataManager.GetExcelSheet<Lumina.Excel.Sheets.ItemUICategory>().Where(c=>c.RowId>0&&!string.IsNullOrWhiteSpace(c.Name.ToString()))){var enabled=organizeCategories.Contains(cat.RowId);if(ImGui.Checkbox(cat.Name.ToString()+"##"+cat.RowId,ref enabled)){if(enabled)organizeCategories.Add(cat.RowId);else organizeCategories.Remove(cat.RowId);organizePreview=[];}}
            ImGui.EndCombo();}
        var q=(int)retrievalQuality;ImGui.SetNextItemWidth(140);if(ImGui.Combo("Quality",ref q,"Any quality\0NQ only\0HQ only\0")){retrievalQuality=(RetrievalQuality)q;organizePreview=[];retrievalPreviewDirty=true;}
        ImGui.SetNextItemWidth(90);ImGui.SliderInt("Leave bag slots free",ref organizeReserve,0,10);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Fill available space per trip. One spare slot helps rearrange full retainers; it may be used temporarily to unblock a swap.");
        ImGui.BeginDisabled(organizeTo==0);if(ImGui.SmallButton("Add movement")){organizeRules.Add(CurrentOrganizeRule());organizePreview=[];}ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton("Clear movements")){organizeRules.Clear();organizePreview=[];}
        foreach(var rule in organizeRules.ToArray()){ImGui.PushID(organizeRules.IndexOf(rule));if(ImGui.SmallButton("×")){organizeRules.Remove(rule);organizePreview=[];}ImGui.SameLine();ImGui.TextUnformatted($"{(rule.AllItems?"All items":string.Join(", ",rule.Categories.Select(c=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.ItemUICategory>().GetRowOrDefault(c)?.Name.ToString()??c.ToString())))} · {(rule.From==StorageRouting.AllSources?"All retainers":names.GetValueOrDefault(rule.From,"Retainer"))} → {names.GetValueOrDefault(rule.To,"Retainer")}");ImGui.PopID();}
        if(ImGui.Button("Check"))RetrievalTry(CheckOrganize);ImGui.SameLine();
        ImGui.BeginDisabled(organizeTo==0&&organizeRules.Count==0);if(ImGui.Button("Organise"))StartOrganize();ImGui.EndDisabled();
        ImGui.EndDisabled();
        if(organizeRunning&&ImGui.Button("Stop organizer"))StopOrganize("Stopped by you; an already-sent move may still complete.");
        ImGui.TextWrapped(organizeStatus);
        var pages=Math.Max(1,(organizePreview.Length+59)/60);organizePreviewPage=Math.Clamp(organizePreviewPage,0,pages-1);
        ImGui.BeginDisabled(organizePreviewPage==0);if(ImGui.SmallButton("Previous page"))organizePreviewPage--;ImGui.EndDisabled();ImGui.SameLine();ImGui.TextUnformatted($"{organizePreviewPage+1}/{pages}");ImGui.SameLine();ImGui.BeginDisabled(organizePreviewPage+1>=pages);if(ImGui.SmallButton("Next page"))organizePreviewPage++;ImGui.EndDisabled();
        if(ImGui.BeginChild("OrganizerPreview",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-10)),true))foreach(var row in organizePreview.Skip(organizePreviewPage*60).Take(60)){DrawRetrievalIcon(row.Item);ImGui.TextUnformatted($"{row.Name}{(row.Hq?" HQ":"")} · {row.Quantity} · {row.Holder} → {names.GetValueOrDefault(OrganizeRules().FirstOrDefault(r=>(r.From==StorageRouting.AllSources||r.From==row.Retainer)&&(r.AllItems||r.Categories.Contains(OrganizeCategory(row.Item)))&&RetainerRetrieval.AllowsQuality(row.Hq,r.Quality))?.To??organizeTo,"Choose destination")}");}ImGui.EndChild();
    }
    private StorageRoute[] OrganizeRules()=>organizeRules.Count>0?organizeRules.ToArray():[CurrentOrganizeRule()];
    private StorageRoute CurrentOrganizeRule()=>new(organizeFrom==0?StorageRouting.AllSources:organizeFrom,organizeTo,organizeCategories.ToArray(),[],AllItems:organizeCategories.Count==0,Quality:retrievalQuality);
    private readonly List<StorageRoute> organizeRules=[];
    private StorageRoute[] organizeActiveRules=[];private int organizeReserve=1;
    private bool organizeBagsOnly;private readonly Dictionary<ulong,ChestStack[]> organizeInventories=[];
    private Task<RoutingResult>? organizeRoutingTask;private RoutedMove[] organizeMoves=[];private int organizeMoveIndex,organizeUnmoved;
    private ulong organizeActiveRetainer;
    private unsafe void StartOrganize()=>StartStorageRouting(OrganizeRules(),false);
    private unsafe void SendRetrievalList(bool duplicates)
    {
        RetrievalTry(()=>{
            if(retrievalSendTo==0)throw new InvalidOperationException("Choose a destination retainer for this plan first.");
            if(duplicates){StartStorageRouting([new(StorageRouting.Bags,retrievalSendTo,[],[],Duplicates:true,Quality:retrievalQuality)],true);return;}
            PreviewRetrieval(false,false);if(retrievalPlan.Length==0&&retrievalSendCategories.Count==0)throw new InvalidOperationException("Choose items or send categories first.");
            var rules=new List<StorageRoute>();
            if(retrievalPlan.Length>0)rules.Add(new(StorageRouting.Bags,retrievalSendTo,[],retrievalPlan.Select(x=>x.ItemId).ToArray(),Quality:retrievalQuality,Amounts:retrievalPlan.ToDictionary(x=>x.ItemId,x=>x.Quantity)));
            if(retrievalSendCategories.Count>0)rules.Add(new(StorageRouting.Bags,retrievalSendTo,retrievalSendCategories.ToArray(),[],Quality:retrievalQuality));
            StartStorageRouting(rules.ToArray(),true);
        });
    }
    private unsafe void StartStorageRouting(StorageRoute[] rules,bool bagsOnly)
    {
        RetrievalTry(()=>{
            if(organizeRunning||retrievalRun.Running||chestRunning||sellerRun.Running||gilTrade.Running)throw new InvalidOperationException("Finish the current inventory operation first.");
            if(!Player.IsLoaded||SellerAddon("RetainerList")==null)throw new InvalidOperationException("Open the summoning bell's retainer list first.");
            var owned=ReadSellerRetainers().Select(x=>x.Id).ToArray();
            if(rules.Any(r=>r.To==0||!owned.Contains(r.To)||r.From!=0&&r.From!=StorageRouting.AllSources&&!owned.Contains(r.From)))throw new InvalidOperationException("Choose this character's retainers for every movement.");
            EnsureChestMove();organizeActiveRules=rules;organizeBagsOnly=bagsOnly;
            organizeSources=owned.Where(id=>rules.Any(r=>r.To==id||!bagsOnly&&(r.From==StorageRouting.AllSources||r.From==id))).ToArray();
            organizePort=new(this){Character=Player.ContentId,World=Player.CurrentWorld.RowId,Quality=RetrievalQuality.Any};
            organizeCarry=0;organizeTransferred=0;organizeSourceIndex=0;organizePending=null;organizeMoveIndex=0;organizeMoves=[];organizeRoutingTask=null;organizeInventories.Clear();organizeRunning=true;OrganizeStage(0);
        });
    }
    private unsafe ChestStack[] OrganizerInventory(bool retainer,bool crystals)=>
        (crystals?new uint[]{retainer?12001u:2001u}:retainer?Enumerable.Range(10000,7).Select(x=>(uint)x).ToArray():new uint[]{0,1,2,3}).SelectMany(ReadChestContainer).ToArray();
    private static bool OrganizerSlotMatches(ChestStack expected,ChestStack actual)=>expected.Item==actual.Item&&expected.Quantity==actual.Quantity&&(expected.Item==0||expected.Hq==actual.Hq);
    private unsafe void TickOrganize(DateTimeOffset now)
    {
        if(organizePort?.Pause.RestorePending==true&&!organizeRunning)organizePort.Pause.Release(now);
        if(!organizeRunning||now<organizeNext)return;organizeNext=now.AddMilliseconds(250);
        try{
            if(organizePort==null||!organizePort.ContextMatches||chestRunning||retrievalRun.Running){StopOrganize("Stopped: character, bell or active task changed.");return;}
            if(now>organizeDeadline){StopOrganize("Stopped: timed out waiting for the game. No automatic retry.");return;}
            if(organizeStage==0){if(organizePort.Prepare())OrganizeStage(1);return;}
            var pause=organizePort.Pause.Check(now);if(!pause.Ready){StopOrganize(pause.Error);return;}
            var manager=InventoryManager.Instance();if(manager==null)return;
            foreach(var op in manager->PendingOperations)if(!op.IsEmpty)return;
            switch(organizeStage){
                case 1:if(organizePort.Select(organizeSources[organizeSourceIndex]))OrganizeStage(2);break;
                case 2:if(organizePort.OpenInventory(organizeSources[organizeSourceIndex])){organizeInventories[organizeSources[organizeSourceIndex]]=OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray();OrganizeStage(3);}break;
                case 3:
                    if(!organizePort.Close(organizeSources[organizeSourceIndex]))break;
                    if(++organizeSourceIndex<organizeSources.Length){OrganizeStage(1);break;}
                    organizeInventories[StorageRouting.Bags]=OrganizerInventory(false,false).Concat(OrganizerInventory(false,true)).ToArray();
                    var snapshot=organizeInventories.ToDictionary(x=>x.Key,x=>x.Value.ToArray());var categories=snapshot.Values.SelectMany(x=>x).Where(x=>x.Item>0).Select(x=>x.Item).Distinct().ToDictionary(x=>x,OrganizeCategory);
                    var routingPlan=new StorageRoutingPlan("Current movements",Player.ContentId,organizeActiveRules,organizeReserve);var bagsOnly=organizeBagsOnly;
                    organizeRoutingTask=Task.Run(()=>StorageRouting.Build(routingPlan,snapshot,id=>categories.GetValueOrDefault(id),bagsOnly,bagsOnly));OrganizeStage(9);break;
                case 9:
                    if(organizeRoutingTask?.IsCompleted!=true)break;var plan=organizeRoutingTask.GetAwaiter().GetResult();organizeRoutingTask=null;
                    organizeMoves=plan.Moves;organizeUnmoved=plan.Remaining;
                    if(organizeMoves.Length==0){StopOrganize(plan.Remaining==0?"Nothing needs moving.":$"No available route for {plan.Remaining} items. Check bag and retainer space.");return;}
                    organizeActiveRetainer=organizeMoves[0].Retainer;OrganizeStage(4);break;
                case 4:if(organizePort.Select(organizeActiveRetainer))OrganizeStage(5);break;
                case 5:if(organizePort.OpenInventory(organizeActiveRetainer))OrganizeStage(6);break;
                case 6:
                    var action=organizeMoves[organizeMoveIndex];var move=action.Move;
                    var src=ReadChestContainer(move.Source.Container).First(s=>s.Slot==move.Source.Slot);var dst=ReadChestContainer(move.Destination.Container).First(s=>s.Slot==move.Destination.Slot);
                    if(!OrganizerSlotMatches(move.Source,src)||!OrganizerSlotMatches(move.Destination,dst)){StopOrganize("Inventory changed after the space check. Stopped before moving another item; Check again.");return;}
                    organizePending=move;chestMove!(manager,(InventoryType)src.Container,(ushort)src.Slot,(InventoryType)dst.Container,(ushort)dst.Slot,move.Quantity);OrganizeStage(7);break;
                case 7:
                    if(!OrganizerConfirmed())break;
                    var done=organizeMoves[organizeMoveIndex];if(done.Internal){}else if(done.Withdraw)organizeCarry+=done.Move.Quantity;else{if(!organizeBagsOnly)organizeCarry-=done.Move.Quantity;organizeTransferred+=done.Move.Quantity;}
                    organizePending=null;organizeMoveIndex++;
                    if(organizeMoveIndex<organizeMoves.Length&&organizeMoves[organizeMoveIndex].Retainer==organizeActiveRetainer)OrganizeStage(6);else OrganizeStage(8);break;
                case 8:
                    if(!organizePort.Close(organizeActiveRetainer))break;
                    if(organizeMoveIndex==organizeMoves.Length){StopOrganize(organizeUnmoved==0?$"Complete: {organizeTransferred} items delivered.":$"Partial: {organizeTransferred} items delivered · {organizeUnmoved} remain because no space was available.");return;}
                    organizeActiveRetainer=organizeMoves[organizeMoveIndex].Retainer;OrganizeStage(4);break;
            }
        }catch(Exception e){StopOrganize("Stopped: "+e.Message);}
    }
    private bool OrganizerConfirmed()
    {
        var move=organizePending!;
        var src=ReadChestContainer(move.Source.Container).First(s=>s.Slot==move.Source.Slot);
        var dst=ReadChestContainer(move.Destination.Container).First(s=>s.Slot==move.Destination.Slot);
        return ChestTransferPlanner.Confirmed(move,src,dst);
    }
}
