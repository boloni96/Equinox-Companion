using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace EquinoxCompanion;
public sealed partial class Plugin
{
    [PluginService] internal static IPartyList FollowParty { get; private set; } = null!;
    private readonly FollowThemSession followSession = new();
    private IDtrBarEntry? followBar;
    private DateTimeOffset nextFollowCheck, lastLeaderSeen, nextTeleportAttempt;
    private Vector3 lastLeaderPosition;
    private uint followTerritory;
    private ulong followLogin;
    private string followStatus = "Stopped. Choose a character, then Start.";
    private string followBarText = "";
    private bool followFault;

    private static unsafe void FollowCommand(string command)
    {
        // Only fixed game commands from this class reach this method; no names or chat input.
        var ui = UIModule.Instance();
        if (ui == null) throw new InvalidOperationException("Game command UI is unavailable.");
        using var text = new Utf8String();
        text.SetString(command);
        ui->ProcessChatBoxEntry(&text, 0, false);
    }
    private void StopFollowThem(string reason = "Stopped.")
    {
        if (followSession.Stop() == FollowAction.Stop && Objects.LocalPlayer != null)
        {
            try { FollowCommand("/automove off"); }
            catch (Exception e) { reason = "FollowThem stopped. Use a movement key to cancel game movement."; errorJournal.Record("follow-stop", "Could not send game stop command", exceptionType:e.GetType().Name); }
        }
        lastLeaderSeen = default;
        receivedPortal = null;
        relayGeneration++;
        followStatus = reason;
        RefreshFollowBar();
    }
    private void ToggleFollowThem()
    {
        if (followSession.Armed) { StopFollowThem(); return; }
        if (string.IsNullOrWhiteSpace(config.FollowThem.TargetName) || config.FollowThem.HomeWorld == 0)
        { followStatus = "Choose a party member or friend first."; return; }
        if (Objects.LocalPlayer == null) { followStatus = "Log in before starting FollowThem."; return; }
        followFault = false;
        followSession.Arm(); followArmedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); relayGeneration++; nextFollowCheck = default;
        followStatus = "Looking for the selected character nearby.";
    }
    private void RefreshFollowBar()
    {
        if (!config.EnableFollowThem) { followBar?.Remove(); followBar = null; followBarText = ""; return; }
        followBar ??= QuickLootBar.Get("Equinox FollowThem");
        followBar.Shown = true;
        var label = followSession.Phase switch
        {
            FollowPhase.Following => config.FollowThem.TargetName,
            FollowPhase.Waiting => "WAITING", FollowPhase.Loading => "LOADING", _ => "STOPPED"
        };
        var text = "FollowThem: " + label;
        if (text != followBarText) { followBar.Text = new SeStringBuilder().AddText(text).Build(); followBarText = text; }
        followBar.Tooltip = new SeStringBuilder().AddText(followStatus + (followSession.Armed ? " Click to stop." : " Click to start.")).Build();
        followBar.OnClick = _ => ToggleFollowThem();
    }
    private unsafe void UpdateFollowThem(DateTimeOffset now)
    {
        if (!config.EnableFollowThem) return;
        if (now < nextFollowCheck) return;
        nextFollowCheck = now.AddMilliseconds(250);
        try
        {
            if (followFault) return;
            RefreshFollowBar();
            var login = Player.IsLoaded ? Player.ContentId : 0;
            if (followLogin != login)
            {
                if (followLogin != 0) StopFollowThem("Stopped after logout or character change.");
                followLogin = login;
            }
            if (!followSession.Armed) return;
            var input = InputManager.Instance();
            if (input != null && (input->GetInputStatus(InputCode.MOVE_FORE) || input->GetInputStatus(InputCode.MOVE_BACK) ||
                input->GetInputStatus(InputCode.MOVE_LEFT) || input->GetInputStatus(InputCode.MOVE_RIGHT) ||
                input->GetInputStatus(InputCode.MOVE_STRIFE_L) || input->GetInputStatus(InputCode.MOVE_STRIFE_R)))
            { StopFollowThem("Stopped by your movement input."); return; }
            var loading = Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51];
            if (loading) { followSession.Observe(true, login != 0, true, false, true); followStatus = "Loading; waiting to find your selected character."; return; }
            if (followTerritory != Client.TerritoryType)
            {
                followTerritory = Client.TerritoryType; lastLeaderSeen = default;
                followSession.Observe(true, login != 0, true, false, true);
            }
            var settings = config.FollowThem;
            var target = Objects.OfType<IPlayerCharacter>().FirstOrDefault(p =>
                p.GameObjectId != Objects.LocalPlayer?.GameObjectId &&
                FollowThemSession.Matches(settings.TargetName, settings.HomeWorld, p.Name.TextValue, p.HomeWorld.RowId));
            var self = Objects.LocalPlayer;
            var nearby = target != null && self != null && target.IsTargetable && Vector3.Distance(self.Position, target.Position) <= 30;
            if (nearby) { lastLeaderPosition = target!.Position; lastLeaderSeen = now; lastLeaderEntity = target.GameObjectId.ToString(); }
            var blocked = Conditions[ConditionFlag.OccupiedInCutSceneEvent] || Conditions[ConditionFlag.WatchingCutscene] || Conditions[ConditionFlag.Unconscious] || Conditions[ConditionFlag.InCombat];
            if (blocked) { StopFollowThem("Stopped during combat, a cutscene or incapacitation. Start again when ready."); return; }
            TryFollowTeleport(now);

            var action = followSession.Observe(true, login != 0, false, nearby, settings.ResumeNearby);
            if (action == FollowAction.Stop && self != null) FollowCommand("/automove off");
            if (action == FollowAction.Start)
            {
                var previous = Targets.Target;
                try { Targets.Target = target; FollowCommand("/follow <t>"); }
                finally { Targets.Target = previous; }
            }
            followStatus = followSession.Phase switch
            {
                FollowPhase.Following => "Following " + settings.TargetName + " using the game's simple follow. No obstacle navigation.",
                FollowPhase.Waiting => "Your selected character isn’t nearby. FollowThem is waiting.",
                _ => "Your selected character isn’t nearby. FollowThem stopped; press Start to try again."
            };
            RefreshFollowBar();
        }
        catch (Exception e)
        {
            try { StopFollowThem(); } catch { followSession.Stop(); }
            followFault = true;
            followStatus = "FollowThem paused after an error. Use movement keys to cancel game follow; press Start to retry.";
            errorJournal.Record("followthem", "FollowThem update failed", exceptionType: e.GetType().Name);
            RefreshFollowBar();
        }
    }
    private unsafe void TryFollowTeleport(DateTimeOffset now)
    {
        if (!config.FollowThem.AcceptPartyTeleports || FollowParty.Length < 2 || now < nextTeleportAttempt) return;
        var telepo = Telepo.Instance();
        if (telepo == null || !telepo->ActiveTeleportRequest) return;
        var addon = (AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        if (addon == null || !addon->IsVisible || addon->PromptText == null) return;
        var prompt = addon->PromptText->NodeText.ToString();
        if (!FollowThemSession.IsPartyTeleportPrompt(prompt)) return;
        nextTeleportAttempt = now.AddSeconds(5);
        addon->FireCallbackInt(0);
    }
    private unsafe void DrawFollowThem()
    {
        ImGui.TextWrapped(followStatus);
        if (ImGui.Button(followSession.Armed ? "Stop FollowThem" : "Start FollowThem")) ToggleFollowThem();
        ImGui.TextWrapped("Local settings. Uses simple game follow; obstacles, mounting and flying require your help. Party teleports need an open English confirmation. Shared portals require Journal V7.11.74 on Cloudflare. Only supported Warp/Exit portals are relayed; destination menus are not guessed.");
        var selected = config.FollowThem.TargetName.Length == 0 ? "Choose a party member or friend" : config.FollowThem.TargetName + " @ " + FollowWorldName(config.FollowThem.HomeWorld);
        if (ImGui.BeginCombo("Character", selected))
        {
            ImGui.TextDisabled("Current party");
            foreach (var member in FollowParty)
                if (member.ContentId != Player.ContentId) FollowCandidate(member.Name.TextValue, member.World.RowId, "party");
            ImGui.Separator(); ImGui.TextDisabled("Friends — open the game's Friends List to load/refresh it");
            var friends = InfoProxyFriendList.Instance();
            if (friends != null && friends->CharData != null && friends->EntryCount <= 200)
                foreach (var friend in friends->CharDataSpan) FollowCandidate(friend.NameString, friend.HomeWorld, "friend");
            ImGui.EndCombo();
        }
        MessageToggle("Resume when the selected character comes nearby", config.FollowThem.ResumeNearby, v => config.FollowThem.ResumeNearby = v);
        MessageToggle("Accept party teleport offers while FollowThem is started", config.FollowThem.AcceptPartyTeleports, v => config.FollowThem.AcceptPartyTeleports = v);
        MessageToggle("Share my portal transitions with paired Companions", config.FollowThem.SharePortalTransitions, v => config.FollowThem.SharePortalTransitions = v);
        MessageToggle("Use my selected character's shared portal (experimental)", config.FollowThem.UseSharedPortals, v => { config.FollowThem.UseSharedPortals = v; receivedPortal=null; relayGeneration++; });
        ImGui.TextWrapped(portalRelayStatus);
        ImGui.TextWrapped("Leader: enable Share my portal transitions; no need to start following anyone. Follower: select that character, enable Use shared portal, then Start. Both need the same pairing key. The instruction expires after 15 seconds and never enters Journal history. You must already be within three yalms. A matching confirmation can be accepted; destination lists and unsupported portals remain manual.");
    }
    private string FollowWorldName(uint id) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(id)?.Name.ToString() ?? id.ToString();
    private void FollowCandidate(string name, uint world, string source)
    {
        if (string.IsNullOrWhiteSpace(name) || world == 0) return;
        if (ImGui.Selectable(name + " @ " + FollowWorldName(world) + "##" + source + world + name))
        {
            StopFollowThem("Selected " + name + ". Press Start when ready.");
            config.FollowThem.TargetName = name; config.FollowThem.HomeWorld = world; Pi.SavePluginConfig(config);
        }
    }
}
