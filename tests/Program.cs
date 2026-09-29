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
