using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class FashionWeek515Tests
{
 [ModuleInitializer] internal static void Run(){
  static void Check(string at,int expected){if(FashionCompletion.WeekNumber(DateTimeOffset.Parse(at))!=expected)throw new Exception("Fashion week at "+at);}
  Check("2026-09-22T08:00:00Z",452);
  Check("2026-11-03T07:59:59Z",457);
  Check("2026-11-03T08:00:00Z",458);
  Check("2026-11-09T03:59:00Z",458);
  Check("2026-11-10T07:59:59Z",458);
  Check("2026-11-10T08:00:00Z",459);
  Check("2026-11-10T09:00:00+01:00",459);
  Check("2026-09-15T08:00:00Z",451);
  Console.WriteLine("515 Fashion Report week reset checks passed.");
 }
}
