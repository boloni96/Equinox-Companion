namespace EquinoxCompanion;
public static class BidHistoryPolicy
{
    // A fetched historical snapshot cannot override the current roster's result.
    // Unacknowledged local observations may confirm an unresolved record, but
    // an older "entered" record must never undo a confirmed server outcome.
    public static SharedHousingBid Prefer(SharedHousingBid existing,SharedHousingBid incoming,bool currentServer)
        =>currentServer||incoming.At>=existing.At&&existing.Status=="entered"?incoming:existing;
    public static string Key(SharedHousingBid b)=>$"{b.WorldId}|{b.District}|{b.Ward}|{b.Plot}|{b.Entry}|{b.CycleStart}";
}
