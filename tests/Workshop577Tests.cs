using EquinoxCompanion;
using System.Numerics;
using System.Runtime.CompilerServices;
internal static class Workshop577Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop arrival/project "+name);}
        var station=new Vector3(.045776367f,.2288208f,-5.2339478f);
        Check("lower workshop floor is within interaction radius",InteractionApproachPolicy.WithinDistance(new(.5402946f,-2f,-3.1120329f),station,4f));
        Check("top of stairs remains outside radius",!InteractionApproachPolicy.WithinDistance(new(.5402946f,0,4.887967f),station,4f));
        Check("horizontal proximity does not ignore another floor",!InteractionApproachPolicy.WithinDistance(station-new Vector3(0,5,0),station,4f));
        var project=new WorkshopProject(528,21792,"Queued project",[],1,1);
        Check("captured wheel stand is rejected",!WorkshopPolicy.MatchesProject(1,9462,project));
        Check("sequence alone is insufficient",!WorkshopPolicy.MatchesProject(528,9462,project));
        Check("result alone is insufficient",!WorkshopPolicy.MatchesProject(1,21792,project));
        Check("matching sequence and result accepted",WorkshopPolicy.MatchesProject(528,21792,project));
    }
}
