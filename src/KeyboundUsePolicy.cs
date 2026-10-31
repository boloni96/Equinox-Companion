namespace EquinoxCompanion;
public static class KeyboundUsePolicy
{
    // "coffer" is resolved exclusively from the exact Keybound Coffer catalogue entry.
    public static bool IsEventCoffer(string key)=>key=="coffer";
    public static bool CanOpenCoffer(string key,int held,bool hasSpace)=>IsEventCoffer(key)&&held>0&&hasSpace;
    public static bool CofferConsumed(int before,int after)=>before>0&&after==before-1;
    public static bool IsUnlock(string key)=>key is "gatling" or "scorpion" or "barret" or "emote" or "framer" or "gold-saucer-roll" or "battles-roll" or "fight-roll";
    public static bool CanUse(string key,bool selected,int unlockState,int held)=>selected&&IsUnlock(key)&&unlockState==2&&held>0;
    public static bool Confirmed(int before,int after,int unlockState)=>before>0&&after<before&&unlockState==1;
}
