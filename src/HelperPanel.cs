using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private static void DrawHelperStatusText(string text,bool stale=false,bool stopped=false,string? status=null,bool notice=false)
    {
        var tone=HelperStatusPolicy.Tone(status??text,stale,stopped,notice);
        var color=tone switch {
            "working"=>new Vector4(.35f,.88f,.5f,1),
            "waiting"=>new Vector4(1,.84f,.3f,1),
            "blocked"=>new Vector4(1,.58f,.25f,1),
            "error"=>new Vector4(1,.35f,.35f,1),
            _=>new Vector4(.67f,.70f,.75f,1)
        };
        ImGui.PushStyleColor(ImGuiCol.Text,color);ImGui.TextWrapped(text);ImGui.PopStyleColor();
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Green: working • Yellow: waiting/paused • Orange: blocked • Red: error • Grey: stopped/status unavailable");
    }
    private string helperStopConfirm="";
    private void DrawHelperPanel()
    {
        if(ImGui.BeginTabBar("helper-sections")){
            if(ImGui.BeginTabItem("FollowThem")){DrawFollowThem();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Quest Helper")){
                ImGui.TextWrapped("Mirror only the followed character's quest conversations during an active session. Vendor/shop interactions and ordinary NPC chatter are ignored. Both characters need Journal V7.11.87 on Cloudflare and the same pairing key.");
                MessageToggle("Share my NPC and dialogue actions with active followers",config.FollowThem.ShareQuestActions,v=>config.FollowThem.ShareQuestActions=v);
                MessageToggle("Enable Quest Helper for my next follow session",config.FollowThem.QuestHelper,v=>config.FollowThem.QuestHelper=v);
                if(ImGui.IsItemHovered())ImGui.SetTooltip("Only the follower grants permission when pressing Start. Stop revokes it. Only the leader can pause/resume. Follower Stop ends permission; the leader cannot restart it.");
                if(config.FollowThem.QuestHelper)MessageToggle("Mirror the leader's cutscene skips (matching, skippable scenes only)",config.FollowThem.SkipLeaderCutscenes,v=>config.FollowThem.SkipLeaderCutscenes=v);
                ImGui.TextWrapped("Quest Helper starts disabled. Changes apply to your next session. Match NPC and response text; differing quest progress blocks assistance. Accepts the same quest only after the leader accepts it. Mirrors verified quest completion; optional reward choices, purchases and general Yes/No prompts remain manual. Mirrors a new leader FATE Level Sync in the same FATE, with right-side direct approach when Lifestream is enabled. No combat automation or obstacle navigation.");
                ImGui.TextWrapped("If the game does not expose a verified skip callback, open its Skip prompt manually. Helper never sends Escape or other keyboard shortcuts to skip.");
                if(followSession.Armed){ImGui.TextWrapped(helperPermission.Quest?"Quest assistance permitted for this session.":"This session allows FollowThem only.");DrawHelperFollowerControls();}
                if(helperBlocked.Length>0)DrawHelperStatusText("Blocked: "+helperBlocked);
                if(ImGui.Button("Open Helper Controls"))helperWindowOpen=true;
                if(ImGui.CollapsingHeader("About Quest Helper")){
                    ImGui.TextWrapped("Thanks to TextAdvance/ECommons and YesAlready for their public dialogue and cutscene research, and FFXIVClientStructs for the game interfaces. Equinox adds session controls and matching leader actions.");
                    if(ImGui.SmallButton("TextAdvance"))Dalamud.Utility.Util.OpenLink("https://github.com/NightmareXIV/TextAdvance");ImGui.SameLine();if(ImGui.SmallButton("YesAlready"))Dalamud.Utility.Util.OpenLink("https://github.com/PunishXIV/YesAlready");
                }
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }
    private void DrawHelperFollowerControls()
    {
        if(!followSession.Armed)return;
        if(helperLastIssue.Length>0)DrawHelperStatusText("Quest Helper blocked: "+helperLastIssue);
        if(helperPermission.QuestPaused)DrawHelperStatusText("Quest Helper paused by the leader; FollowThem remains available.");
        if(helperPermission.LeaderPaused)DrawHelperStatusText("Paused by the leader. You can stop the session at any time.");
        if(helperBlocked.Length>0&&ImGui.Button("Clear blocked dialogue and wait for a new NPC click")){ClearHelperActions();ResumeAfterConfirmedTravel();nextHelperStatus=default;}
    }
    private void DrawHelperControls()
    {
        if(!helperWindowOpen||!config.EnableFollowThem)return;
        ImGui.SetNextWindowSize(new Vector2(450,300),ImGuiCond.FirstUseEver);
        // Native ImGui window settings preserve position and collapsed state.
        if(ImGui.Begin("Helper Controls###EquinoxHelperControls",ref helperWindowOpen)){
            if(followSession.Armed){DrawHelperStatusText(config.FollowThem.TargetName+" — "+(HelperPaused?"Paused by leader":HelperQuestBusy?helperQuestStatus:followStatus));DrawHelperFollowerControls();if(ImGui.Button("Stop my session"))StopFollowThem();ImGui.Separator();}
            if(helperFollowers.Length==0)DrawHelperStatusText("No active followers. A follower starts the session from their own Companion.",stopped:true);
            foreach(var f in helperFollowers){
                ImGui.PushID(f.Id);
                var fresh=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-f.Updated<15000;
                var status=!fresh?"Status unavailable":f.Control=="stop"?"Stopped":f.Control=="pause"?"Paused by leader":f.Status;
                DrawHelperStatusText(f.Name+" — "+status,!fresh,f.Control=="stop",status);
                if(f.QuestPaused)DrawHelperStatusText("Quest Helper paused; FollowThem remains active.");
                ImGui.TextDisabled(f.Quest?"FollowThem + Quest Helper":"FollowThem");
                ImGui.BeginDisabled(!fresh||f.Control=="stop"||helperControlTask!=null||helperControls.Count>0);
                if(ImGui.Button(f.Control=="pause"?"Resume FollowThem":"Pause FollowThem"))SendHelperControl(f,f.Control=="pause"?"resume":"pause");
                if(ImGui.IsItemHovered())ImGui.SetTooltip("Pause discards travel. Resume only resumes following; it does not replay trips made while paused.");
                if(ImGui.Button("Bring follower back"))RequestHelperBring(f);
                if(ImGui.IsItemHovered())ImGui.SetTooltip("Resume this active session and request your latest supported teleport destination, only if you are still in that destination area. Requires Journal V7.11.86. The follower keeps their own travel settings and can Stop.");
                if(f.Quest){if(ImGui.Button(f.QuestPaused?"Resume Quest Helper":"Pause Quest Helper"))SendHelperControl(f,f.QuestPaused?"questResume":"questPause");if(ImGui.IsItemHovered())ImGui.SetTooltip("Pauses NPC, dialogue and FATE Level Sync assistance. FollowThem and travel continue. Resume with a fresh NPC interaction.");}
                ImGui.SameLine();if(ImGui.Button("Stop…"))helperStopConfirm=f.Id;
                ImGui.EndDisabled();
                if(helperStopConfirm==f.Id){ImGui.TextWrapped("End this session? The follower will need to press Start again.");if(ImGui.Button("Yes, end session")){SendHelperControl(f,"stop");helperStopConfirm="";}ImGui.SameLine();if(ImGui.Button("Keep session"))helperStopConfirm="";}
                ImGui.Separator();ImGui.PopID();
            }
            if(helperError.Length>0)DrawHelperStatusText(helperError,notice:true);
        }
        ImGui.End();
    }
}
