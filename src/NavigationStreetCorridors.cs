using System.Numerics;
namespace EquinoxCompanion;
// Known street geometry is a routing hint, never permission to walk through it.
// Mist subdivision's lower-to-upper road includes a descent and two stair turns.
// Coordinate reference: Lifestream HousingData.json at b36320170faafc8315f7585edb45cfbcd34e7046,
// territory 339, plot 42. Only this factual street corridor is represented here.
internal static class NavigationStreetCorridors
{
    private static readonly Vector3[] MistUpperStreet=[
        new(-689.5554f,17.994486f,-620.03986f),
        new(-686.25f,15.75f,-647.75f),new(-685,15.75f,-665),
        new(-684,16,-665.75f),new(-676.75f,19.5f,-665.75f),
        new(-675,19.75f,-665.5f),new(-671,19.75f,-664.5f),
        new(-668.75f,21.25f,-660.25f),new(-668,21.25f,-659.25f),
        new(-667,21.25f,-659.25f),new(-666.5f,21.25f,-659.75f),
        new(-664.75f,25,-669.25f),new(-662.5f,25,-669.5f),
        new(-658.3998f,25.169823f,-655.8979f)];
    public static Vector3[]? Guide(uint territory,Vector3 start,Vector3 goal){
        if(territory!=339)return null;
        if(Vector3.Distance(start,MistUpperStreet[0])<=6&&Vector3.Distance(goal,MistUpperStreet[^1])<=6)
            return MistUpperStreet.Append(goal).ToArray();
        if(Vector3.Distance(start,MistUpperStreet[^1])<=6&&Vector3.Distance(goal,MistUpperStreet[0])<=6)
            return MistUpperStreet.Reverse().Append(goal).ToArray();
        return null;
    }
}
