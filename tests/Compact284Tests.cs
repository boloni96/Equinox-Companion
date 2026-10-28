using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class Compact284Tests
{
 [ModuleInitializer] internal static void Run()
 {
  void Check(string name,bool ok){if(!ok)throw new Exception(name);Console.WriteLine("PASS compact284 "+name);}
  var categories=new[]{new StorageCategory(55,"Dye",6,0),new(49,"Metal",6,0),new(94,"Orchestrion Roll",7,0),new(82,"Gardening",7,0),new(101,"Submersible Hull",6,0)};
  var groups=StorageCategoryGroups.Build(categories);var selected=new HashSet<uint>();
  StorageCategoryGroups.Select(selected,groups.Single(g=>g.Name=="Crafting materials"),true);
  Check("materials excludes dyes and workshop parts",selected.SetEquals(new uint[]{49}));
  Check("orchestrion rolls are collectibles",groups.Single(g=>g.Name=="Collectibles").Categories.Single().Id==94);
  Check("housing still contains gardening",groups.Single(g=>g.Name=="Housing").Categories.Single().Id==82);
  const string title="All Saints’ Wake 2026 · Grizzly Bear Head";
  const string explanation="No alternative verifiedSource information checked 28/10/2026. Listed sources may require quests, currency or access.";
  var presentation=AdventureTaskPresentation.Split(title+explanation+" · Obtained","reward");
  Check("legacy source text removed from reward title",presentation.Label==title);
  Check("source explanation retained for hover",presentation.Details==explanation+" · Obtained");
  Check("uppercase completion suffix removed",AdventureTaskPresentation.Split(title+" · Obtained","reward").Label==title);
  Check("unfinished reward remains compact",AdventureTaskPresentation.Split(title+" · Collect reward","reward").Label==title);
  Check("ordinary source words in names retained",AdventureTaskPresentation.Split("Market Board Painting","reward").Label=="Market Board Painting");
  Check("other tasks untouched",AdventureTaskPresentation.Split(title+explanation,"garden").Label==title+explanation);
 }
}
