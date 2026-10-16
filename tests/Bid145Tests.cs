using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Bid145Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string label){if(!ok)throw new Exception("145: "+label);}
  Check(HousingLottery.Buyer("Enter the lottery?\nOn behalf of your free company.")=="fc","FC after first line");
  Check(HousingLottery.Buyer("Enter the lottery?\nFor a personal plot.")=="private","personal after first line");
  Check(HousingLottery.Buyer("Private estate")=="private","private label");
  Check(HousingLottery.Buyer("Individual")=="private","individual label");
  Check(HousingLottery.Buyer("Free\nCompany")=="fc","wrapped company label");
  Check(HousingLottery.Buyer("Free company or private estate")=="unknown","mixed menu is not selected buyer");
  Check(HousingLottery.Buyer("Your lottery number is 9")=="unknown","submission alone cannot identify type");
  Check(HousingLottery.EntryConfirmation("Do you wish to enter\nthe lottery?"),"wrapped confirmation");
  Console.WriteLine("PASS 145 multiline FC/private confirmation and ambiguous buyer labels");
 }
}
