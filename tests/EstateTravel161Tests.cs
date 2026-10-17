using System.Runtime.CompilerServices;
namespace EquinoxCompanion.Tests;
public static class EstateTravel161Tests
{
 [ModuleInitializer] public static void Run()
 {
  if(!CharacterTravelPolicy.NeedsHomeReturn(10,10,20))throw new Exception("Visiting another world must return home before estate lookup");
  if(CharacterTravelPolicy.NeedsHomeReturn(10,10,10))throw new Exception("Already home: use estate list directly");
  if(CharacterTravelPolicy.NeedsHomeReturn(20,10,30))throw new Exception("Another character's world must not redirect to our home");
  if(CharacterTravelPolicy.NeedsHomeReturn(0,0,30))throw new Exception("Unknown home must not start travel");
  if(CharacterTravelPolicy.SameHomeTraveler(123,456)||CharacterTravelPolicy.SameHomeTraveler(0,0)||!CharacterTravelPolicy.SameHomeTraveler(123,123))throw new Exception("Estate continuation must remain with the original character");
  Console.WriteLine("PASS 161 home-world travel priority and original-character continuation guards");
 }
}
