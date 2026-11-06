using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class InteractionLimits456Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Equal(float actual,float expected){if(actual!=expected)throw new Exception($"Interaction limit {actual} != {expected}");}
        Equal(InteractionApproachPolicy.Limit(8,true,true),10.5f);
        Equal(InteractionApproachPolicy.Limit(2,true,true,1),8f);
        Equal(InteractionApproachPolicy.Limit(9,true,true,2),6f);
        Equal(InteractionApproachPolicy.Limit(8,true,false),6f);
        Equal(InteractionApproachPolicy.Limit(8,true,false,1),5f);
        Equal(InteractionApproachPolicy.Limit(8,true,false,2),4f);
        Equal(InteractionApproachPolicy.Limit(2002737,false,false),4.5f);
        Equal(InteractionApproachPolicy.Limit(2002737,false,false,1),3.2f);
        Equal(InteractionApproachPolicy.Limit(2008126,false,false),3.8f);
        Equal(InteractionApproachPolicy.Limit(2005124,false,false),4f);
        Equal(InteractionApproachPolicy.Limit(2002738,false,false),4f);
        Equal(InteractionApproachPolicy.Limit(2004353,false,false),3.2f);
        Equal(InteractionApproachPolicy.Limit(123,false,false),3.2f);
        foreach(var attempt in new[]{0,1,2}){
            var limit=InteractionApproachPolicy.Limit(8,true,true,attempt);
            if(InteractionApproachPolicy.WithinDistance(Vector3.Zero,new(limit+.1f,0,0),limit))throw new Exception("Outside target-specific limit accepted.");
            if(!InteractionApproachPolicy.WithinDistance(Vector3.Zero,new(limit-.1f,0,0),limit))throw new Exception("Inside limit rejected.");
        }
        if(InteractionApproachPolicy.WithinDistance(Vector3.Zero,new(10,0,0),InteractionApproachPolicy.Limit(8,true,true,1)))throw new Exception("Closer retry must not stop at the initial range.");
        Console.WriteLine("Observed target distances, unmeasured defaults and closer retry limits passed.");
    }
}
