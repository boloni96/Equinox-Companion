using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private IDtrBarEntry? workBar;
    private DateTimeOffset workBarNext;
    private string workBarLast="",workBarDetail="",navigationPhase="Ready",navigationDetail="No navigation request.";
    private NavigationSystemWindow navigationSystemWindow=null!;
    private void RemoveWorkBar(){workBar?.Remove();workBar=null;workBarLast=workBarDetail="";}
    private void UpdateWorkBar(DateTimeOffset now)
    {
        if(now<workBarNext)return;workBarNext=now.AddMilliseconds(250);
        workBar??=QuickLootBar.Get("Equinox Work");
        workBar.Shown=Player.IsLoaded&&(independentSearch!=null||independentPath!=null);
        var tone="working";navigationPhase="Ready";navigationDetail="Navigation system ready; no active route.";
        if(independentSearch!=null||independentPath!=null){
            if(!GameWalkingCollision.Available()){navigationPhase="Reading geometry";tone="waiting";}
            else if(independentSearch!=null){navigationPhase="Calculating";tone="waiting";}
            else if(!independentMoving){navigationPhase="Paused";tone="waiting";}
            else navigationPhase=independentPositionChanged?"Walking":"Ready to move";
            navigationDetail=independentWalkStatus;
        }
        else if(independentLastWalkFailure.Length>0){navigationPhase="Stopped";navigationDetail=independentLastWalkFailure;tone="error";}
        else if(independentArrived){navigationPhase="Arrived";navigationDetail="Destination confirmed; movement released.";}
        var text="Navigation: "+navigationPhase;var key=text+"/"+tone;
        if(key!=workBarLast){workBar.Text=HelperBarText(text,tone);workBarLast=key;}
        var tooltip=navigationDetail+"\nClick: navigation system status, route progress and diagnostics.";
        if(tooltip!=workBarDetail){workBar.Tooltip=new SeStringBuilder().AddText(tooltip).Build();workBarDetail=tooltip;}
        workBar.OnClick=e=>{if(e.ClickType==MouseClickType.Left)navigationSystemWindow.IsOpen=true;};
    }
    private void DrawNavigationSystem()
    {
        ImGui.TextUnformatted("Navigation system · "+navigationPhase);
        ImGui.TextWrapped(navigationDetail);
        ImGui.Separator();
        ImGui.TextUnformatted("Local ground geometry · collision-checked routes");
        if(independentNavigationRun.Length>0){
            ImGui.TextUnformatted($"Areas checked: {independentSearch?.Expanded??independentLastExpanded:N0}");
            ImGui.TextUnformatted($"Calculation: {independentPlanningSeconds:F1} seconds");
            ImGui.TextUnformatted($"Route: {independentWaypoint} / {Math.Max(0,independentLastRoutePoints-1)} points");
            ImGui.TextUnformatted($"Movement reads: {independentForwardReads:N0} · applied: {independentInputOverrides:N0}");
            ImGui.TextDisabled($"Territory {independentTerritory} · destination {independentGoal.X:F1}, {independentGoal.Y:F1}, {independentGoal.Z:F1}");
        }
        ImGui.TextWrapped("The camera stays under your control. Nearby route points are skipped only after a live collision check. Planning caches are local to one search; walking checks the route again.");
        ImGui.BeginDisabled(independentSearch==null&&independentPath==null);
        if(ImGui.Button("Stop current navigation")){
            if(eventAccountActive)StopEventAccount("Navigation stopped by you.");
            else if(nativeEventActive)StopNativeEventQuest("Navigation stopped by you.");
            else if(independentQuestId!=0)StopIndependentQuest("Navigation stopped by you.");
            StopIndependentWalk("Stopped by you.");
        }
        ImGui.EndDisabled();
        if(ImGui.CollapsingHeader("Last stop reason"))ImGui.TextWrapped(independentLastWalkFailure.Length>0?independentLastWalkFailure:independentWalkStatus);
    }
    private sealed class NavigationSystemWindow:Window
    {
        private readonly Plugin p;
        internal NavigationSystemWindow(Plugin p):base("Navigation system · Equinox"){
            this.p=p;Size=new Vector2(440,330);SizeCondition=ImGuiCond.FirstUseEver;RespectCloseHotkey=false;
        }
        public override void Draw()=>p.DrawNavigationSystem();
    }
}

