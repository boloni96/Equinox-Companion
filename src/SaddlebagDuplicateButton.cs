using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe void StartSaddlebagDuplicates()
    {
        if(chestRunning||organizeRunning||retrievalRun.Running||gearRouting||armoireRunning||craftingWithdrawalPending||sellerRun.Running||TraderRunning||retainerGilRunning)throw new InvalidOperationException("Finish the current inventory operation first.");
        var manager=InventoryManager.Instance();if(manager==null)throw new InvalidOperationException("Inventory unavailable.");
        var pages=new List<uint>();
        foreach(var type in new uint[]{4000,4001,4100,4101}){
            var container=manager->GetInventoryContainer((InventoryType)type);
            if(container!=null&&container->IsLoaded&&container->Items!=null&&container->Size is >0 and <=70)pages.Add(type);
        }
        if(pages.Count==0)throw new InvalidOperationException("Wait for the saddlebag contents to load first.");
        foreach(var page in pages)RequireLocalStorage("bag:"+page,page);
        RefreshGearSetProtection();
        localStorageDuplicates=true;localStorageDuplicatePages=pages.ToArray();
        localStorageKey="bag:"+pages[0];localStorageType=pages[0];localStorageTerritory=Client.TerritoryType;
        chestOwner=Player.ContentId;chestWorld=Player.CurrentWorld.RowId;
        localStoragePending=null;localStorageVisited.Clear();chestMoves=0;chestNext=default;chestDeadline=DateTimeOffset.UtcNow.AddSeconds(15);
        localStorageRunning=chestRunning=localStorageStatusShown=true;chestStatus="Finding saddlebag duplicates…";
    }
    private unsafe void DrawSaddlebagDuplicateButton()
    {
        if(!config.EnableRetriever||!Player.IsLoaded)return;
        var addon=SellerAddon("InventoryBuddy");if(addon==null)addon=SellerAddon("InventoryBuddy2");
        if(addon==null||addon->RootNode==null)return;
        var scale=addon->Scale;var width=addon->RootNode->Width*scale;var height=addon->RootNode->Height*scale;
        ImGui.SetNextWindowPos(new Vector2(addon->X+width-12*scale,addon->Y+height-10*scale),ImGuiCond.Always,new Vector2(1,1));
        PushRetainerToolbarStyle();
        if(ImGui.Begin("##EquinoxSaddlebagDuplicates",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing)){
            if(localStorageRunning&&localStorageDuplicates){
                if(ImGui.Button("Stop##SaddleDuplicates"))StopChest("Stopped by you. An already-sent transfer may still complete.");
                MarketHelp(chestStatus);
            }else{
                ImGui.BeginDisabled(chestRunning||organizeRunning||retrievalRun.Running||gearRouting||armoireRunning||sellerRun.Running||TraderRunning||retainerGilRunning);
                if(ImGui.Button("Add duplicates"))RetrievalTry(StartSaddlebagDuplicates);
                ImGui.EndDisabled();
                MarketHelp("Move inventory items already present in loaded normal/premium saddlebags. Fills matching stacks first; HQ and NQ stay separate. Respects gear-set protection. Keeps 0 in bags.");
            }
            var status=retrievalError.Length>0?retrievalError:localStorageDuplicates?chestStatus:"";
            if(status.Length>0){ImGui.SameLine();ImGui.TextDisabled("i");MarketHelp(status);}
        }
        ImGui.End();ImGui.PopStyleVar(2);ImGui.PopStyleColor(7);
    }
}
