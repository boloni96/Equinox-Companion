using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
namespace EquinoxCompanion;
internal sealed record MaintenanceNotice(string Title,string Url,DateTimeOffset? Start,DateTimeOffset? End,bool Completed,DateTimeOffset? Published=null)
{
 internal bool Relevant(string world,string dc)=>Title.Contains("All Worlds",StringComparison.OrdinalIgnoreCase)||world.Length>0&&Title.Contains(world,StringComparison.OrdinalIgnoreCase)||dc.Length>0&&Title.Contains(dc,StringComparison.OrdinalIgnoreCase);
 internal string Identity=>$"{Url}/{Start:O}/{End:O}/{Completed}";
 internal string Message(DateTimeOffset now,TimeZoneInfo zone)
 {
  string Local(DateTimeOffset at)=>TimeZoneInfo.ConvertTime(at,zone).ToString("MMM d, h:mm tt",CultureInfo.InvariantCulture);
  string Left(DateTimeOffset at){var span=at-now;var minutes=Math.Max(0,(int)Math.Ceiling(span.TotalMinutes));return $"{minutes/1440}d {minutes%1440/60}h {minutes%60}m";}
  if(Completed)return Title+" · Completion announced. Check the official notice.";
  if(Start is not {} start||End is not {} end)return Title+" · Schedule not confirmed automatically; check the official notice.";
  if(now<start)return $"{Title} · Starts {Local(start)} ({zone.Id}) · in {Left(start)}. Estimated end {Local(end)}; subject to change.";
  if(now<end)return $"{Title} · Within the scheduled maintenance window. Estimated return {Local(end)} ({zone.Id}) · in {Left(end)}; subject to change.";
  return $"{Title} · Scheduled end {Local(end)} ({zone.Id}) has passed; completion is unconfirmed. Check official updates.";
 }
}
internal sealed class MaintenanceSession
{
 internal ulong Character; internal DateTimeOffset Due; private readonly HashSet<string> sent=[];
 internal void Reset(){Character=0;Due=default;sent.Clear();}
 internal void Observe(ulong id,DateTimeOffset now){if(id!=0&&id!=Character){Reset();Character=id;Due=now.AddMinutes(5);}}
 internal bool Ready(DateTimeOffset now)=>Character!=0&&now>=Due;
 internal bool New(MaintenanceNotice n)=>sent.Add(n.Identity);
}
internal sealed class MaintenanceService:IDisposable
{
 internal const string Index="https://na.finalfantasyxiv.com/lodestone/news/category/2";
 private readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(20)};
 internal static string Plain(string s)=>WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(s,@"<script\b[^>]*>[\s\S]*?</script>","",RegexOptions.IgnoreCase),@"<[^>]+>"," ")).Trim();
 internal static bool Game(string title)=>Regex.IsMatch(title,@"maintenance",RegexOptions.IgnoreCase)&&Regex.IsMatch(title,@"\b(all worlds|worlds?|data cent(er|re))\b",RegexOptions.IgnoreCase)&&!Regex.IsMatch(title,@"lodestone|companion app|mog station|online store",RegexOptions.IgnoreCase);
 internal static MaintenanceNotice Parse(string html,string url)
 {
  var header=Regex.Match(html,"<header[^>]*class=\"news__header\"[^>]*>([\\s\\S]*?)</header>").Groups[1].Value;
  var title=Plain(Regex.Match(header,@"<h1[^>]*>([\s\S]*?)</h1>").Groups[1].Value);
  var body=Regex.Match(html,"<div[^>]*class=\"news__detail__wrapper\"[^>]*>([\\s\\S]*?)</div>").Groups[1].Value;
  if(!Game(title)||body.Length==0)throw new FormatException("Unrecognized maintenance notice");
  var schedule=Plain(Regex.Match(body,@"\[Date\s*&(?:amp;)?\s*Time\]\s*<br\s*/?>\s*([^<]+)",RegexOptions.IgnoreCase).Groups[1].Value);
  var zone=Regex.Match(schedule,@"\((PDT|PST|UTC|GMT|BST|JST)\)").Groups[1].Value;
  var halves=Regex.Replace(schedule,@"\([^)]*\)","").Split(" to ");
  DateTimeOffset? start=null,end=null;
  if(zone.Length>0&&halves.Length==2)
  {
   var offset=TimeSpan.FromHours(zone switch{"PDT"=>-7,"PST"=>-8,"BST"=>1,"JST"=>9,_=>0});
   string Clean(string v)=>Regex.Replace(v,@"\b(Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\.","$1").Replace("a.m.","AM").Replace("p.m.","PM").Trim();
   var a=Clean(halves[0]);var b=Clean(halves[1]);
   if(!Regex.IsMatch(b,@"\b\d{4}\b"))b=Regex.Match(a,@"^[A-Za-z]+\s+\d{1,2},?\s+\d{4}").Value+" "+b;
   if(DateTime.TryParse(a,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var first)&&DateTime.TryParse(b,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var last))
   {var x=new DateTimeOffset(DateTime.SpecifyKind(first,DateTimeKind.Unspecified),offset);var y=new DateTimeOffset(DateTime.SpecifyKind(last,DateTimeKind.Unspecified),offset);if(y>x&&y-x<=TimeSpan.FromDays(7)){start=x.ToUniversalTime();end=y.ToUniversalTime();}}
  }
  var completed=Regex.IsMatch(title,@"\[completed\]|\(completed\)",RegexOptions.IgnoreCase)||Regex.IsMatch(Plain(body),@"maintenance\s+(has been|was|is now)\s+completed",RegexOptions.IgnoreCase);
  var epoch=Regex.Match(header,@"ldst_strftime\((\d{10}),").Groups[1].Value;
  return new(title,url,start,end,completed,long.TryParse(epoch,out var seconds)?DateTimeOffset.FromUnixTimeSeconds(seconds):null);
 }
 internal async Task<MaintenanceNotice[]> Fetch(CancellationToken token)
 {
  async Task<string> Get(string url){var s=await http.GetStringAsync(url,token).ConfigureAwait(false);if(s.Length>2000000)throw new FormatException("Oversized notice");return s;}
  var index=await Get(Index);var links=new List<string>();
  foreach(Match m in Regex.Matches(index,"<a\\b[^>]*href=\"(/lodestone/news/detail/[a-f0-9]{40})\"[^>]*>([\\s\\S]*?)</a>"))
  {var title=Plain(Regex.Match(m.Groups[2].Value,"<p[^>]*class=\"news__list--title\"[^>]*>([\\s\\S]*?)</p>").Groups[1].Value);if(Game(title))links.Add("https://na.finalfantasyxiv.com"+m.Groups[1].Value);}
  if(links.Count==0)throw new FormatException("No game notices found");
  var fetched=await Task.WhenAll(links.Distinct().Take(8).Select(async url=>{try{return Parse(await Get(url),url);}catch(Exception) when(!token.IsCancellationRequested){return null;}})).ConfigureAwait(false);
  var rows=fetched.OfType<MaintenanceNotice>().ToArray();if(rows.Length==0)throw new FormatException("No maintenance details available");
  // Newest official listing wins over the corresponding older follow-up chain.
  return rows.GroupBy(n=>Regex.Replace(n.Title,@"\[[^\]]*\]","").Split('(')[0].Trim(),StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
 }
 public void Dispose()=>http.Dispose();
}
