using System.Numerics;
namespace EquinoxCompanion;
public static class InteractionApproachPolicy
{
    // Observed successful menu openings, with margin. Main crystals share a
    // provisional starting limit; callers must confirm the menu and retry closer.
    public static float Limit(uint baseId,bool crystal,bool mainCrystal,int attempt=0)
    {
        if(crystal)return mainCrystal?(attempt<=0?10.5f:attempt==1?8f:6f):(attempt<=0?6f:attempt==1?5f:4f);
        if(attempt>0)return 3.2f;
        return baseId switch {2002737=>4.5f,2002738 or 2005124=>4f,2008126=>3.8f,_=>3.2f};
    }
    public static bool WithinDistance(Vector3 player,Vector3 target,bool crystal)=>WithinDistance(player,target,crystal?6f:3.2f);
    public static bool WithinDistance(Vector3 player,Vector3 target,float limit)
    {
        var distance=Vector3.DistanceSquared(player,target);
        return float.IsFinite(distance)&&float.IsFinite(limit)&&limit>0&&distance<=limit*limit;
    }
}
