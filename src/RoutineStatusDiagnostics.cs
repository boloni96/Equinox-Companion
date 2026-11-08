using Dalamud.Bindings.ImGui;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private readonly RoutineStatusHistory routineStatusHistory=new();
    private DateTimeOffset routineStatusNext;
    private void CaptureRoutineStatus(DateTimeOffset now)
    {
        if(now<routineStatusNext)return;routineStatusNext=now.AddMilliseconds(250);
        void Add(string name,bool active,string? detail)=>routineStatusHistory.Observe(now,name,active,detail);
        Add("World travel",nativeWorldTrip!=null,nativeTravelStatus);
        Add("Housing travel",nativeHousingTrip!=null,nativeHousingStatus);
        Add("Shopping travel",shoppingTravel!=null,marketTravelStatus);
        Add("Aethernet",nativeAethernetTrip!=null,nativeAethernetStatus);
        Add("House / friend travel",estateTrip!=null||estateHomeReturn!=null||goSeeHomeTrip!=null||goSeeFriendTrip!=null,independentWalkStatus);
        Add("Navigation",FlightActive||independentSearch!=null||independentPath!=null||independentEntranceForward!=null,independentWalkStatus);
        Add("Gardening adventure",gardenTendRoute!=null||gardenAutoPlantPlan!=null||gardenHousePlant!=null||gardenHouseHarvest!=null,gardenTendStatus);
        Add("Garden care",gardenCareRun?.Running==true,gardenCareRun?.Status);
        Add("Adventure",eventAccountActive,eventAccountStatus);
        Add("Quest routine",questChainActive,questChainStatus);
        Add("Quest",nativeEventActive||independentQuestId!=0,questToolsStatus);
        Add("Login / character switch",loginMenuReturn.Active||titleLoginProbe.Active||assignedLogin!=null||characterSwitchTarget!=null&&now<characterSwitchUntil,characterTravelStatus);
        Add("Bring character",bringTrip!=null,bringMessage);
        Add("Gear storage",gearRouting,gearStatus);
        Add("Armoire",armoireRunning,armoireStatus);
        Add("Organizer",organizeRunning,organizeStatus);
        Add("Preparing withdrawal",craftingWithdrawalPending,retrievalError);
        Add("Transfer",retrievalRun.Running,retrievalRun.Status);
        Add(localStorageRunning?"Storage transfer":"Company chest",chestRunning,chestStatus);
        Add("Retainer gil",retainerGilRunning,retainerGilStatus);
        Add("Item trade",itemTrade.Running,itemTrade.Status);
        Add("Gil trade",gilTrade.Running,gilTrade.Status);
        Add("Market prices",sellerRun.Running,sellerRun.Status);
        Add("Rewards",keyboundBuying,keyboundBuyStatus);
        Add("Keybound",keyboundEnabled,keyboundStatus);
        Add("Following",nativeFollowRequested&&followSession.Armed,followStatus);
        Add("Delivery",deliveryRun.Length>0||deliveryReturning,deliveryNotice);
    }
    private object RoutineStatusExport()=>new{capacity=200,dropped=routineStatusHistory.Dropped,coverage="Routine status changes sampled every 250 ms, including terminal status; very short transitions may not be captured. Missing causes are explicitly marked.",records=routineStatusHistory.Entries};
    private void DrawRoutineStatusHistory()
    {
        ImGui.TextWrapped("Recent activity reasons remain here after the loading icon disappears. Included in diagnostic exports. Missing causes are shown explicitly; they are not guessed.");
        var entries=routineStatusHistory.Entries;
        if(entries.Length==0){ImGui.TextDisabled("No activity recorded this session.");return;}
        if(ImGui.BeginChild("RoutineStatusHistory",new System.Numerics.Vector2(0,260),true)){
            foreach(var entry in entries.Reverse()){
                ImGui.TextUnformatted($"{entry.At.ToLocalTime():HH:mm:ss} · {entry.Routine} · {(entry.Active?"Active / waiting":"Ended")}");
                ImGui.TextWrapped(entry.Detail);ImGui.Separator();
            }
        }
        ImGui.EndChild();
    }
}
