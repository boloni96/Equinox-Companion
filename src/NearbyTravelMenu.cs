using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private string nearbyHoverMenu="";
    private DateTimeOffset nearbyHoverUntil;
    private Vector2 nearbyHoverAnchor;
    private ulong nearbyHoverSource;
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
        if(!config.NearbyTravelMenu||NearbyTravelBusy()){nearbyHoverMenu="";return;}
        var now=DateTimeOffset.UtcNow;
        if(now>=nearbyTravelScan){
            nearbyTravelScan=now.AddMilliseconds(500);
            var self=Objects.LocalPlayer;if(self==null)return;
            nearbyTravelObject=Objects.Where(o=>o.IsTargetable&&(o.ObjectKind==ObjectKind.Aetheryte||o.ObjectKind==ObjectKind.EventNpc||o.ObjectKind==ObjectKind.EventObj)&&Vector3.DistanceSquared(o.Position,self.Position)<225)
                .OrderBy(o=>Vector3.DistanceSquared(o.Position,self.Position)).FirstOrDefault(o=>NearbyTransport(o)&&TargetWithinInteractionRange(o))?.GameObjectId??0;
        }
        var source=Objects.FirstOrDefault(o=>o.GameObjectId==nearbyTravelObject);
        if(source==null||!source.IsTargetable){nearbyHoverMenu="";return;}
        // Resolve before opening ImGui scopes. Unavailable worlds must not leave a half-open UI stack.
        EnsureBidWorlds();
        var housingDistrict=source.BaseId switch {2=>"The Lavender Beds",8=>"Mist",9=>"The Goblet",70=>"Empyreum",111=>"Shirogane",_=>""};
        if(housingDistrict.Length==0&&source.ObjectKind!=ObjectKind.Aetheryte)
            housingDistrict=Client.TerritoryType switch{339=>"Mist",340=>"The Lavender Beds",341=>"The Goblet",641=>"Shirogane",979=>"Empyreum",_=>""};
        if(nearbyHoverSource!=source.GameObjectId){nearbyHoverSource=source.GameObjectId;nearbyHoverMenu="";}
        var headings=new List<string>();
        if(source.ObjectKind==ObjectKind.Aetheryte){if(source.BaseId is 2 or 8 or 9)headings.Add("World");headings.Add("Aethernet");}
        if(housingDistrict.Length>0)headings.Add("Housing");
        if(headings.Count==0)headings.Add("Transport");
        var vp=ImGui.GetMainViewport();var pos=config.NearbyTravelMenuPosition;
        if(pos!=4){
            var pivot=pos switch{1=>new Vector2(.5f,1),2=>new Vector2(0,.5f),3=>new Vector2(1,.5f),_=>new Vector2(.5f,0)};
            ImGui.SetNextWindowPos(vp.WorkPos+vp.WorkSize*pivot+new Vector2(pos==2?12:pos==3?-12:0,pos==1?-70:pos==0?8:0),ImGuiCond.Always,pivot);
        }
        var flags=ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoFocusOnAppearing|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoNav;
        if(pos!=4)flags|=ImGuiWindowFlags.NoMove;
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(0,0,0,.35f));
        ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(0,0,0,0));
        var barOpen=ImGui.Begin("##EquinoxTravelHoverBar",flags);
        if(barOpen){
            foreach(var heading in headings){
                if(heading!=headings[0])ImGui.SameLine();
                ImGui.Button(heading,new Vector2(150,ImGui.GetFrameHeight()*1.3f));
                if(ImGui.IsItemHovered()){
                    nearbyHoverMenu=heading;nearbyHoverUntil=now.AddMilliseconds(300);
                    var min=ImGui.GetItemRectMin();var max=ImGui.GetItemRectMax();
                    nearbyHoverAnchor=pos==1?new Vector2(min.X,min.Y-2):pos==3?new Vector2(min.X-2,min.Y):new Vector2(min.X,max.Y+2);
                }
            }
        }
        ImGui.End();ImGui.PopStyleColor(2);
        if(nearbyHoverMenu.Length==0||!headings.Contains(nearbyHoverMenu))return;
        // Separate panel keeps the headings stationary when destinations open.
        var panelPivot=pos==1?new Vector2(0,1):pos==3?new Vector2(1,0):Vector2.Zero;
        ImGui.SetNextWindowPos(nearbyHoverAnchor,ImGuiCond.Always,panelPivot);
        ImGui.SetNextWindowSizeConstraints(new Vector2(260,0),new Vector2(Math.Min(800,vp.WorkSize.X-24),Math.Max(150,vp.WorkSize.Y-120)));
        if(ImGui.Begin("##EquinoxTravelHoverDestinations",ImGuiWindowFlags.AlwaysAutoResize|ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing)){
            if(ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows))nearbyHoverUntil=now.AddMilliseconds(300);
            if(nearbyHoverMenu=="World"){
                    EnsureBidWorlds();var region=TravelRegion(Player.CurrentWorld.RowId);
                    var groups=bidWorlds!.Where(w=>w.Region==region).GroupBy(w=>w.Dc).OrderBy(g=>g.Key).ToArray();
                    if(groups.Length>0&&ImGui.BeginTable("NearbyWorlds",groups.Length,ImGuiTableFlags.BordersInnerV)){
                        foreach(var group in groups){ImGui.TableNextColumn();ImGui.TextDisabled(group.Key);
                            foreach(var world in group){ImGui.BeginDisabled(world.Id==Player.CurrentWorld.RowId);
                                if(ImGui.Selectable(world.Name)&&TargetWithinInteractionRange(source))StartSharedWorldTravel("w "+world.Name);
                                ImGui.EndDisabled();}}
                        ImGui.EndTable();
                    }

                }
                if(nearbyHoverMenu=="Aethernet"){
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

                }
                if(nearbyHoverMenu=="Housing"){
                    ImGui.TextUnformatted(housingDistrict);
                    if(ImGui.BeginTable("NearbyWards",5)){
                        for(var ward=1;ward<=30;ward++){
                            ImGui.TableNextColumn();
                            if(ImGui.Selectable("Ward "+ward)&&TargetWithinInteractionRange(source))
                                StartNativeHousing(Player.CurrentWorld.RowId,housingDistrict,ward,1,wardOnly:true,sourceObject:source.GameObjectId);
                        }
                        ImGui.EndTable();
                    }
                    ImGui.TextDisabled("Stops on arrival in the ward.");

                }
                if(nearbyHoverMenu=="Transport"||nearbyHoverMenu=="Housing"&&source.ObjectKind!=ObjectKind.Aetheryte){
                    var menu=(AtkUnitBase*)GardenGui.GetAddonByName("SelectString").Address;
                    // Only mirror choices from this exact source's currently visible menu.
                    List<string> choices=Targets.Target?.GameObjectId==source.GameObjectId?TransportChoices(menu):[];
                    if(choices.Count>0){
                        for(var i=0;i<choices.Count;i++){
                            var enabled=NearbyTransportChoiceEnabled(menu,i);
                            ImGui.BeginDisabled(!enabled);
                            var clicked=ImGui.Selectable(choices[i]+"##transport"+i);
                            ImGui.EndDisabled();
                            if(!clicked)continue;
                            var current=TransportChoices(menu);
                            if(Targets.Target?.GameObjectId==source.GameObjectId&&i<current.Count&&current[i]==choices[i]&&NearbyTransportChoiceEnabled(menu,i)){
                                RecordTravelPrompt("Nearby transport choice",new{source=source.BaseId,index=i,text=choices[i]});SelectTravelChoice(menu,i);
                            }
                            break;
                        }
                    }else if(ImGui.Button("Open transport destinations")&&TargetWithinInteractionRange(source)&&!QuestConversationVisible()&&!VisibleFollowAddon("SelectYesno")){
                        var system=TargetSystem.Instance();if(system!=null){Targets.Target=source;RecordTravelPrompt("Nearby transport opened",new{source=source.BaseId});system->InteractWithObject((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)source.Address,false);}
                    }
                    ImGui.TextDisabled("Fees and confirmations remain in the game's menu.");

                }
        }
        ImGui.End();
        if(now>nearbyHoverUntil)nearbyHoverMenu="";
    }
    private static unsafe bool NearbyTransportChoiceEnabled(AtkUnitBase* menu,int index)
    {
        if(menu==null||!menu->IsVisible)return false;
        var list=((AddonSelectString*)menu)->PopupMenu.PopupMenu.List;
        return list!=null&&list->IsItemInteractionEnabled&&index>=0&&index<list->ListLength&&!list->GetItemDisabledState(index);
    }
}
