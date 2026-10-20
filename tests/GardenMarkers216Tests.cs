using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;

public static class GardenMarkers216Tests
{
    [ModuleInitializer] public static void Run()
    {
        void Check(bool ok,string label){if(!ok)throw new Exception("GardenMarkers216: "+label);}
        (uint Id,string Name)[] offered=[(7731,"Mirror Apple Seeds"),(7732,"Sun Lemon Seeds"),(7731,"Mirror Apple Seeds")];
        Check(GardenPlantRequirement.TooltipItem("Mirror Apple Seeds",offered)==7731,"shared bag artwork resolves the exact seed ID");
        Check(GardenPlantRequirement.TooltipItem("Sun Lemon Seeds",offered)==7732,"another seed keeps its own ID");
        Check(GardenPlantRequirement.TooltipItem("Mirror Apple",offered)==0,"partial crop name cannot select a seed");
        Check(GardenPlantRequirement.TooltipItem("Unknown Seeds",offered)==0,"unoffered item remains unknown");
        Check(GardenPlantRequirement.TooltipItem("Graine",[(1u,"Graine"),(2u,"Graine")])==0,"ambiguous localized names cannot become green");
        Check(GardenPlantRequirement.TooltipItem("Apfelsamen",[(7731u,"Apfelsamen")])==7731,"exact localized tooltip resolves the offered ID");
        var required=new Dictionary<uint,bool>{{7731,true},{7732,false}};
        Check(GardenPlantRequirement.Required(GardenPlantRequirement.TooltipItem("Mirror Apple Seeds",offered),required)==true,"required seed is green");
        Check(GardenPlantRequirement.Required(GardenPlantRequirement.TooltipItem("Sun Lemon Seeds",offered),required)==false,"other seed is red");
        Check(GardenPlantRequirement.IconMatch(9,[(9u,true),(9u,false)]) is null,"ambiguous artwork still cannot invent green");
        var icon=new Vector4(100,100,150,150);
        Check(!GardenPlantRequirement.MarkerCovered(icon,[]),"no tooltip leaves marker visible");
        Check(GardenPlantRequirement.MarkerCovered(icon,[new(120,120,400,400)]),"partial tooltip overlap hides marker");
        Check(GardenPlantRequirement.MarkerCovered(icon,[new(90,90,160,160)]),"full tooltip overlap hides marker");
        Check(!GardenPlantRequirement.MarkerCovered(icon,[new(160,100,400,400)]),"uncovered neighbouring marker stays visible");
        Check(GardenPlantRequirement.MarkerCovered(icon,[new(160,100,400,400),new(110,110,120,120)]),"either native tooltip can occlude a marker");
    }
}
