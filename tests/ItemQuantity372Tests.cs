using EquinoxCompanion;
internal static class ItemQuantity372Tests
{
    internal static void Run()
    {
        void Check(string name,ItemOfferQuantityDecision actual,ItemOfferQuantityDecision expected)
        {if(actual!=expected)throw new Exception(name+": "+actual+" != "+expected);}
        ItemOfferQuantityDecision D(long seen,int request=2,int available=2,bool popup=false,bool sent=false,double ms=2000,bool selected=true,long before=0)
            =>ItemOfferQuantityPolicy.Decide(before,seen,request,available,selected,popup,sent,ms);
        Check("provisional one waits for popup",D(1,ms:150),ItemOfferQuantityDecision.Wait);
        Check("provisional one enters requested two",D(1,popup:true),ItemOfferQuantityDecision.EnterQuantity);
        Check("matching provisional amount still enters quantity",D(2,popup:true),ItemOfferQuantityDecision.EnterQuantity);
        Check("callback sent only once",D(1,popup:true,sent:true),ItemOfferQuantityDecision.Wait);
        Check("late receipt after callback",D(1,sent:true,ms:150),ItemOfferQuantityDecision.Wait);
        Check("exact settled receipt",D(2,sent:true),ItemOfferQuantityDecision.Complete);
        Check("wrong settled quantity rejects",D(1,sent:true),ItemOfferQuantityDecision.Reject);
        Check("partial stack request",D(17,17,99,sent:true),ItemOfferQuantityDecision.Complete);
        Check("full stack must not override partial",D(99,17,99,sent:true),ItemOfferQuantityDecision.Reject);
        Check("same item from another slot",D(7,before:5),ItemOfferQuantityDecision.Complete);
        Check("single item without popup",D(1,1,1),ItemOfferQuantityDecision.Complete);
        Check("quantity exceeds stack",D(3),ItemOfferQuantityDecision.Reject);
        Check("removed previous offer",D(4,before:5),ItemOfferQuantityDecision.Reject);
        Check("unselected menu cannot confirm",D(2,selected:false),ItemOfferQuantityDecision.Reject);
        Check("crystal partial stack",D(113,113,9000,sent:true),ItemOfferQuantityDecision.Complete);
        Check("no receipt stays pending until outer timeout",D(0),ItemOfferQuantityDecision.Wait);
        Console.WriteLine("Item quantity 372 tests passed.");
    }
}
