using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private unsafe bool TryNamedTeleport(string query,bool explicitTeleport)
    {
        if(!Player.IsLoaded){if(explicitTeleport)FollowChatNotice("TRAVEL — Wait until your character has loaded.");return explicitTeleport;}
        var telepo=Telepo.Instance();if(telepo==null)return false;
        try{
            telepo->UpdateAetheryteList();
            var local=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>();
            var english=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Aetheryte>(Dalamud.Game.ClientLanguage.English);
            var places=new List<TeleportName>();
            foreach(var entry in telepo->TeleportList){
                if(entry.SubIndex!=0||entry.Ward!=0||entry.Plot!=0||entry.AetheryteId==0)continue;
                if(local.GetRowOrDefault(entry.AetheryteId) is not {} row||!row.IsAetheryte)continue;
                var en=english.GetRowOrDefault(entry.AetheryteId);
                places.Add(new(entry.AetheryteId,row.PlaceName.Value.Name.ToString(),row.Territory.Value.PlaceName.Value.Name.ToString(),
                    en?.PlaceName.Value.Name.ToString()??"",en?.Territory.Value.PlaceName.Value.Name.ToString()??"",entry.GilCost));
            }
            var matches=TeleportNameSearch.Matches(places,query);
            if(matches.Length==0){if(explicitTeleport)FollowChatNotice("TRAVEL — No unlocked teleport matches '"+query+"'. Use an aetheryte or map name.");return explicitTeleport;}
            if(matches.Length>1){
                FollowChatNotice($"TRAVEL — {matches.Length} teleports match '{query}'. Use a longer name or one of these commands:");
                foreach(var match in matches.Take(10))FollowChatNotice($"/et #{match.Id} — {match.Name} · {match.Map} · {match.Cost:N0} gil");
                if(matches.Length>10)FollowChatNotice("TRAVEL — More matches exist; narrow the name to see them.");return true;
            }
            var destination=matches[0];
            if(Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Casting]||HelperInCutscene()||QuestConversationVisible()||VisibleFollowAddon("SelectYesno")||telepo->ActiveTeleportRequest){
                FollowChatNotice("TRAVEL — Finish the current loading, combat, duty, cast or conversation first.");return true;
            }
            if(nativeWorldTrip!=null||LifestreamBusy()||estateTrip!=null||estateHomeReturn!=null||followSession.Armed||independentMoving||independentSearch!=null||independentQuestId!=0||assignedLogin!=null||eventAccountActive||gardenTendRoute!=null||shoppingTravel!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null||keyboundBuying){
                FollowChatNotice("TRAVEL — Stop the active routine or journey first.");return true;
            }
            // Revalidate against the live unlocked list; never send a guessed or locked ID.
            foreach(var entry in telepo->TeleportList){
                if(entry.AetheryteId!=destination.Id||entry.SubIndex!=0||entry.Ward!=0||entry.Plot!=0)continue;
                var accepted=telepo->Teleport(entry.AetheryteId,entry.SubIndex);
                FollowChatNotice(accepted?"TRAVEL — Teleporting to "+destination.Name+" · "+destination.Map+".":"TRAVEL — The game refused the teleport to "+destination.Name+".");return true;
            }
            FollowChatNotice("TRAVEL — Destination is no longer available.");return true;
        }catch(Exception e){Log.Warning(e,"Named teleport request failed");FollowChatNotice("TRAVEL — Could not read or request that teleport. Try again once the game has finished loading.");return true;}
    }
}
