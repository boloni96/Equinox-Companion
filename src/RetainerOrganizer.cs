using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong organizeFrom,organizeTo;
    private readonly HashSet<uint> organizeCategories=[];
    private RetrieverStock[] organizePreview=[];
    private bool organizeRunning;private int organizeStage,organizeSourceIndex,organizeTransferred;
    private ulong[] organizeSources=[];private RetrieverPort? organizePort;
    private DateTimeOffset organizeNext,organizeDeadline;
    private ChestStack[] organizeDestination=[];private ChestMove? organizePending;
    private Dictionary<(ulong,uint,bool),int> organizeRemaining=[];
    private uint organizeItem;private bool organizeHq;private int organizeCarry;
    private string organizeStatus="Choose source, destination and categories, then Check.";
    private void OrganizeStage(int stage){organizeStage=stage;organizeStatus=$"{organizeTransferred} items moved · "+(stage switch{0=>"Preparing",1 or 2=>"Checking destination capacity",3 or 7 or 11 or 12=>"Closing retainer",4 or 5=>"Collecting from source",6=>"Confirming collection",8 or 9=>"Depositing in destination",_=>"Confirming deposit"});organizeDeadline=DateTimeOffset.UtcNow.AddSeconds(60);}
    private void StopOrganize(string reason){organizeRunning=false;organizePort?.Release();organizePending=null;organizeStatus=reason+(organizeCarry>0?$" · {organizeCarry} transferred items remain in your bags.":"");retrievalRefreshPending=true;Log.Information("Retainer organizer: {Status}",organizeStatus);}
    private uint OrganizeCategory(uint item)=>DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(item)?.ItemUICategory.RowId??0;
    private void CheckOrganize()
    {
        RefreshRetrievalStock();
        organizePreview=retrievalStock.Where(x=>x.Retainer!=organizeTo&&(organizeFrom==0||x.Retainer==organizeFrom)&&(organizeCategories.Count==0||organizeCategories.Contains(OrganizeCategory(x.Item)))&&RetainerRetrieval.AllowsQuality(x.Hq,retrievalQuality)).ToArray();
        organizeStatus=$"{organizePreview.Length} item/quality groups · recorded inventory; quantities and space are rechecked in game.";
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
        if(ImGui.Button("Check"))RetrievalTry(CheckOrganize);ImGui.SameLine();
        ImGui.BeginDisabled(organizePreview.Length==0||organizeTo==0);if(ImGui.Button("Move preview to retainer"))StartOrganize();ImGui.EndDisabled();
        ImGui.EndDisabled();
        if(organizeRunning&&ImGui.Button("Stop organizer"))StopOrganize("Stopped by you; an already-sent move may still complete.");
        ImGui.TextWrapped(organizeStatus);
        if(ImGui.BeginChild("OrganizerPreview",new(0,Math.Max(80,ImGui.GetContentRegionAvail().Y-10)),true))foreach(var row in organizePreview){DrawRetrievalIcon(row.Item);ImGui.TextUnformatted($"{row.Name}{(row.Hq?" HQ":"")} · {row.Quantity} · {row.Holder} → {names.GetValueOrDefault(organizeTo,"Choose destination")}");}ImGui.EndChild();
    }
    private unsafe void StartOrganize()
    {
        RetrievalTry(()=>{
            if(organizeRunning||retrievalRun.Running||chestRunning||sellerRun.Running||gilTrade.Running)throw new InvalidOperationException("Finish the current inventory operation first.");
            if(!Player.IsLoaded||SellerAddon("RetainerList")==null)throw new InvalidOperationException("Open the summoning bell's retainer list first.");
            var owned=ReadSellerRetainers().Select(x=>x.Id).ToHashSet();
            if(!owned.Contains(organizeTo)||organizePreview.Any(x=>!owned.Contains(x.Retainer)||x.Retainer==organizeTo))throw new InvalidOperationException("Check the list again using this character's retainers.");
            EnsureChestMove();
            organizeRemaining=organizePreview.GroupBy(x=>(x.Retainer,x.Item,x.Hq)).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
            organizeSources=organizePreview.Select(x=>x.Retainer).Distinct().ToArray();if(organizeSources.Length==0)throw new InvalidOperationException("No items in preview.");
            organizePort=new(this){Character=Player.ContentId,World=Player.CurrentWorld.RowId,Quality=RetrievalQuality.Any};
            organizeCarry=0;organizeTransferred=0;organizeSourceIndex=0;organizePending=null;organizeRunning=true;OrganizeStage(0);organizeStatus="Preparing organizer…";
        });
    }
    private unsafe ChestStack[] OrganizerInventory(bool retainer,bool crystals)=>
        (crystals?new uint[]{retainer?12001u:2001u}:retainer?Enumerable.Range(10000,7).Select(x=>(uint)x).ToArray():new uint[]{0,1,2,3}).SelectMany(ReadChestContainer).ToArray();
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
            var source=organizeSources[organizeSourceIndex];
            switch(organizeStage){
                case 1:if(organizePort.Select(organizeTo))OrganizeStage(2);break;
                case 2:if(organizePort.OpenInventory(organizeTo)){organizeDestination=OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray();OrganizeStage(3);}break;
                case 3:if(organizePort.Close(organizeTo))OrganizeStage(4);break;
                case 4:if(organizePort.Select(source))OrganizeStage(5);break;
                case 5:
                    if(!organizePort.OpenInventory(source))break;
                    var stock=OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray();
                    ChestMove? pickup=null;
                    foreach(var s in stock.Where(s=>organizeRemaining.GetValueOrDefault((source,s.Item,s.Hq))>0&&s.Quantity>0)){
                        var room=organizeDestination.Where(d=>((s.Item is >=2 and <=19)==(d.Container==12001))).Sum(d=>d.Item==0?s.Limit:d.Item==s.Item&&d.Hq==s.Hq?Math.Max(0,d.Limit-d.Quantity):0);
                        if(room<=0)continue;
                        var amount=Math.Min(s.Quantity,Math.Min(room,organizeRemaining[(source,s.Item,s.Hq)]));
                        pickup=ChestTransferPlanner.Transfer([s with{Quantity=amount}],OrganizerInventory(false,s.Item is >=2 and <=19),new HashSet<uint>{s.Item},s.Hq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly);
                        if(pickup!=null){pickup=pickup with{Source=s};break;}
                    }
                    if(pickup==null){OrganizeStage(12);break;}
                    organizeItem=pickup.Source.Item;organizeHq=pickup.Source.Hq;organizePending=pickup;
                    chestMove!(manager,(InventoryType)pickup.Source.Container,(ushort)pickup.Source.Slot,(InventoryType)pickup.Destination.Container,(ushort)pickup.Destination.Slot,pickup.Quantity);
                    OrganizeStage(6);break;
                case 6:
                    if(!OrganizerConfirmed())break;
                    organizeCarry=organizePending!.Quantity;organizePending=null;OrganizeStage(7);break;
                case 7:if(organizePort.Close(source))OrganizeStage(8);break;
                case 8:if(organizePort.Select(organizeTo))OrganizeStage(9);break;
                case 9:
                    if(!organizePort.OpenInventory(organizeTo))break;
                    var crystals=organizeItem is >=2 and <=19;
                    var targets=OrganizerInventory(true,crystals);var bags=OrganizerInventory(false,crystals);
                    var bag=bags.FirstOrDefault(s=>s.Item==organizeItem&&s.Hq==organizeHq&&s.Quantity>0);
                    if(bag==null){StopOrganize("Stopped: collected items changed in your bags.");return;}
                    var deposit=ChestTransferPlanner.Transfer([bag with{Quantity=Math.Min(bag.Quantity,organizeCarry)}],targets,new HashSet<uint>{organizeItem},organizeHq?RetrievalQuality.HqOnly:RetrievalQuality.NqOnly);
                    if(deposit==null){StopOrganize("Destination retainer is full.");return;}
                    organizePending=deposit with{Source=bag};
                    chestMove!(manager,(InventoryType)bag.Container,(ushort)bag.Slot,(InventoryType)deposit.Destination.Container,(ushort)deposit.Destination.Slot,deposit.Quantity);OrganizeStage(10);break;
                case 10:
                    if(!OrganizerConfirmed())break;
                    var moved=organizePending!.Quantity;organizeCarry-=moved;organizeTransferred+=moved;organizeRemaining[(source,organizeItem,organizeHq)]-=moved;organizePending=null;
                    organizeStatus=$"{organizeTransferred} items deposited in destination retainer";
                    if(organizeCarry>0)OrganizeStage(9);else{organizeDestination=OrganizerInventory(true,false).Concat(OrganizerInventory(true,true)).ToArray();OrganizeStage(11);}break;
                case 11:if(organizePort.Close(organizeTo))OrganizeStage(4);break;
                case 12:
                    if(!organizePort.Close(source))break;
                    if(++organizeSourceIndex<organizeSources.Length)OrganizeStage(4);
                    else {var remaining=organizeRemaining.Values.Sum();StopOrganize(remaining==0?$"Complete: {organizeTransferred} items organised.":$"Partial: {organizeTransferred} items moved · {remaining} preview items not moved (stock changed or bag/destination space unavailable). Free space and Check again.");}break;
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
