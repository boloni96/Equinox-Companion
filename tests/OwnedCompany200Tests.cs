using EquinoxCompanion;
public static class OwnedCompany200Tests
{
    public static void Run()
    {
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var now = DateTimeOffset.Parse("2026-10-19T17:09:00Z");
        var gate = new OwnedCompanyRefresh();
        Check(gate.Tick("1",now,true,false)==0,"No requests while unsafe/loading/FC UI active");
        Check(gate.Tick("1",now,true,true)==1,"Request local company without opening UI");
        Check(gate.Tick("1",now.AddSeconds(1),true,true)==0,"Do not spam requests");
        Check(gate.Tick("1",now.AddSeconds(5),true,true)==2,"Request membership after company request");
        Check(gate.Tick("1",now.AddSeconds(10),true,true)==0,"Throttle incomplete results");
        Check(gate.Tick("1",now.AddMinutes(1),true,true)==1,"Retry unavailable data automatically");
        Check(gate.Tick("1",now.AddSeconds(65),false,true)==0,"Stop after verified identity");
        Check(gate.Tick("2",now.AddSeconds(66),true,true)==1,"New character gets own request");
        Check(gate.Tick("2",now.AddSeconds(67),true,false)==0,"Opening another FC cancels pending member request");
        Check(gate.Tick("2",now.AddSeconds(72),true,true)==0,"Cancelled request cannot adopt another FC");

        var actor = new Actor("12345","Aveline Lux",35,35,"Famfrit","Famfrit");
        var estate = new Address("0023015300120010",35,339,19,17,0,false,false,"Famfrit","The Lavender Beds");
        var fc = new FreeCompanyDetails("9234567890123456789","Test Company","TEST",35);
        var bid = new LotteryBid("Lavender Beds",19,17,5,"fc","","entered","2026-10-14T15:00:00Z","timers");
        var e = new SyncEvent(new string('a',32),"housing.lottery",now,actor,null,Lottery:bid);
        var fixedBid = OwnedLotteryIdentity.Resolve(e,actor,estate,fc,now);
        Check(fixedBid?.FcId==fc.Id && fixedBid.Entry==5 && fixedBid.Status=="entered","Link FC ID; leave claim to cloud ownership reconciliation");
        Check(OwnedLotteryIdentity.Resolve(e with {Actor=actor with {ContentId="54321"}},actor,estate,fc,now)==null,"Never repair another character's bid");
        Check(OwnedLotteryIdentity.Resolve(e,actor,estate with {Plot=18},fc,now)==null,"Require exact plot");
        Check(OwnedLotteryIdentity.Resolve(e,actor,estate with {WorldId=36},fc,now)==null,"Require home world");
        Check(OwnedLotteryIdentity.Resolve(e with {Lottery=bid with {Buyer="private"}},actor,estate,fc,now)==null,"Do not promote private bids to FC");
        Check(OwnedLotteryIdentity.Resolve(e with {Lottery=bid with {FcId="123"}},actor,estate,fc,now)==null,"Do not overwrite known FC");
        Check(OwnedLotteryIdentity.Resolve(e with {Lottery=bid with {Status="lost"}},actor,estate,fc,now)==null,"Do not replace a loss");
        Check(OwnedLotteryIdentity.Resolve(e,actor,estate,fc,now.AddDays(9))==null,"Do not attach old-round entries");
        Check(OwnedLotteryIdentity.Resolve(e,actor,estate,fc,now.AddDays(-1))==null,"Never infer claim during entry period");
        Console.WriteLine("Owned FC refresh and bid identity regressions passed.");
    }
}
