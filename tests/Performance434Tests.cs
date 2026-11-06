using System.Runtime.CompilerServices;
using EquinoxCompanion;
internal static class Performance434Tests
{
    [ModuleInitializer]internal static void Run()
    {
        var at=DateTimeOffset.UtcNow;var a=new Actor("42","Test",0,0,"Rafflesia");
        var character=new SharedCharacter("c","Test","Rafflesia","","","Main",[],ContentId:"42");
        var other=character with {ContentId="43"};
        var rows=new[]{
            new SyncEvent("s","storage.observed",at,a,null,Storage:new("bag:0","Bags",[100,200,300])),
            new SyncEvent("q","collection.observed",at,a,null,Collection:new("quest",[],[QuestToolsPolicy.KeywardBound],[])),
            new SyncEvent("m","collection.observed",at,a,null,Collection:new("mount",[],[7],[])),
            new SyncEvent("excluded","storage.observed",at,a,null,Storage:new("fc:1","FC",[900]))
        };
        var empty=new CelebrationHistoryIndex();var first=empty.Append(rows,[100,400,900],[new("mount",7,400)]);
        var evidence=first.Read(character);
        if(!evidence.QuestComplete||!evidence.Items.SequenceEqual(new uint[]{100,400}))throw new Exception("Index must preserve quest and rewards while excluding unrelated/shared items.");
        if(first.Read(other).Items.Length!=0||empty.Read(character).Items.Length!=0)throw new Exception("Evidence must stay character-scoped and immutable.");
        var next=first.Append([new SyncEvent("new","storage.observed",at,a,null,Storage:new("retainer:1","Retainer",[900]))],[100,400,900],[]);
        if(next.IndexedRecords!=5||!next.Read(character).Items.SequenceEqual(new uint[]{100,400,900})||first.Read(character).Items.Length!=2)throw new Exception("Append must process only new evidence without modifying published index.");
        if(next.Read(character with {ContentId="",Name=" test ",World=" rafflesia "}).Items.Length!=3)throw new Exception("Name/world fallback must match existing semantics.");
        var roster=new SharedRoster(1,at,[new("p","Person",[character,other])]);
        if(CharacterRegistrationPolicy.Find(roster,a)!=character)throw new Exception("Unique content ID must win over duplicate names.");
        if(CharacterRegistrationPolicy.Find(roster,a with {ContentId="99"})!=null)throw new Exception("Ambiguous name fallback must remain unresolved.");
        Console.WriteLine("PASS434 incremental celebration index, ownership boundaries, immutable snapshots and registration matching");
    }
}
