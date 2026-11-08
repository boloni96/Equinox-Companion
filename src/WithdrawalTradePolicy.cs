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
        return filter switch{1=>tradable,2=>!tradable,3=>spiritbond>0&&!collectible,4=>spiritbond==0||collectible,_=>false};
    }
}
