using EquinoxCompanion;
using System.Runtime.CompilerServices;
internal static class StorageCategoryGroupTests
{
 [ModuleInitializer] internal static void Run(){
  var cats=new[]{new StorageCategory(57,"Furnishing",7,20),new(76,"Outdoor Furnishing",7,19),new(95,"Painting",7,29),new(82,"Gardening",7,25),new(56,"Part",6,9),new(58,"Materia",7,0),new(83,"Demimateria",7,26),new(101,"Submersible Hull",6,15),new(777,"Future category",9,0),new(114,"",0,0)};
  var groups=StorageCategoryGroups.Build(cats);
  void Check(string text,bool ok){if(!ok)throw new Exception(text);Console.WriteLine("PASS storage categories "+text);}
  Check("all named categories appear exactly once",groups.SelectMany(g=>g.Categories).Select(c=>c.Id).Order().SequenceEqual(cats.Where(c=>c.Name.Length>0).Select(c=>c.Id).Order()));
  var housing=groups.Single(g=>g.Name=="Housing");Check("housing excludes generic crafting parts",housing.Categories.All(c=>c.Id!=56)&&groups.Single(g=>g.Name=="Crafting materials").Categories.Any(c=>c.Id==56));
  Check("unknown future categories stay available",groups.Single(g=>g.Name=="Other items").Categories.Any(c=>c.Id==777));
  var selected=new HashSet<uint>{58,57};StorageCategoryGroups.Select(selected,housing,true);Check("parent selection includes all children and preserves others",housing.Categories.All(c=>selected.Contains(c.Id))&&selected.Contains(58));
  StorageCategoryGroups.Select(selected,housing,false);Check("clearing parent does not clear another group",selected.SetEquals(new uint[]{58}));
  Check("full category summary hides child names",StorageCategoryGroups.Summary(groups,housing.Categories.Select(c=>c.Id).ToHashSet()).SequenceEqual(new[]{"Housing"}));
  Check("partial category summary lists checked children only",StorageCategoryGroups.Summary(groups,new HashSet<uint>{57,58}).SequenceEqual(new[]{"Housing: Furnishing","Materia: Materia"}));
  Check("empty assignments have no category summary",StorageCategoryGroups.Summary(groups,new HashSet<uint>()).Length==0);
  Check("workshop parts stay separate",groups.Single(g=>g.Name=="Workshop parts").Categories.Single().Id==101);
 }
}
