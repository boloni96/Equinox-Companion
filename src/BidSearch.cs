using System.Net.Http;
using System.Text.Json;
namespace EquinoxCompanion;
internal sealed record BidPlot(uint World,string District,int Ward,int Plot,int Size,long Price,int? Entries,int? Phase,long? PhaseUntil,int Purchase,double Updated,double FirstSeen)
{
 public string Key=>$"{World}|{District}|{Ward}|{Plot}";
 public bool Current(DateTimeOffset now)=>Phase is >=1 and <=3&&PhaseUntil is {} end&&end>now.ToUnixTimeSeconds();
 public int PhaseCategory(DateTimeOffset now)=>(Purchase&1)==0?3:Current(now)?Phase!.Value-1:4;
 public string PhaseLabel(DateTimeOffset now)=>PhaseNames[PhaseCategory(now)];
 internal static readonly string[] PhaseNames=["Accepting Entries","Results","Unavailable","FCFS","Missing/Outdated"];
 public string Tenants=>(Purchase&6) switch {2=>"Free Company",4=>"Individual",6=>"Free Company / Individual",_=>"Unknown"};
}
internal sealed class BidSearch:IDisposable
{
 private readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(25)};
 public BidSearch(){http.DefaultRequestHeaders.UserAgent.ParseAdd("EquinoxCompanion/0.5.1.144");}
 public async Task<BidPlot[]> Search(uint world,CancellationToken token){using var r=await http.GetAsync($"https://paissadb.zhu.codes/worlds/{world}",HttpCompletionOption.ResponseHeadersRead,token);r.EnsureSuccessStatusCode();await r.Content.LoadIntoBufferAsync(6000000,token);return Parse(await r.Content.ReadAsStringAsync(token),world);}
 internal static BidPlot[] Parse(string json,uint world){
  using var doc=JsonDocument.Parse(json);var root=doc.RootElement;if(root.GetProperty("id").GetUInt32()!=world)throw new JsonException("World mismatch");var rows=new List<BidPlot>();
  foreach(var d in root.GetProperty("districts").EnumerateArray()){
   var name=HousingLottery.District(d.GetProperty("name").GetString()??"");if(name is null)continue;
   foreach(var p in d.GetProperty("open_plots").EnumerateArray()){
    int? optional(string key)=>p.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt32(out var n)?n:null;
    var ward=p.GetProperty("ward_number").GetInt32()+1;var plot=p.GetProperty("plot_number").GetInt32()+1;var size=p.GetProperty("size").GetInt32();var price=p.GetProperty("price").GetInt64();var updated=p.GetProperty("last_updated_time").GetDouble();var first=p.GetProperty("first_seen_time").GetDouble();
    if(p.GetProperty("world_id").GetUInt32()!=world||ward is <1 or >60||plot is <1 or >60||size is <0 or >2||price<0||!double.IsFinite(updated)||!double.IsFinite(first))continue;
    var entries=optional("lotto_entries");var phase=optional("lotto_phase");rows.Add(new(world,name,ward,plot,size,price,entries>=0?entries:null,phase,optional("lotto_phase_until"),p.GetProperty("purchase_system").GetInt32(),updated,first));
   }
  }return rows.DistinctBy(p=>p.Key).ToArray();
 }
 internal static BidPlot[] Sort(IEnumerable<BidPlot> rows,int column,bool descending)=>rows.OrderBy(p=>column==5&&!p.Entries.HasValue||column==6&&!p.Phase.HasValue?1:0).ThenBy(p=>column switch{0=>Array.IndexOf(HousingLotteryDistricts,p.District),1=>p.Ward,2=>p.Plot,3=>p.Size,4=>p.Price,5=>p.Entries??0,6=>p.Phase??0,7=>p.Purchase&6,8=>p.Updated,9=>p.FirstSeen,_=>0},descending?Comparer<double>.Create((a,b)=>b.CompareTo(a)):Comparer<double>.Default).ThenBy(p=>Array.IndexOf(HousingLotteryDistricts,p.District)).ThenBy(p=>p.Ward).ThenBy(p=>p.Plot).ThenBy(p=>p.World).ToArray();
 internal static (int Column,bool Descending) NextSort(int column,bool descending,int clicked)=>column!=clicked?(clicked,false):!descending?(clicked,true):(-1,false);
 internal static readonly string[] HousingLotteryDistricts=["Mist","Lavender Beds","Goblet","Shirogane","Empyreum"];
 public void Dispose()=>http.Dispose();
}
public sealed record SharedHousingBid(string District,int Ward,int Plot,int Entry,string Buyer,string FcId,string Status,string CycleStart,string Evidence,uint WorldId,string World,DateTimeOffset At,int? Winner=null,DateTimeOffset? ResultAt=null,string CheckedBy="",DateTimeOffset? ClaimedAt=null);

internal sealed class BidFilters
{
 internal readonly HashSet<int> Districts=[0,1,2,3,4], Wards=[..Enumerable.Range(1,60)], Plots=[..Enumerable.Range(1,60)], Sizes=[0,1,2], Phases=[0], Tenants=[2,4];
 internal bool Matches(BidPlot p,DateTimeOffset now)=>Districts.Contains(Array.IndexOf(BidSearch.HousingLotteryDistricts,p.District))&&Wards.Contains(p.Ward)&&Plots.Contains(p.Plot)&&Sizes.Contains(p.Size)&&Phases.Contains(p.PhaseCategory(now))&&(Tenants.Count==2||Tenants.Any(t=>(p.Purchase&t)!=0));
 internal void Clear(){Reset(Districts,0,5);Reset(Wards,1,60);Reset(Plots,1,60);Reset(Sizes,0,3);Reset(Phases,0,5);Tenants.Clear();Tenants.UnionWith([2,4]);}
 private static void Reset(HashSet<int> set,int start,int count){set.Clear();set.UnionWith(Enumerable.Range(start,count));}
}

public sealed record BidFavorite(uint World,string District,int Ward,int Plot)
{
 public string Key=>$"{World}|{District}|{Ward}|{Plot}";
}

internal sealed class BidWorldSession
{
 private ulong character;private uint world;private DateTimeOffset since;private bool ready;
 internal bool Ready=>ready;
 internal void Reset(){character=0;world=0;ready=false;since=default;}
 internal (bool Changed,bool Ready,uint World) Observe(bool loaded,ulong id,uint currentWorld,DateTimeOffset now){
  if(!loaded||id==0||currentWorld==0){var changed=character!=0||world!=0||ready;Reset();return(changed,false,0);}
  if(character!=id||world!=currentWorld){character=id;world=currentWorld;since=now;ready=false;return(true,false,0);}
  if(!ready&&now-since>=TimeSpan.FromSeconds(1)){ready=true;return(true,true,world);}
  return(false,ready,world);
 }
}
