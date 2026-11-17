using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Workshop578Tests
{
    [ModuleInitializer] internal static void Run()
    {
        void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS workshop scene return: "+name);}
        var gate=new WorkshopSceneReturn();var now=DateTimeOffset.UtcNow;
        Check("absent menu before scene is not completion",!gate.Observe(false,false,now));
        Check("active scene waits",!gate.Observe(true,false,now));
        Check("first return frame waits",!gate.Observe(false,false,now.AddSeconds(1)));
        Check("return without station menu completes",gate.Observe(false,false,now.AddSeconds(1.5)));
        gate.Reset();
        Check("previous project cannot release next transition",!gate.Observe(false,false,now.AddSeconds(2)));
        gate.Observe(true,false,now.AddSeconds(3));
        gate.Observe(false,false,now.AddSeconds(4));
        Check("loading breaks gameplay confirmation",!gate.Observe(false,true,now.AddSeconds(5)));
        Check("loading exit begins fresh debounce",!gate.Observe(false,false,now.AddSeconds(6)));
        Check("scene reappearing cancels debounce",!gate.Observe(true,false,now.AddSeconds(7)));
        Check("next return waits again",!gate.Observe(false,false,now.AddSeconds(8)));
        Check("naturally finished scene also resumes",gate.Observe(false,false,now.AddSeconds(9)));
    }
}
