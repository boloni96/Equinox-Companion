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
        ImGui.SetNextWindowPos(new Vector2(addon->X+width-26*scale,addon->Y+height-26*scale),ImGuiCond.Always,new Vector2(1,1));
        // Keep the control inside the native footer, with no floating toolbar box.
        MarketGameConfig.TryGet(Dalamud.Game.Config.SystemConfigOption.ColorThemeType,out uint theme);
        var light=theme==1;
        ImGui.PushStyleColor(ImGuiCol.Text,light?new Vector4(.14f,.16f,.17f,1):new Vector4(.90f,.90f,.87f,1));
        ImGui.PushStyleColor(ImGuiCol.Button,light?new Vector4(.70f,.73f,.74f,1):new Vector4(.17f,.22f,.25f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,light?new Vector4(.81f,.84f,.85f,1):new Vector4(.25f,.32f,.36f,1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,light?new Vector4(.60f,.65f,.67f,1):new Vector4(.12f,.18f,.21f,1));
        ImGui.PushStyleColor(ImGuiCol.Border,new Vector4(.43f,.49f,.51f,1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize,0f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(8,3)*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,3f*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize,1f);

        if(ImGui.Begin("##EquinoxSaddlebagDuplicates",ImGuiWindowFlags.NoBackground|ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing)){
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
        ImGui.End();ImGui.PopStyleVar(5);ImGui.PopStyleColor(5);
    }
}
