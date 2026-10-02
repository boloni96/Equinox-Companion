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
    private int emptyCompanySamples;
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
        if (hasTag || details.FreeCompany is not null) emptyCompanySamples = 0;
        else emptyCompanySamples++;
        bool? membership = hasTag || details.FreeCompany is not null ? true : emptyCompanySamples >= 3 && Player.HomeWorld.RowId == Player.CurrentWorld.RowId ? false : null;
        var accountKey = "";
        var lobby = AgentLobby.Instance();
        if (config.PairingKey.Length == 64 && characterState->AccountId != 0 && lobby != null && lobby->IsLoggedIn &&
            lobby->ServiceAccountIndex >= 0 && (lobby->SelectedCharacterContentId == Player.ContentId || lobby->LobbyData.ContentId == Player.ContentId))
        {
            // Only a journal-scoped fingerprint leaves the game; never raw account IDs or session data.
            var identity = $"equinox-account-v1:{characterState->AccountId}:{lobby->ServiceAccountIndex}";
            accountKey = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(config.PairingKey), Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        }
        return details with { AccountKey = accountKey, Msq15Complete = msq15, FcMember = membership };
    }
}
