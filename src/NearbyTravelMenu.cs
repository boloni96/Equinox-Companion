using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private ulong nearbyTravelObject;
    private DateTimeOffset nearbyTravelScan;
    private readonly Dictionary<uint,bool> nearbyTransportKinds=[];
    private (uint Id,string Name)[] nearbyShards=[];
    private uint nearbyShardSource;
    private ulong nearbyShardCharacter;
    private DateTimeOffset nearbyShardRefresh;
    private bool NearbyTravelBusy()=>!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Casting]||HelperInCutscene()||NativeGardenWalking||nativeWorldTrip!=null||nativeHousingTrip!=null||nativeAethernetTrip!=null||estateTrip!=null||assignedLogin!=null||gardenTendRoute!=null||shoppingTravel!=null||independentMoving||independentSearch!=null||followSession.Armed||estateHomeReturn!=null||eventAccountActive||goSeeHomeTrip!=null||goSeeFriendTrip!=null;
    private bool NearbyTransport(IGameObject obj)
    {
        if(obj.ObjectKind==ObjectKind.Aetheryte)return true;
        if(obj.ObjectKind==ObjectKind.EventObj){
            var objRow=DataManager.GetExcelSheet<RawRow>(name:"EObj").GetRowOrDefault(obj.BaseId);
            return objRow is {} dataRow&&dataRow.Columns.Count>0&&(Convert.ToUInt32(dataRow.ReadColumn(0))>>16)==2;
        }
        if(obj.ObjectKind!=ObjectKind.EventNpc)return false;
        if(nearbyTransportKinds.TryGetValue(obj.BaseId,out var known))return known;
        // ENpcData is the first 32 event links. Warp and ChocoboTaxiStand are native handler types.
        var row=DataManager.GetExcelSheet<RawRow>(name:"ENpcBase").GetRowOrDefault(obj.BaseId);
        var transport=false;
        if(row is {} data)for(var i=0;i<Math.Min(32,data.Columns.Count);i++){
            var link=Convert.ToUInt32(data.ReadColumn(i));var kind=link>>16;
            if(kind is 2 or 0x12||kind==0xB&&NearbyTransportScript(link)){transport=true;break;}
        }
        return nearbyTransportKinds[obj.BaseId]=transport;
    }
    private bool NearbyTransportScript(uint id)
    {
        var row=DataManager.GetExcelSheet<RawRow>(name:"CustomTalk").GetRowOrDefault(id);
        if(row is not {} talk)return false;
        for(var i=0;i+1<Math.Min(60,talk.Columns.Count);i+=2){
            var instruction=talk.ReadColumn(i).ToString()??"";
            if(instruction.Contains("WARP",StringComparison.OrdinalIgnoreCase)&&Convert.ToUInt32(talk.ReadColumn(i+1))!=0)return true;
        }
        return false;
    }
    private unsafe void DrawNearbyTravelMenu()
    {
        if(!config.NearbyTravelMenu||NearbyTravelBusy())return;
        var now=DateTimeOffset.UtcNow;
        if(now>=nearbyTravelScan){
            nearbyTravelScan=now.AddMilliseconds(500);
            var self=Objects.LocalPlayer;if(self==null)return;
            nearbyTravelObject=Objects.Where(o=>o.IsTargetable&&(o.ObjectKind==ObjectKind.Aetheryte||o.ObjectKind==ObjectKind.EventNpc||o.ObjectKind==ObjectKind.EventObj)&&Vector3.DistanceSquared(o.Position,self.Position)<225)
                .OrderBy(o=>Vector3.DistanceSquared(o.Position,self.Position)).FirstOrDefault(o=>NearbyTransport(o)&&TargetWithinInteractionRange(o))?.GameObjectId??0;
        }
        var source=Objects.FirstOrDefault(o=>o.GameObjectId==nearbyTravelObject);
        if(source==null||!source.IsTargetable)return;
        // Resolve before opening ImGui scopes. Unavailable worlds must not leave a half-open UI stack.
        EnsureBidWorlds();
        var housingDistrict=source.BaseId switch {2=>"The Lavender Beds",8=>"Mist",9=>"The Goblet",70=>"Empyreum",111=>"Shirogane",_=>""};
        var vp=ImGui.GetMainViewport();var pos=config.NearbyTravelMenuPosition;
        if(pos!=4){
            var pivot=pos switch{1=>new Vector2(.5f,1),2=>new Vector2(0,.5f),3=>new Vector2(1,.5f),_=>new Vector2(.5f,0)};
            ImGui.SetNextWindowPos(vp.WorkPos+vp.WorkSize*pivot+new Vector2(pos==2?12:pos==3?-12:0,pos==1?-70:pos==0?36:0),ImGuiCond.Always,pivot);
        }
        ImGui.SetNextWindowSizeConstraints(new Vector2(250,0),new Vector2(Math.Min(800,vp.WorkSize.X-24),Math.Max(150,vp.WorkSize.Y-120)));
        var flags=ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoFocusOnAppearing;
        if(pos!=4)flags|=ImGuiWindowFlags.NoMove;
        if(ImGui.Begin("Equinox Travel##nearby",flags)){
            ImGui.TextDisabled(source.Name.ToString());
            if(ImGui.BeginTabBar("NearbyTravelTabs")){
                if(source.ObjectKind==ObjectKind.Aetheryte&&source.BaseId is 2 or 8 or 9&&ImGui.BeginTabItem("Worlds")){
                    EnsureBidWorlds();var region=TravelRegion(Player.CurrentWorld.RowId);
                    var groups=bidWorlds!.Where(w=>w.Region==region).GroupBy(w=>w.Dc).OrderBy(g=>g.Key).ToArray();
                    if(groups.Length>0&&ImGui.BeginTable("NearbyWorlds",groups.Length,ImGuiTableFlags.BordersInnerV)){
                        foreach(var group in groups){ImGui.TableNextColumn();ImGui.TextDisabled(group.Key);
                            foreach(var world in group){ImGui.BeginDisabled(world.Id==Player.CurrentWorld.RowId);
                                if(ImGui.Selectable(world.Name)&&TargetWithinInteractionRange(source))StartSharedWorldTravel("w "+world.Name);
                                ImGui.EndDisabled();}}
                        ImGui.EndTable();
                    }
                    ImGui.EndTabItem();
                }
                if(source.ObjectKind==ObjectKind.Aetheryte&&ImGui.BeginTabItem("Aethernet")){
                    if(nearbyShardSource!=source.BaseId||nearbyShardCharacter!=Player.ContentId||now>=nearbyShardRefresh){
                        nearbyShardSource=source.BaseId;nearbyShardCharacter=Player.ContentId;nearbyShardRefresh=now.AddSeconds(5);
                        var sheet=DataManager.GetExcelSheet<Aetheryte>();var group=sheet.GetRowOrDefault(source.BaseId)?.AethernetGroup??0;
                        nearbyShards=group==0?[]:sheet.Where(a=>a.AethernetGroup==group&&a.RowId!=source.BaseId&&AethernetUnlocked(a.RowId)==true)
                            .Select(a=>(a.RowId,a.AethernetName.Value.Name.ToString())).Where(a=>a.Item2.Length>0).ToArray();
                    }
                    foreach(var shard in nearbyShards)if(ImGui.Selectable(shard.Name)&&TargetWithinInteractionRange(source)){
                        if(!RequestUnlockedAethernet(shard.Id,out var reason))FollowChatNotice("TRAVEL — "+reason);
                    }
                    if(nearbyShards.Length==0)ImGui.TextDisabled("Use the transport menu for this location.");
                    ImGui.EndTabItem();
                }
                if(source.ObjectKind==ObjectKind.Aetheryte&&housingDistrict.Length>0&&ImGui.BeginTabItem("Housing wards")){
                    ImGui.TextUnformatted(housingDistrict);
                    if(ImGui.BeginTable("NearbyWards",5)){
                        for(var ward=1;ward<=30;ward++){
                            ImGui.TableNextColumn();
                            if(ImGui.Selectable("Ward "+ward)&&TargetWithinInteractionRange(source))
                                StartNativeHousing(Player.CurrentWorld.RowId,housingDistrict,ward,1,wardOnly:true);
                        }
                        ImGui.EndTable();
                    }
                    ImGui.TextDisabled("Stops on arrival in the ward.");
                    ImGui.EndTabItem();
                }
                if(ImGui.BeginTabItem("Transport")){
                    var menu=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;
                    // Only mirror choices from this exact source's currently visible menu.
                    List<string> choices=Targets.Target?.GameObjectId==source.GameObjectId?TransportChoices(menu):[];
                    if(choices.Count>0){
                        for(var i=0;i<choices.Count;i++)if(ImGui.Selectable(choices[i]+"##transport"+i)){
                            var current=TransportChoices(menu);
                            if(Targets.Target?.GameObjectId==source.GameObjectId&&i<current.Count&&current[i]==choices[i]){
                                RecordTravelPrompt("Nearby transport choice",new{source=source.BaseId,index=i,text=choices[i]});SelectTravelChoice(menu,i);
                            }
                            break;
                        }
                    }else if(ImGui.Button("Open transport destinations")&&TargetWithinInteractionRange(source)&&!QuestConversationVisible()&&!VisibleFollowAddon("SelectYesno")){
                        var system=TargetSystem.Instance();if(system!=null){Targets.Target=source;RecordTravelPrompt("Nearby transport opened",new{source=source.BaseId});system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)source.Address,false);}
                    }
                    ImGui.TextDisabled("Fees and confirmations remain in the game's menu.");
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }
        ImGui.End();
    }
}
