using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Bid146Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string label){if(!ok)throw new Exception("146: "+label);}
  var now=DateTimeOffset.UtcNow;
  var text="Housing Lottery Status\nCurrent Entry\nPlot 8, 7th Ward, Shirogane (Medium)\nLottery Number: 9\nType of Entry: Free Company Representative";
  var b=HousingLottery.ParseTimers(text,now);Check(b is {Buyer:"fc",District:"Shirogane",Ward:7,Plot:8,Entry:9},"screenshot Estate details");
  Check(HousingLottery.ParseTimers(text.Replace("Free Company Representative","Private Individual"),now)?.Buyer=="private","individual entry");
  Check(HousingLottery.ParseTimers(text.Replace("Type of Entry: ","Type of Entry:\n"),now)?.Buyer=="fc","label and value split across nodes");
  Check(HousingLottery.ParseTimers(text.Replace("Current Entry","No Current Entry").Replace("Lottery Number: 9","Lottery Number: —"),now)==null,"no entry ignored");
  Check(HousingLottery.ParseTimers(text.Replace("Housing Lottery Status","Another timer"),now)==null,"unrelated detail ignored");
  Console.WriteLine("PASS 146 labelled Estate timer screenshot, private entry and split nodes");
 }
}
