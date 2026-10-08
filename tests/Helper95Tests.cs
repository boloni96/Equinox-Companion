using EquinoxCompanion;
using System.Numerics;
using System.Runtime.CompilerServices;
internal static class Helper95Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS 95 "+name);}
  var candidates=new[]{new HelperMeetingDestination(1,100,10,30,0),new HelperMeetingDestination(2,100,10,4,0),new HelperMeetingDestination(3,100,11,0,0),new HelperMeetingDestination(4,101,10,0,0)};
  Test("meeting selects nearest on actual territory and map",HelperMeetingPolicy.Select(candidates,100,10,Vector3.Zero)?.Id==2);
  Test("follower may select a different unlocked destination",HelperMeetingPolicy.Select(candidates.Where(c=>c.Id!=2),100,10,Vector3.Zero)?.Id==1);
  Test("unsupported map has no invented route",HelperMeetingPolicy.Select(candidates,100,12,Vector3.Zero)==null);
  Test("nonfinite destination rejected",HelperMeetingPolicy.Select([new(1,100,10,float.NaN,0)],100,10,Vector3.Zero)==null);
  Test("loading is not missing quest",HelperQuestStatusPolicy.State(false,false,false,0)=="checking after loading");
  Test("accepted replay takes precedence over historical completion",HelperQuestStatusPolicy.State(true,true,true,3)=="accepted on follower, step 3");
  Test("completed reported from actual state",HelperQuestStatusPolicy.State(true,false,true,0)=="completed on follower");
  Test("missing acceptance is explicit",HelperQuestStatusPolicy.State(true,false,false,0)=="not accepted on follower");
  var now=DateTimeOffset.UtcNow;
  Test("short actor gap preserves session",FollowLoadingPolicy.Grace(now,now.AddSeconds(-2)));
  Test("grace expires",!FollowLoadingPolicy.Grace(now,now.AddSeconds(-10)));
  Test("unknown actor has no grace",!FollowLoadingPolicy.Grace(now,default));
  Test("clock regression does not extend grace",!FollowLoadingPolicy.Grace(now,now.AddSeconds(1)));
  var trip=new FollowPortalSignal(new string('a',32),"Leader",1,1,"1",100,10,0,0,0,0,0,1,"",TravelKind:"teleport",AetheryteId:1,SourceKind:"CurrentMapMeeting",Arrival:new(0,0,0),ArrivalWorld:1,ArrivalTerritory:100,ArrivalMap:10);
  Test("nearby current-map meeting avoids second teleport",FollowArrivalPolicy.AlreadyAtTravelArrival(trip,1,100,10,Vector3.Zero,false));
  Test("other floor not reconciled",!FollowArrivalPolicy.AlreadyAtTravelArrival(trip,1,100,10,new(0,5,0),false));
  Test("ordinary teleport still requires departure",!FollowArrivalPolicy.AlreadyAtTravelArrival(trip with {SourceKind=""},1,100,10,Vector3.Zero,false));
 }
}
