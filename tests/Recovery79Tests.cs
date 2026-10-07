using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Recovery79Tests
{
    [ModuleInitializer] internal static void Run()
    {
        static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
        var s=new FollowPortalSignal("test","Leader",1,410,"",144,196,0,0,0,0,0,1000,"");
        Test("79 retained request while reconnecting",FollowTravelRecovery.PendingFresh(s,90000));
        Test("79 expired request rejected",!FollowTravelRecovery.PendingFresh(s,121000));
        Test("79 future request rejected",!FollowTravelRecovery.PendingFresh(s,999));
        Test("79 original shorter expiry retained",!FollowTravelRecovery.PendingFresh(s with {ExpiresAt=5000},5000));
        Test("79 longer expiry capped",FollowTravelRecovery.Deadline(s with {ExpiresAt=999999})==121000);
        var estate=s with {TravelKind="friendestate",FriendContentId="42",Destination="Private Estate"};
        Test("79 own private estate",FollowTravelRecovery.OwnEstate(estate,42));
        Test("79 own FC estate",FollowTravelRecovery.OwnEstate(estate with {Destination="Free Company Estate"},42));
        Test("79 other friend unchanged",!FollowTravelRecovery.OwnEstate(estate,43));
        Test("79 unknown estate rejected",!FollowTravelRecovery.OwnEstate(estate with {Destination="Unknown"},42));
        var door=s with {TravelKind="door"};
        Test("79 returned leader clears source door",FollowTravelRecovery.ReturnedDoor(door,true,true,false,false,410,144,196));
        Test("79 nearby leader before departure not cancelled",!FollowTravelRecovery.ReturnedDoor(door,false,true,false,false,410,144,196));
        Test("79 loading door not cancelled",!FollowTravelRecovery.ReturnedDoor(door,true,true,true,false,410,144,196));
        Test("79 successful door not cancelled",!FollowTravelRecovery.ReturnedDoor(door,true,true,false,true,410,144,196));
        Test("79 other map not cancelled",!FollowTravelRecovery.ReturnedDoor(door,true,true,false,false,410,144,197));
    }
}
