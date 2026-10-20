using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class GardenMarkers217Tests
{
    [ModuleInitializer] public static void Run()
    {
        void Check(bool ok,string label){if(!ok)throw new Exception("GardenMarkers217: "+label);}
        Check(GardenPlantRequirement.VisibleCover(true,255,true),"visible native window covers borders");
        Check(!GardenPlantRequirement.VisibleCover(true,0,true),"transparent retained tooltip does not hide the whole picker");
        Check(!GardenPlantRequirement.VisibleCover(true,255,false),"hidden native root cannot suppress markers");
        Check(!GardenPlantRequirement.VisibleCover(false,255,true),"closed native addon cannot suppress markers");
        Check(GardenPlantRequirement.ValidMarkerBounds(new(100,100,146,146)),"normal icon image is eligible");
        Check(GardenPlantRequirement.ValidMarkerBounds(new(200,160,292,252)),"scaled UI icon is eligible");
        Check(!GardenPlantRequirement.ValidMarkerBounds(default),"zero-size outer interaction node leaves fallback available");
        Check(!GardenPlantRequirement.ValidMarkerBounds(new(100,100,400,140)),"wide interaction row cannot become a displaced border");
        Check(!GardenPlantRequirement.ValidMarkerBounds(new(0,0,1920,1080)),"whole native window is not an icon");
        Check(!GardenPlantRequirement.ValidMarkerBounds(new(100,100,50,50)),"inverted bounds are invalid");
        Check(!GardenPlantRequirement.ValidMarkerBounds(new(float.NaN,100,150,150)),"invalid geometry cannot draw");
        var icon=new Vector4(100,100,146,146);
        Check(!GardenPlantRequirement.MarkerCovered(icon,[new(400,400,700,600)]),"unrelated confirmation does not hide every border");
        Check(GardenPlantRequirement.MarkerCovered(icon,[new(90,90,300,300)]),"covered icon remains hidden behind confirmation");
        Check(!GardenPlantRequirement.MarkerCovered(new(350,100,396,146),[new(90,90,300,300)]),"other icons remain visible beside the tooltip");
        Check(GardenPlantRequirement.IconMatch(9,[(9u,true),(9u,false)]) is null,"same-looking seed bags still require exact identity");
    }
}
