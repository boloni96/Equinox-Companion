namespace EquinoxCompanion;
public sealed record OrganizerDresserSnapshot(DateTimeOffset At,ArmoirePiece[] Pieces,uint[] Stored,bool CabinetKnown=true);
public static class OrganizerStoragePolicy
{
    public static bool Cosmetic(int equipLevel,uint itemLevel,bool bonuses)=>equipLevel<=1&&itemLevel<=1&&!bonuses;
    public static bool ExtendWait(int attempts)=>attempts<2;
    public static string Phase(int phase)=>phase switch {0=>"loading storage",1=>"choosing next item",2=>"waiting for withdrawal receipt",3=>"waiting for armoire receipt",_=>"checking storage"};
}
