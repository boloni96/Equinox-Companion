using System.Numerics;
using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Waypoint503Tests
{
 [ModuleInitializer] internal static void Run(){
  // An onward segment passes close to the player but its far endpoint lies
  // beyond an obstacle. A bounded 2.2-yalm check used to discard the corner.
  Vector3[] path=[new(0,0,-1),new(0,0,1),Vector3.Zero,new(5,0,0)];
  var position=Vector3.Zero;
  if(WalkingSteering.AdvanceWaypoint(position,path,1,(_,b)=>b.X<3)!=1)
   throw new Exception("Partial clear join must not discard intervening waypoints");
  if(WalkingSteering.AdvanceWaypoint(position,path,1,(_,_)=>true)!=3)
   throw new Exception("Fully clear onward join should remain available");
  Vector3[] straight=[Vector3.Zero,new(0,0,1),new(0,0,2),new(0,0,3)];
  if(WalkingSteering.AdvanceWaypoint(new(0,0,1),straight,1,(_,_)=>false)!=2)
   throw new Exception("Observed arrival must still consume a reached waypoint");
  Console.WriteLine("503 complete waypoint connector, clear shortcut and observed arrival passed.");
 }
}
