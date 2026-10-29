namespace EquinoxCompanion;
internal static class WithdrawalTradePolicy
{
    internal static bool Allows(bool onlyTradable,bool known,bool untradable,int spiritbond,bool collectible)=>
        !onlyTradable||(known&&!untradable&&spiritbond==0&&!collectible);
}
