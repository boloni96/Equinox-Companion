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
    private readonly FollowFlightGate followFlight = new();
    private readonly FollowStuckWatch followStuck = new();
    private DateTimeOffset followRetryAt;
    private IDtrBarEntry? followBar;
    private DateTimeOffset nextFollowCheck, lastLeaderSeen, nextTeleportAttempt;
    private Vector3 lastLeaderPosition;
    private uint followTerritory;
    private ulong followLogin;
    private readonly FollowReadyGate followReady = new();
    private readonly FollowNoticeGate followNotices = new();
    private string followStatusText = "Stopped. Choose a character, then Start.";
    private string followStatus
    {
        get => followStatusText;
        set
        {
            followStatusText = value;
            if (followNotices.Changed(value) && config.FollowThem.ChatMessages)
                Chat.Print("[FollowThem] " + value);
        }
    }
    private bool FollowTransitionBusy() =>
        Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51] ||
        Conditions[ConditionFlag.Occupied] || Conditions[ConditionFlag.Occupied30] ||
        Conditions[ConditionFlag.OccupiedInEvent] || Conditions[ConditionFlag.OccupiedInQuestEvent] ||
        Conditions[ConditionFlag.Occupied33] || Conditions[ConditionFlag.Occupied38] ||
        Conditions[ConditionFlag.Occupied39] || Conditions[ConditionFlag.OccupiedSummoningBell] ||
        Conditions[ConditionFlag.OccupiedInCutSceneEvent] || Conditions[ConditionFlag.WatchingCutscene] ||
        Conditions[ConditionFlag.WatchingCutscene78] || Conditions[ConditionFlag.Casting] ||
        Conditions[ConditionFlag.MountOrOrnamentTransition] || Conditions[ConditionFlag.LoggingOut];
    private bool CanIssueFollowMovement() => Player.IsLoaded && Objects.LocalPlayer is { IsTargetable: true } &&
        !FollowTransitionBusy() && !Conditions[ConditionFlag.Unconscious];
    private string followBarText = "";
    private bool followFault,followThemSelectTab;

    private string lastFollowCommand = "";
    private DateTimeOffset lastFollowCommandAt;
    private int followCommandRejected;
    private unsafe void FollowCommand(string command)
    {
        // Only fixed game commands from this class reach this method; no names or chat input.
        var ui = UIModule.Instance();
        if (ui == null) throw new InvalidOperationException("Game command UI is unavailable.");
        using var text = new Utf8String();
        text.SetString(command);
        lastFollowCommand=command; lastFollowCommandAt=DateTimeOffset.UtcNow;
        ui->ProcessChatBoxEntry(&text, 0, false);
    }
    private void StopFollowThem(string reason = "Stopped.")
    {
        var wasArmed = followSession.Armed;
        if (followSession.Stop() == FollowAction.Stop && CanIssueFollowMovement())
        {
            try { FollowCommand("/automove off"); }
            catch (Exception e) { reason = "FollowThem stopped. Use a movement key to cancel game movement."; errorJournal.Record("follow-stop", "Could not send game stop command", exceptionType:e.GetType().Name); }
        }
        followFlight.Reset();followStuck.Reset();
        lastLeaderSeen = default;
        receivedPortal = null;
        relayGeneration++;
        followReady.Reset();
        if (wasArmed) followStatus = "STOPPED — " + reason;
        RefreshFollowBar();
    }
    private void FollowChatNotice(string message)
    {
        if (config.FollowThem.ChatMessages) Chat.Print("[FollowThem] " + message);
    }
    private void ToggleFollowThem()
    {
        if (followSession.Armed) { StopFollowThem(); return; }
        if (string.IsNullOrWhiteSpace(config.FollowThem.TargetName) || config.FollowThem.HomeWorld == 0)
        { followStatus = "Choose a party member or friend first."; return; }
        if (Objects.LocalPlayer == null) { followStatus = "Log in before starting FollowThem."; return; }
        followFault = false; Interlocked.Exchange(ref followCommandRejected, 0); followReady.Reset();
        followFlight.Reset();followStuck.Reset();followRetryAt=default;
        followSession.Arm(); followArmedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); relayGeneration++; nextFollowCheck = default;
        followStatus = "STARTED — Looking for " + config.FollowThem.TargetName + " nearby.";
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
        followBar.Tooltip = new SeStringBuilder().AddText(followStatus + (followSession.Armed ? " Left-click to stop." : " Left-click to start.") + " Right-click to open Companion.").Build();
        followBar.OnClick = e => { if(e.ClickType==MouseClickType.Right){followThemSelectTab=true;Open();} else if(e.ClickType==MouseClickType.Left) ToggleFollowThem(); };
    }
    private unsafe void UpdateFollowThem(DateTimeOffset now)
    {
        if (!config.EnableFollowThem) return;
        if (now < nextFollowCheck) return;
        nextFollowCheck = now.AddMilliseconds(250);
        try
        {
            if (Interlocked.Exchange(ref followCommandRejected, 0) != 0)
            {
                // Do not respond to a rejected command with another movement command.
                followSession.Pause();followStuck.Pause();followReady.Reset();followRetryAt=now.AddSeconds(5);
                receivedPortal=null;relayGeneration++;
                followStatus="WAITING — The game rejected movement. FollowThem remains armed and will retry when available.";
                RefreshFollowBar(); return;
            }
            if (followFault||now<followRetryAt) return;
            RefreshFollowBar();
            var transitioning = FollowTransitionBusy();
            // A transient missing local actor while zoning is not a character logout.
            var loading = Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51];
            var login = Player.IsLoaded ? Player.ContentId : (loading ? followLogin : 0);
            if (followLogin != login)
            {
                if (followLogin != 0) StopFollowThem("Logout or character change.");
                followLogin = login;
            }
            if (!followSession.Armed) return;
            if (followTerritory != Client.TerritoryType)
            {
                followTerritory = Client.TerritoryType; lastLeaderSeen = default;
                followReady.Reset();
                followStuck.Pause();
                followSession.Observe(true, login != 0, true, false, true);
            }
            TryFollowTeleport(now);
            if (Conditions[ConditionFlag.InCombat] || Conditions[ConditionFlag.Unconscious])
            { followSession.Pause();followStuck.Pause();followReady.Reset();followStatus="WAITING — Combat or incapacitation; follow remains armed.";RefreshFollowBar();return; }
            if (!followReady.Observe(now, !transitioning && CanIssueFollowMovement()))
            {
                followStuck.Pause();
                followSession.Observe(true, login != 0, true, false, true);
                followStatus = "WAITING — Loading or occupied; waiting until movement is available.";
                RefreshFollowBar(); return;
            }
            var input = InputManager.Instance();
            if (input != null && (input->GetInputStatus(InputCode.MOVE_FORE) || input->GetInputStatus(InputCode.MOVE_BACK) ||
                input->GetInputStatus(InputCode.MOVE_LEFT) || input->GetInputStatus(InputCode.MOVE_RIGHT) ||
                input->GetInputStatus(InputCode.MOVE_STRIFE_L) || input->GetInputStatus(InputCode.MOVE_STRIFE_R)))
            { if(config.FollowThem.StopOnMovement){StopFollowThem("Your movement input.");return;}followSession.Pause();followStuck.Pause();followStatus="WAITING — Your movement; follow resumes when you release movement keys.";RefreshFollowBar();return; }
            var settings = config.FollowThem;
            var target = Objects.OfType<IPlayerCharacter>().FirstOrDefault(p =>
                p.GameObjectId != Objects.LocalPlayer?.GameObjectId &&
                FollowThemSession.Matches(settings.TargetName, settings.HomeWorld, p.Name.TextValue, p.HomeWorld.RowId));
            var self = Objects.LocalPlayer;
            var nearby = target != null && self != null && target.IsTargetable && Vector3.Distance(self.Position, target.Position) <= 30;
            if (nearby) { lastLeaderPosition = target!.Position; lastLeaderSeen = now; lastLeaderEntity = target.GameObjectId.ToString(); }
            if(self!=null&&followStuck.Observe(now,self.Position,nearby?Vector3.Distance(self.Position,target!.Position):null,settings.ResumeNearby,settings.StuckSeconds)){
                if(followSession.Pause()==FollowAction.Stop&&CanIssueFollowMovement())FollowCommand("/automove off");
                followStatus="WAITING — No movement progress; waiting for your selected character to come back within 3 yalms.";RefreshFollowBar();return;
            }

            if(nearby&&TryFollowTakeoff(target!,now)){RefreshFollowBar();return;}
            var action = followSession.Observe(true, login != 0, false, nearby, settings.ResumeNearby);
            if (action == FollowAction.Stop && CanIssueFollowMovement()) FollowCommand("/automove off");
            if (action == FollowAction.Start)
            {
                var previous = Targets.Target;
                try { Targets.Target = target; FollowCommand("/follow <t>"); }
                finally { Targets.Target = previous; }
            }
            followStatus = followSession.Phase switch
            {
                FollowPhase.Following => "FOLLOWING — Follow requested for " + settings.TargetName + ".",
                FollowPhase.Waiting => "WAITING — Your selected character isn’t nearby. FollowThem is waiting.",
                _ => "STOPPED — Your selected character isn’t nearby; press Start to try again."
            };
            RefreshFollowBar();
        }
        catch (Exception e)
        {
            try { StopFollowThem(); } catch { followSession.Stop(); }
            followFault = true;
            followStatus = "ERROR — FollowThem paused after an error. Use movement keys to cancel game follow; press Start to retry.";
            errorJournal.Record("followthem", "FollowThem update failed", exceptionType: e.GetType().Name);
            RefreshFollowBar();
        }
    }
    private unsafe bool TryFollowTakeoff(IPlayerCharacter target,DateTimeOffset now)
    {
        if(!config.FollowThem.FollowTakeoff){followFlight.Reset();return false;}
        var leader=(FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)target.Address;
        if(leader->MoveController.MovementState!=FFXIVClientStructs.FFXIV.Client.Game.Character.MovementStateOptions.Flying||Conditions[ConditionFlag.InFlight]){followFlight.Reset();return false;}
        if(followSession.Pause()==FollowAction.Stop&&CanIssueFollowMovement())FollowCommand("/automove off");
        followStuck.Pause();
        if(!Conditions[ConditionFlag.Mounted]||!Control.CanFly||Conditions[ConditionFlag.Diving]){followStatus="WAITING — Leader is flying; mount up and ensure flight is unlocked here.";return true;}
        var action=FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if(action!=null&&followFlight.Try(now)){
            action->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction,2);
            followStatus="WAITING — Takeoff requested; checking for flight.";
        }else if(followFlight.Exhausted)followStatus="WAITING — Takeoff was not confirmed. Take off manually or let the leader land nearby.";
        return true;
    }
    private unsafe void TryFollowTeleport(DateTimeOffset now)
    {
        if (Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||!config.FollowThem.AcceptPartyTeleports || FollowParty.Length < 2 || now < nextTeleportAttempt) return;
        var telepo = Telepo.Instance();
        if (telepo == null || !telepo->ActiveTeleportRequest) return;
        var addon = (AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        if (addon == null || !addon->IsVisible || addon->PromptText == null) return;
        var prompt = addon->PromptText->NodeText.ToString();
        if (!FollowThemSession.IsPartyTeleportPrompt(prompt)) return;
        nextTeleportAttempt = now.AddSeconds(5);
        addon->FireCallbackInt(0);
        FollowChatNotice("TELEPORT — Accepted the party teleport offer.");
    }
    private unsafe void DrawFollowThem()
    {
        ImGui.TextWrapped(followStatus);
        if (ImGui.Button(followSession.Armed ? "Stop FollowThem" : "Start FollowThem")) ToggleFollowThem();
        ImGui.TextWrapped("Local settings. Uses simple game follow; obstacles still require your help. Mounted takeoff assistance is optional. Party teleports need an open English confirmation. Shared portals require Journal V7.11.76 on Cloudflare. Only supported Warp/Exit portals are relayed; destination menus are not guessed.");
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
        MessageToggle("FollowThem chat messages", config.FollowThem.ChatMessages, v => config.FollowThem.ChatMessages = v);
        MessageToggle("Take off when the selected character flies (while mounted)",config.FollowThem.FollowTakeoff,v=>config.FollowThem.FollowTakeoff=v);
        MessageToggle("Stop FollowThem when I move (optional)",config.FollowThem.StopOnMovement,v=>config.FollowThem.StopOnMovement=v);
        var stuckSeconds=config.FollowThem.StuckSeconds;
        if(ImGui.InputInt("Stuck timeout (seconds)",ref stuckSeconds)){config.FollowThem.StuckSeconds=Math.Clamp(stuckSeconds,5,600);Pi.SavePluginConfig(config);}
        MessageToggle("Resume when the selected character comes nearby", config.FollowThem.ResumeNearby, v => config.FollowThem.ResumeNearby = v);
        MessageToggle("Accept party teleport offers while FollowThem is started", config.FollowThem.AcceptPartyTeleports, v => config.FollowThem.AcceptPartyTeleports = v);
        MessageToggle("Share my travel with active paired followers", config.FollowThem.SharePortalTransitions, v => config.FollowThem.SharePortalTransitions = v);
        MessageToggle("Follow shared Teleport / aethernet destinations (experimental)",config.FollowThem.UseSharedTeleports,v=>{config.FollowThem.UseSharedTeleports=v;relayGeneration++;});
        var gilLimit=config.FollowThem.TeleportGilLimit;
        if(ImGui.InputInt("Maximum gil per followed Teleport",ref gilLimit)){config.FollowThem.TeleportGilLimit=Math.Clamp(gilLimit,0,10000);Pi.SavePluginConfig(config);}
        ImGui.TextWrapped("Shared travel requires an active follow session. Teleport uses your own unlocked public destination and gil, within this limit. Public ward selection from the same city crystal is supported; private estate shortcuts and world travel are excluded. Aethernet requires the same nearby crystal and a matching unlocked menu entry. Party offers use the separate option above.");
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
