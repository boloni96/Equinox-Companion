using EquinoxCompanion;
using System.Numerics;
using System.Runtime.CompilerServices;
internal static class Arrival78Tests
{
    [ModuleInitializer] internal static void Run()
    {
        static void Test(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
        var rows=new[]{(62u,144u,"Gold Saucer Aetheryte Plaza"),(65u,144u,"Wonder Square West"),(69u,388u,"Chocobo Square")};
        Test("78 main crystal exact identity",FollowAethernetArrival.Resolve(rows,144,"Gold Saucer Aetheryte Plaza")==62);
        Test("78 shard exact identity",FollowAethernetArrival.Resolve(rows,144,"Wonder Square West")==65);
        Test("78 other territory rejected",FollowAethernetArrival.Resolve(rows,388,"Gold Saucer Aetheryte Plaza")==0);
        Test("78 blank destination rejected",FollowAethernetArrival.Resolve(rows,144,"")==0);
        Test("78 ambiguous destination rejected",FollowAethernetArrival.Resolve(rows.Append((99u,144u,"Gold Saucer Aetheryte Plaza")),144,"Gold Saucer Aetheryte Plaza")==0);
        var trip=new FollowPortalSignal {TravelKind="aethernet",ArrivalWorld=410,ArrivalTerritory=144,ArrivalMap=196};
        Test("78 reported Saucer return accepted",FollowAethernetArrival.Confirmed(trip,true,true,false,true,410,144,196,0,new(-7.69567f,1.0425029f,3.4210389f),new(-.01532f,3.49426f,-.01532f)));
    }
}
