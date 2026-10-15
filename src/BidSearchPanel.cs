using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Conditions;
using LifeAddress=(string,int,int,int,int,int,int,bool,bool,string);
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private sealed class BidWindow:Window{private readonly Plugin p;public BidWindow(Plugin p):base("Housing · Find a plot###EquinoxBid"){this.p=p;Size=new(1180,570);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(760,380),MaximumSize=new(float.MaxValue)};}public override void Draw()=>p.DrawBidSearch();public override void OnClose()=>p.CancelBidSearch();}
 private BidWindow bidWindow=null!;private readonly BidSearch bidSearch=new();private bool bidCommand;
 private CancellationTokenSource? bidCancel;private Task<BidPlot[]>? bidTask;private uint bidWorld;private ulong bidCharacter;
 private (uint Id,string Name,string Dc,int Region)[]? bidWorlds;
 private BidPlot[] bidPlots=[],bidShown=[];private string bidMessage="",bidTravelMessage="";private DateTimeOffset bidFetched,bidNext,bidRetry,bidAgeCheck;
 private int bidDistrict,bidWard,bidPlot,bidSize,bidTenant,bidPhase=1,bidSort=5,bidRegion;private bool bidDescending,bidDirty=true,bidAuto=true;
 private readonly Dictionary<string,string> bidMarkers=new();private readonly Dictionary<string,List<(string Name,SharedHousingBid Bid)>> bidByPlot=new();private SharedRoster? bidLastRoster;private int bidLastLocalCount=-1;private DateTimeOffset bidRound;
 private void InitializeBidSearch(){bidWindow=new(this);windows.AddWindow(bidWindow);bidCommand=Commands.AddHandler("/bid",new CommandInfo((_,_)=>OpenBidSearch()){HelpMessage="Find housing plots with Paissa, paired bids and Lifestream GO SEE. /equinox bid also opens this window."});}
 private void DisposeBidSearch(){CancelBidSearch();bidSearch.Dispose();if(bidCommand)Commands.RemoveHandler("/bid");windows.RemoveWindow(bidWindow);}
 private void CancelBidSearch(){bidCancel?.Cancel();bidCancel?.Dispose();bidCancel=null;if(bidTask is {} pending)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);bidTask=null;}
 private void OpenBidSearch(){bidWindow.IsOpen=true;nextRosterRead=default;EnsureBidWorlds();if(Player.IsLoaded){bidCharacter=Player.ContentId;bidWorld=Player.HomeWorld.RowId;}if(bidWorld!=0)StartBidSearch();}
 private void EnsureBidWorlds(){bidWorlds??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().Where(w=>w.IsPublic).Select(w=>(Id:w.RowId,Name:w.Name.ToString(),Dc:w.DataCenter.Value.Name.ToString(),Region:(int)w.DataCenter.Value.Region.RowId)).Where(w=>w.Region is >=1 and <=4).OrderBy(w=>w.Name).ToArray();}
 private void StartBidSearch(){CancelBidSearch();bidPlots=[];bidDirty=true;bidFetched=default;if(bidWorld==0)return;bidCancel=new();bidTask=bidSearch.Search(bidWorld,bidCancel.Token);bidMessage="Checking Paissa…";bidRetry=DateTimeOffset.UtcNow.AddSeconds(10);}
 private void RefreshBidSearch(){if(bidTask is not null||DateTimeOffset.UtcNow<bidRetry)return;bidCancel?.Dispose();bidCancel=new();bidTask=bidSearch.Search(bidWorld,bidCancel.Token);bidMessage="Refreshing Paissa…";bidRetry=DateTimeOffset.UtcNow.AddSeconds(10);}
 private string BidWorldName(uint id)=>bidWorlds?.FirstOrDefault(w=>w.Id==id).Name??id.ToString();
 private void RebuildBidMarkers(DateTimeOffset now){
  var round=HousingLottery.Period(now).Start;if(ReferenceEquals(bidLastRoster,config.SharedRoster)&&bidLastLocalCount==config.Discoveries.Count&&bidRound==round)return;
  bidLastRoster=config.SharedRoster;bidLastLocalCount=config.Discoveries.Count;bidRound=round;bidByPlot.Clear();bidMarkers.Clear();
  var merged=new Dictionary<string,(string Name,SharedHousingBid Bid)>();
  void add(string identity,string name,SharedHousingBid b){if(!DateTimeOffset.TryParse(b.CycleStart,out var start)||start!=round)return;var id=$"{identity}|{b.WorldId}|{b.District}|{b.Ward}|{b.Plot}|{b.Entry}";if(!merged.TryGetValue(id,out var old)||b.At>old.Bid.At&&old.Bid.Status=="entered")merged[id]=(name,b);}
  foreach(var c in config.SharedRoster?.People.SelectMany(p=>p.Characters)??[])foreach(var b in c.HousingBids??[])add(c.ContentId.Length>0?c.ContentId:c.Name+"@"+c.World,c.Name,b);
  foreach(var e in config.Discoveries.Where(e=>e.Kind=="housing.lottery"&&e.Lottery is not null).OrderBy(e=>e.At)){var b=e.Lottery!;add(e.Actor.ContentId,e.Actor.Name,new(b.District,b.Ward,b.Plot,b.Entry,b.Buyer,b.FcId,b.Status,b.CycleStart,b.Evidence,e.Actor.HomeWorldId,e.Actor.HomeWorldName??"",e.At));}
  foreach(var v in merged.Values){var b=v.Bid;var key=$"{b.WorldId}|{b.District}|{b.Ward}|{b.Plot}";if(!bidByPlot.TryGetValue(key,out var list))bidByPlot[key]=list=[];list.Add(v);}
  foreach(var (key,list) in bidByPlot)bidMarkers[key]=string.Join("\n",list.Select(x=>$"{x.Name} · {(x.Bid.Buyer=="fc"?"FC":x.Bid.Buyer=="private"?"Private":"Type unknown")} · Entry #{x.Bid.Entry} · {x.Bid.Status}"));
 }
 private void DrawBidSearch(){
  EnsureBidWorlds();var now=DateTimeOffset.UtcNow;
  if(Player.IsLoaded&&bidCharacter!=Player.ContentId){bidCharacter=Player.ContentId;bidWorld=Player.HomeWorld.RowId;StartBidSearch();}
  if(bidTask?.IsCompleted==true){if(bidTask.IsCompletedSuccessfully){bidPlots=bidTask.Result;bidFetched=now;bidMessage=$"{bidPlots.Length} reported open plots · Paissa";bidNext=now.AddSeconds(60);bidDirty=true;}else{_=bidTask.Exception;bidMessage="Paissa unavailable. Previous results kept; try Refresh.";bidNext=now.AddMinutes(2);}bidTask=null;}
  if(bidAuto&&bidWorld!=0&&bidTask is null&&bidFetched!=default&&now>=bidNext)RefreshBidSearch();
  if(now>=bidAgeCheck){bidAgeCheck=now.AddSeconds(30);bidDirty=true;}RebuildBidMarkers(now);
  var period=HousingLottery.Period(now);ImGui.TextUnformatted($"{BidWorldName(bidWorld)} · {(period.Entry?"Entry closes":"Results end")} {HousingLotteryNotices.LocalTime(period.End)} (local)");
  ImGui.SameLine();if(ImGui.SmallButton("Worlds…"))ImGui.OpenPopup("BidWorldPicker");
  if(ImGui.BeginPopup("BidWorldPicker")){
   ImGui.SetNextItemWidth(220);ImGui.Combo("Region",ref bidRegion,"All regions\0Japan\0North America\0Europe\0Oceania\0");
   ImGui.BeginChild("WorldChoices",new Vector2(280,300),true);
   foreach(var dc in bidWorlds!.Where(w=>bidRegion==0||w.Region==bidRegion).GroupBy(w=>w.Dc).OrderBy(g=>g.Key))if(ImGui.TreeNode(dc.Key)){foreach(var w in dc)if(ImGui.Selectable(w.Name,w.Id==bidWorld)){bidWorld=w.Id;StartBidSearch();ImGui.CloseCurrentPopup();}ImGui.TreePop();}ImGui.EndChild();ImGui.EndPopup();
  }
  ImGui.SameLine();ImGui.BeginDisabled(bidTask is not null||bidWorld==0||now<bidRetry);if(ImGui.SmallButton("Refresh"))RefreshBidSearch();ImGui.EndDisabled();ImGui.SameLine();ImGui.Checkbox("Auto 60s",ref bidAuto);
  if(ImGui.BeginTable("BidFilters",6,ImGuiTableFlags.SizingStretchSame)){
   ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);bidDirty|=ImGui.Combo("##District",ref bidDistrict,"All districts\0Mist\0Lavender Beds\0Goblet\0Shirogane\0Empyreum\0");
   ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);bidDirty|=ImGui.Combo("##Size",ref bidSize,"All sizes\0Small\0Medium\0Large\0");
   ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);bidDirty|=ImGui.Combo("##Tenants",ref bidTenant,"Private + FC\0Private\0Free Company\0");
   ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);bidDirty|=ImGui.Combo("##Phase",ref bidPhase,"All phases\0Accepting entries\0Results\0Next round\0Unknown / outdated\0");
   ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputInt("Ward (0=all)",ref bidWard)){bidWard=Math.Clamp(bidWard,0,60);bidDirty=true;}
   ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);if(ImGui.InputInt("Plot (0=all)",ref bidPlot)){bidPlot=Math.Clamp(bidPlot,0,60);bidDirty=true;}ImGui.EndTable();
  }
  if(ImGui.SmallButton("Clear sort & filters")){bidDistrict=bidWard=bidPlot=bidSize=bidTenant=bidPhase=0;bidSort=5;bidDescending=false;bidDirty=true;}
  ImGui.SameLine();ImGui.TextDisabled(bidMessage+(bidFetched==default?"":$" · checked {bidFetched.ToLocalTime():HH:mm:ss}"));
  if((config.SharedRoster?.ProtocolVersion??0)<20)ImGui.TextDisabled("Deploy Journal V7.11.166 and open it once to receive paired bids. Local bids are still shown.");
  if(ImGui.CollapsingHeader($"Your paired bids · {bidByPlot.Values.Sum(x=>x.Count)} entries this round")){
   ImGui.BeginChild("MyBids",new Vector2(0,110),true);foreach(var (key,entries) in bidByPlot){var b=entries[0].Bid;ImGui.PushID(key);ImGui.TextUnformatted($"{BidWorldName(b.WorldId)} · {b.District} W{b.Ward} P{b.Plot}");ImGui.SameLine();if(ImGui.SmallButton("GO SEE"))GoSeeBid(b.WorldId,b.District,b.Ward,b.Plot);ImGui.TextWrapped(bidMarkers[key]);ImGui.PopID();}ImGui.EndChild();
  }
  if(bidTravelMessage.Length>0)ImGui.TextWrapped(bidTravelMessage);
  if(bidDirty){bidShown=BidSearch.Sort(bidPlots.Where(p=>(bidDistrict==0||p.District==BidSearch.HousingLotteryDistricts[bidDistrict-1])&&(bidWard==0||p.Ward==bidWard)&&(bidPlot==0||p.Plot==bidPlot)&&(bidSize==0||p.Size==bidSize-1)&&(bidTenant==0||(p.Purchase&(bidTenant==1?4:2))!=0)&&(bidPhase==0||bidPhase==4?bidPhase==0||!p.Current(now):p.Current(now)&&p.Phase==bidPhase)),bidSort,bidDescending);bidDirty=false;}
  ImGui.TextDisabled($"{bidShown.Length} matching plots · Hover paired bids for entry numbers. Reports can be outdated; confirm at the placard.");
  if(ImGui.BeginTable("BidPlots",12,ImGuiTableFlags.ScrollY|ImGuiTableFlags.ScrollX|ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.Resizable,new Vector2(0,0))){
   string[] headers=["District","Ward","Plot","Size","Price","Entries","Lottery phase","Tenants","Updated","First seen","Paired bids","Visit"];
   for(var i=0;i<headers.Length;i++)ImGui.TableSetupColumn(headers[i],ImGuiTableColumnFlags.WidthFixed,i is 1 or 2?45:i==0?115:i==6?135:90);ImGui.TableSetupScrollFreeze(0,1);ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
   for(var i=0;i<headers.Length;i++){ImGui.TableNextColumn();if(i<10){if(ImGui.SmallButton(headers[i]+(bidSort==i?(bidDescending?" ▼":" ▲"):" ↕"))){if(bidSort==i)bidDescending=!bidDescending;else{bidSort=i;bidDescending=false;}bidDirty=true;}}else ImGui.TextUnformatted(headers[i]);}
   var clip=ImGui.ImGuiListClipper();clip.Begin(bidShown.Length);while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){var p=bidShown[i];ImGui.PushID(p.Key);ImGui.TableNextRow();string[] values=[p.District,p.Ward.ToString(),p.Plot.ToString(),new[]{"Small","Medium","Large"}[p.Size],p.Price.ToString("N0"),p.Entries?.ToString()??"?",p.PhaseLabel(now),p.Tenants,MarketSearch.Age((long)p.Updated,now),MarketSearch.Age((long)p.FirstSeen,now)];foreach(var value in values){ImGui.TableNextColumn();ImGui.TextUnformatted(value);}ImGui.TableNextColumn();if(bidMarkers.TryGetValue(p.Key,out var marker)){ImGui.TextColored(Green,$"Your bids ({bidByPlot[p.Key].Count})");if(ImGui.IsItemHovered())ImGui.SetTooltip(marker);}else ImGui.TextDisabled("—");ImGui.TableNextColumn();ImGui.BeginDisabled(!Player.IsLoaded);if(ImGui.SmallButton("GO SEE"))GoSeeBid(p.World,p.District,p.Ward,p.Plot);ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton("Copy"))ImGui.SetClipboardText($"{BidWorldName(p.World)} · {p.District} · Ward {p.Ward} · Plot {p.Plot}");ImGui.PopID();}clip.End();clip.Destroy();ImGui.EndTable();}
 }
 private void GoSeeBid(uint world,string district,int ward,int plot){
  if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){bidTravelMessage="Wait until you are out of combat and loading, then click GO SEE.";return;}
  try{if(LifestreamBusy()||shoppingTravel is not null){bidTravelMessage="Travel is already in progress.";return;}
   var name=BidWorldName(world);var address=Pi.GetIpcSubscriber<string,string,string,string,bool,bool,LifeAddress>("Lifestream.BuildAddressBookEntry").InvokeFunc(name,district,ward.ToString(),plot.ToString(),false,false);
   Pi.GetIpcSubscriber<LifeAddress,object>("Lifestream.GoToHousingAddress").InvokeAction(address);bidTravelMessage=$"GO SEE requested: {name} · {district} W{ward} P{plot}. Lifestream chooses the nearby shard; further walking follows its address settings.";
  }catch(Exception){bidTravelMessage="Lifestream must be installed and enabled with housing travel support.";}
 }
}
