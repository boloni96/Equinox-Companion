using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record TravelSuggestion(string Command,string Label,string Kind);
    private TravelSuggestion[] travelSuggestions=[];
    private string travelSuggestionInput="";
    private DateTimeOffset travelSuggestionRefresh;
    private ulong travelSuggestionCharacter;
    private static readonly string[] EstateSuggestionCommands=["priv","fc","epriv","efc","workshop","ws","shared","shared2","shared3","sha","sha2","sha3","eshared","eshared2","eshared3","esha","esha2","esha3","stop"];
    private unsafe void DrawTravelCommandSuggestions()
    {
        if(!config.TravelCommandSuggestions||!Player.IsLoaded)return;
        var chat=(AddonChatLog*)GardenGui.GetAddonByName("ChatLog").Address;
        if(chat==null||!chat->IsVisible||chat->TextInput==null||!chat->TextInput->IsActive)return;
        var field=chat->TextInput;
        if(field->RawString.BufUsed>256)return;
        var input=field->RawString.ToString();
        if(!input.StartsWith("/et ",StringComparison.OrdinalIgnoreCase))return;
        var now=DateTimeOffset.UtcNow;
        if(input!=travelSuggestionInput||now>=travelSuggestionRefresh||travelSuggestionCharacter!=Player.ContentId){
            travelSuggestionInput=input;travelSuggestionCharacter=Player.ContentId;travelSuggestionRefresh=now.AddSeconds(2);
            travelSuggestions=FindTravelSuggestions(input[4..]);
        }
        if(travelSuggestions.Length==0)return;
        var vp=ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos+new Vector2(12,vp.WorkSize.Y-100),ImGuiCond.Always,new Vector2(0,1));
        ImGui.SetNextWindowSizeConstraints(new Vector2(250,0),new Vector2(520,Math.Max(150,vp.WorkSize.Y-120)));
        if(ImGui.Begin("Equinox Travel suggestions",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing|ImGuiWindowFlags.NoNav|ImGuiWindowFlags.NoMove)){
            for(var i=0;i<travelSuggestions.Length;i++){
                var item=travelSuggestions[i];
                var key=(VirtualKey)(0x31+i);
                var keyboard=i<9&&Keys[VirtualKey.CONTROL]&&Keys[key];
                if(ImGui.Selectable($"{(i<9?$"[{i+1}] ":"")}{item.Label} [{item.Kind}]##suggest{i}")||keyboard){
                    if(keyboard)Keys[key]=false;
                    // Complete only this still-displayed /et input. Never submit or inspect unrelated chat.
                    if(field->RawString.ToString()==input)field->SetText(item.Command);
                    break;
                }
            }
            ImGui.TextDisabled("Click or Ctrl+1–9 to complete · Enter to travel");
        }
        ImGui.End();
    }
    private unsafe TravelSuggestion[] FindTravelSuggestions(string input)
    {
        var query=TeleportNameSearch.Query(input);var worldOnly=query.StartsWith("w ",StringComparison.OrdinalIgnoreCase)||query.StartsWith("world ",StringComparison.OrdinalIgnoreCase);
        var tpOnly=query.StartsWith("tp ",StringComparison.OrdinalIgnoreCase);
        if(worldOnly||tpOnly)query=query[(query.IndexOf(' ')+1)..].Trim();
        var results=new List<TravelSuggestion>();
        if(!worldOnly&&!tpOnly)foreach(var command in EstateSuggestionCommands.Where(x=>x.Contains(query,StringComparison.OrdinalIgnoreCase)))
            results.Add(new("/et "+command,"/et "+command,"Shortcut"));
        if(!tpOnly){
            EnsureBidWorlds();
            foreach(var world in bidWorlds!.Where(w=>w.Name.Contains(query,StringComparison.OrdinalIgnoreCase)))
                results.Add(new("/et w "+world.Name,"/et w "+world.Name,"World · "+world.Dc));
        }
        if(!worldOnly){
            var telepo=Telepo.Instance();
            if(telepo!=null){
                // Cached native unlock list; no refresh/network work on every keystroke.
                var sheet=DataManager.GetExcelSheet<Aetheryte>();
                foreach(var entry in telepo->TeleportList){
                    if(entry.SubIndex!=0||entry.Ward!=0||entry.Plot!=0||sheet.GetRowOrDefault(entry.AetheryteId) is not {} row||!row.IsAetheryte)continue;
                    var name=row.PlaceName.Value.Name.ToString();var map=row.Territory.Value.PlaceName.Value.Name.ToString();
                    if(name.Contains(query,StringComparison.OrdinalIgnoreCase)||map.Contains(query,StringComparison.OrdinalIgnoreCase))
                        results.Add(new("/et tp #"+row.RowId,"/et "+name,"Teleport"));
                }
            }
        }
        return results.DistinctBy(x=>x.Command).OrderByDescending(x=>x.Label.EndsWith(query,StringComparison.OrdinalIgnoreCase)).ThenBy(x=>x.Label,StringComparer.OrdinalIgnoreCase).Take(12).ToArray();
    }
}
