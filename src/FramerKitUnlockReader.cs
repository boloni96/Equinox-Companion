namespace EquinoxCompanion;
// Catalogue IDs belong to the Journal; the native API takes Item.AdditionalData.
// Cache only immutable metadata, never a character's unlocked/not-unlocked result.
public sealed class FramerKitUnlockReader(Func<uint,uint?> lookup)
{
    private readonly Dictionary<uint,uint> gameIds=[];
    public uint? GameId(uint item)
    {
        if(item==0)return null;
        if(gameIds.TryGetValue(item,out var saved))return saved;
        var id=lookup(item);if(id is not >0)return null;
        gameIds[item]=id.Value;return id;
    }
    public bool? Read(uint item,Func<uint,bool> unlocked)=>GameId(item) is {} id?unlocked(id):null;
}
