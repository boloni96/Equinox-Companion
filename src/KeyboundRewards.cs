using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Bindings.ImGui;
using EquinoxCompanion.Keybound;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset keyboundMgcPollAt,keyboundResultsReadyAt;
    private uint? keyboundMgc,keyboundMgcLimit,keyboundRoundMgc,keyboundLastMgcChange;
    private ulong keyboundMgcCharacter;
    private uint keyboundRewardTerritory;
    private string[] keyboundLastResults=[];
    private unsafe void UpdateKeyboundRewards(IFramework framework)
    {
        if(!config.EnableKeyboundBrawler||(!keyboundEnabled&&!keyboundWindow.IsOpen))return;
        var now=DateTimeOffset.UtcNow;if(now<keyboundMgcPollAt)return;keyboundMgcPollAt=now.AddSeconds(1);
        var character=Player.ContentId;
        if(!Client.IsLoggedIn||character==0||character!=keyboundMgcCharacter){
            keyboundMgc=keyboundMgcLimit=keyboundRoundMgc=keyboundLastMgcChange=null;
            keyboundResultsReadyAt=default;keyboundRewardTerritory=0;keyboundLastResults=[];keyboundMgcCharacter=character;keyboundEarnings=new();
            if(!Client.IsLoggedIn||character==0)return;
        }
        try{
            var currencies=CurrencyManager.Instance();
            if(currencies==null||!currencies->HasItem(52322)){keyboundMgc=keyboundMgcLimit=null;return;}
            keyboundMgc=currencies->GetItemCount(52322);
            var cap=currencies->GetItemMaxCount(52322);keyboundMgcLimit=cap>0?cap:null;
            var results=KeyboundAddon("KTGResult");
            if(Client.TerritoryType==1359&&keyboundRewardTerritory!=1359){
                keyboundRoundMgc=results==null?keyboundMgc:null;keyboundLastMgcChange=null;
            }
            keyboundRewardTerritory=Client.TerritoryType;
            if(keyboundRoundMgc.HasValue)keyboundLastMgcChange=keyboundMgc>=keyboundRoundMgc?keyboundMgc-keyboundRoundMgc:null;
            if(results!=null){
                if(keyboundResultsReadyAt==default)keyboundResultsReadyAt=now;
                keyboundLastResults=KeyboundUi.Texts("KTGResult").Select(t=>t.Text).Where(t=>t.Length>0).Distinct().Take(64).Select(t=>t.Length>256?t[..256]:t).ToArray();
            }else keyboundResultsReadyAt=default;
            ObserveKeyboundShopping(results!=null);
        }catch(Exception ex){keyboundMgc=keyboundMgcLimit=null;keyboundMgcPollAt=now.AddSeconds(10);Log.Warning(ex,"Could not read Keybound MGC");}
    }
    private void DrawKeyboundRewards()
    {
        if(!keyboundMgc.HasValue){ImGui.TextDisabled("MGC: waiting for game balance");return;}
        ImGui.TextUnformatted("MGC: "+keyboundMgc.Value.ToString("N0")+(keyboundMgcLimit.HasValue?" / "+keyboundMgcLimit.Value.ToString("N0"):""));
        if(keyboundMgcLimit.HasValue&&keyboundMgc>=keyboundMgcLimit)ImGui.TextWrapped("MGC at capacity. More credits cannot increase this balance.");
        else if(keyboundLastMgcChange.HasValue&&ImGui.IsItemHovered())ImGui.SetTooltip("Balance change since round start: +"+keyboundLastMgcChange.Value.ToString("N0")+" MGC");
    }
}
