using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private bool retainerGilRunning;private RetrieverPort? retainerGilPort;
    private ulong[] retainerGilTargets=[];private int retainerGilIndex,retainerGilStage;
    private uint retainerGilBefore,retainerGilAmount;private ulong retainerGilTotal;
    private DateTimeOffset retainerGilNext,retainerGilDeadline;
    private string retainerGilButton="";private string retainerGilStatus="";private bool retainerGilOpened;private uint retainerGilSource;
    private void ObserveRetainerBankInput(AddonEvent type,AddonArgs args){if(retainerGilRunning&&args is AddonReceiveEventArgs ev&&(AtkEventType)ev.AtkEventType is AtkEventType.ButtonClick or AtkEventType.MouseClick)StopRetainerGil("Stopped: you interacted with the gil window. No further transfer sent.");}
    private static unsafe uint? ReadRetainerBankSource(AtkUnitBase* bank)
    {
        if(bank==null||bank->UldManager.NodeListCount<=27)return null;
        var node=bank->UldManager.NodeList[27];if(node==null||node->Type!=NodeType.Text)return null;
        var text=TravelMenuText(((AtkTextNode*)node)->NodeText.StringPtr);
        return uint.TryParse(new string((text??"").Where(char.IsAsciiDigit).ToArray()),out var value)?value:null;
    }
    private void StopRetainerGil(string reason){retainerGilRunning=false;retainerGilPort?.Release();retainerGilStatus=reason;}
    private unsafe void StartRetainerGil()
    {
        RetrievalTry(()=>{
            if(craftingWithdrawalPending||organizeRunning||retrievalRun.Running||chestRunning||sellerRun.Running||TraderRunning||retainerGilRunning)throw new InvalidOperationException("Finish the current operation first.");
            var rm=RetainerManager.Instance();var active=rm==null?0:rm->LastSelectedRetainerId;
            if(!Player.IsLoaded||rm==null||(SellerAddon("RetainerList")==null&&!RetrieverActive(active)))throw new InvalidOperationException("Open your retainer list or a retainer first.");
            if(SellerAddon("Bank")!=null)throw new InvalidOperationException("Close the existing gil window first.");
            retainerGilTargets=RetrieverActive(active)?[active]:ReadSellerRetainers().Select(x=>x.Id).ToArray();
            if(retainerGilTargets.Length==0)throw new InvalidOperationException("No retainers available.");

            retainerGilPort=new(this){Character=Player.ContentId,World=Player.CurrentWorld.RowId};retainerGilIndex=0;retainerGilTotal=0;retainerGilRunning=true;GilStage(0);
        });
    }
    private void GilStage(int stage){if(stage==1)retainerGilOpened=false;retainerGilStage=stage;retainerGilDeadline=DateTimeOffset.UtcNow.AddSeconds(30);retainerGilStatus=$"Withdraw gil · {retainerGilIndex+1}/{retainerGilTargets.Length} retainers · {retainerGilTotal:N0} received";}
    private unsafe void TickRetainerGil(DateTimeOffset now)
    {
        if(retainerGilPort?.Pause.RestorePending==true&&!retainerGilRunning)retainerGilPort.Pause.Release(now);
        if(!retainerGilRunning||now<retainerGilNext)return;retainerGilNext=now.AddMilliseconds(300);
        try{
            if(retainerGilPort==null||!retainerGilPort.ContextMatches){StopRetainerGil("Stopped: retainer context changed.");return;}
            if(now>retainerGilDeadline){StopRetainerGil("Stopped: gil step "+retainerGilStage+" was not confirmed. No retry.");return;}
            if(retainerGilStage==0){if(retainerGilPort.Prepare())GilStage(1);return;}
            var pause=retainerGilPort.Pause.Check(now);if(pause.Error.Length>0)throw new InvalidOperationException(pause.Error);if(!pause.Ready)return;
            var id=retainerGilTargets[retainerGilIndex];var inv=InventoryManager.Instance();if(inv==null)return;
            switch(retainerGilStage){
                case 1:if(retainerGilPort.Select(id))GilStage(2);break;
                case 2:
                    if(SellerAddon("InventoryRetainer")!=null||SellerAddon("InventoryRetainerLarge")!=null){var agent=AgentRetainer.Instance();if(agent!=null)agent->Hide();break;}
                    var bank=SellerAddon("Bank");if(bank!=null){GilStage(3);break;}
                    var menu=(AddonSelectString*)SellerAddon("SelectString");if(menu==null){SellerRetainerTalk(id);break;}
                    var label=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Addon>().GetRow(2379).Text.ExtractText();
                    for(var i=0;i<Math.Min(20,(int)menu->PopupMenu.PopupMenu.EntryCount);i++)if(RetainerMenuPolicy.Matches(TravelMenuText(menu->PopupMenu.PopupMenu.EntryNames[i].Value),label)){SelectTravelChoice((AtkUnitBase*)menu,i);retainerGilOpened=true;retainerGilNext=now.AddSeconds(1);break;}break;
                case 3:
                    bank=SellerAddon("Bank");if(bank==null||!RetrieverActive(id)||!retainerGilOpened)break;
                    var button=bank->GetComponentButtonById(35);if(button==null||button->ButtonTextNode==null)break;
                    var text=TravelMenuText(button->ButtonTextNode->NodeText.StringPtr);retainerGilButton=text??"";

                    var rm=RetainerManager.Instance();var ret=rm==null?null:rm->GetActiveRetainer();if(ret==null||ret->RetainerId!=id)break;
                    if(ReadRetainerBankSource(bank)!=ret->Gil){retainerGilStatus="Waiting for the retainer gil balance to match.";break;}
                    retainerGilSource=ret->Gil;retainerGilBefore=inv->GetGil();retainerGilAmount=(uint)Math.Min(ret->Gil,Math.Max(0L,999999999L-retainerGilBefore));
                    if(retainerGilAmount==0){bank->Close(true);GilStage(5);break;}
                    var args=stackalloc AtkValue[2];args[0]=new(){Type=AtkValueType.Int,Int=3};args[1]=new(){Type=AtkValueType.UInt,UInt=retainerGilAmount};bank->FireCallback(2,args);GilStage(4);break;
                case 4:
                    bank=SellerAddon("Bank");if(bank==null||!RetrieverActive(id))break;
                    button=bank->GetComponentButtonById(35);var input=bank->GetComponentNodeById(32);if(button==null||input==null||!button->IsEnabled)break;
                    if(!retainerGilOpened||ReadRetainerBankSource(bank)!=retainerGilSource)throw new InvalidOperationException("Gil source changed before confirmation.");
                    var numeric=input->GetAsAtkComponentNumericInput();if(numeric==null||numeric->Value!=retainerGilAmount)throw new InvalidOperationException("Gil amount changed before confirmation.");
                    if(inv->GetGil()!=retainerGilBefore)throw new InvalidOperationException("Your gil changed before confirmation. Nothing transferred.");
                    var confirm=stackalloc AtkValue[2];confirm[0]=new(){Type=AtkValueType.Int,Int=0};confirm[1]=default;bank->FireCallback(2,confirm);GilStage(6);break;
                case 6:
                    if(inv->GetGil()!=retainerGilBefore+retainerGilAmount)break;
                    retainerGilTotal+=retainerGilAmount;bank=SellerAddon("Bank");if(bank!=null)bank->Close(true);GilStage(5);break;
                case 5:
                    if(!retainerGilPort.Close(id))break;
                    if(++retainerGilIndex==retainerGilTargets.Length){StopRetainerGil($"Complete · {retainerGilTotal:N0} gil received.");break;}GilStage(1);break;
            }
        }catch(Exception e){StopRetainerGil("Stopped: "+e.Message);}
    }
}
