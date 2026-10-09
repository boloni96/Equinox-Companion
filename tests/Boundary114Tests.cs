using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
public static class Boundary114Tests
{
 [ModuleInitializer] public static void Run(){
    void Check(bool ok,string label){if(!ok)throw new Exception("Boundary114: "+label);}
    var m=new FollowBoundaryMotion();var t=DateTimeOffset.FromUnixTimeSeconds(100);
    Check(!m.TryDirection(t,out _),"no invented direction");
    m.Observe(new(0,0,1),t);m.Observe(Vector3.Zero,t.AddMilliseconds(100));
    Check(m.TryDirection(t.AddMilliseconds(400),out var d)&&d==Vector3.UnitZ,"stationary loading frame retains movement");
    Check(m.TryDirection(t.AddMilliseconds(1500),out _),"bounded grace");
    Check(!m.TryDirection(t.AddMilliseconds(1501),out _),"stale direction rejected");
    m.Observe(new(1,0,0),t.AddSeconds(2));
    Check(m.TryDirection(t.AddSeconds(2),out d)&&d==Vector3.UnitX,"latest turn used");
    m.Observe(new(20,0,0),t.AddSeconds(2));Check(!m.TryDirection(t.AddSeconds(2),out _),"position jump rejected");
    m.Observe(new(0,0,1),t.AddSeconds(3));m.Reset();Check(!m.TryDirection(t.AddSeconds(3),out _),"new area resets direction");
 }
}
