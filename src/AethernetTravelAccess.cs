using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    // Read the current character, never a leader's or another account's unlocks.
    private unsafe bool? AethernetUnlocked(uint id){
        var state=UIState.Instance();
        return id==0||!Player.IsLoaded||state==null||!state->PlayerState.IsLoaded?null:state->IsAetheryteUnlocked(id);
    }
    private uint ResolveAethernetDestination(string name,uint source=0){
        var local=DataManager.GetExcelSheet<Aetheryte>();
        var english=DataManager.GetExcelSheet<Aetheryte>(Dalamud.Game.ClientLanguage.English);
        var group=local.GetRowOrDefault(source)?.AethernetGroup??0;
        var matches=local.Where(a=>a.AethernetGroup!=0&&(group==0||a.AethernetGroup==group)&&
            (AethernetTravelPolicy.NameMatches(a.AethernetName.Value.Name.ToString(),name)||
             AethernetTravelPolicy.NameMatches(english.GetRow(a.RowId).AethernetName.Value.Name.ToString(),name)))
            .Select(a=>a.RowId).Distinct().Take(2).ToArray();
        return matches.Length==1?matches[0]:0;
    }
    private bool RequestUnlockedAethernet(uint id,out string reason){
        var unlocked=AethernetUnlocked(id);
        if(unlocked!=true){reason=unlocked==false?"This character has not attuned to the destination shard.":"Aethernet destination or character unlock data is not verified.";return false;}
        if(!Pi.GetIpcSubscriber<uint,bool>("Lifestream.AethernetTeleportById").InvokeFunc(id)){reason="Aethernet request was refused.";return false;}
        reason="";return true;
    }
}
