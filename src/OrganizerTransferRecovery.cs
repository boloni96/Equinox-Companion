namespace EquinoxCompanion;
public enum OrganizerTransferState { Waiting, Confirmed, Unchanged, Changed }
public static class OrganizerTransferRecovery
{
    public static OrganizerTransferState Check(ChestMove move,ChestStack source,ChestStack destination,TimeSpan elapsed)
    {
        if(ChestTransferPlanner.Confirmed(move,source,destination))return OrganizerTransferState.Confirmed;
        if(elapsed<TimeSpan.FromSeconds(8))return OrganizerTransferState.Waiting;
        bool Same(ChestStack a,ChestStack b)=>a.Item==b.Item&&a.Quantity==b.Quantity&&(a.Item==0||a.Hq==b.Hq);
        return Same(move.Source,source)&&Same(move.Destination,destination)?OrganizerTransferState.Unchanged:OrganizerTransferState.Changed;
    }
    // Remove dependent deliveries too: an unconfirmed withdrawal is never cargo.
    // Remaining visits are rebound to live slots before sending another move.
    public static RoutedMove[] DeferItem(RoutedMove[] moves,int index)=>moves.Take(index).Concat(moves.Skip(index).Where(x=>x.Move.Source.Item!=moves[index].Move.Source.Item||x.Move.Source.Hq!=moves[index].Move.Source.Hq)).ToArray();
}
