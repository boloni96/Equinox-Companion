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
 private readonly BidFilters bidFilters=new();private int bidSort=-1,bidRegion;private bool bidDescending,bidDirty=true,bidAuto=true;
 private readonly Dictionary<string,string> bidMarkers=new();private readonly Dictionary<string,List<(string Name,SharedHousingBid Bid)>> bidByPlot=new();private SharedRoster? bidLastRoster;private int bidLastLocalCount=-1;private DateTimeOffset bidRound;
 private void InitializeBidSearch(){bidWindow=new(this);windows.AddWindow(bidWindow);bidCommand=Commands.AddHandler("/bid",new CommandInfo((_,_)=>OpenBidSearch()){HelpMessage="Find housing plots with Paissa, paired bids and Lifestream GO SEE. /equinox bid also opens this window."});}
 private void DisposeBidSearch(){CancelBidSearch();bidSearch.Dispose();if(bidCommand)Commands.RemoveHandler("/bid");windows.RemoveWindow(bidWindow);}
 private void CancelBidSearch(){bidCancel?.Cancel();bidCancel?.Dispose();bidCancel=null;if(bidTask is {} pending)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);bidTask=null;}
 private void OpenBidSearch(){bidWindow.IsOpen=true;nextRosterRead=default;EnsureBidWorlds();if(Player.IsLoaded){bidCharacter=Player.ContentId;bidWorld=Player.CurrentWorld.RowId!=0?Player.CurrentWorld.RowId:Player.HomeWorld.RowId;bidRegion=bidWorlds!.FirstOrDefault(w=>w.Id==bidWorld).Region;}if(bidWorld!=0)StartBidSearch();}
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
  if(Player.IsLoaded&&bidCharacter!=Player.ContentId){bidCharacter=Player.ContentId;bidWorld=Player.CurrentWorld.RowId!=0?Player.CurrentWorld.RowId:Player.HomeWorld.RowId;bidRegion=bidWorlds!.FirstOrDefault(w=>w.Id==bidWorld).Region;StartBidSearch();}
  if(bidTask?.IsCompleted==true){if(bidTask.IsCompletedSuccessfully){bidPlots=bidTask.Result;bidFetched=now;bidMessage=$"{bidPlots.Length} reported open plots · Paissa";bidNext=now.AddSeconds(60);bidDirty=true;}else{_=bidTask.Exception;bidMessage="Paissa unavailable. Previous results kept; try Refresh.";bidNext=now.AddMinutes(2);}bidTask=null;}
  if(bidAuto&&bidWorld!=0&&bidTask is null&&bidFetched!=default&&now>=bidNext)RefreshBidSearch();
  if(now>=bidAgeCheck){bidAgeCheck=now.AddSeconds(30);bidDirty=true;}RebuildBidMarkers(now);
  var period=HousingLottery.Period(now);ImGui.TextUnformatted($"{BidWorldName(bidWorld)} · {(period.Entry?"Entry closes":"Results end")} {HousingLotteryNotices.LocalTime(period.End)} (local)");
  ImGui.SameLine();if(ImGui.SmallButton("Worlds…"))ImGui.OpenPopup("BidWorldPicker");
  if(ImGui.BeginPopup("BidWorldPicker")){
   ImGui.SetNextItemWidth(220);ImGui.Combo("Region",ref bidRegion,"All regions\0Japan\0North America\0Europe\0Oceania\0");
   ImGui.BeginChild("WorldChoices",new Vector2(280,300),true);
   foreach(var region in new[]{(Id:2,Name:"North America"),(Id:3,Name:"Europe"),(Id:1,Name:"Japan"),(Id:4,Name:"Oceania")}){
    if(bidRegion!=0&&bidRegion!=region.Id)continue;
    ImGui.TextDisabled(region.Name);ImGui.Separator();
    foreach(var dc in bidWorlds!.Where(w=>w.Region==region.Id).GroupBy(w=>w.Dc).OrderBy(g=>g.Key))if(ImGui.TreeNode(dc.Key)){
     foreach(var w in dc)if(ImGui.Selectable(w.Name,w.Id==bidWorld)){bidWorld=w.Id;StartBidSearch();ImGui.CloseCurrentPopup();}ImGui.TreePop();
    }
    ImGui.Spacing();
   }ImGui.EndChild();ImGui.EndPopup();
  }
  ImGui.SameLine();ImGui.BeginDisabled(bidTask is not null||bidWorld==0||now<bidRetry);if(ImGui.SmallButton("Refresh"))RefreshBidSearch();ImGui.EndDisabled();ImGui.SameLine();ImGui.Checkbox("Auto 60s",ref bidAuto);
  if(ImGui.SmallButton("Clear sort & filters")){bidFilters.Clear();bidSort=-1;bidDescending=false;bidDirty=true;}
  ImGui.SameLine();ImGui.TextDisabled(bidMessage+(bidFetched==default?"":$" · checked {bidFetched.ToLocalTime():HH:mm:ss}"));
  if((config.SharedRoster?.ProtocolVersion??0)<20)ImGui.TextDisabled("Deploy Journal V7.11.166 and open it once to receive paired bids. Local bids are still shown.");
  if(ImGui.CollapsingHeader($"Your paired bids · {bidByPlot.Values.Sum(x=>x.Count)} entries this round")){
   ImGui.BeginChild("MyBids",new Vector2(0,110),true);foreach(var (key,entries) in bidByPlot){var b=entries[0].Bid;ImGui.PushID(key);ImGui.TextUnformatted($"{BidWorldName(b.WorldId)} · {b.District} W{b.Ward} P{b.Plot}");ImGui.SameLine();if(ImGui.SmallButton("GO SEE"))GoSeeBid(b.WorldId,b.District,b.Ward,b.Plot);ImGui.TextWrapped(bidMarkers[key]);ImGui.PopID();}ImGui.EndChild();
  }
  if(bidTravelMessage.Length>0)ImGui.TextWrapped(bidTravelMessage);
  if(bidDirty){bidShown=BidSearch.Sort(bidPlots.Where(p=>bidFilters.Matches(p,now)),bidSort,bidDescending);bidDirty=false;}
  ImGui.TextDisabled($"{bidShown.Length} matching plots · Hover paired bids for entry numbers. Reports can be outdated; confirm at the placard.");
  if(ImGui.BeginTable("BidPlots",12,ImGuiTableFlags.ScrollY|ImGuiTableFlags.ScrollX|ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.Resizable,new Vector2(0,0))){
   string[] headers=["District","Ward","Plot","Size","Price","Entries","Lottery Phase","Allowed Tenants","Last Updated","First Seen","Paired bids","Visit"];
   float[] widths=[155,85,85,120,110,105,210,225,155,140,110,150];
   for(var i=0;i<headers.Length;i++)ImGui.TableSetupColumn(headers[i],ImGuiTableColumnFlags.WidthFixed,widths[i]);ImGui.TableSetupScrollFreeze(0,1);ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
   for(var i=0;i<headers.Length;i++){
    ImGui.TableNextColumn();ImGui.PushID(i);
    if(i is >=3 and <=6 or 8 or 9){if(ImGui.SmallButton(headers[i]+(bidSort==i?(bidDescending?" ▼":" ▲"):" ↕"))){(bidSort,bidDescending)=BidSearch.NextSort(bidSort,bidDescending,i);bidDirty=true;}}
    else ImGui.TextUnformatted(headers[i]);
    switch(i){
     case 0:DrawBidColumnFilter(headers[i],bidFilters.Districts,BidSearch.HousingLotteryDistricts);break;
     case 1:DrawBidColumnFilter(headers[i],bidFilters.Wards,BidNumbers,1);break;
     case 2:DrawBidColumnFilter(headers[i],bidFilters.Plots,BidNumbers,1);break;
     case 3:DrawBidColumnFilter(headers[i],bidFilters.Sizes,BidSizes);break;
     case 6:DrawBidColumnFilter(headers[i],bidFilters.Phases,BidPlot.PhaseNames);break;
     case 7:DrawBidColumnFilter(headers[i],bidFilters.Tenants,BidTenants,2,2);break;
    }
    ImGui.PopID();
   }
   var clip=ImGui.ImGuiListClipper();clip.Begin(bidShown.Length);while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){var p=bidShown[i];ImGui.PushID(p.Key);ImGui.TableNextRow();string[] values=[p.District,p.Ward.ToString(),p.Plot.ToString(),new[]{"Small","Medium","Large"}[p.Size],p.Price.ToString("N0"),p.Entries?.ToString()??"?",p.PhaseLabel(now),p.Tenants,MarketSearch.Age((long)p.Updated,now),MarketSearch.Age((long)p.FirstSeen,now)];foreach(var value in values){ImGui.TableNextColumn();ImGui.TextUnformatted(value);}ImGui.TableNextColumn();if(bidMarkers.TryGetValue(p.Key,out var marker)){ImGui.TextColored(Green,$"Your bids ({bidByPlot[p.Key].Count})");if(ImGui.IsItemHovered())ImGui.SetTooltip(marker);}else ImGui.TextDisabled("—");ImGui.TableNextColumn();ImGui.BeginDisabled(!Player.IsLoaded);if(ImGui.SmallButton("GO SEE"))GoSeeBid(p.World,p.District,p.Ward,p.Plot);ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton("Copy"))ImGui.SetClipboardText($"{BidWorldName(p.World)} · {p.District} · Ward {p.Ward} · Plot {p.Plot}");ImGui.PopID();}clip.End();clip.Destroy();ImGui.EndTable();}
 }
 private static readonly string[] BidNumbers=Enumerable.Range(1,60).Select(n=>n.ToString()).ToArray(),BidSizes=["Small","Medium","Large"],BidTenants=["Free Company","Individual"];
 private void DrawBidColumnFilter(string title,HashSet<int> selected,string[] labels,int start=0,int step=1){
  ImGui.SameLine();
  // Draw a funnel using the current font height, avoiding a separate icon font dependency.
  var height=ImGui.GetFrameHeight();var pos=ImGui.GetCursorScreenPos();var active=selected.Count!=labels.Length;
  if(ImGui.InvisibleButton("Filter",new Vector2(height+(active?ImGui.CalcTextSize(selected.Count.ToString()).X+3:0),height)))ImGui.OpenPopup("Choices");
  var color=ImGui.GetColorU32(active?ImGuiCol.CheckMark:ImGuiCol.Text);var draw=ImGui.GetWindowDrawList();
  draw.AddTriangleFilled(pos+new Vector2(4,5),pos+new Vector2(height-4,5),pos+new Vector2(height/2,height/2+2),color);
  draw.AddRectFilled(pos+new Vector2(height/2-2,height/2),pos+new Vector2(height/2+2,height-4),color);
  if(active)draw.AddText(pos+new Vector2(height,ImGui.GetStyle().FramePadding.Y),color,selected.Count.ToString());
  if(ImGui.IsItemHovered())ImGui.SetTooltip($"Filter {title} · {selected.Count} selected");
  if(ImGui.BeginPopup("Choices")){
   ImGui.TextUnformatted(title);ImGui.Separator();
   if(ImGui.SmallButton("All")){selected.Clear();for(var i=0;i<labels.Length;i++)selected.Add(start+i*step);bidDirty=true;}
   ImGui.SameLine();if(ImGui.SmallButton("None")){selected.Clear();bidDirty=true;}
   if(labels.Length>10)ImGui.BeginChild("Options",new Vector2(160,280),false);
   for(var i=0;i<labels.Length;i++){var value=start+i*step;var check=selected.Contains(value);if(ImGui.Checkbox(labels[i],ref check)){if(check)selected.Add(value);else selected.Remove(value);bidDirty=true;}}
   if(labels.Length>10)ImGui.EndChild();ImGui.EndPopup();
  }
 }
 private void GoSeeBid(uint world,string district,int ward,int plot){
  if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){bidTravelMessage="Wait until you are out of combat and loading, then click GO SEE.";return;}
  try{if(LifestreamBusy()||shoppingTravel is not null){bidTravelMessage="Travel is already in progress.";return;}
   var name=BidWorldName(world);var address=Pi.GetIpcSubscriber<string,string,string,string,bool,bool,LifeAddress>("Lifestream.BuildAddressBookEntry").InvokeFunc(name,district,ward.ToString(),plot.ToString(),false,false);
   Pi.GetIpcSubscriber<LifeAddress,object>("Lifestream.GoToHousingAddress").InvokeAction(address);bidTravelMessage=$"GO SEE requested: {name} · {district} W{ward} P{plot}. Lifestream chooses the nearby shard; further walking follows its address settings.";
  }catch(Exception){bidTravelMessage="Lifestream must be installed and enabled with housing travel support.";}
 }
}
