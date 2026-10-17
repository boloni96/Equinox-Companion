using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record CharacterSyncStatus(string Character, string HomeWorld, bool Enabled,
        DateTimeOffset? ObservedAt, string GrandCompany, bool Acknowledged, string State);
    private DateTimeOffset nextCharacterSyncStatus;
    private CharacterSyncStatus? characterSyncStatus;
    private CharacterSyncStatus ReadCharacterSyncStatus()
    {
        var id = Player.ContentId.ToString();
        var last = Player.IsLoaded ? config.Discoveries.LastOrDefault(e => e.Kind == "character.updated" && e.Actor.ContentId == id) : null;
        var enabled = config.SyncEnabled && config.SyncCharacterDetails;
        var acknowledged = last is not null && config.SentEvents.Contains(last.Id);
        var state = !Player.IsLoaded ? "Log in to observe character details."
            : !enabled ? "Character upload is disabled in Settings."
            : config.PairingKey.Length != 64 ? "Pair Companion to upload character details."
            : faulted ? "Observation paused after an error; see Diagnostics."
            : last is null ? "Waiting for a complete character observation."
            : !SyncValidation.CanSend(last, DateTimeOffset.UtcNow) ? SyncValidation.HoldReason(last, DateTimeOffset.UtcNow)
            : acknowledged ? "Server acknowledged this record. Journal application is separate."
            : "Recorded locally; waiting for upload.";
        return new(Player.CharacterName, last?.Actor.HomeWorldName ?? "", enabled, last?.At,
            last?.Character?.GrandCompany ?? "", acknowledged, state);
    }
    private void DrawCharacterSyncStatus()
    {
        var now = DateTimeOffset.UtcNow;
        if(characterSyncStatus is null || now >= nextCharacterSyncStatus)
        { characterSyncStatus = ReadCharacterSyncStatus(); nextCharacterSyncStatus = now.AddSeconds(1); }
        var s = characterSyncStatus;
        ImGui.Separator();
        ImGui.TextWrapped("Character sync: " + s.Character + (s.HomeWorld.Length > 0 ? " @ " + s.HomeWorld : ""));
        ImGui.TextWrapped(s.State);
        if(s.ObservedAt is {} at) ImGui.TextWrapped("Last character record: " + DisplayTime.Local(at));
        if(s.GrandCompany.Length > 0) ImGui.TextWrapped("Recorded Grand Company / Rank: " + s.GrandCompany);
    }
}
