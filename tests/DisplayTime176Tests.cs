using System.Globalization;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class DisplayTime176Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string label){if(!ok)throw new Exception("176: "+label);}
  var culture=CultureInfo.CurrentCulture;
  try{
   CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-PT");
   var at=DateTimeOffset.Parse("2026-10-17T19:52:28.9161402+00:00",CultureInfo.InvariantCulture);
   var expected=at.ToLocalTime().ToString("dd MMM yyyy, HH:mm",CultureInfo.InvariantCulture)+" (local)";
   Check(DisplayTime.InText("Fashion Report · 91/100 · observed 2026-10-17T19:52:28.9161402+00:00 · Automatically confirmed")=="Fashion Report · 91/100 · observed "+expected+" · Automatically confirmed","screenshot timestamp and score preserved");
   Check(DisplayTime.InText("2026-10-17T21:52:28+02:00")==expected,"source offset converted to local time");
   Check(DisplayTime.InText("2026-10-17T19:52:28Z")==expected,"UTC suffix");
   Check(DisplayTime.InText("2026-99-17T19:52:28Z")=="2026-99-17T19:52:28Z","invalid timestamp preserved");
   Check(DisplayTime.InText("0/100 · W10 P10 · Pending")=="0/100 · W10 P10 · Pending","non-time details untouched");
  }finally{CultureInfo.CurrentCulture=culture;}
  Console.WriteLine("PASS 176 hover timestamps, explicit offsets, Portuguese locale and unchanged task details");
 }
}
