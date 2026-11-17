using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class WorkshopReport569Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop report "+name);}
        var recorded="Contribute 3 lengths of cedar lumber to the \r\ncompany project?";
        Check("recorded wrapped cedar prompt matches",WorkshopPolicy.ContributionPrompt(recorded,3,"Cedar Lumber","lengths of cedar lumber"));
        Check("wrong quantity rejected",!WorkshopPolicy.ContributionPrompt(recorded,6,"Cedar Lumber","lengths of cedar lumber"));
        Check("wrong item rejected",!WorkshopPolicy.ContributionPrompt(recorded,3,"Iron Rivets","iron rivets"));
        Check("HQ warning whitespace normalized",WorkshopPolicy.NormalizePrompt("Do you really want to trade a high-quality\r\nitem?")=="Do you really want to trade a high-quality item?");
        Check("menu gap waits",!WorkshopPolicy.MenuArrived(false,false,false,false));
        Check("old menu waits",!WorkshopPolicy.MenuArrived(false,false,true,true));
        Check("loaded material menu continues",WorkshopPolicy.MenuArrived(true,false,false,false));
        Check("new choice menu continues",WorkshopPolicy.MenuArrived(false,false,true,false));
        var q=new WorkshopQueueSave{Items=[new(21792,"Bow",4),new(21794,"Pressure Hull",4)],Completed=1};
        Check("active queued hull moved first",WorkshopPolicy.PrioritizeActive(q,21794)&&WorkshopPolicy.Next(q)?.ItemId==21794);
        Check("completed prefix and requested counts preserved",q.Completed==1&&q.Items.First().ItemId==21792&&q.Items.Where(e=>e.ItemId==21792).Sum(e=>e.Quantity)==4&&q.Items.Where(e=>e.ItemId==21794).Sum(e=>e.Quantity)==4);
        Check("unrequested project not adopted",!WorkshopPolicy.PrioritizeActive(q,99));
        q.PendingCollection=21794;Check("pending receipt cannot be reordered",!WorkshopPolicy.PrioritizeActive(q,21792));
    }
}
