namespace EquinoxCompanion;
internal static class WithdrawalTradePolicy
{
    internal static bool Allows(bool onlyTradable,bool known,bool untradable,int spiritbond,bool collectible)=>
        Allows(onlyTradable?1:0,known,untradable,spiritbond,collectible);
    internal static bool Allows(int filter,bool known,bool untradable,int spiritbond,bool collectible)
    {
        if(filter==0)return true;
        if(!known)return false;
        var tradable=!untradable&&spiritbond==0&&!collectible;
        return filter==1?tradable:filter==2&&!tradable;
    }
}
