using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace EquinoxCompanion;
public sealed record AdventureTask(string Id,string Label,string Group,bool Done,bool CanCheck);
public sealed record AdventurePortrait(string Id,string Hash);
public sealed record AdventureStop(string Title,string Subtitle,string HouseId,int Batch,AdventureTask[] Tasks,string World="",string[]? CharacterIds=null,AdventurePortrait[]? Portraits=null);
public sealed record AdventureIntent(string Id,string TaskId,bool Checked);
public sealed record AdventureBoard(string Id,string Generation,string BuiltAt,string Kind,string Title,AdventureStop[] Stops,int Index,long Revision,AdventureIntent[]? Pending=null);
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
