using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
namespace EquinoxCompanion;
public sealed partial class Plugin
{
    private sealed record DeliveryItem(uint ItemId,bool Hq,int Quantity);
    private sealed record DeliveryMeeting(string Id,string Name,string World,string District,int Ward,int Plot);
    private sealed record DeliveryLeg(string Id,string From,string To,string FromAccount,string ToAccount,DeliveryMeeting Meeting,DeliveryItem[] Items,Dictionary<string,JsonElement> Receipts,Dictionary<string,Dictionary<string,int>> Baseline,Dictionary<string,bool> Armed,bool Started=false,int Attempt=0);
    private sealed record DeliveryPlan(string Source,string Recipient,string Intermediary,string[] AccountKeys,DeliveryLeg[] Legs);
    private sealed record DeliveryMember(string AccountKey,string CharacterId,string ContentId,DateTimeOffset Seen);
    private sealed record DeliverySession(string Id,DeliveryPlan Plan,int Index,Dictionary<string,DeliveryMember> Members,string Status,long Revision,bool Stopped=false,bool Paused=false,string Reason="");
    private sealed record DeliveryList(DeliverySession[] Sessions);
    private sealed record DeliveryReply(DeliverySession[]? Sessions,DeliverySession? Session,string Error);
    private sealed class DeliveryWindow(Plugin p):Window("Item delivery · Equinox###EquinoxDelivery")
    {
        public override void OnOpen(){Size??=new(580,520);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new(){MinimumSize=new(420,330),MaximumSize=new(float.MaxValue)};}
        public override void Draw()=>p.DrawItemDelivery();
    }
    private DeliveryWindow? deliveryWindow;
    private readonly HttpClient itemDeliveryClient=new(){Timeout=TimeSpan.FromSeconds(15)};
    private static readonly JsonSerializerOptions DeliveryJson=new(JsonSerializerDefaults.Web);
    private readonly string deliveryClientId=Guid.NewGuid().ToString("N");
    private Task<DeliveryReply>? deliveryRequest;
    private DeliverySession[] deliverySessions=[];
    private string deliverySelected="",deliverySource="",deliveryRecipient="",deliveryIntermediary="",deliveryPreferred="",deliveryMateria="",deliveryItemsText="",deliveryNotice="",deliveryAccount="",deliveryRun="",deliveryPairing="",deliveryTrip="",deliveryTrade="",deliveryResumeGarden="";
    private DateTimeOffset deliveryPoll,deliveryHeartbeat,deliveryActionDeadline,deliveryLastContact,deliveryTick;
    private bool deliverySwitchRequested,deliveryMustReconcile,deliveryOpenCreate,deliveryReturning;
    private void OpenItemDelivery(){if(deliveryWindow==null){deliveryWindow=new(this);windows.AddWindow(deliveryWindow);}deliveryWindow.IsOpen=true;deliveryPoll=default;}
    private (SharedPerson Person,SharedCharacter Character)[] DeliveryCharacters()=>(config.SharedRoster?.People??[]).SelectMany(p=>p.Characters.Select(c=>(p,c))).ToArray();
    private SharedCharacter? DeliveryCharacter(string id)=>DeliveryCharacters().FirstOrDefault(x=>x.Character.Id==id).Character;
    private string CurrentDeliveryAccount(){var c=CurrentSharedCharacter();return c==null?"":DeliveryCharacters().Where(x=>x.Character.Id==c.Id).Select(x=>x.Person.Id+"/"+c.AccountId).FirstOrDefault()??"";}
    private void DeliveryCharacterPicker(string label,ref string id)
    {
        ImGui.SetNextItemWidth(-1);if(ImGui.BeginCombo(label,DeliveryCharacter(id) is {} c?c.Name+" @ "+c.World:"Choose character")){
            foreach(var x in DeliveryCharacters()){if(ImGui.Selectable(x.Character.Name+" @ "+x.Character.World+" · "+x.Person.Name+" / "+x.Character.Account+"##"+x.Character.Id,x.Character.Id==id))id=x.Character.Id;}ImGui.EndCombo();}
    }
    private void DeliveryHousePicker(string label,ref string id,bool materia)
    {
        var houses=DeliveryCharacters().SelectMany(x=>x.Character.Houses).DistinctBy(h=>h.Id).Where(h=>!materia||new[]{"Bismarck","Ravana","Sephirot","Sophia","Zurvan"}.Contains(h.World)).ToArray();
        var selectedId=id;ImGui.SetNextItemWidth(-1);if(ImGui.BeginCombo(label,houses.FirstOrDefault(h=>h.Id==selectedId)?.Name??"Choose fallback house")){
            if(ImGui.Selectable("None",id.Length==0))id="";
            foreach(var h in houses)if(ImGui.Selectable($"{h.Name} · {h.World} · {h.District} W{h.Ward} P{h.Plot}##{h.Id}",id==h.Id))id=h.Id;ImGui.EndCombo();}
    }
    private void DrawItemDelivery()
    {
        if(!config.SyncEnabled||config.PairingKey.Length!=64){ImGui.TextWrapped("Pair both clients with the same Journal first.");return;}
        if(deliveryOpenCreate){ImGui.SetNextItemOpen(true);deliveryOpenCreate=false;}
        if(ImGui.CollapsingHeader("Create delivery")){
            ImGui.BeginDisabled(deliveryRequest!=null||deliveryRun.Length>0);
            DeliveryCharacterPicker("Supplier",ref deliverySource);DeliveryCharacterPicker("Recipient",ref deliveryRecipient);DeliveryCharacterPicker("Intermediary (same-account deliveries)",ref deliveryIntermediary);
            DeliveryHousePicker("Fallback meeting house",ref deliveryPreferred,false);DeliveryHousePicker("Materia fallback house",ref deliveryMateria,true);
            ImGui.TextWrapped("House priority: supplier, intermediary, recipient, then fallback. Cross-region handoffs use Materia houses. The chosen address is shared by both clients.");
            ImGui.InputTextMultiline("Items · e.g. 8x Potting Soil",ref deliveryItemsText,8000,new Vector2(-1,85));
            ImGui.TextDisabled("Exact NQ quantities · tradable items only · put supplies in bags before departure.");
            if(ImGui.Button("Create shared delivery")){
                try{var parsed=MarketShopping.Parse(deliveryItemsText);if(parsed.Errors.Length>0)throw new InvalidOperationException(string.Join("; ",parsed.Errors));
                    var sheet=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English);
                    var items=parsed.Items.Select(x=>{var row=sheet.FirstOrDefault(i=>i.Name.ToString().Equals(x.Name,StringComparison.OrdinalIgnoreCase));if(row.RowId<=1||row.IsUntradable)throw new InvalidOperationException("Unknown or untradable item: "+x.Name);return new DeliveryItem(row.RowId,false,x.Quantity);}).ToArray();
                    DeliveryPost(new{type="create",plan=new{source=deliverySource,recipient=deliveryRecipient,intermediary=deliveryIntermediary,preferredHouse=deliveryPreferred,materiaHouse=deliveryMateria,items}});
                }catch(Exception e){deliveryNotice=e.Message;}
            }ImGui.EndDisabled();
        }
        ImGui.SetNextItemWidth(-1);
        if(ImGui.BeginCombo("Saved delivery",deliverySessions.FirstOrDefault(s=>s.Id==deliverySelected) is {} selected?$"{DeliveryCharacter(selected.Plan.Source)?.Name} → {DeliveryCharacter(selected.Plan.Recipient)?.Name} · {selected.Status}":"Choose session")){
            foreach(var s in deliverySessions)if(ImGui.Selectable($"{DeliveryCharacter(s.Plan.Source)?.Name} → {DeliveryCharacter(s.Plan.Recipient)?.Name} · {s.Status}##{s.Id}",s.Id==deliverySelected))deliverySelected=s.Id;ImGui.EndCombo();}
        var session=deliverySessions.FirstOrDefault(s=>s.Id==deliverySelected);
        if(session!=null){
            ImGui.TextWrapped("Status: "+session.Status+(session.Reason.Length>0?" · "+session.Reason:""));
            foreach(var m in session.Members.Values)ImGui.TextDisabled($"{DeliveryCharacter(m.CharacterId)?.Name??"Switching character"} · {m.AccountKey} · {(DateTimeOffset.UtcNow-m.Seen).TotalSeconds:0}s ago");
            ImGui.SetNextItemWidth(-1);if(ImGui.BeginCombo("This client's account",deliveryAccount.Length>0?deliveryAccount:"Select the account used by this client")){
                foreach(var account in session.Plan.AccountKeys){var name=DeliveryCharacters().Where(x=>x.Person.Id+"/"+x.Character.AccountId==account).Select(x=>x.Person.Name+" / "+x.Character.Account).FirstOrDefault()??account;if(ImGui.Selectable(name+"##"+account,deliveryAccount==account))deliveryAccount=account;}ImGui.EndCombo();}
            ImGui.BeginDisabled(deliveryRequest!=null||!Player.IsLoaded||session.Stopped||session.Index>=session.Plan.Legs.Length);
            if(ImGui.Button(session.Members.ContainsKey(deliveryClientId)?"Activate this client":"Join & activate this client")){
                var c=CurrentSharedCharacter();if(c==null||CurrentDeliveryAccount()!=deliveryAccount)deliveryNotice="Log in on the selected account first; no account number is guessed.";
                else if(!config.EnableGilTrader)deliveryNotice="Enable Gil & Item Trader before starting delivery.";
                else if(TraderRunning||retrievalRun.Running||organizeRunning||chestRunning||followSession.Armed)deliveryNotice="Finish or stop other trading, storage and follow actions first.";
                else {deliveryRun=session.Id;deliveryPairing=config.PairingKey;deliveryTrip=deliveryTrade="";deliverySwitchRequested=false;deliveryMustReconcile=deliveryReturning=false;deliveryActionDeadline=DateTimeOffset.UtcNow.AddMinutes(15);if(gardenTendRoute!=null){deliveryResumeGarden=gardenTendBoard;StopTendAdventure("Paused for item delivery.");}DeliveryPost(new{type="join",id=session.Id,revision=session.Revision,clientId=deliveryClientId,accountKey=deliveryAccount,fingerprint=RememberLoadedLoginAccount(),characterId=c.Id,contentId=c.ContentId});}
            }
            ImGui.SameLine();if(ImGui.Button("Stop both clients"))DeliveryPost(new{type="stop",id=session.Id,revision=session.Revision,clientId=deliveryClientId});
            if(session.Paused&&ImGui.Button("Reconcile bags & resume")){try{deliverySwitchRequested=false;deliveryActionDeadline=DateTimeOffset.UtcNow.AddMinutes(15);DeliveryPost(new{type="reconcile",id=session.Id,revision=session.Revision,clientId=deliveryClientId,legId=session.Plan.Legs[session.Index].Id,counts=DeliveryCounts(session.Plan.Legs[session.Index])});}catch(Exception e){deliveryNotice=e.Message;}}
            ImGui.EndDisabled();
            if(session.Index<session.Plan.Legs.Length){var leg=session.Plan.Legs[session.Index];ImGui.Separator();ImGui.TextWrapped($"Handoff {session.Index+1}/{session.Plan.Legs.Length}: {DeliveryCharacter(leg.From)?.Name} → {DeliveryCharacter(leg.To)?.Name}\nMeet: {leg.Meeting.Name} · {leg.Meeting.World} · {leg.Meeting.District} W{leg.Meeting.Ward} P{leg.Meeting.Plot}");
                foreach(var item in leg.Items)ImGui.TextUnformatted($"{item.Quantity} × {SearchItemName(item.ItemId)}");}
        }
        if(deliveryRun.Length>0&&ImGui.Button("Stop local delivery / return journey")){
            CancelAssignedLogin("Stopped by you.");if(itemTrade.Running)StopItemTrade("Stopped by you.");
            if(deliveryTrip.Length>0||deliveryReturning){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}goSeeHomeTrip=null;}
            deliveryRun=deliveryTrip=deliveryTrade=deliveryResumeGarden="";deliveryReturning=false;deliveryNotice="Local delivery stopped. The partner will pause when this client stops reporting.";
        }
        if(deliveryNotice.Length>0)ImGui.TextWrapped(deliveryNotice);
        ImGui.TextWrapped("Both clients must activate. Lost contact pauses the handoff; reconciliation checks both inventories before any resend. Housing travel uses existing GO SEE routes; inaccessible or blocked movement stops for review.");
    }
    private async Task<DeliveryReply> DeliveryRequest(object? body,string pairing)
    {
        try{using var request=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,"https://equinoxjournal.pages.dev/api/companion/item-deliveries");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",pairing);if(body!=null)request.Content=JsonContent.Create(body,options:DeliveryJson);
            using var response=await itemDeliveryClient.SendAsync(request);await response.Content.LoadIntoBufferAsync(1000000);var text=await response.Content.ReadAsStringAsync();
            if(!response.IsSuccessStatusCode){using var error=JsonDocument.Parse(text);return new(null,null,error.RootElement.TryGetProperty("error",out var message)?message.GetString()??"Delivery request refused.":"Delivery request refused.");}
            return body==null?new(JsonSerializer.Deserialize<DeliveryList>(text,DeliveryJson)?.Sessions,null,""):new(null,JsonSerializer.Deserialize<DeliverySession>(text,DeliveryJson),"");
        }catch(Exception e){return new(null,null,"Delivery connection: "+e.Message);}
    }
    private void DeliveryPost(object body){if(deliveryRequest!=null)return;deliveryRequest=DeliveryRequest(body,config.PairingKey);deliveryPoll=DateTimeOffset.UtcNow.AddSeconds(1);}
    private Dictionary<string,int> DeliveryCounts(DeliveryLeg leg){var counts=ItemTradePlan.BagCounts(ReadItemTradeBags());return leg.Items.ToDictionary(i=>i.ItemId+":"+i.Hq.ToString().ToLowerInvariant(),i=>counts.GetValueOrDefault(new(i.ItemId,i.Hq)));}
    private void PauseItemDelivery(DeliverySession s,string reason){CancelAssignedLogin(reason);if(itemTrade.Running)StopItemTrade(reason);try{if(deliveryTrip.Length>0)Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}deliveryTrip=deliveryTrade="";deliveryNotice=reason;DeliveryPost(new{type="pause",id=s.Id,revision=s.Revision,clientId=deliveryClientId,reason});}
    private void UpdateItemDelivery(DateTimeOffset now)
    {
        if(deliveryRequest?.IsCompleted==true){var reply=deliveryRequest.IsCompletedSuccessfully?deliveryRequest.Result:new DeliveryReply(null,null,"Delivery request failed.");deliveryRequest=null;
            if(reply.Error.Length==0)deliveryLastContact=now;if(reply.Sessions!=null)deliverySessions=reply.Sessions;if(reply.Session!=null){deliverySessions=deliverySessions.Where(s=>s.Id!=reply.Session.Id).Append(reply.Session).ToArray();deliverySelected=reply.Session.Id;}if(reply.Error.Length>0)deliveryNotice=reply.Error;}
        if(deliveryRun.Length>0&&config.PairingKey!=deliveryPairing){if(itemTrade.Running)StopItemTrade("Pairing changed.");deliveryRun="";deliveryNotice="Delivery stopped: pairing changed.";return;}
        if(deliveryRun.Length>0&&!deliveryReturning&&deliveryLastContact!=default&&now-deliveryLastContact>TimeSpan.FromSeconds(15)){deliveryMustReconcile=true;CancelAssignedLogin("Delivery connection lost.");if(itemTrade.Running)StopItemTrade("Delivery connection lost.");if(deliveryTrip.Length>0){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}goSeeHomeTrip=null;deliveryTrip="";}deliveryNotice="Connection lost · delivery paused until both clients reconcile.";}
        if(deliveryRequest!=null||config.PairingKey.Length!=64)return;
        var session=deliverySessions.FirstOrDefault(s=>s.Id==deliveryRun);
        if(session!=null&&!session.Members.ContainsKey(deliveryClientId)){
            CancelAssignedLogin("Delivery client reservation is no longer active.");if(itemTrade.Running)StopItemTrade("Delivery client reservation is no longer active.");
            if(deliveryTrip.Length>0){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}goSeeHomeTrip=null;}
            deliveryRun=deliveryTrip=deliveryTrade="";deliveryNotice="Join this delivery again to verify this client's account.";return;
        }
        if(session!=null&&session.Members.TryGetValue(deliveryClientId,out var member)){
            if(session.Status is "stopped" or "complete"){CancelAssignedLogin("Delivery ended.");
                if(itemTrade.Running)StopItemTrade("Delivery ended.");
                if(session.Status=="complete"&&(!Player.IsLoaded||Player.CurrentWorld.RowId!=Player.HomeWorld.RowId)){
                    if(!deliveryReturning){
                        if(!Player.IsLoaded||LifestreamBusy())return;
                        try{if(!Pi.GetIpcSubscriber<uint,bool>("Lifestream.ChangeWorldById").InvokeFunc(Player.HomeWorld.RowId))throw new InvalidOperationException("Home-world return was refused.");deliveryReturning=true;deliveryActionDeadline=now.AddMinutes(15);deliveryNotice="Delivery confirmed · returning to this character's home world.";}
                        catch(Exception e){deliveryNotice="Delivery confirmed. Return home manually: "+e.Message;deliveryRun=deliveryTrip=deliveryTrade=deliveryResumeGarden="";}return;
                    }
                    if(now<deliveryActionDeadline)return;
                    try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}deliveryNotice="Delivery confirmed; return journey timed out. Return home manually.";deliveryRun=deliveryTrip=deliveryTrade=deliveryResumeGarden="";deliveryReturning=false;return;
                }
                if(deliveryReturning&&LifestreamBusy())return;
                deliveryReturning=false;RequestStorageRefresh();deliveryRun=deliveryTrip=deliveryTrade="";
                if(session.Status=="complete"&&deliveryResumeGarden.Length>0){var board=adventureBoards.FirstOrDefault(b=>b.Id==deliveryResumeGarden);deliveryResumeGarden="";if(board!=null)StartTendAdventure(board);}return;
            }
            if(deliveryMustReconcile){if(session.Status=="paused")deliveryMustReconcile=false;else{PauseItemDelivery(session,"Connection was lost. Reconcile both inventories before resuming.");return;}}
            if(now>=deliveryHeartbeat){var c=CurrentSharedCharacter();DeliveryPost(new{type="heartbeat",id=session.Id,revision=session.Revision,clientId=deliveryClientId,characterId=c?.Id??"",contentId=c?.ContentId??""});deliveryHeartbeat=now.AddSeconds(4);return;}
            if(session.Status=="paused"){CancelAssignedLogin("Delivery paused.");if(itemTrade.Running)StopItemTrade("Delivery partner paused or disconnected.");if(deliveryTrip.Length>0){try{Pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}catch{}goSeeHomeTrip=null;}deliveryTrip=deliveryTrade="";return;}
            if(session.Status is "ready" or "trading"){
                try{if(now>=deliveryTick){deliveryTick=now.AddMilliseconds(500);TickItemDelivery(session,member,now);}}catch(Exception e){PauseItemDelivery(session,e.Message);}if(deliveryRequest!=null)return;
            }
        }
        if(now>=deliveryPoll&&(deliveryWindow?.IsOpen==true||deliveryRun.Length>0)){deliveryRequest=DeliveryRequest(null,config.PairingKey);deliveryPoll=now.AddSeconds(2);}
    }
    private void TickItemDelivery(DeliverySession s,DeliveryMember member,DateTimeOffset now)
    {
        if(s.Index>=s.Plan.Legs.Length)return;var leg=s.Plan.Legs[s.Index];var sending=member.AccountKey==leg.FromAccount;var cid=sending?leg.From:leg.To;var partner=DeliveryCharacter(sending?leg.To:leg.From)??throw new InvalidOperationException("Delivery partner is unavailable.");
        if(now>=deliveryActionDeadline)throw new InvalidOperationException("Delivery step timed out. Review travel or bags.");
        if(!Player.IsLoaded)return;
        if(CurrentDeliveryAccount()!=member.AccountKey)throw new InvalidOperationException("This client is using a different account.");
        if(CurrentSharedCharacter()?.Id!=cid){
            if(deliverySwitchRequested){if(assignedLogin==null&&assignedLoginError.Length>0)throw new InvalidOperationException(assignedLoginError);return;}
            if(itemTrade.Running)throw new InvalidOperationException("Finish the active trade before switching characters.");
            var target=DeliveryCharacter(cid)??throw new InvalidOperationException("Assigned character is missing.");if(!StartAssignedCharacterSwitch(target))throw new InvalidOperationException(characterTravelStatus);if(characterSwitchTarget?.Id!=cid)throw new InvalidOperationException(characterTravelStatus);deliverySwitchRequested=true;return;
        }
        deliverySwitchRequested=false;
        var trip=s.Id+":"+s.Index;var trade=trip+":"+leg.Attempt;
        if(deliveryTrade.Length>0&&deliveryTrade!=trade){deliveryTrade="";deliveryTrip="";deliveryActionDeadline=now.AddMinutes(15);}
        var bags=ReadItemTradeBags();
        if(sending&&deliveryTrade!=trade){
            foreach(var i in leg.Items){var have=bags.Where(b=>b.Item==i.ItemId&&b.Hq==i.Hq&&b.Tradable).Sum(b=>b.Quantity);var reserve=cid==s.Plan.Source?GardenDeliveryReserve(i.ItemId):0;if(have-reserve<i.Quantity)throw new InvalidOperationException($"Put {i.Quantity+reserve} × {SearchItemName(i.ItemId)} in bags first ({reserve} reserved for this character's garden plans).");}
        }
        EnsureBidWorlds();var world=bidWorlds!.FirstOrDefault(w=>w.Name==leg.Meeting.World);if(world.Id==0)throw new InvalidOperationException("Meeting world is unavailable.");
        var atMeeting=atOutdoorEstate&&currentAddress?.WorldId==world.Id&&currentAddress.Ward==leg.Meeting.Ward&&currentAddress.Plot==leg.Meeting.Plot&&currentAddress.DistrictName.Replace("The ","")==leg.Meeting.District.Replace("The ","");
        if(!atMeeting){if(deliveryTrip!=trip){if(LifestreamBusy()||followSession.Armed)throw new InvalidOperationException("Another travel operation is active.");GoSeeBid(world.Id,leg.Meeting.District,leg.Meeting.Ward,leg.Meeting.Plot);deliveryTrip=trip;}return;}
        if(LifestreamBusy()||goSeeHomeTrip!=null)return;
        var home=bidWorlds.FirstOrDefault(w=>w.Name==partner.World);if(home.Id==0)throw new InvalidOperationException("Partner home world unavailable.");
        gilPartnerName=partner.Name;gilPartnerWorld=home.Id;gilPartnerContent=ulong.TryParse(partner.ContentId,out var content)?content:0;gilPartnerRequiresParty=false;
        if(GilTradePartner() is not {} nearby||Objects.LocalPlayer is not {} self||Vector3.Distance(self.Position,nearby.Position)>5)return;
        if(!sending&&deliveryTrade!=trade&&!ItemTradePlan.CanReceiveManifest(leg.Items.Select(i=>new TradeItemAmount(i.ItemId,i.Hq,i.Quantity)).ToArray(),bags,id=>CrystalStorage.IsItem(id)?9999:OrganizerStackLimit(id)))throw new InvalidOperationException("Free enough inventory space for the complete delivery.");
        if(sending&&deliveryTrade!=trade){PrepareTradeItemShortcutLabels();if(tradeItemLabelsTask?.IsFaulted==true)throw new InvalidOperationException("Item trade actions could not be loaded.");if(tradeItemLabelsTask?.IsCompletedSuccessfully!=true)return;}
        if(!leg.Started){
            // The receiver must be listening before its readiness allows a sender to request trade.
            if(!sending&&deliveryTrade!=trade){
                if(TraderRunning)throw new InvalidOperationException("Another trade is active.");OpenGilTrader();gilItemsMode=true;gilReceive=true;itemTradeSelection.Clear();foreach(var i in leg.Items)itemTradeSelection[new(i.ItemId,i.Hq)]=i.Quantity;
                StartItemTrade(exactReceive:true);if(!itemTrade.Running)throw new InvalidOperationException(gilTradeNotice.Length>0?gilTradeNotice:itemTrade.Status);deliveryTrade=trade;
            }
            if(!leg.Armed.ContainsKey(deliveryClientId))DeliveryPost(new{type="arm",id=s.Id,revision=s.Revision,clientId=deliveryClientId,legId=leg.Id,counts=DeliveryCounts(leg)});return;
        }
        if(deliveryTrade!=trade){
            if(TraderRunning)throw new InvalidOperationException("Another trade is active.");OpenGilTrader();gilItemsMode=true;gilReceive=!sending;itemTradeSelection.Clear();foreach(var i in leg.Items)itemTradeSelection[new(i.ItemId,i.Hq)]=i.Quantity;
            StartItemTrade(exactReceive:!sending);if(!itemTrade.Running)throw new InvalidOperationException(gilTradeNotice.Length>0?gilTradeNotice:itemTrade.Status);deliveryTrade=trade;return;
        }
        if(itemTrade.Running)return;
        if(itemTrade.Stage!=ItemTradeStage.Complete)throw new InvalidOperationException(itemTrade.Status);
        if(!leg.Receipts.ContainsKey(deliveryClientId))DeliveryPost(new{type="receipt",id=s.Id,revision=s.Revision,clientId=deliveryClientId,legId=leg.Id,confirmed=true,counts=DeliveryCounts(leg)});
    }
    private int GardenDeliveryReserve(uint item)
    {
        var c=CurrentSharedCharacter();if(c==null)return 0;
        var name=DataManager.GetExcelSheet<Lumina.Excel.Sheets.Item>(Dalamud.Game.ClientLanguage.English).GetRowOrDefault(item)?.Name.ToString();if(string.IsNullOrEmpty(name))return 0;
        var total=0;void Add(string required){if(required==name)total++;}
        var owned=c.Houses.Where(h=>h.World==c.World&&h.OwnerName.Equals(c.Name,StringComparison.OrdinalIgnoreCase)).Select(h=>h.Id).ToHashSet();
        var tasks=adventureBoards.SelectMany(b=>b.Stops).SelectMany(s=>s.Tasks).Where(t=>!t.Done&&t.Rule!=null&&AdventurePolicy.IsGardenTask(t)).Select(t=>t.Rule!).ToArray();
        foreach(var source in GardenPlanSources().Where(p=>tasks.Any(t=>t.Cid==c.Id&&t.HouseId==p.HouseId&&t.Batch==p.Batch)||owned.Contains(p.HouseId)&&!tasks.Any(t=>t.Cid.Length>0&&t.Cid!=c.Id&&t.HouseId==p.HouseId&&t.Batch==p.Batch))){
            var plan=EffectiveGardenPlan(source);foreach(var b in plan.Beds.Where(b=>b.Crop.Length>0&&b.Status!="confirmed")){
                Add(GardenSeedItemName(b.Crop));Add(b.Soil);if(b.ReplantOrder>0&&b.Status is not ("starter" or "replant")){Add(GardenSeedItemName(b.Crop));Add(b.StarterSoil);}
            }
        }
        foreach(var task in tasks.Where(t=>t.Cid==c.Id&&t.Kind=="growingcycle"&&t.Phase=="waiting"&&t.PlanTemplate!=null))foreach(var bed in task.PlanTemplate!.Steps){
            Add(GardenSeedItemName(bed.Crop));Add(bed.Soil);if(bed.ReplantOrder>0){Add(GardenSeedItemName(bed.Crop));Add(bed.StarterSoil);}
        }
        return total;
    }
}
