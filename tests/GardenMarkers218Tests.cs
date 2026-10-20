using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class GardenMarkers218Tests
{
    [ModuleInitializer] public static void Run()
    {
        void Check(bool value,string why){if(!value)throw new Exception("GardenMarkers218: "+why);}
        var soilRow=new Vector4(230,181,280,233);
        Check(GardenPlantRequirement.WithinRow(new(233,185,277,229),soilRow),"visible soil image belongs to its native row");
        Check(!GardenPlantRequirement.WithinRow(new(233,240,277,284),soilRow),"cached template below soil row cannot draw a ghost border");
        Check(!GardenPlantRequirement.WithinRow(new(398,185,442,229),soilRow),"another soil's reused image cannot inherit green");
        Check(GardenPlantRequirement.WithinRow(new(400,300,488,388),new(396,296,492,392)),"scaled game UI keeps icon attached to live row");
        Check(!GardenPlantRequirement.WithinRow(new(100,100,146,146),new(0,0,1920,1080)),"whole window cannot masquerade as a picker row");
        var offered=new[]{(101u,"Mirror Apple Seeds"),(102u,"Old World Fig Seeds"),(103u,"Potting Soil")};
        Check(GardenPlantRequirement.TooltipItem("Mirror Apple Seeds",offered)==101,"exact displayed name distinguishes identical seed artwork");
        Check(GardenPlantRequirement.TooltipItem("Old World Fig Seeds",offered)==102,"each live seed row keeps its own identity");
        Check(GardenPlantRequirement.TooltipItem("Mirror Apple",offered)==0,"partial display name cannot become a green guess");
        Check(GardenPlantRequirement.TooltipItem("Potting Soil",[(103u,"Potting Soil"),(104u,"Potting Soil")])==0,"ambiguous localized name leaves row unmarked");
        Check(GardenPlantRequirement.TooltipItem("Potting Soil",[(103u,"Potting Soil"),(103u,"Potting Soil")])==103,"duplicate inventory stacks preserve same exact item");
        Check(GardenPlantRequirement.MarkerCovered(new(230,181,280,233),[new(220,170,500,400)]),"native tooltip still covers its overlapping border");
        Console.WriteLine("PASS 218 live picker row bounds, ghost template rejection and exact seed identity");
    }
}
