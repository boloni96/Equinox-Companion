using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class WithdrawalTradeFilter290Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string n,bool ok){if(!ok)throw new Exception(n);Console.WriteLine("PASS trade filter290 "+n);}
  Check("untradable type included",WithdrawalTradePolicy.Allows(2,true,true,0,false));
  Check("bound gear included",WithdrawalTradePolicy.Allows(2,true,false,1,false));
  Check("collectible included",WithdrawalTradePolicy.Allows(2,true,false,0,true));
  Check("tradable stack excluded",!WithdrawalTradePolicy.Allows(2,true,false,0,false));
  Check("unknown not treated as untradable",!WithdrawalTradePolicy.Allows(2,false,false,0,false));
  Check("any items includes both",WithdrawalTradePolicy.Allows(0,true,true,1,true)&&WithdrawalTradePolicy.Allows(0,true,false,0,false));
  foreach(var untradable in new[]{false,true})foreach(var bound in new[]{0,1})foreach(var collectible in new[]{false,true})Check("filters partition known stacks",WithdrawalTradePolicy.Allows(1,true,untradable,bound,collectible)!=WithdrawalTradePolicy.Allows(2,true,untradable,bound,collectible));
 }
}
