using System.Numerics;
namespace EquinoxCompanion;
public static class InteractionApproachPolicy
{
    // Conservative physical limits. Targetable/native range-query success alone
    // did not prove usable distance in the reported estate-door interaction.
    public static bool WithinDistance(Vector3 player,Vector3 target,bool crystal)
    {
        var distance=Vector3.DistanceSquared(player,target);
        var limit=crystal?6f:3.2f;
        return float.IsFinite(distance)&&distance<=limit*limit;
    }
}
