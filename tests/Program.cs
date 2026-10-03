using System.Text.Json;
using EquinoxCompanion;
var time = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
var gate = new ObservationGate();
void Check(string name, string? actual, string? expected) { if (actual != expected) throw new Exception($"{name}: {actual} != {expected}"); Console.WriteLine($"PASS {name}"); }
var errorDir = Path.Combine(Path.GetTempPath(), "equinox-errors-" + Guid.NewGuid().ToString("N"));
try
{
    var journal = new ErrorJournal(errorDir);
    journal.Record("test", "Known failure", "event-a", "house.entered");
    journal.Record("test", "Known failure", "event-a", "house.entered");
    Check("error log deduplicates repeats", journal.Snapshot().Length.ToString(), "1");
    var reopened = new ErrorJournal(errorDir);
    Check("error history survives reopening", reopened.Snapshot().Length.ToString(), "1");
    var entry = JsonDocument.Parse(reopened.Snapshot()[0]);
    Check("error event remains identifiable", entry.RootElement.GetProperty("eventId").GetString(), "event-a");
    File.WriteAllText(journal.FilePath, new string(' ', 1_000_001));
    journal.Record("test", "Another failure");
    Check("oversized log rotates", File.Exists(journal.FilePath + ".previous").ToString(), "True");
    Check("new log remains bounded", (new FileInfo(journal.FilePath).Length < 10000).ToString(), "True");
    var blocked = Path.Combine(errorDir, "not-a-directory"); File.WriteAllText(blocked, "file");
    var unavailable = new ErrorJournal(blocked); unavailable.Record("test", "Failure");
    Check("log write failure does not crash", (unavailable.WriteFailure is not null).ToString(), "True");
}
finally { Directory.Delete(errorDir, true); }
var houseOwner = new SharedCharacter("owner", "Owner Example", "World A", "", "", "", []);
var houseTenant = houseOwner with { Id = "tenant", Name = "Tenant Example" };
var sharedPrivate = new SharedHouse("house", "", "Private house", "", "World A", "", 1, 1, "", houseOwner.Name, "", "", null, false);
Check("shared private owner counts", SharedHousingEligibility.CountsForCharacter(houseOwner, sharedPrivate, [houseOwner, houseTenant]).ToString(), "True");
Check("shared private tenant excluded", SharedHousingEligibility.CountsForCharacter(houseTenant, sharedPrivate, [houseOwner, houseTenant]).ToString(), "False");
Check("unknown private owner excluded", SharedHousingEligibility.CountsForCharacter(houseOwner, sharedPrivate with { OwnerName = "" }, [houseOwner]).ToString(), "False");
Check("other-world owner excluded", SharedHousingEligibility.CountsForCharacter(houseOwner with { World = "World B" }, sharedPrivate, [houseOwner]).ToString(), "False");
Check("ambiguous private owner excluded", SharedHousingEligibility.CountsForCharacter(houseOwner, sharedPrivate, [houseOwner, houseOwner with { Id = "duplicate" }]).ToString(), "False");
Check("linked FC member counts for bar", SharedHousingEligibility.CountsForCharacter(houseTenant, sharedPrivate with { Type = "Free Company house" }, [houseOwner, houseTenant]).ToString(), "True");
Check("debounce initial location", gate.Observe("house-a", time), null);
Check("startup inside is not entry", gate.Observe("house-a", time.AddSeconds(2)), "house.observedInside");
Check("standing inside does not repeat", gate.Observe("house-a", time.AddSeconds(10)), null);
Check("transient outside", gate.Observe("outside", time.AddSeconds(11)), null);
Check("transient return", gate.Observe("house-a", time.AddMilliseconds(11250)), null);
Check("no false revisit after transient", gate.Observe("house-a", time.AddSeconds(13)), null);
gate.Observe("outside", time.AddSeconds(14)); gate.Observe("outside", time.AddSeconds(16));
gate.Observe("house-a", time.AddSeconds(17));
Check("real revisit", gate.Observe("house-a", time.AddSeconds(19)), "house.entered");
gate.Reset(); gate.Observe("house-a", time.AddSeconds(20));
Check("character switch/reload is not entry", gate.Observe("house-a", time.AddSeconds(22)), "house.observedInside");
gate.Observe("house-b", time.AddSeconds(23));
Check("unseen doorway stays observation", gate.Observe("house-b", time.AddSeconds(25)), "house.observedInside");

var actor = new Actor("synthetic", "Test", 1, 1);
var address = new Address("house-a", 1, 1, 15, 15, 0, false, false);
GardenSnapshot Sample(int ms, string? target, Address? place = null) =>
    new(time.AddMilliseconds(ms), actor, place ?? address, target, target, null, null, false, []);
var context = GardenContext.Capture(null, Sample(0, "bed-a"));
context = GardenContext.Capture(context, Sample(100, null));
Check("retain target after game clears it", context.CandidateAt(time.AddMilliseconds(150))?.TargetId, "bed-a");
Check("do not use stale framework context", context.CandidateAt(time.AddMilliseconds(900))?.TargetId, null);
context = GardenContext.Capture(context, Sample(1900, null));
Check("recent target expires", context.CandidateAt(time.AddMilliseconds(2100))?.TargetId, null);
context = GardenContext.Capture(context, Sample(2200, "bed-b"));
Check("new target replaces old bed", context.CandidateAt(time.AddMilliseconds(2210))?.TargetId, "bed-b");
context = GardenContext.Capture(context, Sample(2250, null, address with { HouseId = "house-b", Plot = 16 }));
Check("house switch clears target", context.CandidateAt(time.AddMilliseconds(2260))?.TargetId, null);
context = GardenContext.Capture(null, Sample(2300, "bed-a"));
context = GardenContext.Capture(context, Sample(2350, null) with { Actor = actor with { ContentId = "other" } });
Check("character switch clears target", context.CandidateAt(time.AddMilliseconds(2360))?.TargetId, null);
context = GardenContext.Capture(null, Sample(2400, null));
Check("zone or recording reset has no old target", context.CandidateAt(time.AddMilliseconds(2410))?.TargetId, null);
context = GardenContext.Capture(null, Sample(2500, "bed-a") with { Address = null });
Check("unknown property cannot match a bed", context.CandidateAt(time.AddMilliseconds(2510))?.TargetId, null);
Check("failed fertilizer is not success", GardenSignals.Classify(4010), "fertilizing.failed.noFertilizer");
Check("healthy crop is not watering proof", GardenSignals.Classify(4017), "crop.status.doingWell");
Check("immature removal is not harvest", GardenSignals.Classify(4025), "crop.removed");
Check("items alone are not harvest proof", GardenSignals.Classify(751), "item.received");
Check("unknown log cannot create garden action", GardenSignals.Classify(603), "unclassified");

var menu = new GardenMenu(1, time, Sample(0, "bed-a"), "1st Bed, 1st Patch", ["Fertilize Crop", "Tend Crop", "Remove Crop", "Quit"]);
Check("resolve submitted tend option", menu.OptionAt(1), "Tend Crop");
Check("quit stays quit", menu.OptionAt(3), "Quit");
Check("negative callback is not tend", menu.OptionAt(-1), null);
Check("out of range callback stays unknown", menu.OptionAt(4), null);
Check("missing callback stays unknown", menu.OptionAt(null), null);
Check("different bed cannot inherit menu", menu.Matches(time.AddSeconds(1), Sample(1000, "bed-b")).ToString(), "False");
Check("matching bed can use menu", menu.Matches(time.AddSeconds(1), Sample(1000, "bed-a")).ToString(), "True");
Check("expired menu cannot match", menu.Matches(time.AddSeconds(61), Sample(61000, "bed-a")).ToString(), "False");
Check("different property cannot inherit menu", menu.Matches(time.AddSeconds(1), Sample(1000, "bed-a", address with { HouseId = "other" })).ToString(), "False");

var intent = TendIntent.From(menu, "Tend Crop", time)!;
Check("successful tend identifies bed", intent.Confirm(4017, time.AddMilliseconds(400), Sample(400, "bed-a"))?.Bed.ToString(), "1");
Check("successful tend identifies patch", intent.Confirm(4017, time.AddMilliseconds(400), Sample(400, "bed-a"))?.Patch.ToString(), "1");
Check("quit never creates intent", TendIntent.From(menu, "Quit", time)?.EventId, null);
Check("unknown title does not invent bed", TendIntent.From(menu with { Title = "Unknown" }, "Tend Crop", time)?.EventId, null);
Check("failure cannot confirm tend", intent.Confirm(4010, time.AddMilliseconds(400), Sample(400, "bed-a"))?.EventId, null);
Check("wrong target cannot confirm", intent.Confirm(4017, time.AddMilliseconds(400), Sample(400, "bed-b"))?.EventId, null);
Check("wrong house cannot confirm", intent.Confirm(4017, time.AddMilliseconds(400), Sample(400, "bed-a", address with { HouseId = "other" }))?.EventId, null);
Check("wrong character cannot confirm", intent.Confirm(4017, time.AddMilliseconds(400), Sample(400, "bed-a") with { Actor = actor with { ContentId = "other" } })?.EventId, null);
Check("stale intent cannot confirm", intent.Confirm(4017, time.AddSeconds(4), Sample(4000, "bed-a"))?.EventId, null);
Check("earlier log cannot confirm", intent.Confirm(4017, time.AddMilliseconds(-1), Sample(0, "bed-a"))?.EventId, null);
Check("missing target cannot confirm", intent.Confirm(4017, time.AddMilliseconds(400), null)?.EventId, null);
Check("duplicate response has same dedupe key", intent.Confirm(4017, time.AddMilliseconds(450), Sample(450, "bed-a"))?.EventId, intent.EventId);

// Optional private replay input; never copy player diagnostics into the repository.
if (args.Length > 0 && args[0] != "--queue")
{
    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[0]));
    TendIntent? pending = null;
    var saved = new HashSet<string>();
    var quits = 0;
    foreach (var item in document.RootElement.GetProperty("diagnostics").EnumerateArray().OrderBy(x => x.GetProperty("observedAt").GetDateTimeOffset()))
    {
        var at = item.GetProperty("observedAt").GetDateTimeOffset();
        var data = item.GetProperty("data");
        var kind = item.GetProperty("kind").GetString();
        if (kind == "garden.callbackObservation")
        {
            var target = System.Text.Json.JsonSerializer.Deserialize<GardenSnapshot>(data.GetProperty("candidateTarget").GetRawText(), opts)!;
            var option = data.GetProperty("selectedOptionCandidate").GetString();
            if (option == "Quit") quits++;
            pending = TendIntent.From(new GardenMenu(1, at, target, data.GetProperty("menuTitle").GetString()!, []), option, at);
        }
        else if (kind == "game.logObservation")
        {
            var target = System.Text.Json.JsonSerializer.Deserialize<GardenSnapshot>(data.GetProperty("candidateTarget").GetRawText(), opts);
            var result = pending?.Confirm(data.GetProperty("logMessageId").GetUInt32(), at, target);
            if (result is not null) saved.Add(result.EventId);
        }
    }
    Check("private replay: eight confirmed tends", saved.Count.ToString(), "8");
    Check("private replay: three cancelled selections", quits.ToString(), "3");
}

var plantTarget = Sample(0, "bed-a") with { TargetDetails = new(2003757, 1, "EventObj", 0, 0, 0) };
var planting = new PlantIntent("plant-test", time, plantTarget, new(7731, "Mirror Apple Seeds", 7766, "Grade 3 Thanalan Topsoil"));
Check("planting requires success response", planting.Confirm(4017, [1, 1], time.AddSeconds(1), plantTarget)?.EventId, null);
Check("planting failed response never records", planting.Confirm(4005, [1, 1], time.AddSeconds(1), plantTarget)?.EventId, null);
Check("planting exact success", planting.Confirm(4015, [2, 8], time.AddSeconds(1), plantTarget)?.Bed.ToString(), "8");
Check("planting uses confirmed patch", planting.Confirm(4015, [2, 8], time.AddSeconds(1), plantTarget)?.Patch.ToString(), "2");
Check("planting wrong target rejected", planting.Confirm(4015, [1, 1], time.AddSeconds(1), plantTarget with {TargetId="other"})?.EventId, null);
Check("planting wrong character rejected", planting.Confirm(4015, [1, 1], time.AddSeconds(1), plantTarget with {Actor=actor with {ContentId="other"}})?.EventId, null);
Check("planting wrong property rejected", planting.Confirm(4015, [1, 1], time.AddSeconds(1), plantTarget with {Address=address with {Plot=16}})?.EventId, null);
Check("planting invalid bed rejected", planting.Confirm(4015, [1, 9], time.AddSeconds(1), plantTarget)?.EventId, null);
Check("planting missing location rejected", planting.Confirm(4015, [], time.AddSeconds(1), plantTarget)?.EventId, null);
Check("planting expired selection rejected", planting.Confirm(4015, [1, 1], time.AddSeconds(31), plantTarget)?.EventId, null);
Check("planting no seed rejected", (planting with {Plant=planting.Plant with {SeedId=0}}).Confirm(4015, [1, 1], time.AddSeconds(1), plantTarget)?.EventId, null);
var matureMenu = new GardenMenu(1,time,plantTarget,"8th Bed, 1st Patch",["Harvest Crop","Quit","",""]);
Check("opening mature bed identifies bed without action",matureMenu.ReadyLocation()?.Bed.ToString(),"8");
Check("opening mature bed identifies patch",matureMenu.ReadyLocation()?.Patch.ToString(),"1");
Check("growing menu cannot mark ready",(matureMenu with {Options=["Tend Crop","Quit"]}).ReadyLocation()?.Bed.ToString(),null);
Check("ambiguous menu cannot mark ready",(matureMenu with {Options=["Harvest Crop","Tend Crop"]}).ReadyLocation()?.Bed.ToString(),null);
Check("unknown title cannot mark ready",(matureMenu with {Title="A different menu"}).ReadyLocation()?.Bed.ToString(),null);
Check("nongarden target cannot mark ready",(matureMenu with {Target=plantTarget with {TargetDetails=null}}).ReadyLocation()?.Bed.ToString(),null);
Check("parse mature crop from system text",CropChatMatcher.Parse("Curiel Root\nThis crop is ready to be harvested."),"Curiel Root");
Check("parse second crop",CropChatMatcher.Parse("Royal Kukuru Bean\r\nThis crop is ready to be harvested."),"Royal Kukuru Bean");
Check("normal chat is not crop evidence",CropChatMatcher.Parse("I harvested some Curiel Root"),null);
Check("status without crop stays unknown",CropChatMatcher.Parse(CropChatMatcher.ReadyText),null);
Check("sender-style system crop",CropChatMatcher.Parse(CropChatMatcher.ReadyText,"Curiel Root"),"Curiel Root");
bool Known(string n) => n is "Curiel Root" or "Royal Kukuru Bean";
var cm = new CropChatMatcher();
cm.Add(new CropChat(time,plantTarget,"Curiel Root\n"+CropChatMatcher.ReadyText));
cm.Add(matureMenu with {OpenedAt=time.AddMilliseconds(100)});
Check("wait for menu before correlation",cm.Drain(time.AddMilliseconds(200),Known).Count.ToString(),"0");
var matched=cm.Drain(time.AddMilliseconds(400),Known);
Check("chat before menu matches crop",matched.Single().Crop.CropName,"Curiel Root");
Check("chat before menu matches bed",matched.Single().Bed.ToString(),"8");
Check("matched chat consumed once",cm.Drain(time.AddSeconds(1),Known).Count.ToString(),"0");
cm.Clear();cm.Add(matureMenu);
cm.Add(new CropChat(time.AddMilliseconds(50),plantTarget,"Royal Kukuru Bean"));
cm.Add(new CropChat(time.AddMilliseconds(100),plantTarget,CropChatMatcher.ReadyText));
Check("split name and status correlate",cm.Drain(time.AddMilliseconds(500),Known).Single().Crop.CropName,"Royal Kukuru Bean");
cm.Clear();cm.Add(matureMenu);cm.Add(new CropChat(time,plantTarget with {TargetId="different-bed"},"Curiel Root\n"+CropChatMatcher.ReadyText));
Check("neighbour target cannot inherit crop",cm.Drain(time.AddSeconds(1),Known).Count.ToString(),"0");
cm.Clear();cm.Add(matureMenu);cm.Add(matureMenu with {Title="1st Bed, 2nd Patch"});cm.Add(new CropChat(time,plantTarget,"Curiel Root\n"+CropChatMatcher.ReadyText));
Check("ambiguous bed association is rejected",cm.Drain(time.AddSeconds(1),Known).Count.ToString(),"0");
cm.Clear();cm.Add(matureMenu);cm.Add(new CropChat(time.AddSeconds(3),plantTarget,"Curiel Root\n"+CropChatMatcher.ReadyText));
Check("late message cannot reuse old menu",cm.Drain(time.AddSeconds(4),Known).Count.ToString(),"0");
cm.Clear();cm.Add(matureMenu);cm.Add(new CropChat(time,plantTarget with {Actor=actor with {ContentId="other"}},"Curiel Root\n"+CropChatMatcher.ReadyText));
Check("other character cannot inherit crop",cm.Drain(time.AddSeconds(1),Known).Count.ToString(),"0");
cm.Clear();cm.Add(matureMenu);cm.Add(new CropChat(time,plantTarget with {Address=address with {Plot=16}},"Curiel Root\n"+CropChatMatcher.ReadyText));
Check("other property cannot inherit crop",cm.Drain(time.AddSeconds(1),Known).Count.ToString(),"0");
cm.Clear();cm.Add(matureMenu);cm.Add(new CropChat(time,plantTarget,"Invented Plant\n"+CropChatMatcher.ReadyText));
Check("unknown game item is rejected",cm.Drain(time.AddSeconds(1),Known).Count.ToString(),"0");
cm.Clear();cm.Add(matureMenu);cm.Add(new CropChat(time,plantTarget,"Curiel Root\n"+CropChatMatcher.ReadyText));cm.Clear();
Check("zone reset discards old association",cm.Drain(time.AddSeconds(1),Known).Count.ToString(),"0");
cm.Add(matureMenu with {Title="1st Bed, 1st Patch"});
cm.Add(matureMenu with {Title="2nd Bed, 1st Patch",OpenedAt=time.AddMilliseconds(200),Target=plantTarget with {TargetId="next-bed"}});
cm.Add(new CropChat(time,plantTarget,"Curiel Root\n"+CropChatMatcher.ReadyText));
cm.Add(new CropChat(time.AddMilliseconds(200),plantTarget with {TargetId="next-bed"},"Royal Kukuru Bean\n"+CropChatMatcher.ReadyText));
var rapid=cm.Drain(time.AddMilliseconds(600),Known);Check("rapid clicks remain separate",string.Join("|",rapid.Select(x=>$"{x.Bed}:{x.Crop.CropName}")),"1:Curiel Root|2:Royal Kukuru Bean");

Check("mature menu stale count uses its two visible options",GardenMenu.VisibleOptionCount(4,2,"Harvest Crop","Quit").ToString(),"2");
Check("mature menu extra slots are not interpreted",GardenMenu.VisibleOptionCount(4,16,"Harvest Crop","Quit").ToString(),"2");
Check("growing menu preserves all option indices",GardenMenu.VisibleOptionCount(4,4,"Fertilize Crop","Tend Crop").ToString(),"4");
Check("truncated growing menu rejected",GardenMenu.VisibleOptionCount(4,2,"Fertilize Crop","Tend Crop").ToString(),"0");
Check("incomplete mature pair rejected",GardenMenu.VisibleOptionCount(4,1,"Harvest Crop",null).ToString(),"0");

// Queue regression and exact housing-age boundaries.
var realActor = new Actor("123456789", "Test Owner", 409, 409, "Kraken", "Kraken");
var realAddress = new Address("019903D3001B002C", 409, 979, 28, 45, 0, false, false);
var chInfo = new CharacterDetails(23, "BRD", 91, 91, "Au Ra", "Xaela", "Male", [new(23, "BRD", 91)], HighestBattleLevel: 91);
var badCharacter = new SyncEvent(new string('a',32), "character.updated", time, realActor with { HomeWorldId = 0, CurrentWorldId = 0 }, null, Character: chInfo);
var goodCharacter = badCharacter with { Id = new string('b',32), Actor = realActor };
var newerCharacter = goodCharacter with { At = time.AddMinutes(1) };
Check("complete newer snapshot supersedes incomplete login", SyncValidation.SupersededIncompleteCharacter(badCharacter, [newerCharacter], time.AddMinutes(2)).ToString(), "True");
Check("different character cannot supersede incomplete snapshot", SyncValidation.SupersededIncompleteCharacter(badCharacter, [newerCharacter with { Actor = realActor with { ContentId = "888" } }], time.AddMinutes(2)).ToString(), "False");
Check("equal time does not supersede incomplete snapshot", SyncValidation.SupersededIncompleteCharacter(badCharacter, [goodCharacter], time).ToString(), "False");
Check("complete snapshots remain sendable", SyncValidation.SupersededIncompleteCharacter(goodCharacter, [newerCharacter], time.AddMinutes(2)).ToString(), "False");
var estateEvent = new SyncEvent(new string('c',32), "house.discovered", time, realActor, realAddress, House: new("Private house", "Small", "owned-estate-id"));
Check("incomplete world held", SyncValidation.CanSend(badCharacter,time).ToString(), "False");
Check("valid character uploads", SyncValidation.CanSend(goodCharacter,time).ToString(), "True");
Check("house behind invalid record uploads", new[]{badCharacter,estateEvent,goodCharacter}.Count(e=>SyncValidation.CanSend(e,time)).ToString(), "2");
Check("incomplete job held", SyncValidation.CanSend(goodCharacter with { Character = chInfo with { JobId = 0 } },time).ToString(), "False");
foreach (var boundary in new[]{ (0d,HousingBand.Recent),(7d,HousingBand.Recent),(7.999d,HousingBand.Recent),(8d,HousingBand.Warning),(30.999d,HousingBand.Warning),(31d,HousingBand.Urgent),(45d,HousingBand.Urgent),(45.001d,HousingBand.Overdue) })
    Check($"housing band {boundary.Item1}",HousingStatus.Band(time,time.AddDays(boundary.Item1)).ToString(),boundary.Item2.ToString());
Check("unknown entry stays unknown", HousingStatus.Band(null,time).ToString(), "Unknown");
var ownerVisit = new HouseObservation("owner",time,"house.entered",realActor,realAddress);
var visitor = realActor with { ContentId = "555", Name = "Guest" };
var guestVisit = ownerVisit with { EventId="guest",Actor=visitor,ObservedAt=time.AddDays(2) };
Check("guest does not reset private estimate", HousingStatus.LastEligibleEntry(estateEvent,[estateEvent],[ownerVisit,guestVisit])?.ToString("O"),time.ToString("O"));
Check("login observation does not reset", HousingStatus.LastEligibleEntry(estateEvent,[estateEvent],[ownerVisit with { Kind="house.observedInside" }])?.ToString("O"),null);
var fcDetail = new HouseDetails("Free Company house","Small","owned-estate-id",new("999","Example FC","FC",409));
var fcEstate = estateEvent with { House=fcDetail };
var memberEstate = fcEstate with { Actor=visitor };
Check("linked FC member counts", HousingStatus.LastEligibleEntry(fcEstate,[fcEstate,memberEstate],[ownerVisit,guestVisit])?.ToString("O"),time.AddDays(2).ToString("O"));
// Optional replay uses a private diagnostic export, never included in a release.
if (args.Length > 1 && args[0] == "--queue") {
    using var recorded = System.Text.Json.JsonDocument.Parse(File.ReadAllText(args[1]));
    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var entries = recorded.RootElement.GetProperty("observedDetails").Deserialize<SyncEvent[]>(options)!;
    var at = recorded.RootElement.GetProperty("exportedAt").GetDateTimeOffset();
    var held = entries.Where(e=>!SyncValidation.CanSend(e,at)).ToArray();
    Check("live export holds only bad world record",held.Length.ToString(),"1");
    Check("live export identifies recorded zero world",held[0].Actor.HomeWorldId.ToString(),"0");
    Check("live FC discovery remains sendable",entries.Where(e=>e.Kind=="house.discovered"&&e.At>=at.AddMinutes(-10)).All(e=>SyncValidation.CanSend(e,at)).ToString(),"True");
}

Check("entry chat identifies private owner", HouseEntryNotice.Format(ownerVisit,estateEvent)?.Contains("your Private House").ToString(), "True");
Check("entry chat identifies FC member", HouseEntryNotice.Format(ownerVisit,fcEstate)?.Contains("your FC House").ToString(), "True");
Check("login observation never chats", HouseEntryNotice.Format(ownerVisit with { Kind="house.observedInside" },estateEvent), null);
Check("guest never labelled owner", HouseEntryNotice.Format(guestVisit,estateEvent)?.Contains("ownership unconfirmed").ToString(), "True");
var pairedHome = sharedPrivate with { GameHouseId = realAddress.HouseId, OwnerName = "Paired Owner", Name = "Our home" };
var pairedRoster = new SharedRoster(1,time,[new("person","Person",[houseOwner with { Houses = [pairedHome] }])]);
Check("paired owner appears in visiting chat", HouseEntryNotice.Format(guestVisit,null,pairedRoster)?.Contains("Paired Owner's Private house").ToString(), "True");
Check("paired visitor is never called owner", HouseEntryNotice.Format(guestVisit,null,pairedRoster)?.Contains("your Private House").ToString(), "False");
Check("another estate cannot supply owner", HouseEntryNotice.Format(guestVisit with { Address = realAddress with { HouseId = "0000000000000001" } },null,pairedRoster)?.Contains("ownership unconfirmed").ToString(), "True");
Check("workshop never triggers estate chat", HouseEntryNotice.Format(ownerVisit with { Address=realAddress with { Workshop=true } },estateEvent), null);

var collectionEvent=estateEvent with {Kind="collection.observed",Address=null,Collection=new("mount",[1,2],[1],[2])};
Check("collection does not require house",SyncValidation.CanSend(collectionEvent,time).ToString(),"True");
Check("collection rejects unknown unlocked id",SyncValidation.CanSend(collectionEvent with {Collection=new("mount",[1],[9],[])},time).ToString(),"False");
Check("collection rejects null known array",SyncValidation.CanSend(collectionEvent with {Collection=new("mount",null!,[],[])},time).ToString(),"False");
Check("fashion rejects score from another cycle",SyncValidation.CanSend(estateEvent with {Kind="fashion.observed",Address=null,Fashion=new(80,3,0,time.AddDays(-8).ToString("O"))},time).ToString(),"False");
var unmappedTarget=plantTarget with {TargetDetails=plantTarget.TargetDetails! with {EventArgument=5652}};
cm.Clear();cm.Add(new CropChat(time,unmappedTarget,"Curiel Root\n"+CropChatMatcher.ReadyText));
Check("no-permission chat waits for menu",cm.Drain(time.AddMilliseconds(500),Known,true).Count.ToString(),"0");
var pendingCrop=cm.Drain(time.AddMilliseconds(2200),Known,true).Single();
Check("unmapped chat retains crop",pendingCrop.Crop.CropName,"Curiel Root");
Check("unmapped chat never invents patch",pendingCrop.Patch.ToString(),"0");
Check("unmapped chat keeps calibration requirement",pendingCrop.Crop.Evidence,"garden-system-message-awaiting-calibration");
Check("unmapped observation consumed once",cm.Drain(time.AddMilliseconds(2400),Known,true).Count.ToString(),"0");
var emptyMenu = matureMenu with { Options = ["Plant Seeds", "Quit"] };
Check("numbered empty menu identifies bed", emptyMenu.EmptyLocation()?.Bed.ToString(), "8");
Check("mature menu is never empty", matureMenu.EmptyLocation()?.Bed.ToString(), null);
Check("ambiguous empty menu is rejected", (emptyMenu with { Options = ["Plant Seeds", "Tend Crop", "Quit"] }).EmptyLocation()?.Bed.ToString(), null);
Check("unnumbered empty menu is rejected", (emptyMenu with { Title = "Garden" }).EmptyLocation()?.Bed.ToString(), null);
Check("empty menu ignores stale option count", GardenMenu.VisibleOptionCount(4, 2, "Plant Seeds", "Quit").ToString(), "2");
Check("old website keeps house entries flowing", SyncValidation.SupportedByWebsite("house.entered", 1).ToString(), "True");
Check("old website holds new collection observations", SyncValidation.SupportedByWebsite("collection.observed", 1).ToString(), "False");
Check("old website holds empty-bed observations", SyncValidation.SupportedByWebsite("garden.empty", 1).ToString(), "False");
Check("updated website accepts new observations", SyncValidation.SupportedByWebsite("garden.empty", 2).ToString(), "True");
Check("hairstyle uses collection contract", SyncValidation.CanSend(collectionEvent with { Collection = new("hairstyle", [637], [637], []) }, time).ToString(), "True");
Check("storage waits for protocol3", SyncValidation.SupportedByWebsite("storage.observed",2).ToString(), "False");
Check("storage accepted by protocol3", SyncValidation.SupportedByWebsite("storage.observed",3).ToString(), "True");
Check("named retainer inventory accepted", SyncValidation.CanSend(collectionEvent with { Kind="storage.observed", Collection=null, Storage=new("retainer:123:10003", "Retainer Example · Inventory 4", [12345]) }, time).ToString(), "True");
Check("empty loaded container accepted", SyncValidation.CanSend(collectionEvent with { Kind="storage.observed", Collection=null, Storage=new("bag:0", "Inventory 1", []) }, time).ToString(), "True");
Check("shared chest has explicit scope", SyncValidation.CanSend(collectionEvent with { Kind="storage.observed", Collection=null, Storage=new("fc:123:20004", "Available in FC Chest · Inventory 5", [12345]) }, time).ToString(), "True");

var profileCompany = new FreeCompanyDetails("9281074407080476924", "Company Test", "TEST", 410, "Master Test", new(30, 5, "company-profile", "Rafflesia", "2021-05-24T00:00:00Z", "Our company", EstateName: "Test Estate"));
Check("unsigned FC profile identity", CompanyProfileIdentity.Id(unchecked((long)9281074407080476924UL)), "9281074407080476924");
Check("FC profile complete", SyncValidation.CompanyReady(profileCompany).ToString(), "True");
Check("FC profile loading rank rejected", SyncValidation.CompanyReady(profileCompany with { Profile=profileCompany.Profile! with {Rank=0} }).ToString(), "False");
Check("FC profile held for older website", SyncValidation.SupportedByWebsite("company.observed", 3).ToString(), "False");
Check("FC profile supported after website update", SyncValidation.SupportedByWebsite("company.observed", 4).ToString(), "True");
var companySign = new PlacardDetails("visitor", new("0000000000000001",410,641,1,2,0,false,false),"Test Estate","Small",2,"Company Test");
Check("FC profile matches viewed estate", CompanyProfileIdentity.MatchesPlacard(profileCompany,companySign).ToString(),"True");
Check("FC profile rejects another world", CompanyProfileIdentity.MatchesPlacard(profileCompany,companySign with {Address=companySign.Address with {WorldId=411}}).ToString(),"False");
Check("FC profile rejects another company", CompanyProfileIdentity.MatchesPlacard(profileCompany,companySign with {OwnerName="Other Company"}).ToString(),"False");
Check("FC profile rejects another estate", CompanyProfileIdentity.MatchesPlacard(profileCompany,companySign with {Name="Other Estate"}).ToString(),"False");

Check("combined profile categories readable", CompanyProfileIdentity.Flags(5,["Tank","Healer","DPS"]),"Tank, DPS");

var groupedCharacter = new SharedCharacter("group-test", "Test", "Rafflesia", "Dynamis", "NA", "MAIN", [], "main-id", false, false);
Check("boosted without FC remains Regulars", SharedCharacterGrouping.Group(groupedCharacter), "Regulars");
Check("boosted FC member remains Regulars", SharedCharacterGrouping.Group(groupedCharacter with { FcMember = true }), "Regulars");
Check("unboosted FC member is Floater", SharedCharacterGrouping.Group(groupedCharacter with { NeedsBoost = true, FcMember = true }), "Floaters");
Check("unboosted without FC is Empty", SharedCharacterGrouping.Group(groupedCharacter with { NeedsBoost = true }), "Empty");
Check("stable account identity", SharedCharacterGrouping.AccountKey(groupedCharacter), "main-id");
var memberDisplay = houseOwner with { FcId = "123" };
var ownFcDisplay = sharedPrivate with { Id = "fc", Type = "Free Company house", FcId = "123", OwnerName = "Another Master" };
var sharedFcDisplay = ownFcDisplay with { Id = "shared-fc", FcId = "999" };
Check("private owner display", SharedHousePresentation.Label(memberDisplay,sharedPrivate,true),"Private");
Check("private tenant display", SharedHousePresentation.Label(memberDisplay,sharedPrivate,false),"Shared");
Check("own FC member display", SharedHousePresentation.Label(memberDisplay,ownFcDisplay,false),"FC");
Check("other FC shared display", SharedHousePresentation.Label(memberDisplay,sharedFcDisplay,false),"Shared");
Check("unknown FC membership shared display", SharedHousePresentation.Label(houseTenant,ownFcDisplay,false),"Shared");
var mixedEstates = new[]{(House: sharedPrivate with {Id="shared-private"}, Own:false),(House:sharedFcDisplay, Own:false),(House:ownFcDisplay, Own:false),(House:sharedPrivate, Own:true)};
Check("Private FC then all Shared stable order",string.Join(",",mixedEstates.OrderBy(x=>SharedHousePresentation.Order(SharedHousePresentation.Label(memberDisplay,x.House,x.Own))).Select(x=>x.House.Id)),"house,fc,shared-private,shared-fc");

Check("unknown progress waits", SharedCharacterGrouping.Group(groupedCharacter with { NeedsBoost = null }), "Pending sync");
Check("unknown FC waits", SharedCharacterGrouping.Group(groupedCharacter with { NeedsBoost = true, FcMember = null }), "Pending sync");

var gardenAddress = new Address("0000000000000001",410,641,15,7,0,false,false,"Rafflesia","Shirogane");
var gardenPlan = new SharedGardenPlan("house", "Home", "Rafflesia", "Shirogane", 15, 7, 1, "Krakka Root", DateTimeOffset.UtcNow, [], "0000000000000001", 3);
Check("garden exact estate", SharedGardenLocation.Match(gardenAddress,[gardenPlan])??"none","house");
Check("garden another world rejected", SharedGardenLocation.Match(gardenAddress with {WorldName="Seraph"},[gardenPlan])??"none","none");
Check("garden another ward rejected", SharedGardenLocation.Match(gardenAddress with {Ward=16},[gardenPlan])??"none","none");
Check("garden another estate ID rejected", SharedGardenLocation.Match(gardenAddress with {HouseId="0000000000000002"},[gardenPlan])??"none","none");
Check("garden loading has no previous guide", SharedGardenLocation.Match(null,[gardenPlan])??"none","none");
Check("garden duplicate house rejected", SharedGardenLocation.Match(gardenAddress,[gardenPlan,gardenPlan with {HouseId="other"}])??"none","none");
Check("garden multiple batches same house", SharedGardenLocation.Match(gardenAddress,[gardenPlan,gardenPlan with {Batch=2}])??"none","house");
var careNow=DateTimeOffset.UtcNow;
var careBed=new SharedGardenCareBed(1,false,false,careNow.AddHours(-24),careNow.AddHours(-4),careNow.AddDays(1));
Check("garden due tending reminder",GardenCareStatus.Due(careBed,careNow)??"none","tend");
Check("garden cared-for quiet",GardenCareStatus.Due(careBed with {Watered=careNow,NextTend=careNow.AddHours(12)},careNow)??"none","none");
Check("garden confirmed ready harvest",GardenCareStatus.Due(careBed with {Ready=true},careNow)??"none","harvest");
Check("garden held mature quiet",GardenCareStatus.Due(careBed with {Ready=true,KeepMature=true},careNow)??"none","none");
Check("garden estimated maturity is check",GardenCareStatus.Due(careBed with {Watered=careNow,NextTend=careNow.AddHours(12),HarvestAt=careNow.AddMinutes(-1)},careNow)??"none","check maturity");
Check("garden unknown timing needs care check",GardenCareStatus.Due(careBed with {Watered=null,NextTend=null},careNow)??"none","check care");
Check("garden compact house batches",GardenCareStatus.Message("House X","tend",[3,2,2]),"[Equinox] Tending due at 'House X': Batch 2, 3.");
Check("garden estimated death countdown",GardenCareStatus.Message("House X","tend",[2,3],careNow.AddMinutes(385),careNow),"[Equinox] Tending due at 'House X': Batch 2, 3. [~06h:25m to die]");
Check("garden unknown death never invented",GardenCareStatus.Message("House X","tend",[2],null,careNow),"[Equinox] Tending due at 'House X': Batch 2. [death timer unknown]");
Check("garden expired estimate not declared dead",GardenCareStatus.Message("House X","tend",[2],careNow.AddMinutes(-1),careNow),"[Equinox] Tending due at 'House X': Batch 2. [death risk - check now]");
Check("garden separate batch countdowns",GardenCareStatus.BatchMessage("House X","tend",[(2,careNow.AddMinutes(385)),(3,careNow.AddMinutes(700))],careNow),"[Equinox] Tending due at 'House X': Batch 2 [~06h:25m], Batch 3 [~11h:40m].");
Check("garden earliest bed per batch",GardenCareStatus.BatchMessage("House X","tend",[(2,careNow.AddHours(8)),(2,careNow.AddHours(4))],careNow),"[Equinox] Tending due at 'House X': Batch 2 [~04h:00m].");

Check("garden quiet before twelve hours",GardenCareStatus.Due(careBed with {Watered=careNow.AddHours(-11)},careNow)??"none","none");
Check("garden reminder at twelve hours",GardenCareStatus.Due(careBed with {Watered=careNow.AddHours(-12)},careNow)??"none","tend");
var fertilizeIntent=TendIntent.From(menu,"Fertilize Crop",time)!;
Check("fertilizer successful observed action",fertilizeIntent.Confirm(4016,time.AddSeconds(1),menu.Target)?.Kind,"garden.fertilized");
Check("fertilizer failure never records",fertilizeIntent.Confirm(4012,time.AddSeconds(1),menu.Target)?.Kind,null);
Check("fertilizer cannot masquerade as tending",fertilizeIntent.Confirm(4017,time.AddSeconds(1),menu.Target)?.Kind,null);
Check("fertilizer wrong bed rejected",fertilizeIntent.Confirm(4016,time.AddSeconds(1),Sample(0,"bed-b"))?.Kind,null);
Check("fertilizer old website holds safely",SyncValidation.SupportedByWebsite("garden.fertilized",5).ToString(),"False");
Check("fertilizer protocol6 enabled",SyncValidation.SupportedByWebsite("garden.fertilized",6).ToString(),"True");

var starterJson = """{"bed":1,"crop":"Krakka Root","soil":"Grade 3 Thanalan Topsoil","status":"starter","order":1,"replantOrder":9,"starterSoil":"Potting Soil","checkExisting":true}""";
var starterBed = JsonSerializer.Deserialize<SharedGardenBed>(starterJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Check("starter guide order survives sync", starterBed.Order.ToString(), "1");
Check("starter return step survives sync", starterBed.ReplantOrder.ToString(), "9");
Check("starter soil survives sync", starterBed.StarterSoil, "Potting Soil");
Check("starter unknown warning survives sync", starterBed.CheckExisting.ToString(), "True");
var legacyBed = JsonSerializer.Deserialize<SharedGardenBed>("""{"bed":1,"crop":"Krakka Root","soil":"Potting Soil"}""", new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
Check("older saved guide remains readable", legacyBed.ReplantOrder.ToString(), "0");
var shortcut = new Shortcut { Key="F8",Ctrl=true };
Check("shortcut exact modifiers",shortcut.Matches("F8",true,false,false).ToString(),"True");
Check("shortcut other modifiers excluded",shortcut.Matches("F8",true,false,true).ToString(),"False");
Check("shortcut unassigned disabled",new Shortcut().Matches("None",false,false,false).ToString(),"False");
Check("shortcut config roundtrip",JsonSerializer.Deserialize<Shortcut>(JsonSerializer.Serialize(shortcut))!.Label,"Ctrl + F8");
Check("digit shortcut friendly label",new Shortcut{Key="Key1",Alt=true}.Label,"Alt + 1");
string[] availableTabs=["housing","person:a","person:b","submarines","settings"];
Check("repair persons after housing",string.Join(",",TabOrderPolicy.Reconcile(["tests","housing","submarines","settings","person:b","person:a"],availableTabs,true)),"housing,person:b,person:a,submarines,settings");
Check("late roster restores persons before subs",string.Join(",",TabOrderPolicy.Reconcile(["housing","submarines","settings"],availableTabs,false)),"housing,person:a,person:b,submarines,settings");
Check("settings always last",string.Join(",",TabOrderPolicy.Reconcile(["settings","person:b","housing","person:a","submarines"],availableTabs,false)),"person:b,housing,person:a,submarines,settings");
Check("absent person order retained",string.Join(",",TabOrderPolicy.Reconcile(["housing","person:b","person:a","submarines","settings"],["housing","submarines","settings"],false)),"housing,person:b,person:a,submarines,settings");

var arCharacter=new SharedCharacter("ar-c","Member","Rafflesia","","","Account",[],FcMember:true,FcId:"987");
var arSource=JsonSerializer.Deserialize<AutoRetainerCharacter>("""{"CID":123,"Name":"Member","World":"Rafflesia","FCID":987,"Ceruleum":1737,"RepairKits":858,"NumSubSlots":4,"Gil":999999,"OfflineSubmarineData":[{"Name":"Hope","ReturnTime":0}],"AdditionalSubmarineData":{"Hope":{"Level":85,"Part1":21794,"Part2":21795,"Part3":21796,"Part4":21797,"CurrentExp":123,"NextLevelExp":456,"Points":"AQIAAAA="}}}""")!;
Check("AR character and FC matched",AutoRetainerCache.Match(arSource,[arCharacter])?.Id,"ar-c");
Check("AR same name wrong world blocked",AutoRetainerCache.Match(arSource,[arCharacter with{World="Golem"}])?.Id,null);
Check("AR ambiguous identity blocked",AutoRetainerCache.Match(arSource,[arCharacter,arCharacter with{Id="other"}])?.Id,null);
Check("AR stale FC membership blocked",AutoRetainerCache.Match(arSource,[arCharacter with{FcId="999"}])?.Id,null);
Check("AR explicit FC departure blocked",AutoRetainerCache.Match(arSource,[arCharacter with{FcMember=false}])?.Id,null);
var arCache=AutoRetainerCache.Copy(arSource,id=>"Item "+id);
Check("AR caches preserve supplies",arCache.Ceruleum+":"+arCache.RepairKits,"1737:858");
Check("AR valid cached vessel needs no invented registration",AutoRetainerCache.Valid(arCache,DateTimeOffset.UtcNow).ToString(),"True");
arSource.AdditionalSubmarineData["Hope"].Points[0]=99;
Check("AR copied arrays independent of source",arCache.Submarines[0].Route[0].ToString(),"1");
Check("AR unrelated inventory excluded",JsonSerializer.Serialize(arCache).Contains("Gil").ToString(),"False");
Check("AR malformed supply rejected",AutoRetainerCache.Valid(arCache with{RepairKits=-1},DateTimeOffset.UtcNow).ToString(),"False");
Check("AR duplicate vessel rejected",AutoRetainerCache.Valid(arCache with{Submarines=[arCache.Submarines[0],arCache.Submarines[0]]},DateTimeOffset.UtcNow).ToString(),"False");
Check("AR old website held",SyncValidation.SupportedByWebsite("submarines.cached",6).ToString(),"False");
Check("AR protocol7 supported",SyncValidation.SupportedByWebsite("submarines.cached",7).ToString(),"True");

Check("AR routes serialize as numeric arrays",JsonSerializer.Serialize(arCache).Contains("\"Route\":[1,2,0,0,0]").ToString(),"True");

var numericVoyage=new SubmarineDetails(0,"Hope",85,0,1,[1,2,3,4],[1,2]);
Check("direct submarine routes serialize as number array",JsonSerializer.Serialize(numericVoyage).Contains("\"Route\":[1,2]").ToString(),"True");
Check("legacy direct submarine route readable",JsonSerializer.Deserialize<SubmarineDetails>(JsonSerializer.Serialize(numericVoyage).Replace("[1,2]","\"AQI=\""))!.Route.Length.ToString(),"2");

var captureKeys=new ShortcutCapture();
Check("capture modifier alone waits",captureKeys.Step([],true,false,false,false)?.Label,null);
Check("capture chord waits until release",captureKeys.Step(["F8"],true,false,false,false)?.Label,null);
Check("capture waits for modifier release",captureKeys.Step([],true,false,false,false)?.Label,null);
Check("capture release preserves modifiers",captureKeys.Step([],false,false,false,false)?.Label,"Ctrl + F8");
Check("capture does not save twice",captureKeys.Step([],false,false,false,false)?.Label,null);
var multiKeys=new ShortcutCapture();multiKeys.Step(["A","B"],false,false,false,false);
Check("capture rejects multiple ordinary keys",multiKeys.Step([],false,false,false,false)?.Label,null);
multiKeys.Step(["Key1"],false,true,false,false);
Check("capture recovers after bad chord",multiKeys.Step([],false,false,false,false)?.Label,"Alt + 1");

// Immediate guide projection must work before any website round trip.
var liveAt=DateTimeOffset.Parse("2026-10-02T10:00:00Z");
var liveAddress=new Address("0000000000000001",410,641,15,15,0,false,false,"Rafflesia","Shirogane");
var liveActor=new Actor("123","Gardener",410,410,"Rafflesia","Rafflesia");
var liveBeds=Enumerable.Range(1,8).Select(i=>new SharedGardenBed(i,i%2==1?"Krakka Root":"Mirror Apple","Grade 3 Thanalan Topsoil","planned","Not synced yet","",null,null,0,false,Order:i,ReplantOrder:i==1?9:0,StarterSoil:i==1?"Potting Soil":"")).ToArray();
var livePlan=new SharedGardenPlan("h","Haven","Rafflesia","Shirogane",15,15,1,"Curiel Root",liveAt,liveBeds,liveAddress.HouseId);
SyncEvent LivePlant(int bed,int minute,string soil)=>new("live"+minute,"garden.planted",liveAt.AddMinutes(minute),liveActor,liveAddress,1,bed,new PlantDetails(1,liveBeds[bed-1].Crop,2,soil));
var liveEvents=Enumerable.Range(1,8).Select(i=>LivePlant(i,i,i==1?"Potting Soil":"Grade 3 Thanalan Topsoil")).ToArray();
Check("local first plant confirms starter",GardenLive.Apply(livePlan,liveEvents.Take(1),x=>x).Beds[0].Status,"starter");
Check("local neighbours trigger replant",GardenLive.Apply(livePlan,liveEvents,x=>x).Beds[0].Status,"replant");
var localDone=GardenLive.Apply(livePlan,liveEvents.Append(LivePlant(1,10,"Grade 3 Thanalan Topsoil")),x=>x);
Check("local final planting completes plan",(localDone.CompletedAt is not null).ToString(),"True");
var localHarvest=GardenLive.Apply(localDone,[new SyncEvent("empty","garden.empty",liveAt.AddMinutes(11),liveActor,liveAddress,1,2)],x=>x);
Check("local harvest keeps completed plan",(localHarvest.CompletedAt is not null).ToString(),"True");
Check("other batch cannot confirm selected tab",GardenLive.Apply(livePlan,[liveEvents[0] with {Patch=2}],x=>x).Beds[0].Status,"planned");
Check("other world cannot confirm selected tab",GardenLive.Apply(livePlan,[liveEvents[0] with {Address=liveAddress with {WorldName="Halicarnassus"}}],x=>x).Beds[0].Status,"planned");
var localTend=GardenLive.Apply(localDone,[new SyncEvent("tend","garden.tended",liveAt.AddMinutes(12),liveActor,liveAddress,1,2)],x=>x);
Check("local tending preserves planting time",localTend.Beds[1].Planted.ToString(),liveEvents[1].At.ToString());
Check("local tending records actual gardener",localTend.Beds[1].TendedBy,"Gardener");

var withClear=GardenLive.Apply(livePlan,liveEvents.Append(new SyncEvent("clear","garden.empty",liveAt.AddMinutes(9),liveActor,liveAddress,1,1)).Append(LivePlant(1,10,"Grade 3 Thanalan Topsoil")),x=>x);
Check("local empty then replant completes",withClear.Beds[0].Status,"confirmed");
Check("local empty then replant complete stamp",(withClear.CompletedAt is not null).ToString(),"True");
var fertBase=GardenLive.Apply(livePlan,[liveEvents[1]],x=>x,_=>5);
var fertEvent=new SyncEvent("fert","garden.fertilized",liveAt.AddHours(1),liveActor,liveAddress,1,2);
var fertResult=GardenLive.Apply(fertBase,[fertEvent],x=>x);
Check("local fertilizer shortens growth",(fertResult.Beds[1].HarvestAt<fertBase.Beds[1].HarvestAt).ToString(),"True");
Check("local fertilizer leaves tending unchanged",fertResult.Beds[1].Watered.ToString(),fertBase.Beds[1].Watered.ToString());
Check("local fertilizer does not double apply",GardenLive.Apply(fertResult,[fertEvent],x=>x).Beds[1].HarvestAt.ToString(),fertResult.Beds[1].HarvestAt.ToString());

Check("guidance starts at bed one",GardenGuidance.Next(livePlan)?.Bed.ToString(),"1");
Check("guidance after starter advances to bed two",GardenGuidance.Next(GardenLive.Apply(livePlan,liveEvents.Take(1),x=>x))?.Bed.ToString(),"2");
Check("guidance unfinished replant is final step",GardenGuidance.Next(GardenLive.Apply(livePlan,liveEvents,x=>x))?.ReplantOrder.ToString(),"9");
Check("completed guide never returns to bed one",(GardenGuidance.Next(localDone) is null).ToString(),"True");
Check("harvest never restarts completed guide",(GardenGuidance.Next(localHarvest) is null).ToString(),"True");
Check("reject batch order that starts elsewhere",GardenGuidance.ValidOrder(livePlan with {Beds=liveBeds.Select(b=>b with {Order=b.Bed==1?2:b.Bed==2?1:b.Order}).ToArray()}).ToString(),"False");
Check("individual bed retains its identity",GardenGuidance.Next(livePlan with {Beds=[liveBeds[4] with {Order=0}]})?.Bed.ToString(),"5");

var wrongPlant=liveEvents[1] with {Plant=liveEvents[1].Plant! with {SoilName="Grade 1 Shroud Topsoil"}};
var wrongGuide=GardenLive.Apply(livePlan,[liveEvents[0],wrongPlant],x=>x);
Check("wrong soil marks only its bed different",wrongGuide.Beds[1].Status,"different");
Check("mistake returns guide to affected bed",GardenGuidance.Next(wrongGuide)?.Bed.ToString(),"2");
Check("mistake preserves starter progress",wrongGuide.Beds[0].Status,"starter");
var repairedGuide=GardenLive.Apply(wrongGuide,[new SyncEvent("repair-clear","garden.empty",liveAt.AddMinutes(3),liveActor,liveAddress,1,2),LivePlant(2,4,"Grade 3 Thanalan Topsoil")],x=>x);
Check("corrected planting clears different mark",repairedGuide.Beds[1].Status,"confirmed");
Check("corrected planting advances guide automatically",GardenGuidance.Next(repairedGuide)?.Bed.ToString(),"3");

Check("death risk survives later maturity estimate",GardenTiming.DeathRisk(false,liveAt.AddHours(48),liveAt.AddDays(3),liveAt.AddDays(4)).ToString(),"True");
Check("maturity before death avoids dead estimate",GardenTiming.DeathRisk(false,liveAt.AddHours(48),liveAt.AddDays(1),liveAt.AddDays(4)).ToString(),"False");
Check("confirmed mature never dies",GardenTiming.DeathRisk(true,liveAt.AddHours(48),null,liveAt.AddDays(4)).ToString(),"False");
Check("unknown care cannot invent death",GardenTiming.DeathRisk(false,null,liveAt.AddDays(3),liveAt.AddDays(4)).ToString(),"False");
