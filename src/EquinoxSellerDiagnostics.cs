using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly SellerTrace sellerTrace=new();
    private readonly SellerBellSession sellerBellSession=new();
    private SellerNativeState? sellerLastNativeState;
    private string sellerLastRunState="";
    private sealed record SellerNativeState(bool Bell,bool List,bool Selling,bool Editor,bool Comparison,bool History,
        ulong SelectedRetainer,ulong InventoryRetainer,bool RetainerActive,int Inventory,int Slot,bool Loaded,bool Symbolic,
        uint Item,int Quantity,int EditorQuantity,int Price,uint SearchItem,bool Waiting,int Listings,
        uint CompareNode,bool CompareEnabled,string CompareEvents,int Expected,int Error,string Phase,bool ManualCaptured);

    private void TraceSeller(string stage,string message,object? detail=null)=>sellerTrace.Add(DateTimeOffset.UtcNow,stage,message,detail);
    private void TraceSellerDecision(ulong retainer,MarketStock item,SellerDecision decision,bool applied,string mode)=>
        TraceSeller("Price decision",$"{item.Name} · {(applied?"Updated":decision.CanApply?"Suggested":decision.Status.ToString())} · {decision.Reason}",
            new {mode,retainer,item.ItemId,item.Hq,item.Slot,before=item.Price,decision,applied,
                listings=sellerComparison.Expected,historyReceived=sellerComparison.HistoryReceived,
                history=sellerComparison.History.Take(20).ToArray()});
    private static unsafe string SellerButtonEvents(AtkComponentButton* button)
    {
        if(button==null||button->OwnerNode==null)return "unavailable";
        var events=new List<string>();var e=button->OwnerNode->AtkEventManager.Event;
        for(var i=0;e!=null&&i<16;i++,e=e->NextEvent)events.Add($"{(int)e->State.EventType}:{e->Param}");
        return string.Join(",",events);
    }
    private unsafe SellerNativeState ReadSellerNativeState()
    {
        var rm=RetainerManager.Instance();var ui=UIModule.Instance();var order=ui==null?null:ui->GetItemOrderModule();
        var selected=rm==null?0:rm->LastSelectedRetainerId;
        var agent=AgentRetainer.Instance();var edit=(AddonRetainerSell*)SellerAddon("RetainerSell");
        var manager=InventoryManager.Instance();
        var inventory=edit==null||agent==null||manager==null?null:manager->GetInventoryContainer(agent->SellItemInventoryType);
        var slot=edit==null||agent==null?-1:agent->SellItemInventorySlot;
        var raw=inventory!=null&&inventory->IsLoaded&&slot>=0&&slot<inventory->Size?inventory->GetInventorySlot(slot):null;
        var item=SellerNativeInterop.Resolve(raw);
        var proxy=InfoProxyItemSearch.Instance();var button=edit==null?null:edit->ComparePrices;
        return new(Conditions[ConditionFlag.OccupiedSummoningBell],SellerAddon("RetainerList")!=null,SellerAddon("RetainerSellList")!=null,edit!=null,SellerAddon("ItemSearchResult")!=null,SellerAddon("ItemSearchHistory")!=null,
            selected,order==null?0:order->ActiveRetainerId,SellerActive(selected),agent==null?-1:(int)agent->SellItemInventoryType,slot,inventory!=null&&inventory->IsLoaded,raw!=null&&raw->IsSymbolic,
            item==null?0:item->ItemId,item==null?0:item->Quantity,edit==null||edit->Quantity==null?-1:edit->Quantity->Value,edit==null||edit->AskingPrice==null?-1:edit->AskingPrice->Value,
            proxy==null?0:proxy->SearchItemId,proxy!=null&&proxy->WaitingForListings,proxy==null?-1:(int)proxy->ListingCount,
            button==null||button->OwnerNode==null?0:button->OwnerNode->NodeId,button!=null&&button->IsEnabled,SellerButtonEvents(button),sellerComparison.Expected,sellerComparison.Error,sellerRun.State.ToString(),manualEditor!=null);
    }
    private unsafe void ObserveSellerDiagnostics(DateTimeOffset now)
    {
        var atBell=Conditions[ConditionFlag.OccupiedSummoningBell]||SellerAddon("RetainerList")!=null||SellerAddon("RetainerSellList")!=null||SellerAddon("RetainerSell")!=null;
        if(sellerBellSession.Observe(atBell,now)){
            if(sellerRun.Running)sellerRun.Stop("Stopped: summoning bell closed.");
            if(config.Seller.CloseAtBell){sellerWindow.IsOpen=false;minimizedLaunchers.Remove("Equinox Seller");}
            TraceSeller("Bell","Closed",new {sellerClosed=config.Seller.CloseAtBell});
        }
        var state=sellerRun.State+" · "+sellerRun.Status;
        if(state!=sellerLastRunState){sellerLastRunState=state;TraceSeller("Run",state);}
        if(!atBell&&!sellerRun.Running)return;
        var native=ReadSellerNativeState();
        if(native!=sellerLastNativeState){sellerLastNativeState=native;TraceSeller("Native state",$"{native.Phase} · editor {native.Editor} · comparison {native.Comparison} · item {native.Item}",native);}
    }
    private void SellerNativeEvent(AddonEvent e,AddonArgs args)
    {
        if(args is not AddonReceiveEventArgs ev)return;
        TraceSeller("Native event",$"{args.AddonName} · type {(int)ev.AtkEventType} · parameter {ev.EventParam}");
    }
    private object ReadSellerDiagnosticExport()=>new {
        enabled=config.Seller.Enabled,hookEnabled=sellerResultHook?.IsEnabled==true,sellerLiveError,sellerNotice,marketDataError,
        run=new {phase=sellerRun.State.ToString(),sellerRun.Status,sellerRun.Running,sellerRun.Paused,sellerRun.Apply,sellerRun.RetainerIndex,sellerRun.RetainerCount,sellerRun.ItemIndex,sellerRun.ItemCount},
        manual=new {manualEditor,manualRetainer,inventory=manualInventory.ToString(),manualAutoCompare,manualShowHistory,manualSuspendUntilClose,manualEditorAt,manualCompareAt},
        comparison=new {sellerComparison.Item,sellerComparison.World,sellerComparison.RequestedAt,sellerComparison.RespondedAt,sellerComparison.Expected,sellerComparison.Error,sellerComparison.ReceivedCount,sellerComparison.PacketRequest,sellerComparison.HistoryReceived,sellerComparison.HistoryRequested,historyRows=sellerComparison.History.Length},
        saleHistory=new {hookEnabled=marketHistoryHook?.IsEnabled==true,status=marketHistoryStatus,queued=marketRetainerHistories.Count,retainers=marketLedger.Retainers.Select(r=>new {r.Name,records=marketLedger.Income.Count(x=>x.RetainerId==r.Id&&x.Id.StartsWith("history:"))}).ToArray()},
        settings=new {config.Seller.OpenCompare,config.Seller.OpenHistory,config.Seller.ClickSetsPrice,config.Seller.CopyPrice,config.Seller.ClickConfirms,config.Seller.OpenAtBell,config.Seller.CloseAtBell,
            config.Seller.UseHistory,config.Seller.HistoryRule,config.Seller.HistoryDays,config.Seller.MinimumHistorySales,config.Seller.HistoryOutlierPercent,
            config.Seller.PercentUndercut,config.Seller.UndercutGil,config.Seller.UndercutPercent,config.Seller.LowOutlierPercent,config.Seller.RoundTo,config.Seller.RaiseCompetitivePrices,config.Seller.MaximumIncreasePercent,config.Seller.MinimumPrices,config.Seller.ExcludedItems},
        priceChecks=marketLedger.Prices.TakeLast(200).ToArray(),
        manualDecision,
        locks=new {marketBuddyLocked,autoRetainer=new {sellerAutoRetainerPause.Held,sellerAutoRetainerPause.PreviouslySuppressed,sellerAutoRetainerPause.RestorePending,sellerAutoRetainerPause.Status}},
        relatedPlugins=Pi.InstalledPlugins.Where(x=>x.InternalName.Contains("Market",StringComparison.OrdinalIgnoreCase)||x.InternalName=="AutoRetainer").Select(x=>new {x.InternalName,x.IsLoaded}).ToArray(),
        lastNative=sellerLastNativeState,recent=sellerTrace.Snapshot()
    };
    private void DrawSellerDiagnostics()
    {
        PollDiagnosticExport();
        ImGui.TextWrapped(sellerTrace.Latest);
        MarketHelp("A bounded local trace of Seller states, requests and blocked actions. Included in Export diagnostics; no garden recording or pairing upload is needed.");
        ImGui.BeginDisabled(DiagnosticExportBusy);
        if(ImGui.SmallButton(DiagnosticExportBusy?"Exporting…":"Export diagnostics##seller"))Export();
        ImGui.EndDisabled();ImGui.SameLine();
        if(ImGui.SmallButton("Open export folder##seller"))OpenDiagnosticFolder(Path.Combine(Pi.GetPluginConfigDirectory(),"exports"));
        if(ImGui.SmallButton("Start fresh trace")){sellerTrace.Clear();sellerLastNativeState=null;sellerLastRunState="";TraceSeller("Trace","Reproduce the failing action, then export diagnostics.");}
        if(DiagnosticExportBusy)ImGui.TextDisabled("Writing diagnostic file…");
        else if(diagnosticExportError.Length>0)ImGui.TextWrapped(diagnosticExportError);
        else if(exportPath!=null){ImGui.TextDisabled("Export ready");MarketHelp(exportPath);}
        if(ImGui.TreeNode("Recent actions")){
            foreach(var row in sellerTrace.Snapshot().TakeLast(10).Reverse()){ImGui.TextWrapped($"{row.At.ToLocalTime():HH:mm:ss} · {row.Stage} · {row.Message}");}
            ImGui.TreePop();
        }
    }
}
