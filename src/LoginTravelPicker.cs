using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record LoginTravelTarget(ulong Character,string Name,uint Home,uint Current,int Service);
    private LoginTravelTarget? loginTravelTarget;
    private bool loginTravelOpen,loginTravelNoLogin;
    private string loginTravelMessage="";
    private (uint Id,string Name,string Dc,int Region)[][] loginTravelWorlds=[];
    private unsafe LoginTravelTarget? ReadLoginTravelTarget(ulong character=0)
    {
        if(Player.IsLoaded||Client.IsLoggedIn||SellerAddon("_CharaSelectListMenu")==null)return null;
        var lobby=AgentLobby.Instance();
        if(lobby==null||lobby->TemporaryLocked||lobby->ServiceAccountIndex is <0 or >15||lobby->LobbyData.CharaSelectEntries.LongCount is <=0 or >512)return null;
        if(character==0)character=lobby->HoveredCharacterContentId;
        if(character==0)return null;
        LoginTravelTarget? found=null;var matches=0;
        foreach(var item in lobby->LobbyData.CharaSelectEntries){
            var entry=item.Value;
            if(entry==null||entry->ContentId!=character||(entry->LoginFlags&CharaSelectCharacterEntryLoginFlags.Locked)!=0)continue;
            found=new(character,entry->NameString,entry->HomeWorldId,entry->CurrentWorldId,lobby->ServiceAccountIndex);matches++;
        }
        return matches==1&&found!=null&&found.Name.Length>0&&TravelDc(found.Home)!=0&&TravelDc(found.Current)!=0?found:null;
    }
    private void AddLoginTravelMenu(IMenuOpenedArgs args)
    {
        if(args.Target is not MenuTargetDefault||ReadLoginTravelTarget() is not {} target)return;
        args.AddMenuItem(new MenuItem{PrefixChar='E',Name=new SeStringBuilder().AddText("Equinox Travel").Build(),OnClicked=_=>{
            loginTravelTarget=target;loginTravelNoLogin=false;loginTravelMessage="";EnsureBidWorlds();
            var homeRegion=bidWorlds!.FirstOrDefault(w=>w.Id==target.Home).Region;
            loginTravelWorlds=bidWorlds.Where(w=>NativeTravelPolicy.RegionReachable((uint)homeRegion,(uint)w.Region))
                .GroupBy(w=>(w.Region,w.Dc)).OrderBy(g=>g.Key.Region).ThenBy(g=>g.Key.Dc).Select(g=>g.OrderBy(w=>w.Name).ToArray()).ToArray();
            loginTravelOpen=true;
        }});
    }
    private bool LoginTravelBusy()=>nativeWorldTrip!=null||nativeHousingTrip!=null||nativeAethernetTrip!=null||assignedLogin!=null||estateTrip!=null||estateHomeReturn!=null||titleLoginProbe.Active||loginMenuReturn.Active||bringTrip!=null;
    private void DrawLoginTravelPicker()
    {
        if(!loginTravelOpen||loginTravelTarget is not {} target)return;
        if(Player.IsLoaded){loginTravelOpen=false;return;}
        ImGui.SetNextWindowSize(new Vector2(780,420),ImGuiCond.FirstUseEver);
        if(ImGui.Begin("Equinox Travel###EquinoxLoginTravel",ref loginTravelOpen,ImGuiWindowFlags.NoCollapse)){
            ImGui.TextUnformatted(target.Name+" @ "+WorldName(target.Home)+" · Service account "+(target.Service+1));
            ImGui.TextDisabled("Current world: "+WorldName(target.Current));
            var busy=LoginTravelBusy();
            ImGui.BeginDisabled(busy);ImGui.Checkbox("Do not log in after transfer",ref loginTravelNoLogin);ImGui.EndDisabled();
            if(loginTravelNoLogin)ImGui.TextWrapped("Destinations that require an in-game World Visit are disabled while this is checked.");
            var valid=ReadLoginTravelTarget(target.Character)==target;
            if(!valid&&!busy)ImGui.TextWrapped("The character list or service account changed. Reopen Equinox Travel from the intended character.");
            if(nativeWorldTrip?.Owner=="login-menu"){
                ImGui.TextWrapped(nativeTravelStatus);
                if(ImGui.SmallButton("Stop travel"))StopNativeWorldTravel("Character-menu travel stopped.");
            }else if(loginTravelMessage.Length>0)ImGui.TextWrapped(loginTravelMessage);
            if(loginTravelWorlds.Length>0&&ImGui.BeginTable("worlds",loginTravelWorlds.Length,ImGuiTableFlags.SizingStretchSame|ImGuiTableFlags.BordersInnerV)){
                foreach(var dc in loginTravelWorlds)ImGui.TableSetupColumn(dc[0].Dc);
                ImGui.TableHeadersRow();ImGui.TableNextRow();
                var sourceDc=TravelDc(target.Current);var homeDc=TravelDc(target.Home);
                foreach(var dc in loginTravelWorlds){
                    ImGui.TableNextColumn();var destinationDc=TravelDc(dc[0].Id);
                    foreach(var world in dc){
                        var route=NativeTravelPolicy.FromCharacterList(target.Current,target.Home,world.Id,sourceDc,homeDc,destinationDc,loginTravelNoLogin);
                        ImGui.BeginDisabled(busy||!valid||route==LoginTravelRoute.Unavailable);
                        if(ImGui.Button((world.Id==target.Home?"Home · ":"")+world.Name+"##"+world.Id,new Vector2(-1,0)))StartLoginTravel(target,world.Id);
                        var hovered=ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);ImGui.EndDisabled();
                        if(hovered&&route==LoginTravelRoute.Unavailable)ImGui.SetTooltip(world.Id==target.Current?"Already on this world.":"This destination requires logging in for World Visit. Uncheck Do not log in after transfer.");
                    }
                }
                ImGui.EndTable();
            }
        }
        ImGui.End();
    }
    private unsafe void StartLoginTravel(LoginTravelTarget target,uint destination)
    {
        try{
            if(LoginTravelBusy()||LifestreamBusy())throw new InvalidOperationException("Finish or stop the active journey first.");
            if(ReadLoginTravelTarget(target.Character)!=target)throw new InvalidOperationException("The selected character, world or service account changed. Reopen the picker.");
            if(SellerAddon("ContextMenu")!=null||SellerAddon("SelectYesno")!=null||SellerAddon("SelectOk")!=null||SellerAddon("SelectString")!=null||SellerAddon("_CharaSelectWorldServer")!=null)throw new InvalidOperationException("Close the open game prompt before selecting a destination.");
            var homeRegion=bidWorlds!.FirstOrDefault(w=>w.Id==target.Home).Region;
            var destinationRegion=bidWorlds.FirstOrDefault(w=>w.Id==destination).Region;
            if(!NativeTravelPolicy.RegionReachable((uint)homeRegion,(uint)destinationRegion))throw new InvalidOperationException("This destination is unavailable for the character's home region.");
            var route=NativeTravelPolicy.FromCharacterList(target.Current,target.Home,destination,TravelDc(target.Current),TravelDc(target.Home),TravelDc(destination),loginTravelNoLogin,TravelRegion(target.Current),TravelRegion(target.Home),TravelRegion(destination));
            if(route==LoginTravelRoute.Unavailable)throw new InvalidOperationException("This route needs an in-game aetheryte. Turn off “Do not log in after transfer” to travel within the region.");
            var lobby=AgentLobby.Instance();if(lobby==null)return;
            var trip=new NativeWorldTrip{Owner="login-menu",Character=target.Character,Name=target.Name,Home=target.Home,Source=target.Current,Destination=destination,
                Leg=route==LoginTravelRoute.ReturnHome?target.Home:route==LoginTravelRoute.LoginThenWorld?target.Current:destination,
                Service=target.Service,Started=DateTimeOffset.UtcNow,ReturnHome=route==LoginTravelRoute.ReturnHome,NoLogin=loginTravelNoLogin,
                ReconnectLoginList=TravelDc(lobby->WorldId)!=TravelDc(target.Current),PreferredGateway=TravelCityPolicy.Preferred(config.TravelCity,config.ShoppingCity,false)};
            nativeWorldTrip=trip;
            SetNativeTravelStage(trip,route==LoginTravelRoute.LoginThenWorld?NativeTravelStage.Login:NativeTravelStage.CharacterMenu,
                route==LoginTravelRoute.LoginThenWorld?"Logging in for regional World Visit":trip.ReturnHome?"Returning the selected character home":"Preparing DC travel from character selection");
            loginTravelMessage=nativeTravelStatus;
            RecordTravelPrompt("Character-menu travel requested",new{trip.Character,trip.Name,trip.Home,trip.Source,trip.Destination,trip.Service,trip.NoLogin,route});
        }catch(Exception ex){loginTravelMessage=ex.Message;}
    }
}
