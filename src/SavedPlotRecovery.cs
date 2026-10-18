using System.Text.Json;
namespace EquinoxCompanion;
internal static class SavedPlotRecovery
{
 internal static BidFavorite[] Clean(IEnumerable<BidFavorite>? plots)=>(plots??[]).Where(p=>p is not null&&p.World>0&&p.World<=65535&&p.Ward is >=1 and <=30&&p.Plot is >=1 and <=60&&HousingLottery.District(p.District??"") is not null).Select(p=>p with{District=HousingLottery.District(p.District)!,Notes=p.Notes??"",WorldName=p.WorldName??""}).DistinctBy(p=>p.Key).ToArray();
 internal static bool ContainsAll(IEnumerable<BidFavorite> expected,IEnumerable<BidFavorite>? actual){
  if(actual is null)return false;var keys=actual.Select(p=>p.Key).ToHashSet();return expected.All(p=>keys.Contains(p.Key));
 }
 internal static BidFavorite[] Read(string json){
  using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});
  var value=doc.RootElement;if(value.ValueKind==JsonValueKind.Object){if(!value.TryGetProperty("BidFavorites",out value))return [];}
  if(value.ValueKind!=JsonValueKind.Array)return [];
  return Clean(JsonSerializer.Deserialize<BidFavorite[]>(value.GetRawText(),new JsonSerializerOptions{PropertyNameCaseInsensitive=true}));
 }
}
