namespace EquinoxCompanion;
internal sealed class SaddlebagDepositReceipt(long bagsBefore,long saddleBefore,int maximum)
{
    internal enum Result { Waiting, Settled, Reverted }
    private DateTimeOffset? stableAt;
    private int seenQuantity;
    internal int Quantity=>seenQuantity;
    internal Result Observe(long bags,long saddle,DateTimeOffset now)
    {
        var removed=bagsBefore-bags;var added=saddle-saddleBefore;
        if(removed<=0||removed!=added||removed>maximum){
            if(stableAt.HasValue)return Result.Reverted;
            return Result.Waiting;
        }
        if(seenQuantity>removed)return Result.Reverted;
        if(seenQuantity!=(int)removed){seenQuantity=(int)removed;stableAt=now;return Result.Waiting;}
        return stableAt.HasValue&&now>stableAt.Value?Result.Settled:Result.Waiting;
    }
}
