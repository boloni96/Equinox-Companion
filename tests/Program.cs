using EquinoxCompanion;
var time = DateTimeOffset.Parse("2026-09-29T00:00:00Z");
var gate = new ObservationGate();
void Check(string name, string? actual, string? expected) { if (actual != expected) throw new Exception($"{name}: {actual} != {expected}"); Console.WriteLine($"PASS {name}"); }
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
if (args.Length > 0)
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
