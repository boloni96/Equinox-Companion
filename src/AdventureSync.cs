using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;
public sealed record AdventureTask(string Id,string Label,string Group,bool Done,bool CanCheck,string Status="",bool ServerConfirmed=false,AdventureTaskRule? Rule=null);
public sealed record AdventureTaskRule(string Kind="",string HouseId="",int Batch=0,int Bed=0,string Cid="",uint[]? Quests=null);
public sealed record AdventurePortrait(string Id,string Hash);
public sealed record AdventureStop(string Title,string Subtitle,string HouseId,int Batch,AdventureTask[] Tasks,string World="",string[]? CharacterIds=null,AdventurePortrait[]? Portraits=null);
public sealed record AdventureIntent(string Id,string TaskId,bool Checked);
public sealed record AdventurePresence(string World,string At,string Area="",uint Territory=0,string HouseId="");
public sealed record AdventureBoard(string Id,string Generation,string BuiltAt,string Kind,string Title,AdventureStop[] Stops,int Index,long Revision,AdventureIntent[]? Pending=null,int Protocol=1,bool TravelGrouping=false,Dictionary<string,AdventurePresence>? Locations=null,bool PresenceReporting=false);
public sealed record AdventureBoards(AdventureBoard[] Boards);
public sealed record AdventureReply(AdventureBoard[]? Boards,string Status);
public static class AdventurePolicy
{
 public static bool Valid(AdventureBoard b)=>b is not null&&b.Id is {Length:>0 and <=160}&&b.Generation is {Length:>0 and <=100}&&b.Title is {Length:<=160}&&b.Kind is "garden" or "multi" or "solo"&&b.Stops is {Length:<=500}&&b.Index>=0&&b.Index<=b.Stops.Length&&b.Revision>0&&b.Stops.All(s=>s is not null&&s.Title is {Length:<=300}&&s.Subtitle is {Length:<=500}&&(s.Portraits is null||s.Portraits.Length<=200&&s.Portraits.All(p=>p is not null&&p.Id is {Length:>0 and <=100}&&System.Text.RegularExpressions.Regex.IsMatch(p.Hash??"","^[a-f0-9]{64}$")))&&s.Tasks is not null&&s.Tasks.All(t=>t is not null&&t.Id is {Length:<=500}&&t.Label is {Length:<=500}&&t.Group is {Length:<=300}))&&b.Stops.Sum(s=>s.Tasks.Length)<=10000;
 public static int? FollowIndex(AdventureBoard board,string characterId,string world)
 {
  var matches=board.Stops.Select((s,i)=>(Stop:s,Index:i)).Where(x=>string.Equals(x.Stop.World,world,StringComparison.OrdinalIgnoreCase)).ToArray();
  var exact=matches.Where(x=>characterId.Length>0&&x.Stop.CharacterIds?.Contains(characterId)==true).ToArray();if(exact.Length>0)matches=exact;
  if(matches.Any(x=>x.Index==board.Index&&!Complete(x.Stop)))return null;
  var next=matches.FirstOrDefault(x=>!Complete(x.Stop));return next.Stop is null||next.Index==board.Index?null:next.Index;
 }
 public static string CurrentHouse(Address? address,uint currentWorld,IEnumerable<SharedHouse> houses)
 {
  if(address is null||address.Apartment||address.Room!=0&&!address.Workshop||address.Plot<1||address.WorldId!=currentWorld)return "";
  static string Norm(string? text)=>(text??"").Trim().ToLowerInvariant();
  static string District(string? text)=>Norm(text).StartsWith("the ")?Norm(text)[4..]:Norm(text);
  var matches=houses.Where(h=>Norm(h.World)==Norm(address.WorldName)&&District(h.District)==District(address.DistrictName)&&h.Ward==address.Ward&&h.Plot==address.Plot).Select(h=>h.Id).Distinct().Take(2).ToArray();
  return matches.Length==1?matches[0]:"";
 }
 public static int? HouseIndex(AdventureBoard board,string houseId,string world)
 {
  if(houseId.Length==0)return null;
  var matches=board.Stops.Select((s,i)=>(Stop:s,Index:i)).Where(x=>string.Equals(x.Stop.World,world,StringComparison.OrdinalIgnoreCase)&&(x.Stop.HouseId==houseId||x.Stop.Tasks.Any(t=>TaskHouseId(t)==houseId))).ToArray();
  if(matches.Any(x=>x.Index==board.Index))return board.Index;
  var next=matches.FirstOrDefault(x=>!Complete(x.Stop));
  return next.Stop is not null?next.Index:matches.Length>0?matches[0].Index:null;
 }
 private static string LegacyTaskField(AdventureTask task,string field)
 {
  var start=task.Id.IndexOf('{');var end=task.Id.LastIndexOf('}');if(start<0||end<start)return "";
  try{using var json=JsonDocument.Parse(task.Id.Substring(start,end-start+1));return json.RootElement.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";}catch(JsonException){return "";}
 }
 public static bool IsGardenTask(AdventureTask task)=>task.Rule?.Kind is "care" or "harvest" or "plant" or "inspect" or "inspecthouse" or "empty" or "planplant" or "garden";
 public static bool IsFashion(AdventureTask task)=>task.Rule?.Kind=="fashion"||(task.Rule is null&&LegacyTaskField(task,"multiFashion").Length>0);
 public static bool IsHouseVisit(AdventureTask task)=>task.Rule?.Kind=="visit"||(task.Rule is null&&LegacyTaskField(task,"multiHouse").Length>0);
 public static string TaskHouseId(AdventureTask task)=>!string.IsNullOrEmpty(task.Rule?.HouseId)?task.Rule.HouseId:LegacyTaskField(task,"multiHouse");
 public static string TaskCharacterId(AdventureTask task,SharedCharacter[] characters)
 {
  var cid=task.Rule?.Cid;if(!string.IsNullOrEmpty(cid)&&characters.Any(c=>c.Id==cid))return cid;
  cid=LegacyTaskField(task,"multiFashion");if(cid.Length==0)cid=LegacyTaskField(task,"multiCharacter");
  if(cid.Length>0&&characters.Any(c=>c.Id==cid))return cid;
  var houseId=TaskHouseId(task);
  if(houseId.Length>0){var eligible=characters.Where(c=>c.Houses.Any(h=>h.Id==houseId)).ToArray();var owner=eligible.FirstOrDefault(c=>c.Houses.Any(h=>h.Id==houseId&&string.Equals(h.OwnerName.Trim(),c.Name.Trim(),StringComparison.OrdinalIgnoreCase)));return owner?.Id??eligible.FirstOrDefault()?.Id??"";}
  return characters.FirstOrDefault(c=>task.Group.StartsWith(c.Name+" · ",StringComparison.Ordinal)||task.Group==c.Name)?.Id??"";
 }
 public static AdventureBoard[] Reconcile(AdventureBoard[] current,AdventureBoard[] incoming,bool partial)
 {
  var old=current.ToDictionary(b=>b.Id);
  var next=incoming.Select(b=>old.TryGetValue(b.Id,out var previous)&&previous.Generation==b.Generation&&previous.Revision==b.Revision&&previous.Protocol==b.Protocol?previous:b).ToArray();
  if(partial){var updates=next.ToDictionary(b=>b.Id);return current.Select(b=>updates.GetValueOrDefault(b.Id)??b).Concat(next.Where(b=>!old.ContainsKey(b.Id))).ToArray();}
  return current.Length==next.Length&&current.Zip(next).All(pair=>ReferenceEquals(pair.First,pair.Second))?current:next;
 }
 public static bool CanEdit(AdventureTask task)=>task.CanCheck&&!task.ServerConfirmed;
 public static bool Complete(AdventureStop stop)=>stop.Tasks.Length>0&&stop.Tasks.All(t=>t.Done);
 public static bool Prompt(bool? previous,AdventureStop current)=>previous==false&&Complete(current);
}
public sealed class AdventureSync:IDisposable
{
 private readonly HttpClient client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};
 private readonly CancellationTokenSource cancel=new();
 private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 public async Task<AdventureReply> Read(string key)=>await Request(key,null);
 public async Task<AdventureReply> Move(string key,AdventureBoard b,int index)=>await Request(key,new{id=b.Id,generation=b.Generation,revision=b.Revision,action="move",index});
 public async Task<AdventureReply> Location(string key,AdventureBoard b,string cid,string world,string area,uint territory,string houseId)=>await Request(key,new{id=b.Id,generation=b.Generation,revision=b.Revision,action="location",cid,world,area,territory,houseId});
 public async Task<AdventureReply> Travel(string key,AdventureBoard b,string cid,string world,string area,uint territory,string houseId)=>await Request(key,new{id=b.Id,generation=b.Generation,revision=b.Revision,action="travel",cid,world,area,territory,houseId});
 public async Task<AdventureReply> Check(string key,AdventureBoard b,string taskId,bool value)=>await Request(key,new{id=b.Id,generation=b.Generation,revision=b.Revision,action="check",taskId,@checked=value});
 private async Task<AdventureReply> Request(string key,object? body)
 {
  try{
   using var request=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Patch,"https://equinoxjournal.pages.dev/api/companion/adventures");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);if(body is not null)request.Content=JsonContent.Create(body,options:Json);
   using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancel.Token);
   if(!response.IsSuccessStatusCode)return new(null,response.StatusCode==System.Net.HttpStatusCode.Conflict?"Adventure changed elsewhere. Refreshing; try your action again.":response.StatusCode==System.Net.HttpStatusCode.Unauthorized?"Pairing key invalid or revoked.":response.StatusCode==System.Net.HttpStatusCode.NotFound?"Deploy Journal V7.11.172 or newer for paired adventures.":$"Adventure connection unavailable (HTTP {(int)response.StatusCode}).");
   await response.Content.LoadIntoBufferAsync(4000000);var text=await response.Content.ReadAsStringAsync(cancel.Token);
   if(body is not null){var board=JsonSerializer.Deserialize<AdventureBoard>(text,Json);if(board is null||!AdventurePolicy.Valid(board))return new(null,"Invalid adventure response.");return new([board],"Action shared with the Journal.");}
   var result=JsonSerializer.Deserialize<AdventureBoards>(text,Json);if(result?.Boards is not {Length:<=100}||!result.Boards.All(AdventurePolicy.Valid))return new(null,"Invalid adventure response.");return new(result.Boards,"Paired adventures up to date.");
  }catch(OperationCanceledException){return new(null,"Adventure request timed out; your saved route is kept.");}catch(Exception){return new(null,"Adventure connection unavailable; retrying.");}
 }
 public void Dispose(){cancel.Cancel();client.Dispose();cancel.Dispose();}
}

// Follow changes in location, not server polling or checklist completion.
public sealed class AdventureLocationSession
{
 private string candidate="",settled="",settledContext="";private DateTimeOffset since;
 public void Reset(){candidate=settled=settledContext="";since=default;}
 public (bool Changed,bool ContextChanged) Observe(bool ready,string context,string house,DateTimeOffset now)
 {
  if(!ready){Reset();return(false,false);}
  var key=context+"|"+house;
  if(candidate!=key){candidate=key;since=now;return(false,false);}
  if(settled==key||now-since<TimeSpan.FromSeconds(1))return(false,false);
  var changed=settledContext!=context;settled=key;settledContext=context;return(true,changed);
 }
}
