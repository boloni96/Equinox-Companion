using System.Runtime.CompilerServices;
using System.Text.Json;
using EquinoxCompanion;
internal static class Bid143Tests
{
 [ModuleInitializer]internal static void Run(){
  void Check(bool ok,string label){if(!ok)throw new Exception("143: "+label);}
  var f=new BidFavorite(86,"Mist",6,16);var p=new BidPlot(86,"Mist",6,16,0,3000000,null,null,null,7,0,0);
  Check(f.Key==p.Key,"favorite matches report address");Check((f with{World=87}).Key!=f.Key,"world-specific address");
  var config=new Configuration{BidFavorites=[f]};var restored=JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(config))!;
  Check(restored.BidFavorites.Single()==f,"saved favorites round trip");
  var snapshot=config.Snapshot();config.BidFavorites.Clear();Check(snapshot.BidFavorites.Single()==f,"pending save snapshot isolated from later toggles");
  Check(JsonSerializer.Deserialize<Configuration>("{}")!.BidFavorites.Count==0,"existing config defaults empty");
  Console.WriteLine("PASS 143 favorite identity, persistence and snapshot isolation");
 }
}
