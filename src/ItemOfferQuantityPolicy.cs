namespace EquinoxCompanion;

// A visible quantity editor is an unfinished offer, even if the provisional
// quantity happens to equal the request. Never advance until it has closed.
internal enum ItemOfferQuantityDecision { Wait, EnterQuantity, Complete, Reject }
internal static class ItemOfferQuantityPolicy
{
    internal static ItemOfferQuantityDecision Decide(long before,long observed,int requested,int available,
        bool selected,bool numericVisible,bool quantitySent,double elapsedMs)
    {
        if(requested<=0||requested>available||before<0||observed<before||observed>before+available)
            return ItemOfferQuantityDecision.Reject;
        if(!selected)return observed==before?ItemOfferQuantityDecision.Wait:ItemOfferQuantityDecision.Reject;
        if(numericVisible)return quantitySent?ItemOfferQuantityDecision.Wait:ItemOfferQuantityDecision.EnterQuantity;
        if(observed==before+requested)return elapsedMs>=800?ItemOfferQuantityDecision.Complete:ItemOfferQuantityDecision.Wait;
        if(observed==before||elapsedMs<1500)return ItemOfferQuantityDecision.Wait;
        return ItemOfferQuantityDecision.Reject;
    }
}
