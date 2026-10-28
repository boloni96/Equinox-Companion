using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record RetainerNavigationEntry(DateTimeOffset At,ulong Retainer,string State);
    private readonly Queue<RetainerNavigationEntry> retainerNavigation=[];
    private void TraceRetainerNavigation(string state){var id=CurrentRetainerIdentity();if(retainerNavigation.LastOrDefault() is {} last&&last.Retainer==id&&last.State==state)return;retainerNavigation.Enqueue(new(DateTimeOffset.UtcNow,id,state));while(retainerNavigation.Count>60)retainerNavigation.Dequeue();}
    private static unsafe ulong CurrentRetainerIdentity(){var rm=RetainerManager.Instance();return rm==null?0:rm->LastSelectedRetainerId;}
    private static unsafe bool RetrieverActive(ulong id)
    {
        if(SellerActive(id))return true;
        var rm=RetainerManager.Instance();
        if(id==0||rm==null||!rm->IsReady||rm->LastSelectedRetainerId!=id||!Conditions[ConditionFlag.OccupiedSummoningBell]||SellerAddon("RetainerList")!=null)return false;
        var current=rm->GetActiveRetainer();if(current==null||current->RetainerId!=id)return false;
        var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu==null)return false;
        var entries=new List<string>();for(var i=0;i<Math.Min(20,(int)menu->PopupMenu.PopupMenu.EntryCount);i++)entries.Add(TravelMenuText(menu->PopupMenu.PopupMenu.EntryNames[i].Value)??"");
        var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>();
        // Inventory ownership may not be populated until inventory is opened. Verify
        // the selected native retainer and both distinctive menu actions instead.
        return RetainerMenuPolicy.SelectedMenu(id,rm->LastSelectedRetainerId,current->RetainerId,true,entries,sheet.GetRow(2378).Text.ExtractText(),sheet.GetRow(2379).Text.ExtractText());
    }
}
