using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private IDtrBarEntry? workBar;
    private DateTimeOffset workBarNext;
    private string workBarLast="",workBarDetail="";
    private bool workBarEvent=true;
    private void RemoveWorkBar(){workBar?.Remove();workBar=null;workBarLast=workBarDetail="";}
    private void UpdateWorkBar(DateTimeOffset now)
    {
        if(now<workBarNext)return;workBarNext=now.AddMilliseconds(250);
        if(eventAccountActive||nativeEventActive||keyboundEnabled||keyboundBuying)workBarEvent=true;
        else if(independentQuestId!=0||questChainActive)workBarEvent=false;
        if(!config.EnableKeyboundBrawler&&independentQuestId==0&&!questChainActive&&!nativeEventActive&&independentNavigationRun.Length==0){RemoveWorkBar();return;}
        workBar??=QuickLootBar.Get("Equinox Work");
        workBar.Shown=Player.IsLoaded;
        var eventTool=workBarEvent;
        var active=eventAccountActive||nativeEventActive||independentQuestId!=0||questChainActive;
        var label="Ready";var detail="Choose an action to start.";
        var tone="working";
        if(eventTool&&keyboundBuying){label=keyboundUsingRewards?"Using rewards":"Buying rewards";detail=keyboundBuyStatus;}
        else if(independentSearch!=null){label="Calculating route";detail=independentWalkStatus;tone="waiting";}
        else if(independentPath!=null){
            label=!independentMoving?"Paused":independentPositionChanged?"Walking":"Ready to move";
            detail=independentWalkStatus;

        }
        else if(active){label="Working";detail=nativeEventActive?nativeEventStatus:independentQuestId!=0?questToolsStatus:eventAccountStatus;}
        else if(eventTool&&keyboundEnabled){label="Working";detail=keyboundStatus;}
        else if(independentLastWalkFailure.Length>0){label="Stopped · check details";detail=independentLastWalkFailure;tone="error";}
        else if(independentArrived){label="Arrived";detail="Destination confirmed; movement released.";}
        else if(independentNavigationRun.Length>0){label="Stopped";detail=independentWalkStatus;tone="stopped";}
        if(label is "Paused" or "Ready to move")tone="waiting";
        var text="Equinox: "+label;var key=text+"/"+tone;
        if(key!=workBarLast){workBar.Text=HelperBarText(text,tone);workBarLast=key;}
        var tooltip=detail+"\nClick to open "+(eventTool?"the event helper.":"quest tools.");
        if(tooltip!=workBarDetail){workBar.Tooltip=new SeStringBuilder().AddText(tooltip).Build();workBarDetail=tooltip;}
        workBar.OnClick=e=>{if(e.ClickType!=MouseClickType.Left)return;if(workBarEvent)OpenKeybound();else questToolsWindow.IsOpen=true;};
    }
}
