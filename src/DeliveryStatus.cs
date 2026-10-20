namespace EquinoxCompanion;

public sealed record DeliveryPeer(string InstallationId, DateTimeOffset? LastSeenAt, DateTimeOffset? ReceivedAt, long ReceivedRevision, long ReceivedCursor, bool Online, string LastCharacterName = "");
public sealed record DeliveryStatus(long UploadedCursor, long AppliedCursor, long RosterRevision, DeliveryPeer[] Peers);
public sealed record DeliveryResult(DeliveryStatus? Status, string Error = "");
public sealed record DeliveryAckResult(bool Success, string Error = "");
public sealed record BidHistoryPage(string CharacterId, long Revision, SharedHousingBid[] Bids, int Total, string? NextCursor);
public sealed record BidHistoryResult(BidHistoryPage? Page, string Error = "", bool Expired = false);

public static class DeliveryPolicy
{
    public static bool InstallationValid(string value)=>value is not null&&value.Length==32&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool ReceiptValid(string value)=>value is not null&&value.Length==64&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool CanAcknowledge(RosterResult result, SharedRoster? installed)=>installed is not null&&ReceiptValid(result.Receipt)&&result.ReceiptRevision==installed.Revision&&(result.Roster is not null||result.NotModified);
    public static bool Received(DeliveryPeer peer,long cursor)=>cursor>0&&peer.ReceivedAt is not null&&peer.ReceivedCursor>=cursor;
    public static bool Valid(DeliveryStatus status)=>status.UploadedCursor>=0&&status.AppliedCursor>=0&&status.RosterRevision>=0&&status.Peers is {Length:<=100}&&status.Peers.All(p=>p is not null&&InstallationValid(p.InstallationId)&&p.ReceivedCursor>=0&&p.ReceivedRevision>=0);
}
