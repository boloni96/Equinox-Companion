using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record IndicatorWork(string Label,string Detail,string Count,Action Stop);
    private bool workIndicatorPreview,workIndicatorDragging;
    private Vector2 workIndicatorDragOffset;
    private const string IndicatorStopReason="Stopped by you from the Goddess indicator.";
    private void StopIndicatorGarden()
    {
        StopTendAdventure(IndicatorStopReason);
        gardenHouseHarvest=null;StopHousePlant(IndicatorStopReason);
        StopEstateTravel(IndicatorStopReason);StopIndependentWalk(IndicatorStopReason);
    }
    private void StopIndicatorDelivery()
    {
        CancelAssignedLogin(IndicatorStopReason);
        if(itemTrade.Running)StopItemTrade(IndicatorStopReason);
        if(deliveryTrip.Length>0||deliveryReturning){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}goSeeHomeTrip=null;}
        deliveryRun=deliveryTrip=deliveryTrade=deliveryResumeGarden="";deliveryReturning=false;
        deliveryNotice="Local delivery stopped. The partner will pause when this client stops reporting.";
        StopIndependentWalk(IndicatorStopReason);
    }
    private void StopIndicatorTravel()
    {
        if(followSession.Armed)StopFollowThem(IndicatorStopReason);
        StopEstateTravel(IndicatorStopReason);
        if(shoppingTravel!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null){
            shoppingTravel=null;goSeeHomeTrip=null;goSeeFriendTrip=null;
            try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}
        }
        StopIndependentWalk(IndicatorStopReason);
    }
    private IndicatorWork? CurrentIndicatorWork()
    {
        if(gardenTendRoute!=null||gardenCareRun?.Running==true||gardenAutoPlantPlan!=null||gardenHousePlant!=null||gardenHouseHarvest!=null){
            var care=gardenCareRun?.Running==true?gardenCareRun:null;
            return new(care?.Action.ToString()??"Gardening",care?.Status??gardenTendStatus,care==null?"…":$"{care.Completed}/{care.Total}",StopIndicatorGarden);
        }
        var delivery=deliverySessions.FirstOrDefault(s=>s.Id==deliveryRun);
        if(deliveryRun.Length>0&&(deliveryReturning||delivery is {Paused:false,Stopped:false}&&delivery.Status is not ("paused" or "complete" or "stopped")))
            return new("Delivery",deliveryNotice,"…",StopIndicatorDelivery);
        if(eventAccountActive)return new("Adventure",eventAccountStatus,$"{eventAccountCompleted}/{eventAccountCharacters.Length}",()=>StopEventAccount(IndicatorStopReason));
        if(nativeEventActive)return new("Event quest",questToolsStatus,"…",()=>StopNativeEventQuest(IndicatorStopReason));
        if(independentQuestId!=0&&!independentUserPaused)return new("Quest",questToolsStatus,"…",()=>StopIndependentQuest(IndicatorStopReason));
        if(estateTrip!=null||estateHomeReturn!=null)return new("House travel",independentWalkStatus,"…",StopIndicatorTravel);
        if(shoppingTravel!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null)return new("Travel",independentWalkStatus,"…",StopIndicatorTravel);
        if(organizeRunning)return new("Organizer",organizeStatus,"…",()=>StopOrganize(IndicatorStopReason));
        if(retrievalRun.Running)return new("Transfer",retrievalRun.Status,"…",()=>retrievalRun.Stop(IndicatorStopReason));
        if(chestRunning)return new("Company chest",chestStatus,"…",()=>StopChest(IndicatorStopReason));
        if(retainerGilRunning)return new("Retainer gil",retainerGilStatus,"…",()=>StopRetainerGil(IndicatorStopReason));
        if(itemTrade.Running)return new("Item trade",itemTrade.Status,"…",()=>StopItemTrade(IndicatorStopReason));
        if(gilTrade.Running)return new("Gil trade",gilTrade.Status,"…",()=>StopGilTrade(IndicatorStopReason));
        if(sellerRun.Running)return new("Seller",sellerNotice,"…",()=>sellerRun.Stop(IndicatorStopReason));
        if(keyboundBuying)return new("Rewards",keyboundBuyStatus,"…",()=>StopKeyboundBuying(IndicatorStopReason));
        if(keyboundEnabled&&!keyboundStatus.StartsWith("Paused",StringComparison.OrdinalIgnoreCase))return new("Keybound",keyboundStatus,"…",()=>StopKeybound(IndicatorStopReason));
        if((FlightActive||independentSearch!=null||independentPath!=null||independentEntranceForward!=null)&&!independentUserPaused)
            return new("Navigation",independentWalkStatus,"…",StopIndicatorTravel);
        if(nativeFollowRequested&&followSession.Armed)return new("Following",followStatus,"…",()=>StopFollowThem(IndicatorStopReason));
        return null;
    }
    private void DrawWorkIndicatorSettings()
    {
        var enabled=config.WorkIndicatorEnabled;if(ImGui.Checkbox("Show loading indicator during active work",ref enabled)){config.WorkIndicatorEnabled=enabled;SaveConfiguration();}
        var style=Math.Clamp(config.WorkIndicatorStyle,0,2);if(ImGui.Combo("Icon",ref style,new[]{"Goddess casting ring","Original Goddess","Simple blue ring"},3)){config.WorkIndicatorStyle=style;SaveConfiguration();}
        var size=config.WorkIndicatorSize;if(ImGui.SliderFloat("Size",ref size,80,220,"%.0f px")){config.WorkIndicatorSize=size;SaveConfiguration();}
        var locked=config.WorkIndicatorLocked;if(ImGui.Checkbox("Lock position",ref locked)){config.WorkIndicatorLocked=locked;SaveConfiguration();}
        if(ImGui.Button(workIndicatorPreview?"Close preview":"Preview / move indicator"))workIndicatorPreview=!workIndicatorPreview;
        ImGui.SameLine();if(ImGui.Button("Reset position")){config.WorkIndicatorX=config.WorkIndicatorY=-1;SaveConfiguration();}
        ImGui.TextWrapped("Drag the icon with the left mouse button. Right-click stops the routine shown. Confirmed progress is kept; an action already accepted by the game may finish. The indicator disappears when work ends. Preview is only for positioning and can be closed with right-click.");
    }
    private void DrawWorkIndicator()
    {
        var work=CurrentIndicatorWork();
        if(!workIndicatorPreview&&(!config.WorkIndicatorEnabled||work==null)){workIndicatorDragging=false;return;}
        var preview=work==null&&workIndicatorPreview;
        var viewport=ImGui.GetMainViewport();var size=Math.Clamp(config.WorkIndicatorSize,80,220);var bounds=new Vector2(size,size+25);
        var area=viewport.Size;var origin=viewport.Pos;
        var pos=new Vector2(config.WorkIndicatorX<0?(area.X-size)/2:config.WorkIndicatorX,config.WorkIndicatorY<0?35:config.WorkIndicatorY);
        pos=Vector2.Clamp(pos,Vector2.Zero,Vector2.Max(Vector2.Zero,area-bounds));
        ImGui.SetNextWindowPos(origin+pos,ImGuiCond.Always);ImGui.SetNextWindowSize(bounds,ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,Vector2.Zero);
        var flags=ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoBackground|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoFocusOnAppearing|ImGuiWindowFlags.NoNav;
        if(ImGui.Begin("Goddess activity###EquinoxWorkIndicator",flags)){
            var top=ImGui.GetCursorScreenPos();ImGui.InvisibleButton("##activity",bounds);
            var hover=ImGui.IsItemHovered();
            if(hover&&ImGui.IsMouseClicked(ImGuiMouseButton.Right)){
                if(preview)workIndicatorPreview=false;
                else {CurrentIndicatorWork()?.Stop();workIndicatorPreview=false;}
                workIndicatorDragging=false;
            }
            if(hover&&ImGui.IsMouseClicked(ImGuiMouseButton.Left)&&!config.WorkIndicatorLocked){workIndicatorDragging=true;workIndicatorDragOffset=ImGui.GetMousePos()-origin-pos;}
            if(workIndicatorDragging){
                if(ImGui.IsMouseDown(ImGuiMouseButton.Left)){
                    var moved=Vector2.Clamp(ImGui.GetMousePos()-origin-workIndicatorDragOffset,Vector2.Zero,Vector2.Max(Vector2.Zero,area-bounds));
                    config.WorkIndicatorX=moved.X;config.WorkIndicatorY=moved.Y;
                }else{workIndicatorDragging=false;SaveConfiguration();}
            }
            var draw=ImGui.GetWindowDrawList();var style=Math.Clamp(config.WorkIndicatorStyle,0,2);
            var center=top+new Vector2(.515f*size,.63f*size);var radius=size*.285f;
            if(style<2){
                var file=style==0?"goddess-loading.png":"goddess-complete.png";
                var image=Textures.GetFromFile(Path.Combine(Pi.AssemblyLocation.DirectoryName!,file)).GetWrapOrDefault();
                if(image!=null){
                    if(style==0)draw.AddImage(image.Handle,top,top+new Vector2(size),Vector2.Zero,Vector2.One);
                    else {var h=size*.58f;var w=h*image.Width/image.Height;draw.AddImage(image.Handle,top+new Vector2((size-w)/2,0),top+new Vector2((size+w)/2,h));}
                }
            }
            var time=(float)ImGui.GetTime();
            uint C(float r,float g,float b,float a)=>ImGui.ColorConvertFloat4ToU32(new(r,g,b,a));
            draw.AddCircleFilled(center,radius*.72f,C(.02f,.05f,.12f,.8f),48);
            draw.AddCircle(center,radius,C(.15f,.5f,1,.35f),64,2);
            var angle=time*2.7f;var pulse=.75f+.25f*MathF.Sin(time*3);
            for(var i=0;i<32;i++){
                var a=angle+i*.045f;var b=a+.05f;var alpha=(i+1)/32f;
                var p=center+new Vector2(MathF.Cos(a),MathF.Sin(a))*radius;var q=center+new Vector2(MathF.Cos(b),MathF.Sin(b))*radius;
                draw.AddLine(p,q,C(.15f,.65f,1,alpha*.25f),7);
                draw.AddLine(p,q,C(.35f,.9f,1,alpha*pulse),3);
            }
            var count=preview?"3/8":work?.Count??"…";var font=ImGui.GetFont();var fontSize=Math.Clamp(size*.15f,15,28);
            var countSize=ImGui.CalcTextSize(count)*(fontSize/ImGui.GetFontSize());
            draw.AddText(font,fontSize,center-countSize/2,C(.9f,.98f,1,1),count);
            var label=preview?"Preview":work?.Label??"";var labelSize=ImGui.CalcTextSize(label);
            var labelAt=top+new Vector2((size-labelSize.X)/2,size+3);
            draw.AddText(labelAt+Vector2.One,C(0,0,0,1),label);draw.AddText(labelAt,C(.8f,.92f,1,1),label);
            if(hover&&!workIndicatorDragging){ImGui.BeginTooltip();ImGui.PushTextWrapPos(ImGui.GetFontSize()*24);ImGui.TextUnformatted(preview?"Position preview":work?.Detail??"");ImGui.TextUnformatted(preview?"Right-click: close preview":"Right-click: stop this routine");if(!config.WorkIndicatorLocked)ImGui.TextUnformatted("Left-drag: move");ImGui.PopTextWrapPos();ImGui.EndTooltip();}
        }
        ImGui.End();ImGui.PopStyleVar();
    }
}
