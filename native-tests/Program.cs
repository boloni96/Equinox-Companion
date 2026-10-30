using System.Text;
using EquinoxCompanion;

// Names and row order from the 219 soil/seed capture; valid SeString colour
// payloads reproduce its formatting without depending on game memory.
var soil = new[] { "Potting Soil", "Grade 3 Shroud Topsoil", "Grade 3 Thanalan Topsoil", "Grade 3 Thanalan Topsoil" };
var seeds = new[] { "Thavnairian Onion Seeds", "Sun Lemon Seeds", "Apricot Kernels", "Old World Fig Seeds", "Old World Fig Seeds", "Old World Fig Seeds", "Mirror Apple Seeds", "Old World Fig Seeds", "Krakka Root Seeds", "Mirror Apple Seeds", "Old World Fig Seeds", "Royal Kukuru Seeds", "Curiel Root Seeds" };
int checks = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
unsafe string Read(byte[] bytes) { fixed (byte* text = bytes) return GardenMenuText.Read(text); }
byte[] Label(string name) => [0x02, 0x48, 0x04, 0xF2, 0x02, 0x25, 0x03, 0x02, 0x49, 0x04, 0xF2, 0x02, 0x26, 0x03, ..Encoding.UTF8.GetBytes(name), 0x02, 0x49, 0x02, 0x01, 0x03, 0x02, 0x48, 0x02, 0x01, 0x03, 0];
foreach (var name in soil.Concat(seeds).Concat(new[] { "Terreau de Thanalan III", "Spiegela­pfelsamen", "ミラ－アップルの種", "  Potting Soil  " }))
{
    Check(Read(Label(name)) == name.Trim(), $"Formatted label did not decode: {name}");
    Check(Read([..Encoding.UTF8.GetBytes(name), 0]) == name.Trim(), $"Plain label changed: {name}");
}
// Reproduce the formatted confirmation from the 0.5.1.316 Bed 2 report.
byte[] prompt = [..Encoding.UTF8.GetBytes("Prepare the bed with a "), ..Label("bag of grade 3 Thanalan topsoil")[..^1], ..Encoding.UTF8.GetBytes(" and a "), ..Label("bag of mirror apple seeds")[..^1], (byte)'?', 0];
Check(Read(prompt) == "Prepare the bed with a bag of grade 3 Thanalan topsoil and a bag of mirror apple seeds?", "Plant confirmation must decode colour payloads before matching exact seed and soil.");
var soilDecoded = soil.Select(x => Read(Label(x))).ToArray();
var seedsDecoded = seeds.Select(x => Read(Label(x))).ToArray();
Check(soilDecoded.Select((name, index) => (name, index)).Where(x => x.name == "Potting Soil").Select(x => x.index).SequenceEqual(new[] { 0 }), "Only Potting Soil should be required in the captured starter step.");
Check(seedsDecoded.Select((name, index) => (name, index)).Where(x => x.name == "Mirror Apple Seeds").Select(x => x.index).SequenceEqual(new[] { 6, 9 }), "Both Mirror Apple stacks must match; Sun Lemon and Old World Fig share their icon but must not match.");
Check(Read([0]) == "", "Empty labels must remain unresolved.");
Check(Read(Enumerable.Repeat((byte)'x', 2049).ToArray()) == "", "Unterminated labels must not be read past the limit.");
unsafe { Check(GardenMenuText.Read(null) == "", "Null label must remain unresolved."); }
Console.WriteLine($"Native garden label regression checks passed: {checks}.");

SellerNativeTests.Run();
