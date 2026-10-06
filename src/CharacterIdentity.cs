using System.Security.Cryptography;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game;
using NativeCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private DateTimeOffset characterReadyAt;
    private static string Ordinal(int n) => n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
    private unsafe CharacterDetails AddIdentityAndProgress(CharacterDetails details)
    {
        var local = Objects.LocalPlayer;
        var state = UIState.Instance();
        if (local is null || state == null || !state->PlayerState.IsLoaded || DateTimeOffset.UtcNow < characterReadyAt) return details;
        var characterState = (NativeCharacter*)local.Address;
        if (characterState == null || characterState->ContentId != Player.ContentId) return details;
        // Completed level-15 MSQ: the two game-data variants of It's Probably Pirates.
        var msq15 = QuestManager.IsQuestComplete(65781) || QuestManager.IsQuestComplete(66211);
        var hasTag = !string.IsNullOrWhiteSpace(local.CompanyTag.TextValue);
        // Missing tag/proxy data is unknown, not evidence of leaving an FC.
        bool? membership = hasTag || details.FreeCompany is not null ? true : null;
        var accountKey = "";
        var lobby = AgentLobby.Instance();
        // The loaded local player above proves the active character. Character-
        // selection IDs belong to the lobby UI and are not an in-world identity gate.
        if (config.PairingKey.Length == 64 && characterState->AccountId != 0 && lobby != null && lobby->IsLoggedIn &&
            lobby->ServiceAccountIndex >= 0)
        {
            // Only a journal-scoped fingerprint leaves the game; never raw account IDs or session data.
            var identity = $"equinox-account-v1:{characterState->AccountId}:{lobby->ServiceAccountIndex}";
            accountKey = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(config.PairingKey), Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        }
        var ps = &state->PlayerState;
        var lang = Dalamud.Game.ClientLanguage.English;
        var nameday = ps->BirthMonth is >= 1 and <= 12 && ps->BirthDay is >= 1 and <= 32
            ? $"{Ordinal(ps->BirthDay)} Sun of the {Ordinal((ps->BirthMonth + 1) / 2)} {(ps->BirthMonth % 2 == 1 ? "Astral" : "Umbral")} Moon" : "";
        var guardian = ps->GuardianDeity > 0 ? DataManager.GetExcelSheet<Lumina.Excel.Sheets.GuardianDeity>(lang).GetRowOrDefault(ps->GuardianDeity)?.Name.ToString() ?? "" : "";
        var town = ps->StartTown > 0 ? DataManager.GetExcelSheet<Lumina.Excel.Sheets.Town>(lang).GetRowOrDefault(ps->StartTown)?.Name.ToString() ?? "" : "";
        var gc = ps->GrandCompany;
        var company = gc == 0 && nameday.Length > 0 ? "None" : gc is >= 1 and <= 3 ? DataManager.GetExcelSheet<Lumina.Excel.Sheets.GrandCompany>(lang).GetRowOrDefault(gc)?.Name.ToString() ?? "" : "";
        if (gc is >= 1 and <= 3)
        {
            var rank = ps->GetGrandCompanyRank();
            if (rank > 0)
            {
                var rankName = (gc, Player.Sex == 0) switch
                {
                    (1, true) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.GCRankLimsaMaleText>(lang).GetRowOrDefault(rank)?.NameRank.ToString(),
                    (1, false) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.GCRankLimsaFemaleText>(lang).GetRowOrDefault(rank)?.NameRank.ToString(),
                    (2, true) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.GCRankGridaniaMaleText>(lang).GetRowOrDefault(rank)?.NameRank.ToString(),
                    (2, false) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.GCRankGridaniaFemaleText>(lang).GetRowOrDefault(rank)?.NameRank.ToString(),
                    (3, true) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.GCRankUldahMaleText>(lang).GetRowOrDefault(rank)?.NameRank.ToString(),
                    (3, false) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.GCRankUldahFemaleText>(lang).GetRowOrDefault(rank)?.NameRank.ToString(),
                    _ => null
                };
                company += " / " + (string.IsNullOrWhiteSpace(rankName) ? "Rank " + rank : rankName);
            }
        }
        return details with { AccountKey = accountKey, Msq15Complete = msq15, FcMember = membership, Nameday = nameday, Guardian = guardian, CityState = town, GrandCompany = company };
    }
}
