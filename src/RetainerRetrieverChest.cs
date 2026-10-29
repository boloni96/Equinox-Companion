using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ChestToolsWindow chestToolsWindow=null!;
    private int chestTab,chestDestination=1,chestKeep,chestMode,chestMoves,chestUnmoved;
    private bool chestRunning,chestWasOpen,chestDock=true;
    private ulong chestOwner,chestCompany;private uint chestWorld;
    private uint[] chestPages=[];private int chestPageIndex;
    private DateTimeOffset chestNext,chestDeadline;private ChestMove? chestPending;
    private Dictionary<uint,int> chestGoals=[];private RetrievalQuality chestQuality;
    private string chestStatus="Open the FC chest to use these tools.";
    private unsafe delegate int ChestQuantityMove(InventoryManager* manager,InventoryType source,ushort sourceSlot,InventoryType destination,ushort destinationSlot,int quantity);
    private ChestQuantityMove? chestMove;
    private sealed class ChestToolsWindow(Plugin p):Window("##EquinoxChestToolsBar",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing)
    {
        public override void Draw()=>p.DrawChestToolbar();
        public override unsafe void PreDraw(){
            RespectCloseHotkey=false;AllowPinning=false;
            var addon=SellerAddon("FreeCompanyChest");
            if(p.chestDock&&addon!=null){var height=ImGui.GetFrameHeight()+8;var y=addon->Y-height-4;Position=new Vector2(Math.Max(0,(int)addon->X),y>=0?y:addon->Y+addon->GetScaledHeight(true)+4);PositionCondition=ImGuiCond.Always;}else Position=null;
            PushRetainerToolbarStyle();
        }
        public override void PostDraw(){ImGui.PopStyleVar(2);ImGui.PopStyleColor(7);}
    }
    private unsafe int ChestAccess(uint container)
    {
        var addon=SellerAddon("FreeCompanyChest");if(addon==null)return 0;
        // Native chest access: two bits per item page; 0 none, 1 view, 2 deposit, 3 full.
        if(container is >=20000 and <=20004)return (int)((*(uint*)((byte*)addon+0x4DC)>>(2*(int)(container-20000)))&3);
        return container==22001?(int)*(uint*)((byte*)addon+0x4E0):0;
    }
    private void StopChest(string reason){chestRunning=false;chestPending=null;chestStatus=reason;retrievalPreviewDirty=true;Log.Information("FC chest: {Status}",reason);}
    private unsafe ChestStack[] ReadChestContainer(uint type)
    {
        var manager=InventoryManager.Instance();if(manager==null)throw new InvalidOperationException("Inventory unavailable.");
        var c=manager->GetInventoryContainer((InventoryType)type);if(c==null||!c->IsLoaded||c->Items==null||c->Size is <=0 or >200)throw new InvalidOperationException("Waiting for chest inventory. Open the selected tab and try again.");
        var rows=new List<ChestStack>();for(var i=0;i<c->Size;i++){
            var s=c->Items[i];var item=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(s.ItemId);
            rows.Add(CrystalStorage.Normalize(new(type,i,s.ItemId,s.Quantity,(s.Flags&InventoryItem.ItemFlags.HighQuality)!=0,item.HasValue?(int)item.Value.StackSize:0,item.HasValue&&!item.Value.IsUntradable&&s.SpiritbondOrCollectability==0)));
        }return rows.ToArray();
    }
    private unsafe void BeginChestPage(DateTimeOffset now)
    {
        var page=chestPages[chestPageIndex];var addon=SellerAddon("FreeCompanyChest");if(addon==null)throw new InvalidOperationException("Chest closed.");
        var values=stackalloc AtkValue[2];values[0].Type=AtkValueType.Int;values[0].Int=0;values[1].Type=AtkValueType.Int;values[1].Int=page==22001?5:(int)page-20000;
        addon->FireCallback(2,values);GameMain.ExecuteCommand(404,(int)page);
        if(chestMode==3)GameMain.ExecuteCommand(404,19999+chestDestination);
        chestNext=now.AddSeconds(2);chestDeadline=now.AddSeconds(15);
        chestStatus=$"Reading {(page==22001?"crystals":"chest tab "+(page-19999))}…";
    }
    private void EnsureChestMove()
    {
            if(chestMove==null){
                // Native quantity-transfer entry point; signature failure disables movement.
                if(!QuickLootScanner.TryScanText("48 89 5C 24 10 48 89 6C 24 18 56 57 41 55 41 56 41 57 48 83 EC 30 8D BA 60 F0 FF FF 45 0F BF E8 8D 82 FC EF FF FF 41 8B E9 44 8B FA 4C 8B F1",out var address))throw new InvalidOperationException("Chest transfer unavailable for this game version.");
                chestMove=Marshal.GetDelegateForFunctionPointer<ChestQuantityMove>(address);
            }
    }
    private unsafe void StartChest(int mode,bool selected=false)
    {
        RetrievalTry(()=>{
            if(craftingWithdrawalPending||retainerGilRunning||organizeRunning||chestRunning||retrievalRun.Running||sellerRun.Running||gilTrade.Running)throw new InvalidOperationException("Finish the current inventory operation first.");
            var agent=AgentFreeCompanyChest.Instance();var fc=InfoProxyFreeCompany.Instance();
            if(!Player.IsLoaded||Player.CurrentWorld.RowId!=Player.HomeWorld.RowId||SellerAddon("FreeCompanyChest")==null||agent==null||!agent->IsAgentActive()||fc==null||fc->Id==0)throw new InvalidOperationException("Open your FC chest on your home world first.");
            EnsureChestMove();
            chestMode=mode;chestQuality=retrievalQuality;chestGoals=[];
            if(mode==4&&retrievalEntrust!=null){chestGoals=retrievalEntrust.Amounts(retrievalItems.Select(x=>x.ItemId),new uint[]{0,1,2,3,2001}.SelectMany(ReadChestContainer),chestQuality,OrganizeCategory,retrievalSendCategories);if(chestGoals.Count==0)throw new InvalidOperationException("No matching inventory items or crystals to entrust after keep amounts and protection.");}
            else if(mode==0||mode==3||mode==4){PreviewRetrieval(selected,false);if(retrievalPlan.Length==0&&(mode!=4||retrievalSendCategories.Count==0))throw new InvalidOperationException("Choose items in Organizer first.");chestGoals=retrievalPlan.ToDictionary(x=>x.ItemId,x=>checked(x.Quantity+(mode!=0||retrievalSubtract?0:RetrievalHeld(x.ItemId,chestQuality))));}
            if(mode==4&&retrievalEntrust==null&&retrievalSendCategories.Count>0)foreach(var group in new uint[]{0,1,2,3,2001}.SelectMany(ReadChestContainer).Where(x=>retrievalSendCategories.Contains(OrganizeCategory(x.Item))&&RetainerRetrieval.AllowsQuality(x.Hq,chestQuality)).GroupBy(x=>x.Item))chestGoals.TryAdd(group.Key,group.Sum(x=>x.Quantity));
            var candidates=chestTab==0?new uint[]{20000,20001,20002,20003,20004,22001}:new[]{chestTab==6?22001u:19999u+(uint)chestTab};
            chestPages=candidates.Where(p=>(ChestAccess(p)==3||(mode==1||mode==4)&&ChestAccess(p)==2)&&(mode!=3||p!=22001&&p!=19999+chestDestination)).ToArray();
            if(mode==3&&ChestAccess((uint)(19999+chestDestination))<2)throw new InvalidOperationException("Destination tab does not allow deposits.");
            if(chestPages.Length==0)throw new InvalidOperationException("No selected chest tabs permit this action.");
            chestOwner=Player.ContentId;chestWorld=Player.CurrentWorld.RowId;chestCompany=fc->Id;chestPageIndex=0;chestMoves=0;chestUnmoved=0;chestPending=null;
            BeginChestPage(DateTimeOffset.UtcNow);chestRunning=true;
        });
    }
    private unsafe void TickChest(DateTimeOffset now)
    {
        var open=config.EnableRetriever&&SellerAddon("FreeCompanyChest")!=null;
        if(open&&!chestWasOpen)chestToolsWindow.IsOpen=true;
        if(!open)chestToolsWindow.IsOpen=false;chestWasOpen=open;
        if(!chestRunning)return;
        try{
            var fc=InfoProxyFreeCompany.Instance();
            if(!open||!Player.IsLoaded||Player.ContentId!=chestOwner||Player.CurrentWorld.RowId!=chestWorld||fc==null||fc->Id!=chestCompany||organizeRunning||sellerRun.Running||gilTrade.Running||retrievalRun.Running){StopChest("Stopped: chest or character changed, or another task started.");return;}
            if(now<chestNext)return;chestNext=now.AddMilliseconds(250);
            var page=chestPages[chestPageIndex];var access=ChestAccess(page);
            if(chestMode==3&&ChestAccess((uint)(19999+chestDestination))<2){StopChest("Stopped: destination access changed.");return;}
            if(access!=3&&!((chestMode==1||chestMode==4)&&access==2)){StopChest("Stopped: chest permissions changed.");return;}
            var manager=InventoryManager.Instance();if(manager==null)throw new InvalidOperationException("Inventory unavailable.");
            foreach(var op in manager->PendingOperations)if(!op.IsEmpty){if(now>chestDeadline)StopChest("Stopped: inventory transaction timed out. Check the chest before retrying.");return;}
            var destination=chestMode==3?ReadChestContainer((uint)(19999+chestDestination)):Array.Empty<ChestStack>();
            var chest=ReadChestContainer(page);var bags=(page==22001?new uint[]{2001}:new uint[]{0,1,2,3}).SelectMany(ReadChestContainer).ToArray();
            if(chestPending is {} pending){
                var all=chest.Concat(bags).Concat(destination).ToArray();var src=all.FirstOrDefault(s=>s.Container==pending.Source.Container&&s.Slot==pending.Source.Slot);var dst=all.FirstOrDefault(s=>s.Container==pending.Destination.Container&&s.Slot==pending.Destination.Slot);
                if(src==null||dst==null||!ChestTransferPlanner.Confirmed(pending,src,dst)){if(now>chestDeadline)StopChest("Stopped: transfer was not confirmed. No automatic retry.");return;}
                ReportStorageTransfer(pending.Source.Container<10000?"Entrusted":pending.Destination.Container<10000?"Withdrew":"Moved within chest",pending.Source.Item,pending.Source.Hq,pending.Quantity,"FC chest");
                if(chestMode==4)chestGoals[pending.Source.Item]-=pending.Quantity;chestPending=null;chestMoves++;chestStatus=$"{chestMoves} transfers confirmed";
            }
            var next=chestMode switch{0=>ChestTransferPlanner.Withdraw(chest,bags,chestGoals,chestQuality),1=>ChestTransferPlanner.Duplicates(chest,bags,chestKeep),3=>ChestTransferPlanner.Transfer(chest,destination,chestGoals.Keys.ToHashSet(),chestQuality),4=>ChestTransferPlanner.Send(bags,chest,chestGoals,chestQuality),_=>ChestTransferPlanner.Merge(chest)};
            if(next==null){
                if(chestMode==3)chestUnmoved+=chest.Where(x=>chestGoals.ContainsKey(x.Item)&&RetainerRetrieval.AllowsQuality(x.Hq,chestQuality)).Sum(x=>x.Quantity);
                if(++chestPageIndex<chestPages.Length){BeginChestPage(now);return;}
                var missing=chestMode==0?chestGoals.Sum(x=>Math.Max(0,x.Value-RetrievalHeld(x.Key,chestQuality))):chestMode==4?chestGoals.Values.Sum():chestUnmoved;
                StopChest($"{chestMoves} transfers confirmed"+(missing>0?$" · {missing} items remain (stock or destination capacity unavailable).":" · Finished."));return;
            }
            chestPending=next;chestDeadline=now.AddSeconds(12);
            chestMove!(manager,(InventoryType)next.Source.Container,(ushort)next.Source.Slot,(InventoryType)next.Destination.Container,(ushort)next.Destination.Slot,next.Quantity);
        }catch(Exception e){StopChest("Stopped: "+e.Message);}
    }
    private void DrawRetrievalChest()
    {
        ImGui.BeginDisabled(chestRunning||organizeRunning||retrievalRun.Running);
        ImGui.SetNextItemWidth(160);ImGui.Combo("Tabs",ref chestTab,"All accessible tabs\0Tab 1\0Tab 2\0Tab 3\0Tab 4\0Tab 5\0Crystals\0");
        if(ImGui.Button("Withdraw list"))StartChest(0);ImGui.SameLine();if(ImGui.Button("Selected"))StartChest(0,true);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Uses checked list entries, the current material mode, quality and subtract-bags setting in Organizer.");
        if(ImGui.Button("Entrust list to chest"))StartChest(4);
        if(ImGui.Button("Deposit duplicates"))StartChest(1);ImGui.SameLine();if(ImGui.Button("Merge stacks"))StartChest(2);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Combines partial stacks within each selected chest tab. HQ and NQ stay separate.");
        ImGui.SetNextItemWidth(80);if(ImGui.InputInt("Keep in bags",ref chestKeep)){chestKeep=Math.Clamp(chestKeep,0,RetainerRetrieval.MaxQuantity);config.StorageKeepInBags=chestKeep;SaveConfiguration();}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Duplicate deposits leave this quantity of each item and quality in your bags. Only items already present in that chest tab are deposited. No quality conversion.");
        ImGui.SetNextItemWidth(90);ImGui.SliderInt("Destination tab",ref chestDestination,1,5);
        if(ImGui.Button("Move list items to tab"))StartChest(3);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Moves all matching items from the selected source tabs to this destination. Uses item identities from the current material plan and the selected quality. Crystals remain in their dedicated inventory.");
        ImGui.EndDisabled();
        if(chestRunning&&ImGui.Button("Stop"))StopChest("Stopped by you. An already-sent transfer may still complete.");
        if(ImGui.SmallButton("Open Organizer"))OpenRetriever();ImGui.SameLine();ImGui.Checkbox("Dock to chest",ref chestDock);
        ImGui.TextWrapped(chestStatus);if(retrievalError.Length>0)ImGui.TextWrapped(retrievalError);
    }
}
