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
    private readonly FollowRecoveryWatch followRecovery = new();
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
        EndHelperSession();
        followSession.Stop();
        UpdateFollowLease(DateTimeOffset.UtcNow);
        if(wasArmed)RequestFollowMovementStop();
        followFlight.Reset();followStuck.Reset();followRecovery.Reset();mountAttempts=0;followStuckStopRequested=false;
        lastLeaderSeen = default;
        ResetTravelQueue();receivedPortal = null;pendingAethernet=null;pendingWard=null;pendingTransport=null;CancelLifestreamTravel();CancelFollowApproach();pendingDutyLeave=null;
        relayGeneration++;
        followReady.Reset();
        if (wasArmed) followStatus = followStopPending||followStopUnconfirmed ? "STOPPING — Stop requested; waiting for game follow cancellation." : "STOPPED — " + reason;
        RefreshFollowBar();
    }
    private void FollowChatNotice(string message)
    {
        if (config.FollowThem.ChatMessages) Chat.Print("[FollowThem] " + message);
    }
    private void ToggleFollowThem()
    {
        if (followSession.Armed) { StopFollowThem(); return; }
        if(HelperPolicy.HasLeaderRole(helperFollowers)){helperWindowOpen=true;FollowChatNotice("You are being followed. Use Helper Controls to pause or end those sessions before starting as a follower.");return;}
        if(followStopPending||followStopUnconfirmed){FollowChatNotice("Stop is awaiting confirmation. Tap and release a movement key before restarting.");return;}
        if (string.IsNullOrWhiteSpace(config.FollowThem.TargetName) || config.FollowThem.HomeWorld == 0)
        { followStatus = "Choose a party member or friend first."; return; }
        if (Objects.LocalPlayer == null) { followStatus = "Log in before starting FollowThem."; return; }
        followFault = false; Interlocked.Exchange(ref followCommandRejected, 0); followReady.Reset();
        followFlight.Reset();followStuck.Reset();followRecovery.Reset();mountAttempts=0;followStuckStopRequested=false;followManualInputSeen=false;followRetryAt=default;
        StartHelperSession();followSession.Arm(); followArmedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); relayGeneration++; nextFollowCheck = default;
        followStatus = "STARTED — Looking for " + config.FollowThem.TargetName + " nearby.";
    }
    private void RefreshFollowBar()
    {
        if (!config.EnableFollowThem) { followBar?.Remove(); followBar = null; followBarText = ""; return; }
        followBar ??= QuickLootBar.Get("Equinox FollowThem");
        followBar.Shown = true;
        var followers=ActiveHelperFollowers();
        if(followers.Length>0){
            var leaderText="FollowThem: Followed by "+string.Join(", ",followers.Select(x=>x.Name));
            if(leaderText!=followBarText){followBar.Text=new SeStringBuilder().AddText(leaderText).Build();followBarText=leaderText;}
            var paused=followers.All(x=>x.Control=="pause");
            followBar.Tooltip=new SeStringBuilder().AddText((paused?"Paused. Left-click to resume.":"Left-click to pause.")+" Right-click for Helper Controls.").Build();
            followBar.OnClick=e=>{
                if(e.ClickType==MouseClickType.Right){helperWindowOpen=true;return;}
                if(e.ClickType!=MouseClickType.Left)return;
                var current=ActiveHelperFollowers();
                var command=current.All(x=>x.Control=="pause")?"resume":"pause";
                foreach(var follower in current)SendHelperControl(follower,command);
            };
            return;
        }
        var label = HelperPaused ? "PAUSED" : !followSession.Armed&&(followStopPending||followStopUnconfirmed) ? "STOPPING" : followSession.Armed&&followRecovery.AwaitingMovement ? "WAITING" : followSession.Phase switch
        {
            FollowPhase.Following => config.FollowThem.TargetName,
            FollowPhase.Waiting => "WAITING", FollowPhase.Loading => "LOADING", _ => "STOPPED"
        };
        var text = "FollowThem: " + label;
        if (text != followBarText) { followBar.Text = new SeStringBuilder().AddText(text).Build(); followBarText = text; }
        followBar.Tooltip = new SeStringBuilder().AddText(followStatus + (followSession.Armed ? " Left-click to stop." : followStopPending||followStopUnconfirmed ? " Tap and release a movement key to confirm stop." : " Left-click to start.") + " Right-click to open Companion and Helper status.").Build();
        followBar.OnClick = e => { if(e.ClickType==MouseClickType.Right){followThemSelectTab=true;helperWindowOpen=true;Open();} else if(e.ClickType==MouseClickType.Left) ToggleFollowThem(); };
    }
    private bool followManualInputSeen,followStuckStopRequested;
    private unsafe void UpdateFollowThem(DateTimeOffset now)
    {
        UpdateFollowMovementStop(now);
        if (!config.EnableFollowThem) return;
        if(followSession.Armed&&FollowMovementKeysHeld())followManualInputSeen=true;
        if (now < nextFollowCheck) return;
        nextFollowCheck = now.AddMilliseconds(250);
        try
        {
            if (Interlocked.Exchange(ref followCommandRejected, 0) != 0)
            {
                // Do not respond to a rejected command with another movement command.
                nativeFollowRequested=false;followStopUnconfirmed=false;followStopStationary.Reset();
                RecordFollowTravel("Follow command rejected",new {command=lastFollowCommand});
                if(!followSession.Armed){RequestFollowMovementStop();return;}
                followSession.Pause();followStuck.Reset();followRecovery.Reset();followStuckStopRequested=false;followReady.Reset();followRetryAt=now.AddSeconds(5);
                // Keep the captured trip: a rejected follow command must not discard travel.
                followStatus="WAITING — The game rejected movement. FollowThem remains armed and will retry when available.";
                RefreshFollowBar(); return;
            }
            if (followFault||now<followRetryAt) return;
            if(helperPendingFate!=null&&(helperFateApproaching||helperFateStopRequested)){followSession.Pause();followStuck.Pause();followStatus="FATE — Approaching the leader’s sync position.";RefreshFollowBar();return;}
            if(HelperPaused||HelperQuestBusy){followSession.Pause();followStuck.Pause();followStatus=HelperPaused?"PAUSED — "+"Paused by the followed character.":"QUEST — "+(helperBlocked.Length>0?helperBlocked:helperQuestStatus);RefreshFollowBar();return;}
            RefreshFollowBar();
            // Menu/loading gates must not age out a leader who is still visible.
            if(followSession.Armed&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51]){
                var visibleLeader=Objects.OfType<IPlayerCharacter>().FirstOrDefault(p=>p.GameObjectId!=Objects.LocalPlayer?.GameObjectId&&p.IsTargetable&&FollowThemSession.Matches(config.FollowThem.TargetName,config.FollowThem.HomeWorld,p.Name.TextValue,p.HomeWorld.RowId));
                if(visibleLeader!=null){lastLeaderPosition=visibleLeader.Position;lastLeaderSeen=now;lastLeaderEntity=visibleLeader.GameObjectId.ToString();}
            }
            if(followSession.Armed&&config.FollowThem.StopOnMovement&&FollowMovementKeysHeld()){StopFollowThem("Your movement input.");return;}
            if(followSession.Armed){TryFollowInvitations(now);TryFollowTeleport(now);}
            if(followSession.Armed&&HoldAcceptedPartyTeleport(now)){
                followSession.Pause();followReady.Reset();followStuck.Pause();
                followStatus="WAITING — Completing the accepted party teleport.";RefreshFollowBar();return;
            }
            if(travelQueue.Count>0||pendingTransport!=null||pendingWard!=null||pendingAethernet!=null||receivedPortal!=null||travelAwaitingArrival!=null){if(nativeFollowRequested&&!followStopPending&&!followStopUnconfirmed)RequestFollowMovementStop();followSession.Pause();followStatus="WAITING — Completing the selected travel action.";return;}
            if(pendingDutyLeave!=null){followSession.Pause();followStatus="WAITING — "+portalRelayStatus;RefreshFollowBar();return;}
            if(followApproach!=null){followSession.Pause();followStatus="WAITING — "+portalRelayStatus;RefreshFollowBar();return;}
            if(lifestreamTravelOwned){followSession.Pause();followStatus="WAITING — Lifestream travel in progress.";return;}
            var transitioning = FollowTransitionBusy();
            // A transient missing local actor while zoning is not a character logout.
            var loading = Conditions[ConditionFlag.BetweenAreas] || Conditions[ConditionFlag.BetweenAreas51];
            var login = Player.IsLoaded ? Player.ContentId : (loading ? followLogin : 0);
            if(lifestreamTravelOwned&&!Player.IsLoaded)login=followLogin;
            if (followLogin != login)
            {
                if (followLogin != 0) StopFollowThem("Logout or character change.");
                followLogin = login;
            }
            if (!followSession.Armed) return;
            if (followTerritory != Client.TerritoryType)
            {
                followTerritory = Client.TerritoryType;
                followReady.Reset();
                followStuck.Pause();
                followSession.Observe(true, login != 0, true, false, true);
            }
            if (Conditions[ConditionFlag.InCombat] || Conditions[ConditionFlag.Unconscious])
            { followSession.Pause();followStuck.Pause();followReady.Reset();followStatus="WAITING — Combat or incapacitation; follow remains armed.";RefreshFollowBar();return; }
            if (!followReady.Observe(now, !transitioning && CanIssueFollowMovement()))
            {
                followStuck.Pause();
                followSession.Observe(true, login != 0, true, false, true);
                followStatus = "WAITING — Loading or occupied; waiting until movement is available.";
                RefreshFollowBar(); return;
            }
            if (followManualInputSeen || FollowMovementKeysHeld())
            { followManualInputSeen=false;if(config.FollowThem.StopOnMovement){StopFollowThem("Your movement input.");return;}followSession.Pause();followStuck.Reset();followRecovery.Reset();followStatus="WAITING — Your movement; follow resumes when you release movement keys.";RefreshFollowBar();return; }
            var settings = config.FollowThem;
            var target = Objects.OfType<IPlayerCharacter>().FirstOrDefault(p =>
                p.GameObjectId != Objects.LocalPlayer?.GameObjectId &&
                FollowThemSession.Matches(settings.TargetName, settings.HomeWorld, p.Name.TextValue, p.HomeWorld.RowId));
            var self = Objects.LocalPlayer;
            var nearby = target != null && self != null && target.IsTargetable;
            if (nearby) { lastLeaderPosition = target!.Position; lastLeaderSeen = now; lastLeaderEntity = target.GameObjectId.ToString(); }
            if(self!=null&&followStuck.Observe(now,self.Position,nearby?Vector3.Distance(self.Position,target!.Position):null,settings.ResumeNearby,settings.StuckSeconds,nearby?target!.Position:null)){
                if(!followStuckStopRequested){RequestFollowMovementStop();followStuckStopRequested=true;}
                followSession.Pause();
                followStatus=followStopPending||followStopUnconfirmed?"STOPPING — Stuck timeout reached; waiting for game movement cancellation.":"WAITING — No movement progress; waiting for your selected character to return closer.";RefreshFollowBar();return;
            }

            followStuckStopRequested=false;
            if(nearby&&TryFollowMount(target!,now)){RefreshFollowBar();return;}
            if(nearby&&TryFollowTakeoff(target!,now)){RefreshFollowBar();return;}
            if(nearby&&followSession.MovementRequested&&followRecovery.Retry(now,self!.Position,Vector3.Distance(self.Position,target!.Position))){
                followSession.Pause();FollowChatNotice("WAITING — No movement detected; requesting follow again.");
            }
            if(!nearby)followRecovery.Reset();
            var action = followSession.Observe(true, login != 0, false, nearby, settings.ResumeNearby);
            if (action == FollowAction.Stop || !nearby && nativeFollowRequested && !followStopUnconfirmed) RequestFollowMovementStop();
            if (action == FollowAction.Start && (followStopPending||followStopUnconfirmed)){followSession.Pause();return;}
            if (action == FollowAction.Start)
            {
                // Keep the exact selected actor targeted while the game processes <t>.
                // Restoring the old target immediately could invalidate the queued command.
                if(Targets.Target?.GameObjectId!=target!.GameObjectId){
                    Targets.Target=target;followSession.Pause();
                    followStatus="WAITING — Selecting your character before following.";return;
                }
                if(!target.IsTargetable||!CanIssueFollowMovement()){followSession.Pause();return;}
                try { nativeFollowRequested=true; FollowCommand("/follow <t>"); }
                catch { nativeFollowRequested=false; throw; }
            }
            followStatus = followSession.Phase switch
            {
                FollowPhase.Following => followRecovery.AwaitingMovement ? "WAITING — Follow requested, but movement has not resumed." : "FOLLOWING — Follow requested for " + settings.TargetName + ".",
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
        followSession.Pause();RequestFollowMovementStop();
        followStuck.Pause();
        if(!Conditions[ConditionFlag.Mounted]||!Control.CanFly||Conditions[ConditionFlag.Diving]){followStatus="WAITING — Leader is flying; mount up and ensure flight is unlocked here.";return true;}
        var action=FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        if(action!=null&&followFlight.Try(now)){
            action->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction,2);
            followStatus="WAITING — Takeoff requested; checking for flight.";
        }else if(followFlight.Exhausted)followStatus="WAITING — Takeoff was not confirmed. Take off manually or let the leader land nearby.";
        return true;
    }
    private DateTimeOffset acceptedPartyTeleportAt;
    private unsafe void TryFollowTeleport(DateTimeOffset now)
    {
        if (HoldHelperTravel||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.Unconscious]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||!config.FollowThem.AcceptPartyTeleports || FollowParty.Length < 2 || now < nextTeleportAttempt) return;
        var telepo = Telepo.Instance();
        if (telepo == null || !telepo->ActiveTeleportRequest) return;
        var addon = (AddonSelectYesno*)GardenGui.GetAddonByName("SelectYesno").Address;
        if (addon == null || !addon->IsVisible || addon->PromptText == null) return;
        var prompt = addon->PromptText->NodeText.ToString();
        if (!FollowThemSession.IsPartyTeleportPrompt(prompt)) return;
        if(followStopPending||followStopUnconfirmed)return;
        if(!travelStepReady){PauseFollowForTravel();return;}
        nextTeleportAttempt = now.AddSeconds(5);
        CapturePartyTeleport(prompt,now);
        usingSharedTravel=true;try{addon->FireCallbackInt(0);}finally{usingSharedTravel=false;}
        FollowChatNotice("TELEPORT — Accepted the party teleport offer.");
    }
    private unsafe void DrawFollowThem()
    {
        ImGui.TextWrapped(followStatus);
        ImGui.BeginDisabled(!followSession.Armed&&(followStopPending||followStopUnconfirmed));
        if (ImGui.Button(followSession.Armed ? "Stop FollowThem" : followStopPending||followStopUnconfirmed ? "Waiting for stop confirmation" : "Start FollowThem")) ToggleFollowThem();
        ImGui.EndDisabled();
        DrawHelperFollowerControls();
        ImGui.TextWrapped("Local settings. Uses simple game follow; obstacles still require your help. Mounted takeoff assistance is optional. Party teleports need an open English confirmation. Instance and gate travel requires Journal V7.11.80 on Cloudflare. Transport menus must match on both characters.");
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
        if(ImGui.CollapsingHeader("Movement & approach",ImGuiTreeNodeFlags.DefaultOpen)){
        MessageToggle("Prefer the leader’s right side when approaching interactions",config.FollowThem.PreferRightSide,v=>config.FollowThem.PreferRightSide=v);
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Prefer a small right-side offset. If already in interaction range, stop and use the exact target without forcing the offset.");
        MessageToggle("FollowThem chat messages", config.FollowThem.ChatMessages, v => config.FollowThem.ChatMessages = v);
        MessageToggle("Accept party invitations from the selected character",config.FollowThem.AcceptPartyInvites,v=>config.FollowThem.AcceptPartyInvites=v);
        MessageToggle("Accept duty-ready prompts when queued with the selected character",config.FollowThem.AcceptDutyReady,v=>config.FollowThem.AcceptDutyReady=v);
        MessageToggle("Dismount when the selected character dismounts",config.FollowThem.FollowDismount,v=>config.FollowThem.FollowDismount=v);
        MessageToggle("Mount when the selected character mounts",config.FollowThem.FollowMount,v=>config.FollowThem.FollowMount=v);
        MessageToggle("Leave duty when the followed character leaves (waits for loot)",config.FollowThem.LeaveDuties,v=>config.FollowThem.LeaveDuties=v);
        DrawLifestreamSettings();
        MessageToggle("Take off when the selected character flies (while mounted)",config.FollowThem.FollowTakeoff,v=>config.FollowThem.FollowTakeoff=v);
        MessageToggle("Stop FollowThem when I move (optional)",config.FollowThem.StopOnMovement,v=>config.FollowThem.StopOnMovement=v);
        DrawCommittedInteger("Stuck timeout (seconds)", config.FollowThem.StuckSeconds, 5, 600, value => config.FollowThem.StuckSeconds = value);
        MessageToggle("Resume when the selected character comes nearby", config.FollowThem.ResumeNearby, v => config.FollowThem.ResumeNearby = v);
        }
        if(ImGui.CollapsingHeader("Travel & shared destinations")){
        MessageToggle("Accept party teleport offers while FollowThem is started", config.FollowThem.AcceptPartyTeleports, v => config.FollowThem.AcceptPartyTeleports = v);
        MessageToggle("Share my travel with active paired followers", config.FollowThem.SharePortalTransitions, v => config.FollowThem.SharePortalTransitions = v);
        MessageToggle("Follow shared Teleport / aethernet destinations (experimental)",config.FollowThem.UseSharedTeleports,v=>{config.FollowThem.UseSharedTeleports=v;relayGeneration++;});
        MessageToggle("Meet at shared Teleports even when the selected character is on another map",config.FollowThem.MeetAtTeleports,v=>config.FollowThem.MeetAtTeleports=v);
        DrawCommittedInteger("Maximum gil per followed Teleport", config.FollowThem.TeleportGilLimit, 0, 10000, value => config.FollowThem.TeleportGilLimit = value);
        ImGui.TextWrapped("Shared travel requires an active follow session. Teleport uses your own unlocked public destination and gil, within this limit. Ward entry, exact available estate teleports and matched transport menus are experimental. World/Data Center travel uses the separate Lifestream options. Aethernet requires the same nearby crystal and a matching unlocked menu entry. Party offers use the separate option above.");
        MessageToggle("Use my selected character's shared portal (experimental)", config.FollowThem.UseSharedPortals, v => { config.FollowThem.UseSharedPortals = v; receivedPortal=null; relayGeneration++; });
        ImGui.TextWrapped(portalRelayStatus);
        ImGui.TextWrapped("Leader: enable Share my travel; no need to start following anyone. Follower: select that character, enable shared destinations and/or portals, then Start. Both need the same pairing key. The instruction expires after two minutes and never enters Journal history. Be beside the same source. Matching transport menus and confirmations can be replayed; unsupported dialogs remain manual. Lifestream must be installed separately for its optional routes.");
        }
    }
    private readonly Dictionary<uint, int> integerEditDrafts = new();
    private void DrawCommittedInteger(string label, int current, int minimum, int maximum, Action<int> apply, bool saveConfig = true)
    {
        var id = ImGui.GetID(label);
        var value = integerEditDrafts.TryGetValue(id, out var draft) ? draft : current;
        ImGui.InputInt(label, ref value);
        integerEditDrafts[id] = value;
        // Keep incomplete typing out of live travel settings and serialize only on commit.
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            value = Math.Clamp(value, minimum, maximum);
            if (value != current)
            {
                apply(value);
                if (saveConfig) SaveConfiguration();
            }
        }
        if (!ImGui.IsItemActive()) integerEditDrafts.Remove(id);
    }
    private string FollowWorldName(uint id) => DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().GetRowOrDefault(id)?.Name.ToString() ?? id.ToString();
    private void FollowCandidate(string name, uint world, string source)
    {
        if (string.IsNullOrWhiteSpace(name) || world == 0) return;
        if (ImGui.Selectable(name + " @ " + FollowWorldName(world) + "##" + source + world + name))
        {
            StopFollowThem("Selected " + name + ". Press Start when ready.");
            config.FollowThem.TargetName = name; config.FollowThem.HomeWorld = world; SaveConfiguration();
        }
    }
}

