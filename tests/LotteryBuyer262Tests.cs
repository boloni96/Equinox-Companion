using System.Runtime.CompilerServices;
using EquinoxCompanion;

internal static class LotteryBuyer262Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Lottery buyer: "+why);}
        // Addon 7046: selected purpose comes before general ownership rules.
        var terms="\nOnce submitted, a lottery entry may not be withdrawn.\nPlayers may only own one private and one free company estate per World per service account.\nYou may not purchase land on behalf of your free company for thirty days after joining.";
        var fc="Do you wish to deposit 20,000,000 gil to enter the lottery for this free company plot?"+terms;
        var personal="Do you wish to deposit 20,000,000 gil to enter the lottery for this personal plot?"+terms;
        Check(HousingLottery.EntryConfirmation(fc),"FC confirmation recognized");
        Check(HousingLottery.Buyer(fc)=="fc","FC purpose beats private wording in generic terms");
        Check(HousingLottery.Buyer(personal)=="private","personal purpose beats FC wording in generic terms");
        Check(HousingLottery.Buyer(fc.Replace("free company plot","free\ncompany\nplot"))=="fc","wrapped selected purpose");
        Check(HousingLottery.Buyer(fc.ToUpperInvariant())=="fc","case insensitive");
        Check(HousingLottery.Buyer(fc+"\n"+fc)=="fc","AtkValues and visible text nodes can repeat the same prompt");
        Check(HousingLottery.Buyer(fc+"\n"+personal)=="unknown","conflicting selected purposes rejected");
        Check(HousingLottery.Buyer("Available to free companies. Available to private buyers.")=="unknown","mixed eligibility does not manufacture a selection");
        Check(HousingLottery.Buyer("Private buyer or free company representative")=="unknown","choice list remains ambiguous");
        Check(HousingLottery.Buyer("Type of Entry: Free Company Representative\nPrivate eligibility rules")=="fc","explicit timer type wins over other rules");
        Check(HousingLottery.Buyer("Type of Entry: Private Buyer\nFree company rules")=="private","private timer label wins");
        Check(HousingLottery.Buyer("Free Company Representative")=="fc","existing FC type label");
        Check(HousingLottery.Buyer("Individual")=="private","existing individual type label");

        var now=DateTimeOffset.Parse("2026-10-27T20:50:00Z");
        var timer="Housing Lottery Status\nCurrent Entry\nPlot 41, 26th Ward, The Lavender Beds (Medium)\nLottery Number: 54\nType of Entry:\nFree Company Representative";
        var ticket=HousingLottery.ParseTimers(timer,now)!;
        Check(ticket is {Buyer:"fc",Entry:54,Ward:26,Plot:41,District:"Lavender Beds"},"reported ticket recovered from timer type");
        Check(HousingLottery.Valid(ticket),"repair uses existing valid timers protocol");
        Check(HousingLottery.ParseTimers(timer.Replace("Free Company Representative","Private Buyer"),now)?.Buyer=="private","private timer recovery");
        Check(HousingLottery.ParseTimers(timer.Replace("Type of Entry:\nFree Company Representative","Type of Entry:\nUnknown"),now)?.Buyer=="unknown","unreadable type is not inferred from FC membership");
        var receipt=ticket with {Buyer="unknown",Evidence="submission"};
        var context=new LotteryBuyerContext(100,200,ticket.District,ticket.Ward,ticket.Plot,ticket.CycleStart,"fc",now);
        Check(context.Match(100,200,receipt,now.AddSeconds(2))=="fc","matching submission receives captured type");
        Check(context.Match(101,200,receipt,now.AddSeconds(2))=="unknown","another paired character cannot borrow the type");
        Check(context.Match(100,201,receipt,now.AddSeconds(2))=="unknown","another world cannot borrow the type");
        Check(context.Match(100,200,receipt with {Plot=42},now.AddSeconds(2))=="unknown","cancelled prompt at another plot cannot leak");
        Check(context.Match(100,200,receipt with {Ward=25},now.AddSeconds(2))=="unknown","different ward");
        Check(context.Match(100,200,receipt with {District="Mist"},now.AddSeconds(2))=="unknown","different district");
        Check(context.Match(100,200,receipt with {CycleStart=HousingLottery.Period(now).Start.AddDays(-9).ToString("O")},now.AddSeconds(2))=="unknown","different cycle");
        Check(context.Match(100,200,receipt,now.AddMinutes(6))=="unknown","expired confirmation");
        Check(context.Match(100,200,receipt,now.AddSeconds(-1))=="unknown","future confirmation");
        Check((context with {Buyer="unknown"}).Match(100,200,receipt,now.AddSeconds(2))=="unknown","ambiguous confirmation stays unknown");
        Check((context with {Buyer="private"}).Match(100,200,receipt,now.AddSeconds(2))=="private","private submission still supported");
        Console.WriteLine($"Lottery buyer and timer recovery checks passed: {checks}.");
    }
}
