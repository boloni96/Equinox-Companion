using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string helperStopConfirm="";
    private void DrawHelperPanel()
    {
        if(ImGui.BeginTabBar("helper-sections")){
            if(ImGui.BeginTabItem("FollowThem")){DrawFollowThem();ImGui.EndTabItem();}
            if(ImGui.BeginTabItem("Quest Helper")){
                ImGui.TextWrapped("Mirror the followed character's NPC conversations during an active session. Both characters need Journal V7.11.82 on Cloudflare and the same pairing key.");
                MessageToggle("Share my NPC and dialogue actions with active followers",config.FollowThem.ShareQuestActions,v=>config.FollowThem.ShareQuestActions=v);
                MessageToggle("Enable Quest Helper for my next follow session",config.FollowThem.QuestHelper,v=>config.FollowThem.QuestHelper=v);
                if(ImGui.IsItemHovered())ImGui.SetTooltip("Only the follower grants permission when pressing Start. Stop revokes it. The leader can pause/resume their own pause, but cannot override the follower's Pause or restart a stopped session.");
                if(config.FollowThem.QuestHelper)MessageToggle("Mirror the leader's cutscene skips (matching, skippable scenes only)",config.FollowThem.SkipLeaderCutscenes,v=>config.FollowThem.SkipLeaderCutscenes=v);
                ImGui.TextWrapped("Quest Helper starts disabled. Changes apply to your next session. Match NPC and response text; differing quest progress blocks assistance. Rewards, purchases and general Yes/No prompts remain manual. No combat automation or obstacle navigation.");
                ImGui.TextWrapped("If the game does not expose a verified skip callback, open its Skip prompt manually. Helper never sends Escape or other keyboard shortcuts to skip.");
                if(followSession.Armed){ImGui.TextWrapped(helperPermission.Quest?"Quest assistance permitted for this session.":"This session allows FollowThem only.");DrawHelperFollowerControls();}
                if(helperBlocked.Length>0)ImGui.TextWrapped("Blocked: "+helperBlocked);
                if(ImGui.Button("Open Helper Controls"))helperWindowOpen=true;
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }
    private void DrawHelperFollowerControls()
    {
        if(!followSession.Armed)return;
        if(ImGui.Button(helperPermission.LocalPaused?"Resume my session":"Pause my session"))SetHelperLocalPause(!helperPermission.LocalPaused);
        if(helperPermission.LeaderPaused)ImGui.TextWrapped("The leader has paused this session too.");
        if(helperBlocked.Length>0&&ImGui.Button("Clear blocked dialogue and wait for a new NPC click")){ClearHelperActions();ResumeAfterConfirmedTravel();nextHelperStatus=default;}
    }
    private void DrawHelperControls()
    {
        if(!helperWindowOpen||!config.EnableFollowThem)return;
        ImGui.SetNextWindowSize(new Vector2(450,300),ImGuiCond.FirstUseEver);
        // Native ImGui window settings preserve position and collapsed state.
        if(ImGui.Begin("Helper Controls###EquinoxHelperControls",ref helperWindowOpen)){
            if(followSession.Armed){ImGui.TextWrapped("Following "+config.FollowThem.TargetName);DrawHelperFollowerControls();if(ImGui.Button("Stop my session"))StopFollowThem();ImGui.Separator();}
            if(helperFollowers.Length==0)ImGui.TextWrapped("No active followers. A follower starts the session from their own Companion.");
            foreach(var f in helperFollowers){
                ImGui.PushID(f.Id);
                var fresh=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()-f.Updated<15000;
                ImGui.TextWrapped(f.Name+" — "+(!fresh?"Status unavailable":f.Control=="stop"?"Stopped":f.Paused?"Paused by follower":f.Control=="pause"?"Paused by leader":f.Status));
                ImGui.TextDisabled(f.Quest?"FollowThem + Quest Helper":"FollowThem");
                ImGui.BeginDisabled(!fresh||f.Control=="stop"||helperControlTask!=null);
                if(ImGui.Button(f.Control=="pause"?"Resume":"Pause"))SendHelperControl(f,f.Control=="pause"?"resume":"pause");
                ImGui.SameLine();if(ImGui.Button("Stop…"))helperStopConfirm=f.Id;
                ImGui.EndDisabled();
                if(helperStopConfirm==f.Id){ImGui.TextWrapped("End this session? The follower will need to press Start again.");if(ImGui.Button("Yes, end session")){SendHelperControl(f,"stop");helperStopConfirm="";}ImGui.SameLine();if(ImGui.Button("Keep session"))helperStopConfirm="";}
                ImGui.Separator();ImGui.PopID();
            }
            if(SharingQuest&&ImGui.Button("Advance my current dialogue"))AdvanceHelperLeaderTalk();
            if(helperError.Length>0)ImGui.TextWrapped(helperError);
        }
        ImGui.End();
    }
    private unsafe void AdvanceHelperLeaderTalk()
    {
        if(helperCaptureNpc is not {} npc)return;
        var talk=HelperTalk();if(talk.Signature.Length==0)return;
        EmitHelper("talk",talk.Text,talk.Signature,"Talk",HelperScene());
        helperReplaying=true;try{AdvanceTravelTalk(new("helper",Player.CharacterName,Player.HomeWorld.RowId,npc.World,"",npc.Territory,npc.Map,npc.BaseId,0,npc.Position.X,npc.Position.Y,npc.Position.Z,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),"",SourceKind:"EventNpc"));}finally{helperReplaying=false;}
    }
}
