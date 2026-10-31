using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;

public sealed partial class Plugin
{
    private bool armoireRunning,armoireOpenedLookup;
    private readonly HashSet<uint> armoireCategories=[];
    private bool armoireSinglePieces=true,armoireOutfitPieces=true;
    private HashSet<uint> ArmoireExclusions()=>config.ArmoireExcludedItems.TryGetValue(Player.ContentId,out var set)?set:config.ArmoireExcludedItems[Player.ContentId]=[];
    private ulong armoireCharacter;
    private uint armoireTerritory;
    private int armoirePhase,armoireMoved,armoireSkipped;
    private DateTimeOffset armoireNext,armoireDeadline;
    private ArmoirePiece? armoirePending;
    private readonly Queue<ArmoirePiece> armoireQueue=[];
    private Dictionary<uint,uint>? armoireRows;
    private Dictionary<uint,uint[]>? armoireSets;
    private string armoireStatus="Open your glamour dresser, then organize eligible gear into the armoire.";
    private (uint Item,string Name,string Status)[] armoirePreview=[];
    private static readonly InventoryType[] ArmoireContainers=[InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4,
        InventoryType.EquippedItems,InventoryType.ArmoryMainHand,InventoryType.ArmoryOffHand,InventoryType.ArmoryHead,InventoryType.ArmoryBody,
        InventoryType.ArmoryHands,InventoryType.ArmoryLegs,InventoryType.ArmoryFeets,InventoryType.ArmoryEar,InventoryType.ArmoryNeck,
        InventoryType.ArmoryWrist,InventoryType.ArmoryRings];

    private void DrawArmoireOrganizer()
    {
        ImGui.TextWrapped("Move armoire-compatible gear out of the glamour dresser, including individual pieces in outfit sets.");
        ImGui.BeginDisabled(armoireRunning);
        if(ImGui.Button("Check eligible gear"))RetrievalTry(()=>ScanArmoire(false));
        ImGui.SameLine();if(ImGui.Button("Dresser → Armoire"))StartArmoireOrganizer();
        if(ImGui.CollapsingHeader("Armoire options")){
            ImGui.TextUnformatted("Dyed gear is always protected (both dye channels).");
            DrawStorageCategoryPicker("Gear categories",armoireCategories,"All gear categories");
            ImGui.Checkbox("Individual pieces",ref armoireSinglePieces);ImGui.SameLine();ImGui.Checkbox("Outfit set pieces",ref armoireOutfitPieces);
            if(ImGui.SmallButton("Clear saved item exclusions")){ArmoireExclusions().Clear();SaveConfiguration();}
            ImGui.TextWrapped("Check eligible gear, then tick Keep beside any item you want left in the dresser. Exclusions are saved for this character.");
            ImGui.TextWrapped("Uses the current game's armoire eligibility list. Already stored items and copies also held in inventory, equipment or armoury are skipped. Keep two bag slots free. Stop leaves any withdrawn piece in your inventory.");
        }
        ImGui.EndDisabled();
        if(armoireRunning&&ImGui.Button("Stop armoire transfer"))StopArmoireOrganizer("Stopped by you");
        ImGui.TextWrapped(armoireStatus);
        if(armoirePreview.Length>0&&ImGui.CollapsingHeader($"Eligible gear · {armoirePreview.Length}###ArmoirePreview")){
            ImGui.BeginChild("ArmoireRows",new(0,ImGui.GetTextLineHeightWithSpacing()*10),true);
            var clip=ImGui.ImGuiListClipper();clip.Begin(armoirePreview.Length,ImGui.GetTextLineHeightWithSpacing());
            while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){var r=armoirePreview[i];ImGui.PushID((int)r.Item);ImGui.PushID(i);var keep=ArmoireExclusions().Contains(r.Item);ImGui.BeginDisabled(armoireRunning);if(ImGui.Checkbox("Keep",ref keep)){if(keep)ArmoireExclusions().Add(r.Item);else ArmoireExclusions().Remove(r.Item);SaveConfiguration();}ImGui.EndDisabled();ImGui.SameLine();ImGui.TextUnformatted(r.Name+" · "+(keep?"Excluded":r.Status));ImGui.PopID();ImGui.PopID();}
            clip.End();clip.Destroy();ImGui.EndChild();
        }
    }
    private void LoadArmoireCatalogue()
    {
        armoireRows??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>().Where(r=>r.Item.RowId>0).GroupBy(r=>r.Item.RowId).ToDictionary(g=>g.Key,g=>g.First().RowId);
        armoireSets??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.MirageStoreSetItem>().ToDictionary(r=>r.RowId,r=>r.Items.Select(i=>i.RowId).ToArray());
    }
    private static unsafe bool ArmoireDresserReady()
    {
        var a=AgentMiragePrismPrismBox.Instance();var m=MirageManager.Instance();
        return a!=null&&a->IsAgentActive()&&a->IsDataLoaded&&a->Data!=null&&m!=null&&m->PrismBoxLoaded;
    }
    private static unsafe (int Held,int Free) ArmoireInventory(uint item)
    {
        var m=InventoryManager.Instance();if(m==null)throw new InvalidOperationException("Inventory unavailable.");
        var held=0;var free=0;
        foreach(var type in ArmoireContainers){
            var c=m->GetInventoryContainer(type);
            if(c==null||!c->IsLoaded||c->Items==null)throw new InvalidOperationException("Inventory or armoury is still loading.");
            for(var i=0;i<c->Size;i++){
                var s=c->Items[i];if(s.IsSymbolic)continue;
                if(item>0&&s.ItemId%1000000==item&&s.Quantity>0)held+=s.Quantity;
                if((uint)type<=3&&(s.ItemId==0||s.Quantity==0))free++;
            }
        }
        return(held,free);
    }
    private static unsafe bool ArmoireSourcePresent(ArmoirePiece p)
    {
        var m=MirageManager.Instance();if(m==null||!m->PrismBoxLoaded)return true;
        return m->PrismBoxItemIds[p.DresserSlot]==p.SourceId&&(p.SetColumn<0||m->IsSetSlotUnlocked((uint)p.DresserSlot,p.SetColumn));
    }
    private unsafe void ScanArmoire(bool queue)
    {
        if(!ArmoireDresserReady())throw new InvalidOperationException("Open the glamour dresser and wait for it to load.");
        var state=UIState.Instance();if(state==null||!state->Cabinet.IsCabinetLoaded())throw new InvalidOperationException("Armoire contents are not loaded yet. Dresser → Armoire loads them automatically.");
        LoadArmoireCatalogue();var m=MirageManager.Instance();var previews=new List<(uint,string,string)>();var pieces=new List<ArmoirePiece>();
        for(var i=0;i<m->PrismBoxItemIds.Length;i++){
            var raw=m->PrismBoxItemIds[i];var item=raw%1000000;if(item==0)continue;
            if(armoireSets!.TryGetValue(item,out var set)&&set.Any(x=>x>0)){
                for(var j=0;j<Math.Min(11,set.Length);j++)if(set[j]>0&&armoireRows!.TryGetValue(set[j],out var row)&&m->IsSetSlotUnlocked((uint)i,j))
                    pieces.Add(new(i,raw,set[j],row,j,0,0));
            }else if(armoireRows!.TryGetValue(item,out var row))pieces.Add(new(i,raw,item,row,-1,m->PrismBoxStain0Ids[i],m->PrismBoxStain1Ids[i]));
        }
        var planned=new HashSet<uint>();if(queue)armoireQueue.Clear();
        foreach(var p in pieces.OrderBy(p=>p.Dye1!=0||p.Dye2!=0)){
            var inv=ArmoireInventory(p.ItemId);var reason=ArmoireTransferPolicy.Blocked(p,state->Cabinet.IsItemInCabinet(p.CabinetId),inv.Held,inv.Free);
            if(reason.Length==0)reason=ArmoireFilter(p);
            if(reason.Length==0&&!planned.Add(p.ItemId))reason="Duplicate dresser copy";
            previews.Add((p.ItemId,DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(p.ItemId)?.Name.ExtractText()??p.ItemId.ToString(),reason.Length==0?"Ready":reason));
            if(queue&&reason.Length==0)armoireQueue.Enqueue(p);
        }
        armoirePreview=previews.ToArray();armoireStatus=$"{planned.Count} ready · {pieces.Count-planned.Count} skipped";
    }
    private string ArmoireFilter(ArmoirePiece p)
    {
        if(ArmoireExclusions().Contains(p.ItemId))return "Excluded";
        if(p.SetColumn<0?!armoireSinglePieces:!armoireOutfitPieces)return "Source filtered";
        if(armoireCategories.Count>0&&!armoireCategories.Contains(OrganizeCategory(p.ItemId)))return "Category filtered";
        return "";
    }
    private static unsafe bool ArmoireLiveDyed(ArmoirePiece p)
    {
        if(p.SetColumn>=0)return false; // Outfit mask bytes are not dye channels.
        var m=MirageManager.Instance();
        return m==null||m->PrismBoxStain0Ids[p.DresserSlot]!=0||m->PrismBoxStain1Ids[p.DresserSlot]!=0;
    }
    private static unsafe bool ArmoireRestoredSafe(uint item)
    {
        var m=InventoryManager.Instance();if(m==null)return false;
        var count=0;
        foreach(var type in ArmoireContainers){
            var c=m->GetInventoryContainer(type);if(c==null||!c->IsLoaded||c->Items==null)return false;
            for(var i=0;i<c->Size;i++){
                var s=c->Items[i];if(s.IsSymbolic||s.ItemId%1000000!=item||s.Quantity<=0)continue;
                if(type==InventoryType.EquippedItems||s.Quantity!=1||s.Condition<30000||s.Stains[0]!=0||s.Stains[1]!=0)return false;
                for(var j=0;j<5;j++)if(s.Materia[j]!=0)return false;
                count++;
            }
        }
        return count==1;
    }
    private void StartArmoireOrganizer()=>RetrievalTry(()=>{
        if(!config.EnableRetriever||!Player.IsLoaded)throw new InvalidOperationException("Enable Organizer and log in first.");
        if(armoireRunning||organizeRunning||retrievalRun.Running||chestRunning||retainerGilRunning||sellerRun.Running||TraderRunning||craftingWithdrawalPending)throw new InvalidOperationException("Finish the current inventory operation first.");
        if(!ArmoireDresserReady())throw new InvalidOperationException("Open the glamour dresser first.");
        armoireCharacter=Player.ContentId;armoireTerritory=Client.TerritoryType;armoireMoved=armoireSkipped=0;armoirePending=null;armoireQueue.Clear();
        armoireOpenedLookup=false;armoirePhase=0;armoireRunning=true;armoireNext=default;armoireDeadline=DateTimeOffset.UtcNow.AddSeconds(20);armoireStatus="Loading armoire contents…";
    });
    private unsafe void StopArmoireOrganizer(string reason)
    {
        armoireRunning=false;armoireQueue.Clear();armoireStatus=$"{reason} · {armoireMoved} stored · {armoireSkipped} skipped"+(armoirePending!=null?". Check your bags/armoury for the current piece.":"");
        armoirePending=null;
        if(armoireOpenedLookup){var a=AgentCabinetWithdraw.Instance();if(a!=null&&a->IsAgentActive())a->Hide();armoireOpenedLookup=false;}
        retrievalRefreshPending=true;
    }
    private unsafe void TickArmoireOrganizer(DateTimeOffset now)
    {
        if(!armoireRunning||now<armoireNext)return;armoireNext=now.AddMilliseconds(250);
        try{
            if(!config.EnableRetriever||!Player.IsLoaded||Player.ContentId!=armoireCharacter||Client.TerritoryType!=armoireTerritory||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||HelperInCutscene()) {StopArmoireOrganizer("Stopped: character or location changed");return;}
            if(organizeRunning||retrievalRun.Running||chestRunning||retainerGilRunning||sellerRun.Running||TraderRunning||craftingWithdrawalPending){StopArmoireOrganizer("Stopped: another inventory operation started");return;}
            if(now>armoireDeadline){StopArmoireOrganizer("Stopped: game did not confirm the transfer in time");return;}
            var state=UIState.Instance();if(state==null)return;
            var inventory=InventoryManager.Instance();if(inventory==null)return;
            foreach(var operation in inventory->PendingOperations)if(!operation.IsEmpty)return;
            if(armoirePhase==0){
                if(!state->Cabinet.IsCabinetLoaded()){
                    if(!armoireOpenedLookup){var a=AgentCabinetWithdraw.Instance();if(a==null)return;if(a->IsAgentActive())throw new InvalidOperationException("Close the existing armoire window before starting.");a->Show();armoireOpenedLookup=true;}return;
                }
                if(armoireOpenedLookup){var a=AgentCabinetWithdraw.Instance();if(a!=null)a->Hide();armoireOpenedLookup=false;}
                ScanArmoire(true);armoirePhase=1;armoireDeadline=now.AddSeconds(20);return;
            }
            if(!ArmoireDresserReady()||!state->Cabinet.IsCabinetLoaded()){StopArmoireOrganizer("Stopped: dresser closed or storage data unavailable");return;}
            if(armoirePhase==1){
                if(armoireQueue.Count==0){StopArmoireOrganizer("Finished");return;}
                var p=armoireQueue.Dequeue();var inv=ArmoireInventory(p.ItemId);
                var blocked=ArmoireTransferPolicy.Blocked(p,state->Cabinet.IsItemInCabinet(p.CabinetId),inv.Held,inv.Free);
                if(blocked.Length==0)blocked=ArmoireFilter(p);
                if(!ArmoireSourcePresent(p)||blocked.Length>0||ArmoireLiveDyed(p)){armoireSkipped++;armoireDeadline=now.AddSeconds(20);return;}
                var m=MirageManager.Instance();bool sent;
                if(p.SetColumn>=0){var mask=ArmoireTransferPolicy.RestoreMask(p.SetColumn);byte* bits=stackalloc byte[2];bits[0]=(byte)mask;bits[1]=(byte)(mask>>8);sent=m->RestorePrismBoxSetItem((uint)p.DresserSlot,bits);}
                else sent=m->RestorePrismBoxItem((uint)p.DresserSlot);
                if(!sent){armoireSkipped++;armoireDeadline=now.AddSeconds(20);return;}
                armoirePending=p;armoirePhase=2;armoireDeadline=now.AddSeconds(20);armoireStatus=$"{armoireMoved} stored · withdrawing item {p.ItemId}";return;
            }
            if(armoirePending is not {} pending)throw new InvalidOperationException("Missing transfer receipt.");
            var held=ArmoireInventory(pending.ItemId).Held;
            if(armoirePhase==2){
                if(!ArmoireTransferPolicy.RestoreConfirmed(ArmoireSourcePresent(pending),held))return;
                // Only the newly restored copy exists; never let the native armoire
                // operation consume a pre-existing equipped/armoury/bag copy.
                if(!ArmoireRestoredSafe(pending.ItemId))throw new InvalidOperationException("Restored item changed, is equipped, dyed, damaged or has materia; kept in inventory.");
                if(state->Cabinet.IsItemInCabinet(pending.CabinetId))throw new InvalidOperationException("Another operation stored this item; withdrawn copy kept.");
                if(!state->Cabinet.StoreCabinetItem(pending.CabinetId))throw new InvalidOperationException("Game declined armoire storage; withdrawn piece kept in inventory.");
                armoirePhase=3;armoireDeadline=now.AddSeconds(20);armoireStatus=$"{armoireMoved} stored · waiting for armoire confirmation";return;
            }
            if(ArmoireTransferPolicy.StoreConfirmed(state->Cabinet.IsItemInCabinet(pending.CabinetId),held)){
                armoireMoved++;armoirePending=null;armoirePhase=1;armoireDeadline=now.AddSeconds(20);armoireStatus=$"{armoireMoved} stored · {armoireQueue.Count} remaining";
            }
        }catch(Exception e){StopArmoireOrganizer("Stopped: "+e.Message);Log.Warning(e,"Armoire organizer stopped");}
    }
}
