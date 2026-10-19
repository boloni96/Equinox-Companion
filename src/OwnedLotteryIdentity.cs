namespace EquinoxCompanion;

public static class OwnedLotteryIdentity
{
    public static LotteryBid? Resolve(SyncEvent observation, Actor actor, Address estate, FreeCompanyDetails company, DateTimeOffset now)
    {
        var bid = observation.Lottery;
        if (observation.Kind != "housing.lottery" || observation.Actor.ContentId != actor.ContentId ||
            observation.Actor.HomeWorldId != actor.HomeWorldId || estate.WorldId != actor.HomeWorldId || company.WorldId != estate.WorldId ||
            bid is not { Buyer: "fc", FcId: "" } || bid.Status is "lost" or "forfeited" ||
            !DateTimeOffset.TryParse(bid.CycleStart, out var cycle) || now < cycle.AddDays(5) || now >= cycle.AddDays(9) ||
            HousingLottery.District(estate.DistrictName ?? "") != bid.District || estate.Ward != bid.Ward || estate.Plot != bid.Plot ||
            SyncValidation.CompleteCompany(company) is null) return null;
        return bid with { FcId = company.Id };
    }
}
