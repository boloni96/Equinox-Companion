using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
namespace EquinoxCompanion;

// IPC DTO: Dalamud converts Lifestream's HousePathData by public member name.
public sealed class EstateTravelPathData
{
    public List<Vector3> PathToEntrance { get; set; } = [];
    public List<Vector3> PathToWorkshop { get; set; } = [];
    public ulong CID { get; set; }
    public bool IsPrivate { get; set; }
}

public sealed partial class Plugin
{
    private void StartEstateTravelCommand((int Property,int Mode,string Label) destination)
    {
        if(!Player.IsLoaded||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){
            FollowChatNotice("TRAVEL — Wait until your character has loaded.");return;
        }
        if(Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BoundByDuty]||Conditions[ConditionFlag.Casting]||HelperInCutscene()){
            FollowChatNotice("TRAVEL — Finish combat, duty, casting or the cutscene before estate travel.");return;
        }
        if(followSession.Armed||independentMoving||assignedLogin!=null||eventAccountActive||gardenTendRoute!=null||shoppingTravel!=null){
            FollowChatNotice("TRAVEL — Stop the active follower, routine or journey before estate travel.");return;
        }
        try{
            if(LifestreamBusy()){FollowChatNotice("TRAVEL — Lifestream is already busy.");return;}
            // Never use PropertyType.Auto or a configured enter-mode default here.
            if(Player.CurrentWorld.RowId==Player.HomeWorld.RowId){
                var has=destination.Property==1
                    ?Pi.GetIpcSubscriber<bool?>("Lifestream.HasPrivateHouse").InvokeFunc()
                    :Pi.GetIpcSubscriber<bool?>("Lifestream.HasFreeCompanyHouse").InvokeFunc();
                if(has==false){FollowChatNotice("TRAVEL — This character has no available "+(destination.Property==1?"private":"FC")+" estate teleport.");return;}
            }
            if(destination.Mode!=0){
                var paths=Pi.GetIpcSubscriber<ulong,(EstateTravelPathData? Private,EstateTravelPathData? FC)>("Lifestream.GetHousePathData").InvokeFunc(Player.ContentId);
                var path=destination.Property==1?paths.Private:paths.FC;
                if(path==null||path.CID!=Player.ContentId||path.IsPrivate!=(destination.Property==1)||path.PathToEntrance.Count==0){
                    FollowChatNotice("TRAVEL — Register this character's "+(destination.Property==1?"private":"FC")+" house entrance path in Lifestream first. No travel started.");return;
                }
            }
            Pi.GetIpcSubscriber<int,int?,object>("Lifestream.EnqueuePropertyShortcut").InvokeAction(destination.Property,destination.Mode);
            FollowChatNotice("TRAVEL — Requested travel to "+destination.Label+". Returning to your home world first if needed.");
        }catch(Exception e){
            Log.Warning(e,"Estate travel IPC failed");
            FollowChatNotice("TRAVEL — Estate travel could not start. Check that Lifestream is enabled and up to date.");
        }
    }
}
