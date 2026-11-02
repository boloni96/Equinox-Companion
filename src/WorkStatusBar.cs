using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    // Fixed-height header; details are in a tooltip and the existing status panel.
    private void DrawWorkStatusBar(bool eventTool)
    {
        var active=eventAccountActive||nativeEventActive||independentQuestId!=0||questChainActive;
        var label="Ready";var detail="Choose an action to start.";
        var color=new Vector4(.24f,.48f,.68f,1);var fraction=1f;
        if(eventTool&&keyboundBuying){label=keyboundUsingRewards?"Using rewards":"Buying rewards";detail=keyboundBuyStatus;}
        else if(independentSearch!=null){label="Calculating route";detail=independentWalkStatus;fraction=0;}
        else if(independentPath!=null){
            label=!independentMoving?"Paused":independentPositionChanged?"Walking":"Ready to move";
            detail=independentWalkStatus;
            fraction=independentPositionChanged?Math.Clamp((float)independentWaypoint/Math.Max(1,independentPath.Length-1),0,1):0;
        }
        else if(active){label="Working";detail=nativeEventActive?nativeEventStatus:independentQuestId!=0?questToolsStatus:eventAccountStatus;}
        else if(eventTool&&keyboundEnabled){label="Working";detail=keyboundStatus;}
        else if(!eventTool&&followSession.Armed){label="Working";detail=followStatus;}
        else if(independentLastWalkFailure.Length>0){label="Stopped · check details";detail=independentLastWalkFailure;color=new(1,.38f,.25f,1);}
        else if(independentArrived){label="Arrived";detail="Destination confirmed; movement released.";}
        else if(independentNavigationRun.Length>0){label="Stopped";detail=independentWalkStatus;color=new(.5f,.53f,.58f,1);}
        if(label is "Paused" or "Ready to move")color=new(.8f,.59f,.18f,1);
        if(label=="Walking")color=new(.22f,.65f,.4f,1);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram,color);
        ImGui.ProgressBar(fraction,new Vector2(-1,ImGui.GetFrameHeight()),label);
        ImGui.PopStyleColor();
        if(ImGui.IsItemHovered())ImGui.SetTooltip(detail);
    }
}
