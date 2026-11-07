using System.Numerics;
namespace EquinoxCompanion;
// Known street geometry is a routing hint, never permission to walk through it.
// Mist subdivision's lower-to-upper road includes a descent and two stair turns.
// Coordinate reference: Lifestream HousingData.json at b36320170faafc8315f7585edb45cfbcd34e7046,
// Factual street coordinates for the reported Mist 42, Goblet 22 and Shirogane 3/11 approaches.
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
    private static readonly Vector3[] Goblet22=[
        new(160.1938f,-30.000004f,-32.66404f),
        new(157.75f,-26.25f,-21.5f),
        new(157.25f,-25.75f,-19.75f),
        new(157.0f,-25.75f,-19.25f),
        new(152.25f,-25.75f,-18.5f),
        new(151.25f,-25.75f,-18.5f),
        new(131.0f,-25.75f,-19.0f),
        new(128.0f,-25.5f,-20.5f),
        new(127.25f,-17.75f,-37.0f),
        new(125.25f,-17.75f,-37.0f),
        new(122.25f,-17.75f,-27.75f),
        new(110.5f,-13.75f,-23.25f),
        new(106.721466f,-14.000001f,-22.213255f)];
    private static readonly Vector3[] Shirogane11=[
        new(100.3069f,30.02f,25.35472f),
        new(87.0f,30.25f,20.0f),
        new(85.75f,30.25f,20.0f),
        new(76.0f,23.0f,32.75f),
        new(70.10685f,22.0f,42.908703f),
        new(75.25f,22.25f,51.0f),
        new(77.80867f,22.0f,53.54726f)];
    private static readonly Vector3[] Shirogane3=[
        new(-5.845466f,10.020003f,100.10435f),
        new(25.75f,10.25f,115.75f),
        new(24.75f,9.75f,118.5f),
        new(22.0f,6.5f,124.75f),
        new(16.72184f,5.999982f,130.41754f)];
    public static Vector3[]? Guide(uint territory,Vector3 start,Vector3 goal){
        Vector3[][] streets=territory switch{339=>[MistUpperStreet],341=>[Goblet22],641=>[Shirogane11,Shirogane3],_=>[]};
        foreach(var street in streets){
            var arrivalMargin=territory==341?8:6;
            if(Vector3.Distance(start,street[0])<=6&&Vector3.Distance(goal,street[^1])<=arrivalMargin)
                return street.Append(goal).ToArray();
            if(Vector3.Distance(start,street[^1])<=arrivalMargin&&Vector3.Distance(goal,street[0])<=6)
                return street.Reverse().Append(goal).ToArray();
        }
        return null;
    }
}
