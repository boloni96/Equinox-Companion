using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Performance502Tests
{
 [ModuleInitializer] internal static void Run(){
  var at=DateTimeOffset.UtcNow;var actor=new Actor("42","Test",1,1,"World");
  var character=new SharedCharacter("c","Test","World","","","Main",[],ContentId:"42");
  var unrelated=Enumerable.Range(0,1000).Select(i=>new SyncEvent(i.ToString(),"collection.observed",at,actor,null,Collection:new("unrelated",[],[7,8,9],[]))).ToArray();
  var rows=unrelated.Concat(new[]{
   new SyncEvent("reward","collection.observed",at,actor,null,Collection:new("mount",[],[],[7])),
   new SyncEvent("quest","collection.observed",at,actor,null,Collection:new("quest",[],[QuestToolsPolicy.KeywardBound],[])),
   new SyncEvent("bag","storage.observed",at,actor,null,Storage:new("bag:0","Bag",[8])),
   new SyncEvent("shared","storage.observed",at,actor,null,Storage:new("fc:0","FC",[9]))
  }).ToArray();
  var result=new CelebrationHistoryIndex().Append(rows,[8,9,100],[new("mount",7,100)]).Read(character);
  if(!result.QuestComplete||!result.Items.SequenceEqual(new uint[]{8,100}))throw new Exception("Completion optimization changed owned reward/quest semantics");
  var cache=new UiProjectionCache<int>(1000);var source=new object();var builds=0;
  for(var i=0;i<1000;i++)cache.Get(source,"garden1",i,()=>++builds);
  if(builds!=1)throw new Exception("Garden grouping rebuilt without changes");
  cache.Get(source,"garden2",999,()=>++builds);if(builds!=2)throw new Exception("New care must invalidate immediately");
  cache.Get(source,"garden2",1999,()=>++builds);if(builds!=3)throw new Exception("Fallback refresh missing");
  Console.WriteLine("502 reward history equivalence and garden refresh policy passed.");
 }
}
