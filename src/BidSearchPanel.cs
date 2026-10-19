using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Conditions;
using LifeAddress=(string,int,int,int,int,int,int,bool,bool,string);
namespace EquinoxCompanion;
public sealed partial class Plugin
{
 private sealed class BidWindow:Window{private readonly Plugin p;public BidWindow(Plugin p):base("Housing · Find a plot###EquinoxBid"){this.p=p;RespectCloseHotkey=false;Size=new(1180,570);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(760,380),MaximumSize=new(float.MaxValue)};}public override void Draw()=>p.DrawWindowWithLauncher(p.DrawBidSearch);public override void PostDraw()=>HandleNativeCollapse(this,()=>p.MinimizeLauncher("Housing bids"));public override void OnClose()=>p.CancelBidSearch();}
 private BidWindow bidWindow=null!;private readonly BidSearch bidSearch=new();private bool bidCommand;private readonly HashSet<string> bidShortcutCommands=new();
 private CancellationTokenSource? bidCancel;private Task<BidPlot[]>? bidTask;private uint bidWorld;private readonly BidWorldSession bidSession=new();
 private (uint Id,string Name,string Dc,int Region)[]? bidWorlds;
 private BidPlot[] bidPlots=[],bidShown=[];private string bidMessage="",bidTravelMessage="";private DateTimeOffset bidFetched,bidNext,bidRetry,bidAgeCheck;
 private readonly BidFilters bidFilters=new();private int bidSort=-1,bidRegion;private bool bidDescending,bidDirty=true,bidAuto=true,bidFavoritesOnly;
 private readonly HashSet<string> bidFavoriteKeys=new();
 private readonly Dictionary<string,string> bidMarkers=new();private readonly Dictionary<string,List<(string Name,string Owner,SharedHousingBid Bid)>> bidByPlot=new();private (string Name,string Owner,SharedHousingBid Bid)[] bidRows=[];private SharedRoster? bidLastRoster;private int bidLastLocalCount=-1;private DateTimeOffset bidRound;
 private void InitializeBidSearch(){config.BidFavorites=SavedPlotRecovery.Clean(config.BidFavorites).ToList();KeepSavedPlotRecovery();bidFavoriteKeys.UnionWith(config.BidFavorites.Select(f=>f.Key));bidWindow=new(this);windows.AddWindow(bidWindow);bidCommand=Commands.AddHandler("/bid",new CommandInfo((_,_)=>OpenBidSearch()){HelpMessage="Find housing plots with Paissa, paired bids and Lifestream GO SEE. /equinox bid also opens this window."});foreach(var (command,section) in new[]{("/saved",1),("/favorite",2),("/favourite",2)}){if(Commands.AddHandler(command,new CommandInfo((_,_)=>OpenBidSection(section)){HelpMessage=section==1?"Open paired saved plots.":"Open paired favourite plots."}))bidShortcutCommands.Add(command);}}
 private void OpenBidSection(int section){if(!bidWindow.IsOpen)OpenBidSearch();bidSection=section;favouriteNext=default;savedNext=default;}
 private void DisposeBidSearch(){foreach(var command in bidShortcutCommands)Commands.RemoveHandler(command);bidShortcutCommands.Clear();CancelBidSearch();bidSearch.Dispose();if(bidCommand)Commands.RemoveHandler("/bid");windows.RemoveWindow(bidWindow);}
 private void CancelBidSearch(){bidCancel?.Cancel();bidCancel?.Dispose();bidCancel=null;if(bidTask is {} pending)_=pending.ContinueWith(t=>{_=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);bidTask=null;}
 private void OpenBidSearch(){bidWindow.IsOpen=true;favouriteNext=default;nextRosterRead=default;bidSession.Reset();ClearBidWorld();UpdateBidWorld(DateTimeOffset.UtcNow);}
 private void ClearBidWorld(){CancelBidSearch();bidWorld=0;bidPlots=[];bidShown=[];bidFetched=default;bidNext=default;bidTravelMessage="";bidDirty=true;bidMessage="Waiting for character and current world…";}
 private void UpdateBidWorld(DateTimeOffset now){
  var ready=Player.IsLoaded&&!Conditions[ConditionFlag.BetweenAreas]&&!Conditions[ConditionFlag.BetweenAreas51];
  var change=bidSession.Observe(ready,Player.ContentId,Player.CurrentWorld.RowId,now);
  if(!change.Changed)return;
  if(!change.Ready){ClearBidWorld();return;}
  EnsureBidWorlds();bidWorld=change.World;bidRegion=bidWorlds!.FirstOrDefault(w=>w.Id==bidWorld).Region;
  bidLastRoster=null;bidLastLocalCount=-1;nextRosterRead=default;
  if(bidWindow.IsOpen)StartBidSearch();
 }

 private void EnsureBidWorlds(){bidWorlds??=DataManager.GetExcelSheet<Lumina.Excel.Sheets.World>().Where(w=>w.IsPublic).Select(w=>(Id:w.RowId,Name:w.Name.ToString(),Dc:w.DataCenter.Value.Name.ToString(),Region:(int)w.DataCenter.Value.Region.RowId)).Where(w=>w.Region is >=1 and <=4).OrderBy(w=>w.Name).ToArray();}
 private void StartBidSearch(){CancelBidSearch();bidPlots=[];bidDirty=true;bidFetched=default;if(bidWorld==0)return;bidCancel=new();bidTask=bidSearch.Search(bidWorld,bidCancel.Token);bidMessage="Checking Paissa…";bidRetry=DateTimeOffset.UtcNow.AddSeconds(10);}
 private void RefreshBidSearch(){if(bidTask is not null||DateTimeOffset.UtcNow<bidRetry)return;bidCancel?.Dispose();bidCancel=new();bidTask=bidSearch.Search(bidWorld,bidCancel.Token);bidMessage="Refreshing Paissa…";bidRetry=DateTimeOffset.UtcNow.AddSeconds(10);}
 private string BidWorldName(uint id)=>bidWorlds?.FirstOrDefault(w=>w.Id==id).Name??id.ToString();
 private bool bidHistory;
 private void RebuildBidMarkers(DateTimeOffset now){
  var round=HousingLottery.Period(now).Start;if(ReferenceEquals(bidLastRoster,config.SharedRoster)&&bidLastLocalCount==config.Discoveries.Count&&bidRound==round)return;
  bidLastRoster=config.SharedRoster;bidLastLocalCount=config.Discoveries.Count;bidRound=round;bidByPlot.Clear();bidMarkers.Clear();
  var merged=new Dictionary<string,(string Name,string Owner,SharedHousingBid Bid)>();
  void add(string identity,string name,string owner,SharedHousingBid b){if(!DateTimeOffset.TryParse(b.CycleStart,out var start))return;var id=$"{identity}|{b.WorldId}|{b.District}|{b.Ward}|{b.Plot}|{b.Entry}|{b.CycleStart}";if(!merged.TryGetValue(id,out var old)||b.At>old.Bid.At&&old.Bid.Status=="entered")merged[id]=(name,owner,b);}
  var characters=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters.Select(c=>(Person:p,Character:c))).ToArray();
  string owner(SharedPerson p,SharedCharacter c)=>$"{p.Name} · {(string.IsNullOrWhiteSpace(c.Account)?p.Accounts?.FirstOrDefault(a=>a.Id==c.AccountId)?.Name??"Account unknown":c.Account)}";
  foreach(var (p,c) in characters)foreach(var b in c.HousingBids??[])add(c.ContentId.Length>0?c.ContentId:c.Name+"@"+c.World,c.Name,owner(p,c),b);
  foreach(var e in config.Discoveries.Where(e=>e.Kind=="housing.lottery"&&e.Lottery is not null).OrderBy(e=>e.At)){var b=e.Lottery!;
   var matches=characters.Where(x=>!string.IsNullOrEmpty(x.Character.ContentId)?x.Character.ContentId==e.Actor.ContentId:x.Character.Name==e.Actor.Name&&x.Character.World==e.Actor.HomeWorldName).Take(2).ToArray();
   var match=matches.Length==1?matches[0]:default;
   var identity=match.Character is not null?(match.Character.ContentId.Length>0?match.Character.ContentId:match.Character.Name+"@"+match.Character.World):e.Actor.ContentId;
   add(identity,e.Actor.Name,match.Character is not null?owner(match.Person,match.Character):"Person / account pending sync",new(b.District,b.Ward,b.Plot,b.Entry,b.Buyer,b.FcId,b.Status,b.CycleStart,b.Evidence,e.Actor.HomeWorldId,e.Actor.HomeWorldName??"",e.At));}
  var observedResults=config.Discoveries.Where(e=>e.Kind=="housing.lottery-result"&&e.LotteryResult is not null).Select(e=>e.LotteryResult!).ToArray();
  foreach(var original in merged.Values){var v=(Name:original.Name,Owner:original.Owner,Bid:original.Bid with {Status=HousingLottery.ObservedStatus(original.Bid,observedResults)});var b=v.Bid;var key=$"{b.WorldId}|{b.District}|{b.Ward}|{b.Plot}";if(!bidByPlot.TryGetValue(key,out var list))bidByPlot[key]=list=[];list.Add(v);}
  bidRows=bidByPlot.Values.SelectMany(x=>x).OrderBy(x=>x.Owner).ThenBy(x=>x.Name).ThenBy(x=>x.Bid.WorldId).ThenBy(x=>x.Bid.District).ThenBy(x=>x.Bid.Ward).ThenBy(x=>x.Bid.Plot).ThenBy(x=>x.Bid.Entry).ToArray();
  foreach(var (key,list) in bidByPlot.Where(x=>x.Value.Any(v=>DateTimeOffset.Parse(v.Bid.CycleStart)==round)))bidMarkers[key]=string.Join("\n",list.Select(x=>$"{x.Owner} · {x.Name} · {HousingLottery.BuyerLabel(x.Bid.Buyer)} · Entry #{x.Bid.Entry} · {HousingLottery.StatusLabel(x.Bid.Status)}"));
 }
 private void DrawBidSearch(){
  EnsureBidWorlds();var now=DateTimeOffset.UtcNow;
  if(DrawHousingUpgrades())return;
  if(!bidSession.Ready){ImGui.TextDisabled("Waiting for character and current world…");return;}
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
  ImGui.SameLine();if(ImGui.Checkbox("Saved plots only",ref bidFavoritesOnly))bidDirty=true;
  if(ImGui.Checkbox("★ Favourite locations only",ref bidLocationFavoritesOnly))bidDirty=true;
  if(savedStatus.Length>0)ImGui.TextDisabled(savedStatus);
  if(ImGui.SmallButton("Clear sort & filters")){bidFilters.Clear();bidFavoritesOnly=false;bidLocationFavoritesOnly=false;bidSort=-1;bidDescending=false;bidDirty=true;}
  ImGui.SameLine();ImGui.TextDisabled(bidMessage+(bidFetched==default?"":$" · checked {bidFetched.ToLocalTime():HH:mm:ss}"));
  if((config.SharedRoster?.ProtocolVersion??0)<20)ImGui.TextDisabled("Deploy Journal V7.11.166 and open it once to receive paired bids. Local bids are still shown.");
  DrawPairedBidRows();

  windowStatus=bidTravelMessage.Length>0?bidTravelMessage:bidMessage;
  DrawGoSeeHomeStop();
  if(bidDirty){bidShown=BidSearch.Sort(bidPlots.Where(p=>bidFilters.Matches(p,now)&&(!bidLocationFavoritesOnly||IsFavouriteLocation(p.District,p.Ward,p.Plot))&&(!bidFavoritesOnly||bidFavoriteKeys.Contains(p.Key))),bidSort,bidDescending);bidDirty=false;}
  ImGui.TextDisabled($"{bidShown.Length} matching plots · Hover paired bids for entry numbers. Reports can be outdated; confirm at the placard.");
  if(ImGui.BeginTable("BidPlots",12,ImGuiTableFlags.ScrollY|ImGuiTableFlags.ScrollX|ImGuiTableFlags.RowBg|ImGuiTableFlags.BordersInnerH|ImGuiTableFlags.Resizable,new Vector2(0,0))){
   string[] headers=["District","Ward","Plot","Size","Price","Entries","Lottery Phase","Allowed Tenants","Last Updated","First Seen","Paired bids","Visit"];
   float[] widths=[190,85,85,120,110,105,210,225,155,140,110,150];
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
   var clip=ImGui.ImGuiListClipper();clip.Begin(bidShown.Length);while(clip.Step())for(var i=clip.DisplayStart;i<clip.DisplayEnd;i++){var p=bidShown[i];ImGui.PushID(p.Key);ImGui.TableNextRow();string[] values=[p.District,p.Ward.ToString(),p.Plot.ToString(),new[]{"Small","Medium","Large"}[p.Size],p.Price.ToString("N0"),p.Entries?.ToString()??"?",p.PhaseLabel(now),p.Tenants,MarketSearch.Age((long)p.Updated,now),MarketSearch.Age((long)p.FirstSeen,now)];for(var v=0;v<values.Length;v++){ImGui.TableNextColumn();if(v==0){var saved=bidFavoriteKeys.Contains(p.Key);if(DrawSavedPlotButton("SavePlot",saved))ToggleBidFavorite(new(p.World,p.District,p.Ward,p.Plot));if(ImGui.IsItemHovered())ImGui.SetTooltip(saved?"Remove from saved plots":"Save this plot for later");ImGui.SameLine();if(ImGui.SmallButton(IsFavouriteLocation(p.District,p.Ward,p.Plot)?"★##Location":"☆##Location"))ChooseFavouriteLocation(p.District,p.Ward,p.Plot);if(ImGui.IsItemHovered())ImGui.SetTooltip("Save this favourite plot here; choose any ward or this ward only.");ImGui.SameLine();}ImGui.TextUnformatted(values[v]);}ImGui.TableNextColumn();if(bidMarkers.TryGetValue(p.Key,out var marker)){ImGui.TextColored(Green,$"Your bids ({bidByPlot[p.Key].Count})");if(ImGui.IsItemHovered())ImGui.SetTooltip(marker);}else ImGui.TextDisabled("—");ImGui.TableNextColumn();ImGui.BeginDisabled(!Player.IsLoaded);if(ImGui.SmallButton("GO SEE"))GoSeeBid(p.World,p.District,p.Ward,p.Plot);ImGui.EndDisabled();ImGui.SameLine();if(ImGui.SmallButton("Copy"))ImGui.SetClipboardText($"{BidWorldName(p.World)} · {p.District} · Ward {p.Ward} · Plot {p.Plot}");ImGui.PopID();}clip.End();clip.Destroy();ImGui.EndTable();}
 }
 private void DrawPairedBidRows(){
  var count=bidByPlot.Values.Sum(x=>x.Count);
  if(!ImGui.CollapsingHeader("Your paired bids###PairedBids"))return;
  ImGui.Checkbox("Show finished history",ref bidHistory);
  if(count==0){ImGui.TextDisabled("No recorded bids.");return;}
  var height=Math.Min(6,count)*ImGui.GetFrameHeightWithSpacing()+ImGui.GetStyle().ScrollbarSize+8;
  if(!ImGui.BeginTable("MyBids",4,ImGuiTableFlags.ScrollY|ImGuiTableFlags.ScrollX|ImGuiTableFlags.RowBg|ImGuiTableFlags.SizingFixedFit,new Vector2(0,height)))return;
  ImGui.TableSetupColumn("Person / account");ImGui.TableSetupColumn("Plot");ImGui.TableSetupColumn("Entry");ImGui.TableSetupColumn("Actions");
  var row=0;
  foreach(var entry in bidRows.Where(x=>bidHistory?!HousingBidActive(x.Bid):HousingBidActive(x.Bid))){
   var b=entry.Bid;var address=$"{BidWorldName(b.WorldId)} · {b.District} W{b.Ward} P{b.Plot}";
   var details=$"{entry.Name} · {HousingLottery.BuyerLabel(b.Buyer)} · Entry #{b.Entry} · {HousingLottery.BidStatusLabel(b)}";
   if(b.RefundedAt is not null)details+=" · refund collected";else if(b.Status=="lost")details+=" · refund not confirmed";
   if(b.FcClaimedAt is not null&&b.Status is not ("claimed" or "entered"))details+=" · FC estate claimed";
   ImGui.PushID(row++);ImGui.TableNextRow();ImGui.TableNextColumn();ImGui.TextUnformatted(entry.Owner);CopyTextOnClick(entry.Owner);
   ImGui.TableNextColumn();ImGui.TextUnformatted(address);CopyTextOnClick(address);ImGui.TableNextColumn();ImGui.TextUnformatted(details);if(ImGui.IsItemHovered())ImGui.SetTooltip(HousingLottery.BidEvidence(b)+"\nClick to copy character name.");if(ImGui.IsItemClicked())ImGui.SetClipboardText(entry.Name);
   ImGui.TableNextColumn();if(ImGui.SmallButton("GO SEE"))GoSeeBid(b.WorldId,b.District,b.Ward,b.Plot);
   if(b.Status=="lost"&&b.RefundedAt is null){ImGui.SameLine();ImGui.BeginDisabled((config.SharedRoster?.ProtocolVersion??0)<22);if(ImGui.SmallButton("I collected the refund"))RecordManualRefund(entry.Name,b);ImGui.EndDisabled();}
   ImGui.PopID();
  }
  ImGui.EndTable();
 }
 private static bool HousingBidActive(SharedHousingBid b)=>HousingLottery.BidActive(b);
 private void RecordManualRefund(string name,SharedHousingBid b){
  var matches=(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters).Where(c=>c.Name==name&&c.HousingBids?.Any(x=>x.WorldId==b.WorldId&&x.District==b.District&&x.Ward==b.Ward&&x.Plot==b.Plot&&x.Entry==b.Entry&&x.CycleStart==b.CycleStart)==true).Take(2).ToArray();
  if(matches.Length!=1||matches[0].ContentId.Length==0)return;var c=matches[0];var now=DateTimeOffset.UtcNow;
  KeepDiscovery(new(Guid.NewGuid().ToString("N"),"housing.lottery",now,new(c.ContentId,c.Name,b.WorldId,b.WorldId,c.World,c.World),null,Lottery:new(b.District,b.Ward,b.Plot,b.Entry,b.Buyer,b.FcId,"lost",b.CycleStart,"manual-refund",b.FcName)));
 }
 private static bool DrawSavedPlotButton(string id,bool saved){
  var size=ImGui.GetTextLineHeight();var at=ImGui.GetCursorScreenPos();
  var clicked=ImGui.Button("##"+id,new Vector2(size+8,size+4));
  var draw=ImGui.GetWindowDrawList();var ink=ImGui.GetColorU32(saved?ImGuiCol.CheckMark:ImGuiCol.Text);var a=at+new Vector2(4,2);var b=a+new Vector2(size);
  draw.AddRect(a,b,ink,1,ImDrawFlags.None,1.5f);
  draw.AddRectFilled(a+new Vector2(size*.23f,0),a+new Vector2(size*.72f,size*.34f),ink);
  draw.AddRect(a+new Vector2(size*.2f,size*.55f),b-new Vector2(size*.2f,1),ink);
  return clicked;
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
  if(goSeeHomeTrip is not null){bidTravelMessage="Already returning home for estate travel. Use Stop GO SEE to cancel.";return;}
  if(DateTimeOffset.UtcNow<goSeeNativeUntil){bidTravelMessage="An estate teleport was just requested. Wait for it to finish.";return;}
  if(goSeeFriendTrip is not null){bidTravelMessage="Already checking a friend’s estate teleport.";return;}
  if(DateTimeOffset.UtcNow<characterSwitchUntil){bidTravelMessage="Wait for the character switch or stop it first.";return;}
  if(!Player.IsLoaded||Conditions[ConditionFlag.InCombat]||Conditions[ConditionFlag.BetweenAreas]||Conditions[ConditionFlag.BetweenAreas51]){bidTravelMessage="Wait until you are out of combat and loading, then click GO SEE.";return;}
  try{
   var lifestreamBusy=false;try{lifestreamBusy=LifestreamBusy();}catch{/* Native estate teleports do not require Lifestream. */}
   if(lifestreamBusy||shoppingTravel is not null){bidTravelMessage="Travel is already in progress.";return;}
   if(StartGoSeeHome(world,district,ward,plot)||TryGoSeeEstateTeleport(world,district,ward,plot)||StartGoSeeFriend(world,district,ward,plot))return;
   GoSeeAddress(world,district,ward,plot);
  }catch(Exception){bidTravelMessage="Lifestream must be installed and enabled with housing travel support.";}
 }
 private void GoSeeAddress(uint world,string district,int ward,int plot){
  try{
   var name=BidWorldName(world);var address=Pi.GetIpcSubscriber<string,string,string,string,bool,bool,LifeAddress>("Lifestream.BuildAddressBookEntry").InvokeFunc(name,district,ward.ToString(),plot.ToString(),false,false);
   Pi.GetIpcSubscriber<LifeAddress,object>("Lifestream.GoToHousingAddress").InvokeAction(address);bidTravelMessage=$"GO SEE requested: {name} · {district} W{ward} P{plot}. Lifestream chooses the nearby shard; further walking follows its address settings.";
  }catch(Exception){bidTravelMessage="Lifestream must be installed and enabled with housing travel support.";}
 }
}
