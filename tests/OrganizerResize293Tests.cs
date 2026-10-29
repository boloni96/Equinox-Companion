using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class OrganizerResize293Tests
{
 [ModuleInitializer] internal static void Run(){
  void Check(string label,bool ok){if(!ok)throw new Exception(label);Console.WriteLine("PASS resize293 "+label);}
  var calls=0;var largest=0;
  float Measure(string s){calls++;largest=Math.Max(largest,s.Length);return s.Length;}
  var fitted=BoundedUiText.Fit(new string('x',2_000_000),80,Measure);
  Check("huge status has bounded measurement work",fitted.Length==80&&fitted.EndsWith("…")&&calls<=12&&largest<=513);
  Check("short text unchanged",BoundedUiText.Fit("Ready",80,Measure)=="Ready");
  Check("narrow footer",BoundedUiText.Fit("Ready",0,Measure)==""&&BoundedUiText.Fit("Ready",.5f,Measure)=="");
  Check("single line",BoundedUiText.Fit("a\nb",80,Measure)=="a b");
  Check("surrogate pair not split",BoundedUiText.Prefix("a😀b",2)=="a");
  var gate=new WindowResizeGate();
  Check("ordinary click does not pause",!gate.Update(720,500,true,false,1000));
  Check("resize grip pauses before dimensions change",gate.Update(720,500,true,true,1010));
  Check("drag remains paused",gate.Update(600,400,true,false,1100));
  Check("holding grip remains paused",gate.Update(600,400,true,false,1400));
  Check("release settles then resumes",gate.Update(600,400,false,false,1450)&&!gate.Update(600,400,false,false,1521));
  Check("programmatic resize does not latch",!gate.Update(720,500,false,false,1600));
  Check("interior column resize excluded",!WindowResizeGate.OnBorder(350,250,720,500));
  Check("bottom right grip accepted",WindowResizeGate.OnBorder(712,492,720,500));
  Check("other windows excluded",!WindowResizeGate.OnBorder(900,492,720,500));
 }
}
