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
    private bool chestRunning,chestWasOpen,chestDock=true,chestAllAvailable;
    private ulong chestOwner,chestCompany;private uint chestWorld;
    private bool chestDestinationRequested,chestPageReady;
    private ChestStack[]? chestPageSample;
    private uint[] chestPages=[];private int chestPageIndex;
    private DateTimeOffset chestNext,chestDeadline;private ChestMove? chestPending;
    private Dictionary<uint,int> chestGoals=[];private RetrievalQuality chestQuality;
    private string chestStatus="Open the FC chest to use these tools.";
    private unsafe delegate int ChestQuantityMove(InventoryManager* manager,InventoryType source,ushort sourceSlot,InventoryType destination,ushort destinationSlot,int quantity);
    private ChestQuantityMove? chestMove;
    private sealed class ChestToolsWindow(Plugin p):Window("##EquinoxChestToolsBar",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing)
    {
        public override void Draw()=>p.MeasureWindow(WindowName,DrawMeasuredContent);
        private void DrawMeasuredContent()=>p.DrawChestToolbar();
        public override unsafe void PreDraw()=>p.MeasureWindow(WindowName,PreDrawMeasured,"PreDraw");
        private unsafe void PreDrawMeasured(){
            RespectCloseHotkey=false;AllowPinning=false;
            var addon=SellerAddon("FreeCompanyChest");
            if(p.chestDock&&addon!=null){var height=ImGui.GetFrameHeight()+8;var y=addon->Y-height-4;Position=new Vector2(Math.Max(0,(int)addon->X),y>=0?y:addon->Y+addon->GetScaledHeight(true)+4);PositionCondition=ImGuiCond.Always;}else Position=null;
            PushRetainerToolbarStyle();
        }
        public override void PostDraw()=>p.MeasureWindow(WindowName,PostDrawMeasured,"PostDraw");
        private void PostDrawMeasured(){ImGui.PopStyleVar(2);ImGui.PopStyleColor(7);}
    }
    private unsafe int ChestAccess(uint container)
    {
        var addon=SellerAddon("FreeCompanyChest");if(addon==null)return 0;
        // Native chest access: two bits per item page; 0 none, 1 view, 2 deposit, 3 full.
        if(container is >=20000 and <=20004)return (int)((*(uint*)((byte*)addon+0x4DC)>>(2*(int)(container-20000)))&3);
        return container==22001?(int)*(uint*)((byte*)addon+0x4E0):0;
    }
    private void StopChest(string reason){localStorageQueue.Clear();if(NativeSaddleTransfer){TraceSaddleDeposit("stopped",new{reason});CancelSaddleDepositMenu();}localStorageRunning=false;localStoragePending=null;RequestStorageRefresh();chestRunning=false;chestPending=null;chestStatus=reason;retrievalPreviewDirty=true;Log.Information("FC chest: {Status}",reason);}
    private unsafe ChestStack[] ReadChestContainer(uint type)
    {
        var manager=InventoryManager.Instance();if(manager==null)throw new InvalidOperationException("Inventory unavailable.");
        var c=manager->GetInventoryContainer((InventoryType)type);if(c==null||!c->IsLoaded||c->Items==null||c->Size is <=0 or >200)throw new InvalidOperationException("Waiting for chest inventory. Open the selected tab and try again.");
        var rows=new List<ChestStack>();for(var i=0;i<c->Size;i++){
            var s=c->Items[i];var item=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(s.ItemId);
            rows.Add(CrystalStorage.Normalize(new(type,i,s.ItemId,s.Quantity,(s.Flags&InventoryItem.ItemFlags.HighQuality)!=0,item.HasValue?(int)item.Value.StackSize:0,item.HasValue&&!item.Value.IsUntradable&&s.SpiritbondOrCollectability==0)));
        }return rows.ToArray();
    }
    private DateTimeOffset chestTabRetryAt;
    private unsafe AtkComponentRadioButton* ChestPageButton(uint page)
    {
        var addon=SellerAddon("FreeCompanyChest");if(addon==null||!addon->IsReady)return null;
        var index=page==22001?5:(int)page-20000;
        if(index is <0 or >5)return null;
        // AddonFreeCompanyChest: five item radio pointers at 0x250,
        // followed by crystals at 0x278. Matches the access fields below.
        var tab=*(AtkComponentRadioButton**)((byte*)addon+0x250+index*8);
        return tab!=null&&tab->OwnerNode!=null&&tab->GetComponentType()==ComponentType.RadioButton&&tab->OwnerNode->IsVisible()?tab:null;
    }
    private unsafe bool ChestPageSelected(uint page,DateTimeOffset now)
    {
        var tab=ChestPageButton(page);
        if(tab==null){chestStatus="Waiting for the company chest tab controls…";return false;}
        var addon=SellerAddon("FreeCompanyChest");
        // StageFlag and IsDataNotReady disable tab work while a server request
        // or the game's UI lock is active. Cached containers alone are not ready.
        if(addon==null||*((byte*)addon+0x4EC)!=0||*((byte*)addon+0x4F4)!=0||!tab->IsEnabled){chestStatus="Waiting for the company chest to unlock its tabs…";return false;}
        var kind=*((byte*)addon+0x4D8);var index=*((byte*)addon+0x4D9);
        if(tab->IsSelected&&(page==22001?kind==1:kind==0&&index==page-20000))return true;
        chestPageReady=false;chestPageSample=null;
        if(now<chestTabRetryAt)return false;
        var evt=KeyboundButtonEvent((AtkComponentButton*)tab);
        if(evt==null){chestStatus="Waiting for the chest tab selection event…";return false;}
        var click=*evt;chestTabRetryAt=now.AddSeconds(1);
        tab->SetActive();SendKeyboundButtonEvent(addon,&click);
        chestStatus=$"Selecting {(page==22001?"crystals":"chest tab "+(page-19999))}…";
        return false;
    }
    private void BeginChestPage(DateTimeOffset now)
    {
        chestDestinationRequested=chestMode!=3;chestPageReady=false;chestPageSample=null;
        chestTabRetryAt=default;chestNext=now;chestDeadline=now.AddSeconds(20);
        chestStatus="Waiting for the chest tab to be ready…";
    }
    private void EnsureChestMove()
    {
            if(chestMove==null){
                // Native quantity-transfer entry point; signature failure disables movement.
                if(!QuickLootScanner.TryScanText("48 89 5C 24 10 48 89 6C 24 18 56 57 41 55 41 56 41 57 48 83 EC 30 8D BA 60 F0 FF FF 45 0F BF E8 8D 82 FC EF FF FF 41 8B E9 44 8B FA 4C 8B F1",out var address))throw new InvalidOperationException("Chest transfer unavailable for this game version.");
                chestMove=Marshal.GetDelegateForFunctionPointer<ChestQuantityMove>(address);
            }
    }
    private unsafe void StartChest(int mode,bool selected=false,Dictionary<uint,int>? explicitGoals=null,uint[]? explicitPages=null,RetrievalQuality? explicitQuality=null)
    {
        RetrievalTry(()=>{
            if(craftingWithdrawalPending||retainerGilRunning||organizeRunning||chestRunning||retrievalRun.Running||sellerRun.Running||TraderRunning)throw new InvalidOperationException("Finish the current inventory operation first.");
            var agent=AgentFreeCompanyChest.Instance();var fc=InfoProxyFreeCompany.Instance();
            if(!Player.IsLoaded||Player.CurrentWorld.RowId!=Player.HomeWorld.RowId||SellerAddon("FreeCompanyChest")==null||agent==null||!agent->IsAgentActive()||fc==null||fc->Id==0)throw new InvalidOperationException("Open your FC chest on your home world first.");
            EnsureChestMove();
            chestMode=mode;chestQuality=explicitQuality??retrievalQuality;chestGoals=[];chestAllAvailable=explicitGoals==null&&retrievalTransferAll&&!chestCrystals;
            if(explicitGoals!=null)chestGoals=new(explicitGoals);
            else if(chestCrystals&&(mode==0||mode==4)){chestQuality=RetrievalQuality.Any;chestGoals=CrystalTransferSelection.Goals(mode==0,chestCrystalFamily,chestCrystalElement,chestCrystalAmount,chestKeep,ReadChestContainer(2001));}
            else if(mode==4&&retrievalEntrust!=null){chestGoals=retrievalEntrust.Amounts(FilteredPlanItems().Select(x=>x.ItemId),new uint[]{0,1,2,3,2001}.SelectMany(ReadChestContainer),chestQuality,OrganizeCategory,Array.Empty<uint>());if(chestGoals.Count==0)throw new InvalidOperationException("No matching inventory items or crystals to entrust after keep amounts and protection.");}
            else if(mode==4){chestGoals=PlanEntrustEntries().ToDictionary(e=>e.ItemId,e=>e.Quantity);if(chestGoals.Count==0)throw new InvalidOperationException("No listed items match the plan filters.");}
            else if(mode==0||mode==3){if(retrievalEntrust!=null)retrievalEntrustWithdrawal=true;PreviewRetrieval(selected,false,mode!=4);if(retrievalPlan.Length==0&&(mode!=4||retrievalSendCategories.Count==0))throw new InvalidOperationException("Choose items in Organizer first.");chestGoals=retrievalPlan.ToDictionary(x=>x.ItemId,x=>chestAllAvailable?int.MaxValue:checked(x.Quantity+(mode!=0||retrievalSubtract?0:RetrievalHeld(x.ItemId,chestQuality))));}
            if(chestCrystals&&mode==3)throw new InvalidOperationException("Crystals stay in dedicated storage.");
            var candidates=explicitPages??(chestCrystals?new uint[]{22001}:chestTab==0?new uint[]{20000,20001,20002,20003,20004,22001}:new[]{chestTab==6?22001u:19999u+(uint)chestTab});
            chestPages=candidates.Where(p=>(mode!=1||config.ChestDuplicatesIncludeCrystals||p!=22001)&&(ChestAccess(p)==3||(mode==1||mode==4)&&ChestAccess(p)==2)&&(mode!=3||p!=22001&&p!=19999+chestDestination)).ToArray();
            if(mode==3&&ChestAccess((uint)(19999+chestDestination))<2)throw new InvalidOperationException("Destination tab does not allow deposits.");
            if(chestPages.Length==0)throw new InvalidOperationException("No selected chest tabs permit this action.");
            chestOwner=Player.ContentId;chestWorld=Player.CurrentWorld.RowId;chestCompany=fc->Id;chestPageIndex=0;chestMoves=0;chestUnmoved=0;chestPending=null;
            BeginChestPage(DateTimeOffset.UtcNow);chestRunning=true;
        });
    }
    private unsafe void TickChest(DateTimeOffset now)
    {
        UpdateLiveCompanyChest(now);
        if(localStorageRunning){TickLocalStorage(now);return;}
        var open=config.EnableRetriever&&SellerAddon("FreeCompanyChest")!=null;
        if(open&&!chestWasOpen)chestToolsWindow.IsOpen=true;
        if(!open)chestToolsWindow.IsOpen=false;chestWasOpen=open;
        if(!chestRunning)return;
        try{
            var fc=InfoProxyFreeCompany.Instance();
            if(!open||!Player.IsLoaded||Player.ContentId!=chestOwner||Player.CurrentWorld.RowId!=chestWorld||fc==null||fc->Id!=chestCompany||organizeRunning||sellerRun.Running||TraderRunning||retrievalRun.Running){StopChest("Stopped: chest or character changed, or another task started.");return;}
            if(now<chestNext)return;chestNext=now.AddMilliseconds(250);
            var page=chestPages[chestPageIndex];var access=ChestAccess(page);
            if(chestMode==3&&ChestAccess((uint)(19999+chestDestination))<2){StopChest("Stopped: destination access changed.");return;}
            if(access!=3&&!((chestMode==1||chestMode==4)&&access==2)){StopChest("Stopped: chest permissions changed.");return;}
            var manager=InventoryManager.Instance();if(manager==null)throw new InvalidOperationException("Inventory unavailable.");
            foreach(var op in manager->PendingOperations)if(!op.IsEmpty){if(now>chestDeadline)StopChest("Stopped: inventory transaction timed out. Check the chest before retrying.");return;}
            if(!ChestPageSelected(page,now)){
                if(now>chestDeadline)StopChest("Stopped: the requested chest tab did not become ready. No new transfer sent.");
                return;
            }
            if(!chestDestinationRequested){
                GameMain.ExecuteCommand(404,19999+chestDestination);chestDestinationRequested=true;
                chestNext=now.AddSeconds(2);chestStatus="Waiting for the destination chest tab…";return;
            }
            var needed=new[]{page}.Concat(chestMode==3?new[]{(uint)(19999+chestDestination)}:Array.Empty<uint>());
            foreach(var type in needed){var container=manager->GetInventoryContainer((InventoryType)type);
                if(container==null||!container->IsLoaded||container->Items==null||container->Size is <=0 or >200){
                    if(now>chestDeadline)StopChest("Stopped: chest tab data did not load. No transfer sent.");
                    else chestStatus="Waiting for chest tab data…";return;
                }
            }
            var destination=chestMode==3?ReadChestContainer((uint)(19999+chestDestination)):Array.Empty<ChestStack>();
            var chest=ReadChestContainer(page);var bags=(page==22001?new uint[]{2001}:new uint[]{0,1,2,3}).SelectMany(ReadChestContainer).ToArray();
            if(!chestPageReady){
                var sample=chest.Concat(destination).ToArray();
                if(chestPageSample is null||!sample.SequenceEqual(chestPageSample)){
                    if(now>chestDeadline){StopChest("Stopped: chest data did not settle. No transfer sent.");return;}
                    chestPageSample=sample;chestNext=now.AddMilliseconds(750);chestStatus="Waiting for chest contents to settle…";return;
                }
                chestPageReady=true;chestPageSample=null;
            }
            if(chestPending is {} pending){
                var all=chest.Concat(bags).Concat(destination).ToArray();var src=all.FirstOrDefault(s=>s.Container==pending.Source.Container&&s.Slot==pending.Source.Slot);var dst=all.FirstOrDefault(s=>s.Container==pending.Destination.Container&&s.Slot==pending.Destination.Slot);
                if(src==null||dst==null||!ChestTransferPlanner.Confirmed(pending,src,dst)){if(now>chestDeadline)StopChest("Stopped: transfer was not confirmed. No automatic retry.");return;}
                ReportStorageTransfer(pending.Source.Container<10000?"Entrusted":pending.Destination.Container<10000?"Withdrew":"Moved within chest",pending.Source.Item,pending.Source.Hq,pending.Quantity,"FC chest");
                if(chestMode==4)chestGoals[pending.Source.Item]-=pending.Quantity;chestPending=null;chestMoves++;chestStatus=$"{chestMoves} transfers confirmed · waiting for the chest to settle";
                chestNext=now.AddMilliseconds(1000);return;
            }
            RefreshGearSetProtection();
            var depositBags=bags.Where(s=>!OrganizerGearProtected(s.Item)).ToArray();
            var next=chestMode switch{0=>ChestTransferPlanner.Withdraw(chest,bags,chestGoals,chestQuality),1=>ChestTransferPlanner.Duplicates(chest,depositBags,chestKeep,config.ChestDuplicatesIncludeCrystals,config.DuplicateDepositExclusions),3=>ChestTransferPlanner.Transfer(chest,destination,chestGoals.Keys.ToHashSet(),chestQuality),4=>ChestTransferPlanner.Send(depositBags,chest,chestGoals,chestQuality),_=>ChestTransferPlanner.Merge(chest)};
            if(next==null){
                if(chestMode==3)chestUnmoved+=chest.Where(x=>chestGoals.ContainsKey(x.Item)&&RetainerRetrieval.AllowsQuality(x.Hq,chestQuality)).Sum(x=>x.Quantity);
                if(++chestPageIndex<chestPages.Length){BeginChestPage(now);return;}
                var missing=chestMode==0&&chestAllAvailable?0:chestMode==0?chestGoals.Sum(x=>Math.Max(0,x.Value-RetrievalHeld(x.Key,chestQuality))):chestMode==4?chestGoals.Values.Sum():chestUnmoved;
                StopChest($"{chestMoves} transfers confirmed"+(missing>0?$" · {missing} items remain (stock or destination capacity unavailable).":" · Finished."));return;
            }
            chestPending=next;chestDeadline=now.AddSeconds(12);
            chestNext=now.AddMilliseconds(500);
            chestMove!(manager,(InventoryType)next.Source.Container,(ushort)next.Source.Slot,(InventoryType)next.Destination.Container,(ushort)next.Destination.Slot,next.Quantity);
        }catch(Exception e){StopChest("Stopped: "+e.Message);}
    }
    private void DrawChestDuplicateOptions()
    {
        var include=config.ChestDuplicatesIncludeCrystals;
        if(ImGui.Checkbox("Include crystals in Duplicates",ref include)){config.ChestDuplicatesIncludeCrystals=include;SaveConfiguration();}
        MarketHelp("When disabled, Duplicates skips shards, crystals and clusters, including the crystals tab. Separate crystal Withdraw and Entrust actions are unchanged.");
    }
    private void DrawRetrievalChest()
    {
        ImGui.BeginDisabled(chestRunning||organizeRunning||retrievalRun.Running||TraderRunning);
        if(ImGui.BeginTable("ChestHeader",2,ImGuiTableFlags.SizingStretchSame)){
            ImGui.TableNextRow();ImGui.TableNextColumn();DrawChestSelection();ImGui.TableNextColumn();
            ImGui.BeginDisabled(chestCrystals);ImGui.SetNextItemWidth(-1);ImGui.Combo("##ChestTabs",ref chestTab,"All accessible tabs\0Tab 1\0Tab 2\0Tab 3\0Tab 4\0Tab 5\0Crystals\0");ImGui.EndDisabled();ImGui.EndTable();
        }
        if(chestCrystals)DrawChestCrystalOptions();else DrawTransferAmount();
        if(ImGui.Button("Withdraw"))StartChest(0);ImGui.SameLine();if(ImGui.Button("Entrust"))StartChest(4);ImGui.SameLine();
        if(ImGui.SmallButton("Options…"))ImGui.OpenPopup("ChestPanelOptions");
        if(ImGui.BeginPopup("ChestPanelOptions")){
            DrawChestDuplicateOptions();
            if(!chestCrystals&&ImGui.Button("Withdraw selected list items"))StartChest(0,true);
            if(ImGui.Button("Deposit duplicates"))StartChest(1);MarketHelp("Deposit matching bag items already stored in the selected tabs, leaving Keep in bags.");ImGui.SameLine();DrawDuplicateExclusionsButton("ChestPanel");
            if(!chestCrystals){ImGui.TextUnformatted("Keep in bags");DrawChestKeepControl();if(ImGui.Button("Merge stacks"))StartChest(2);
                ImGui.SetNextItemWidth(100);ImGui.SliderInt("Destination tab",ref chestDestination,1,5);if(ImGui.Button("Move list items to tab"))StartChest(3);}
            ImGui.Checkbox("Dock to chest",ref chestDock);ImGui.EndPopup();
        }
        ImGui.EndDisabled();
        if(chestRunning){ImGui.SameLine();if(ImGui.Button("Stop"))StopChest("Stopped by you. An already-sent transfer may still complete.");}
        ImGui.BeginChild("ChestStatus",new(0,Math.Max(60,ImGui.GetContentRegionAvail().Y-8)),false);
        ImGui.TextWrapped(chestStatus);if(retrievalError.Length>0)ImGui.TextWrapped(retrievalError);
        if(chestCrystals)ImGui.TextWrapped("No saved plan needed. Select shards, crystals or clusters and an element above. FC permissions and storage caps still apply.");
        ImGui.EndChild();
    }
}


